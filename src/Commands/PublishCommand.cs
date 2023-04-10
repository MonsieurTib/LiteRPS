using System.Buffers;
using System.Text;

namespace LiteRPS.Commands;

public class PublishCommand : ICommand
{
    private readonly TaskCompletionSource _completionSource;
    private readonly string _payload;
    private readonly string _topic;
    private static ReadOnlySpan<byte> Command => "PUBLISH"u8;

    public PublishCommand(string topic, string payload)
    {
        _topic = topic;
        _payload = payload;
        _completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    
    public Task WaitToCompleteAsync() => _completionSource.Task;

    public int Write(IBufferWriter<byte> writer)
    {
        var offset = 0;
        
        var topicLength = _topic.Length.ToString();
        var payloadLength = _payload.Length.ToString();
        var totalLength = Command.Length + _topic.Length + _payload.Length + topicLength.Length + payloadLength.Length + 20;

        var span = writer.GetSpan(totalLength);
        span[offset++] = (byte)'*';
        span[offset++] = (byte)'3';
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        span[offset++] = (byte)'$'; // COMMAND
        span[offset++] = '0' + 7;
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        Command.CopyTo(span[offset..]);
        offset += Command.Length;
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        span[offset++] = (byte)'$'; // TOPIC
        Encoding.UTF8.GetBytes(topicLength.AsSpan(), span[offset..]);
        offset += topicLength.Length;
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        Encoding.UTF8.GetBytes(_topic.AsSpan(), span[offset..]);
        offset += _topic.Length;
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        span[offset++] = (byte)'$'; // PAYLOAD 
        Encoding.UTF8.GetBytes(payloadLength.AsSpan(), span[offset..]);
        offset += payloadLength.Length;
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        Encoding.UTF8.GetBytes(_payload.AsSpan(), span[offset..]);
        offset += _payload.Length;

        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        writer.Advance(offset);
        return offset;
    }

    public void Complete() => _completionSource.TrySetResult();
}