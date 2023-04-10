using LiteRPS.Commands;
using Microsoft.Extensions.Logging;

namespace LiteRPS;

public class RedisClient
{
    private readonly ILogger<RedisClient> _logger;
    private readonly RedisConnection _pubConnection;
    private readonly RedisConnection _subConnection;

    private readonly SubscriptionManager _subscriptionManager = new();
    private TaskCompletionSource _waitForConnection;
    
    public event Func<string, Task>? ReConnected;

    public RedisClient(ConfigurationOptions options, ILoggerFactory loggerFactory)
    {
        _waitForConnection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        _subConnection = new RedisConnection(options, loggerFactory);
        _subConnection.OnMessageReceived += MessageReceived;

        _pubConnection = new RedisConnection(options, loggerFactory);
        _pubConnection.Closed += PubConnectionOnClosed;
        _pubConnection.ReConnected += PubConnectionOnReConnected;
        
        _logger = loggerFactory.CreateLogger<RedisClient>();
    }

    private async Task PubConnectionOnReConnected()
    {
        _logger.LogInformation("Publish connection reconnected at {time}", DateTime.Now);
        ReConnected?.Invoke("PublishConnection");
        var tasks = new List<Task>();

        foreach (var topic in _subscriptionManager.GetSubscribedTopics)
        {
            var command = new SubscribeCommand(topic);

            if (!_subConnection.Commands.Writer.TryWrite(command))
            {
                throw new Exception($"can't subscribe to {topic}");
            }

            tasks.Add(command.WaitToCompleteAsync());
        }

        await Task.WhenAll(tasks);

        _waitForConnection.TrySetResult();
        _waitForConnection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private Task PubConnectionOnClosed()
    {
        _logger.LogInformation("Publish connection closed at {time}", DateTime.Now);
        return Task.CompletedTask;
    }

    public async Task StartAsync()
    {
        var pubTask = _pubConnection.StartAsync();
        var subTask = _subConnection.StartAsync();

        await Task.WhenAll(pubTask, subTask);
    }

    private async Task MessageReceived((string topic, string payload) msg) => await _subscriptionManager.PublishAsync(msg.topic, msg.payload);

    public async Task<IDisposable> SubscribeAsync<T>(string topic, Func<T, Task> callback)
    {
        var command = new SubscribeCommand(topic);

        if (!_subConnection.Commands.Writer.TryWrite(command))
        {
            throw new Exception($"can't subscribe to {topic}");
        }

        await command.WaitToCompleteAsync();
        return _subscriptionManager.Add(topic, callback);
    }

    public Task UnSubscribeAsync(string topic)
    {
        var command = new UnSubscribeCommand(topic);

        if (!_subConnection.Commands.Writer.TryWrite(command))
        {
            throw new Exception($"can't unsubscribe to {topic}");
        }

        return command.WaitToCompleteAsync();
    }

    public async Task SendMessageAsync(string topic, string payload)
    {
        if (_pubConnection.ConnectionState != RedisConnectionState.Connected)
        {
            Console.WriteLine("connection not connected, waiting for sending message");
            await _waitForConnection.Task;
            Console.WriteLine("connection is now reconnected, sending message");
        }

        var command = new PublishCommand(topic, payload);

        if (!_pubConnection.Commands.Writer.TryWrite(command))
        {
            throw new Exception($"can't send message to {topic}");
        }

        await command.WaitToCompleteAsync();
    }
}