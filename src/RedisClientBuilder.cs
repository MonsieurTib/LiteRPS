using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LiteRPS;

public class RedisClientBuilder
{
    private bool _built;

    public RedisClientBuilder()
    {
        Services = new ServiceCollection();
        Services.TryAddSingleton<RedisClient>();
        Services.AddLogging();
    }

    public IServiceCollection Services { get; }

    public RedisClient Build()
    {
        if (_built)
        {
            throw new InvalidOperationException("already built");
        }

        _built = true;

        var serviceProvider = Services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<RedisClient>();
    }
}