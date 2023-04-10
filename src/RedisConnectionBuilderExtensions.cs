using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace LiteRPS;

public static class RedisClientBuilderExtensions
{
    public static RedisClientBuilder ConfigureLogging(this RedisClientBuilder builder, Action<ILoggingBuilder> configureLogging)
    {
        builder.Services.AddLogging(configureLogging);
        return builder;
    }

    public static RedisClientBuilder ConfigureOptions(this RedisClientBuilder builder, Action<ConfigurationOptions> configureOptions)
    {
        var options = new ConfigurationOptions();
        configureOptions(options);
        builder.Services.TryAddSingleton(options);
        return builder;
    }
}