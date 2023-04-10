using System.Buffers;
using System.Buffers.Text;
using System.IO.Pipelines;
using System.Text;
using LiteRPS.Resp;

namespace LiteRPS;

public class RedisProtocolReader : IDisposable
{
    private readonly PipeReader _reader;
    private readonly Task _readLoop;
    private readonly CancellationTokenSource _stoppingToken = new();

    public Func<(string topic, string payload), Task> MessageReceived;
    public Func<int, Task> MessageSent;
    public Func<string, Task> Subscribed;
    public Func<string, Task> UnSubscribed;

    public RedisProtocolReader(PipeReader reader)
    {
        _reader = reader;
        _readLoop = Task.Run(ReadLoopAsync, _stoppingToken.Token);
    }

    public RedisProtocolReader()
    {
    }

    private static ReadOnlySpan<byte> MessageRequest => "message"u8;
    private static ReadOnlySpan<byte> SubscribeCommandBytes => "subscribe"u8;
    private static ReadOnlySpan<byte> UnSubscribeCommandBytes => "unsubscribe"u8;
    private static ReadOnlySpan<byte> CrlfDelimiter => "\r\n"u8;

    public void Dispose()
    {
        _reader.CancelPendingRead();
        _stoppingToken.Cancel();
        _readLoop.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task ReadLoopAsync()
    {
        while (!_stoppingToken.IsCancellationRequested)
        {
            var result = await _reader.ReadAsync(_stoppingToken.Token);
            if (result.IsCompleted)
            {
                break;
            }

            var buffer = result.Buffer;

            while (TryParseMessage(ref buffer, out var respResult))
                switch (respResult)
                {
                    case RespArray { Items: [RespBulkString messageCommand, RespBulkString topic, RespBulkString payload] }
                        when messageCommand.Value.ToSpan().SequenceEqual(MessageRequest):
                        await MessageReceived((topic, payload));
                        break;
                    case RespArray { Items: [RespBulkString subscribeCommand, RespBulkString subscribingTopic, RespInt _] }
                        when subscribeCommand.Value.ToSpan().SequenceEqual(SubscribeCommandBytes):
                        await Subscribed(subscribingTopic);
                        break;
                    case RespArray { Items: [RespBulkString unSubscribeCommand, RespBulkString unSubscribingTopic, RespInt _] }
                        when unSubscribeCommand.Value.ToSpan().SequenceEqual(UnSubscribeCommandBytes):
                        await UnSubscribed(unSubscribingTopic);
                        break;
                    case RespInt value:
                        await MessageSent(value);
                        break;
                    default:
                        throw new InvalidOperationException("unknow message received from Redis");
                }

            _reader.AdvanceTo(buffer.Start, buffer.End);
        }

        await _reader.CompleteAsync();
    }

    public bool TryParseMessage(ref ReadOnlySequence<byte> buffer, out RespObject? result)
    {
        result = default;
        var sequenceReader = new SequenceReader<byte>(buffer);
        if (!TryParseMessageImpl(ref sequenceReader, out result))
        {
            return false;
        }

        buffer = buffer.Slice(sequenceReader.Consumed);
        return true;
    }

    private bool TryParseMessageImpl(ref SequenceReader<byte> reader, out RespObject? respObject)
    {
        respObject = default;
        if (!reader.TryPeek(0, out var prefix))
        {
            return false;
        }

        switch ((char)prefix)
        {
            case '*':
            {
                reader.Advance(1);

                if (!TryReadArray(ref reader, out var respArray))
                {
                    return false;
                }

                respObject = respArray;
                break;
            }
            case '$':
            {
                reader.Advance(1);

                if (!TryReadBulkString(ref reader, out var respBulkString))
                {
                    return false;
                }

                respObject = respBulkString;
                break;
            }
            case ':':
            {
                reader.Advance(1);

                if (!TryReadInt(ref reader, out var intValue))
                {
                    return false;
                }

                respObject = new RespInt(intValue);
                break;
            }
            default:
            {
                var t = (char)prefix;
                throw new Exception($"unsupported/not yet supported type : {t}");
            }
        }

        return true;
    }

    private bool TryReadArray(ref SequenceReader<byte> reader, out RespArray? o)
    {
        o = default;

        if (!TryReadInt(ref reader, out var length))
        {
            return false;
        }

        var items = new List<RespObject>(length);

        while (TryParseMessageImpl(ref reader, out var item))
        {
            if (item != null)
            {
                items.Add(item);
            }

            if (items.Count == length)
            {
                break;
            }
        }

        if (items.Count != length)
        {
            return false;
        }

        o = new RespArray
        {
            Items = items
        };

        return true;
    }

    private bool TryReadBulkString(ref SequenceReader<byte> reader, out RespBulkString? value)
    {
        value = default;

        if (!TryReadInt(ref reader, out var length))
        {
            return false;
        }

        if (!reader.TryReadExact(length, out var result))
        {
            return false;
        }

        value = new RespBulkString(result);
        reader.Advance(CrlfDelimiter.Length);

        return true;
    }

    private bool TryReadInt(ref SequenceReader<byte> reader, out int result)
    {
        result = -1;

        if (!reader.TryReadTo(out ReadOnlySequence<byte> segment, CrlfDelimiter))
        {
            return false;
        }

        if (segment.IsSingleSegment)
        {
            if (!Utf8Parser.TryParse(segment.First.Span, out result, out _))
            {
                throw new Exception("RespArray invalid length");
            }
        }
        else
        {
            Span<byte> local = stackalloc byte[20];
            segment.CopyTo(local);
            if (!Utf8Parser.TryParse(local, out result, out _))
            {
                throw new Exception("RespArray invalid length");
            }
        }

        return true;
    }
}