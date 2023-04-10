using LiteRPS;
using Microsoft.Extensions.Logging;

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
Console.WriteLine("subscribing");

const int count = 10;
var subscribingTasks = new List<Task>();
for (var i = 0; i < count; i++)
{
    var n = i;
    var sub = redisClient.SubscribeAsync<string>($"topic_{n}", s =>
    {
        Console.WriteLine($"message received from sub_{n} : {s}");
        return Task.CompletedTask;
    });
    subscribingTasks.Add(sub);
}

//await redisClient.UnSubscribeAsync("topic_0");

await Task.WhenAll(subscribingTasks);
Console.WriteLine("subscribed");
while (true)
{
    Console.WriteLine("press any key to send messages");
    Console.ReadKey();
    var sendingTask = new List<Task>();
    for (var i = 0; i < count; i++)
    {
        var payload = $"SuperHelloWorld_{i}";
        if (i == 2)
        {
            payload = "start_"+string.Join(string.Empty, Enumerable.Range(0, 5000).Select(index => 'a'))+"_end";
        }

        sendingTask.Add(redisClient.SendMessageAsync($"topic_{i}", payload));
    }

    await Task.WhenAll(sendingTask);
    Console.WriteLine("messages sent");
}
