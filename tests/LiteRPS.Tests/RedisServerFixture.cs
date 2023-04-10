using Docker.DotNet;
using Docker.DotNet.Models;
using Xunit.Abstractions;

namespace LiteRPS.Tests;

public class RedisServerFixture : IAsyncDisposable
{
    private const int Port = 9300;
    private const string Host = "redis.local";
    private const string ContainerName = "lite-rps-tests";
    private readonly DockerClient _client;
    private readonly CancellationTokenSource _stoppingToken = new();
    private string? _containerId;
    private bool _disposed;
    private ITestOutputHelper _testOutputHelper;

    public RedisServerFixture()
    {
        _client = new DockerClientConfiguration()
            .CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _stoppingToken.Cancel();
            _stoppingToken.Dispose();

            try
            {
                await StopAsync();
                await _client.Containers.RemoveContainerAsync(_containerId, new ContainerRemoveParameters());
                _client.Dispose();
            }
            catch (Exception ex)
            {
                _testOutputHelper.WriteLine($"{nameof(RedisServerFixture)}.{nameof(DisposeAsync)} failed : {ex.Message}");
            }
        }
    }

    public void SetLogger(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public async Task StartAsync()
    {
        _testOutputHelper.WriteLine($"{nameof(RedisServerFixture)}.{nameof(StartAsync)}");
        var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                {
                    "name", new Dictionary<string, bool>
                    {
                        { ContainerName, true }
                    }
                }
            }
        });
        var existing = containers.FirstOrDefault();
        if (existing == null)
        {
            _testOutputHelper.WriteLine($"{nameof(RedisServerFixture)} can not find existing container {ContainerName}, so creating one");
            var container = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = "redis",
                Name = ContainerName,
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            "6379/tcp", new List<PortBinding>
                            {
                                new() { HostIP = "", HostPort = Port.ToString() }
                            }
                        }
                    }
                }
            }, _stoppingToken.Token);
            _containerId = container.ID;
            await _client.Containers.StartContainerAsync(_containerId, new ContainerStartParameters(), _stoppingToken.Token);
        }
        else
        {
            _testOutputHelper.WriteLine($"{nameof(RedisServerFixture)} container {ContainerName} found with current state {existing.State}");
            _containerId = existing.ID;
            if (!existing.State.Equals("running"))
            {
                await _client.Containers.StartContainerAsync(_containerId, new ContainerStartParameters(), _stoppingToken.Token);
            }
        }
    }

    public async Task StopAsync()
    {
        _testOutputHelper.WriteLine($"{nameof(RedisServerFixture)}.{nameof(StopAsync)}");
        await _client.Containers.StopContainerAsync(_containerId, new ContainerStopParameters(), _stoppingToken.Token);
    }

    public async Task<RedisClient> CreateConnectionAsync()
    {
        var redisClient = new RedisClientBuilder()
            .ConfigureOptions(options =>
            {
                options.Host = Host;
                options.Port = Port;
                options.Ssl = false;
            })
            .Build();
        await redisClient.StartAsync();
        return redisClient;
    }
}