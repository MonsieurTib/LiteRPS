# LiteRPS
LiteRPS is a lightweight .NET client for Redis that focuses on pub/sub functionality. Initially developed as an experiment to practice network protocol, it is currently not recommended for use in production environments.

## Features
- [publish/subscribe](https://redis.com/glossary/pub-sub/)
- [pipelining](https://redis.io/docs/manual/pipelining/)

## Getting start

```csharp

var redisClient = new RedisClientBuilder()
    .ConfigureOptions(options =>
    {
        options.Host = "redis.local";
        options.Port = 6379;
        options.Ssl = false;
    })
    .ConfigureLogging(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Trace);
        builder.AddConsole();
    }).Build();

redisClient.ReConnected += connectionType =>
{
    Console.WriteLine($" {connectionType} reconnected..");
    return Task.CompletedTask;
};

await redisClient.StartAsync();

var sub = await redisClient.SubscribeAsync<string>("foo", msg =>
{
    Console.WriteLine($"message received from foo : {msg}");
    return Task.CompletedTask;
});

await redisClient.SendMessageAsync("foo", "hello")

```
