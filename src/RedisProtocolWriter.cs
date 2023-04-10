using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading.Channels;
using LiteRPS.Commands;

namespace LiteRPS;

public class RedisProtocolWriter : IDisposable
{
    private readonly ChannelReader<ICommand> _commandReader;
    private readonly ConcurrentQueue<ICommand> _processingCommands;
    private readonly CancellationTokenSource _stoppingToken = new();
    private readonly Task _writeLoop;
    private readonly PipeWriter _writer;

    public RedisProtocolWriter(PipeWriter writer, ChannelReader<ICommand> commandReader, ConcurrentQueue<ICommand> processingCommands)
    {
        _writer = writer;
        _commandReader = commandReader;
        _processingCommands = processingCommands;
        _writeLoop = Task.Run(WriteLoopAsync, _stoppingToken.Token);
    }

    public void Dispose()
    {
        _stoppingToken.Cancel();
        _writeLoop.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task WriteLoopAsync()
    {
        while (await _commandReader.WaitToReadAsync())
        {
            while (_commandReader.TryRead(out var command))
            {
                var write = command.Write(_writer);
                _processingCommands.Enqueue(command);
            }

            var flushResult = _writer.FlushAsync();
            if (!flushResult.IsCompleted)
            {
                await flushResult;
            }
        }
    }
}