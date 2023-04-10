namespace LiteRPS;

public class ConfigurationOptions
{
    public readonly RetryPolicy RetryPolicy = new LinearRetryPolicy(10, TimeSpan.FromSeconds(10));
    public string Host { get; set; }
    public int Port { get; set; }
    public string Password { get; set; }
    public bool Ssl { get; set; } = true;
}