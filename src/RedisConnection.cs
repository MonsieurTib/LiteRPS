using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using LiteRPS.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiteRPS;

public class RedisConnection 
{
    private readonly ConfigurationOptions _configurationOptions = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly IPAddress _ipAddress;
    private readonly IPEndPoint _ipEndPoint;
    private readonly ILogger<RedisConnection> _logger;

    private readonly ConcurrentQueue<ICommand> _pendingCommands;
    private IDuplexPipe _application;
    private Task _receiveLoop;
    private Task _reconnectTask;
    private RedisProtocolReader _redisProtocolReader;
    private RedisProtocolWriter _redisProtocolWriter;
    private Socket _socket;
    private CancellationTokenSource _stoppingToken = new();

    private IDuplexPipe _transport;
    private Task _writeLoop;
    public RedisConnectionState ConnectionState = RedisConnectionState.Closed;

    public Func<(string topic, string payload), Task> OnMessageReceived;
    public Channel<ICommand> Commands { get; }
    public event Func<Task>? ReConnected;
    public event Func<Task>? Closed;

    public RedisConnection(ConfigurationOptions options, ILoggerFactory loggerFactory)
    {
        _configurationOptions = options;
        var ipHostInfo = Dns.GetHostEntry(_configurationOptions.Host);
        _ipAddress = ipHostInfo.AddressList[0];
        _ipEndPoint = new IPEndPoint(_ipAddress, _configurationOptions.Port);

        _pendingCommands = new ConcurrentQueue<ICommand>();
        Commands = Channel.CreateUnbounded<ICommand>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true,
            AllowSynchronousContinuations = false
        });
        
        var factory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = factory.CreateLogger<RedisConnection>();
    }
    
    public async Task StartAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            await InternalConnectAsync();
            _logger.LogInformation("successfully connected to {Host}:{Port}", _configurationOptions.Host, _configurationOptions.Port);
            ConnectionState = RedisConnectionState.Connected;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task ReconnectAsync()
    {
        _logger.LogInformation("trying reconnect to {Host}:{Port}", _configurationOptions.Host, _configurationOptions.Port);
        ConnectionState = RedisConnectionState.Reconnecting;
        var retry = _configurationOptions.RetryPolicy;

        while (retry.ShouldRetry())
        {
            try
            {
                await Task.Delay(retry.NextRetry());
                await InternalConnectAsync();
                _logger.LogInformation("successfully reconnected to {Host}:{Port}", _configurationOptions.Host, _configurationOptions.Port);
                ConnectionState = RedisConnectionState.Connected;
                ReConnected?.Invoke();
                return;
            }
            catch
            {
                // Nothing to do, just retry and cross fingers :-)
            }
        }
        _logger.LogError("Maximum retry reached");
    }

    private async Task InternalConnectAsync()
    {
        _socket = new Socket(_ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await _socket.ConnectAsync(_ipEndPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "connection to {Host}:{Port} failed", _configurationOptions.Host, _configurationOptions.Port);
            throw;
        }

        var pair = DuplexPipe.CreateConnectionPair(PipeOptions.Default, PipeOptions.Default);

        _transport = pair.transport;
        _application = pair.application;

        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_stoppingToken.Token));
        _writeLoop = Task.Run(() => WriteLoopAsync(_stoppingToken.Token));

        if (_configurationOptions.Ssl)
        {
            var ssl = new SslDuplexPipe(pair.transport,
                new StreamPipeReaderOptions(leaveOpen: true),
                new StreamPipeWriterOptions(leaveOpen: true),
                stream => new SslStream(stream, false, (_, _, _, _) => true));

            _logger.LogInformation("authenticating");
            await ssl.Stream.AuthenticateAsClientAsync(_configurationOptions.Host);
            _transport = ssl;

            await _transport.Output.WriteAsync(Encoding.UTF8.GetBytes($"AUTH {_configurationOptions.Password}\r\n"));
            await _transport.Output.FlushAsync();
        }

        _redisProtocolReader = new RedisProtocolReader(_transport.Input);
        _redisProtocolReader.MessageReceived += MessageReceived;
        _redisProtocolReader.MessageSent += MessageSent;
        _redisProtocolReader.Subscribed += Subscribed;
        _redisProtocolReader.UnSubscribed += UnSubscribed;
        _redisProtocolWriter = new RedisProtocolWriter(_transport.Output, Commands.Reader, _pendingCommands);
    }

    private Task MessageReceived((string topic, string payload) arg)
    {
        OnMessageReceived?.Invoke(arg);
        return Task.CompletedTask;
    }

    private Task MessageSent(int receiverCount)
    {
        if (!_pendingCommands.TryDequeue(out var pendingCommand) || pendingCommand is not PublishCommand publishCommand)
        {
            throw new InvalidOperationException();
        }

        publishCommand.Complete();
        return Task.CompletedTask;
    }

    private Task Subscribed(string topic)
    {
        if (!_pendingCommands.TryDequeue(out var pendingCommand))
        {
            return Task.FromException(new ArgumentException("can not dequeue from pendingCommand"));
        }

        if (pendingCommand is not SubscribeCommand subscribeCommand)
        {
            return Task.FromException(new ArgumentException("pending command is not a subscribe command "));
        }

        subscribeCommand.Complete();
        return Task.CompletedTask;
    }

    private Task UnSubscribed(string topic)
    {
        if (!_pendingCommands.TryDequeue(out var pendingCommand))
        {
            return Task.FromException(new ArgumentException("can not dequeue from pendingCommand"));
        }

        if (pendingCommand is not UnSubscribeCommand unSubscribeCommand)
        {
            return Task.FromException(new ArgumentException("pending command is not an unSubscribe command "));
        }

        unSubscribeCommand.Complete();
        return Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                var buffer = _application.Output.GetMemory();
                try
                {
                    var bytesRead = await _socket.ReceiveAsync(buffer, SocketFlags.None, stoppingToken);
                    if (bytesRead == 0)
                    {
                        Closed?.Invoke();
                        _redisProtocolReader.Dispose();
                        _redisProtocolWriter.Dispose();
                        ConnectionState = RedisConnectionState.Closed;
                        break;
                    }

                    _application.Output.Advance(bytesRead);
                    await _application.Output.FlushAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "connection lost");
                    break;
                }
            }
        }
        finally
        {
            await HandleConnectionCloseAsync();
        }
    }

    private async Task WriteLoopAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            var result = await _application.Input.ReadAsync(stoppingToken).ConfigureAwait(false);
            var buffer = result.Buffer;
            if (result.IsCanceled)
            {
                break;
            }

            var end = buffer.End;
            var isCompleted = result.IsCompleted;
            if (!buffer.IsEmpty)
            {
                try
                {
                    if (buffer.IsSingleSegment)
                    {
                        await _socket.SendAsync(buffer.First, SocketFlags.None, stoppingToken);
                    }
                    else
                    {
                        await _socket.SendAsync(buffer.ToArray(), SocketFlags.None, stoppingToken);
                    }
                }
                catch (SocketException ex)
                {
                    _logger.LogError(ex, "can not write to socket");
                }
            }

            _application.Input.AdvanceTo(end);
            if (isCompleted)
            {
                break;
            }
        }
    }

    private async Task HandleConnectionCloseAsync()
    {
        await _connectionLock.WaitAsync();
        _socket.Dispose();
        try
        {
            _stoppingToken.Cancel();
            _stoppingToken = new CancellationTokenSource();
            _reconnectTask = ReconnectAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}