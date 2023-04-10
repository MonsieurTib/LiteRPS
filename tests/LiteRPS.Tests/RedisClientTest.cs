using Xunit.Abstractions;

namespace LiteRPS.Tests;

public class RedisClientTest : IClassFixture<RedisServerFixture>, IAsyncLifetime
{
    private readonly RedisServerFixture _redisServer;
    private readonly ITestOutputHelper _testOutputHelper;

    public RedisClientTest(RedisServerFixture redisServer, ITestOutputHelper testOutputHelper)
    {
        _redisServer = redisServer;
        _redisServer.SetLogger(testOutputHelper);
        _testOutputHelper = testOutputHelper;
    }

    public async Task InitializeAsync()
    {
        await _redisServer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _redisServer.DisposeAsync();
    }

    [Fact]
    public async Task TestAutomaticReconnect()
    {
        var tcs = new TaskCompletionSource();
        var reconnectedEventTriggered = false;

        var clientConnection = await _redisServer.CreateConnectionAsync();
        clientConnection.ReConnected += connectionType =>
        {
            _testOutputHelper.WriteLine($"{nameof(TestAutomaticReconnect)} ReConnected handled - connectionType {connectionType}");
            reconnectedEventTriggered = true;
            tcs.SetResult();
            return Task.CompletedTask;
        };

        await _redisServer.StopAsync();
        await _redisServer.StartAsync();

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(reconnectedEventTriggered);
    }
}