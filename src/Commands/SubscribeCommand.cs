using System.Buffers;
using System.Text;

namespace LiteRPS.Commands;

public class SubscribeCommand : ICommand
{
    private readonly TaskCompletionSource _completionSource;
    private readonly string _topic;
    private static ReadOnlySpan<byte> Command => "SUBSCRIBE"u8;

    public SubscribeCommand(string topic)
    {
        _topic = topic;
        _completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task WaitToCompleteAsync() => _completionSource.Task;

    public int Write(IBufferWriter<byte> writer)
    {
        //*RAW COMMAND = $"*2\r\n$9\r\nSUBSCRIBE\r\n${topic.Length}\r\n{topic}\r\n";
        var offset = 0;
        
        var topicLength = _topic.Length.ToString();
        var totalLength = Command.Length + _topic.Length + topicLength.Length + 15;

        var span = writer.GetSpan(totalLength);
        span[offset++] = (byte)'*';
        span[offset++] = (byte)'2';
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        span[offset++] = (byte)'$';
        span[offset++] = '0' + 9;
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        Command.CopyTo(span[offset..]);
        offset += Command.Length;
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        span[offset++] = (byte)'$';
        Encoding.UTF8.GetBytes(topicLength.AsSpan(), span[offset..]);
        offset += topicLength.Length;
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        Encoding.UTF8.GetBytes(_topic.AsSpan(), span[offset..]);
        offset += _topic.Length;
        span[offset++] = (byte)'\r';
        span[offset++] = (byte)'\n';

        writer.Advance(offset);
        return offset;
    }

    public void Complete() => _completionSource.TrySetResult();
}