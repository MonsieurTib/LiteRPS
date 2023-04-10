using System.Buffers;
using System.IO.Pipelines;
using LiteRPS.Resp;
using Xunit.Abstractions;

namespace LiteRPS.Tests;

public class RedisProtocolTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public RedisProtocolTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void ParseNewMessageTest()
    {
        var msg = new ReadOnlySequence<byte>("*3\r\n$7\r\nmessage\r\n$5\r\ntopic\r\n$7\r\npayload\r\n".Select(c => (byte)c).ToArray());
        var protocol = new RedisProtocolReader();
        var topicResult = string.Empty;
        var payloadResult = string.Empty;

        if (protocol.TryParseMessage(ref msg, out var respResult))
        {
            if (respResult is RespArray { Items: [RespBulkString command, RespBulkString topic, RespBulkString payload] })
            {
                topicResult = topic.ToString();
                payloadResult = payload.ToString();
            }
        }

        Assert.True(topicResult == "topic" && payloadResult == "payload");
    }

    [Fact]
    public async Task ParseChunckedPartTest()
    {
        var pipe = new Pipe();
        var tcs = new TaskCompletionSource();
        var protocol = new RedisProtocolReader(pipe.Reader);
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("*3\r\n"u8.ToArray()));
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("$7\r\n"u8.ToArray()));
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("mess"u8.ToArray()));
        await Task.Delay(1_000);
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("age\r\n"u8.ToArray()));
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("$5\r\n"u8.ToArray()));
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("top"u8.ToArray()));
        await pipe.Writer.FlushAsync();
        await Task.Delay(1_000);
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("ic\r\n"u8.ToArray()));
        await pipe.Writer.FlushAsync();
        await Task.Delay(1_000);
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("$7\r"u8.ToArray()));
        await Task.Delay(1_000);
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("\n"u8.ToArray()));
        await Task.Delay(1_000);
        await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>("payload\r\n"u8.ToArray()));
        await pipe.Writer.FlushAsync();

        var topicResult = string.Empty;
        var payloadResult = string.Empty;
        protocol.MessageReceived += tuple =>
        {
            topicResult = tuple.topic;
            payloadResult = tuple.payload;
            tcs.SetResult();
            return Task.CompletedTask;
        };

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(topicResult == "topic" && payloadResult == "payload");
    }
}