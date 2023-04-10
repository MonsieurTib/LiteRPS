namespace LiteRPS;

public class LinearRetryPolicy : RetryPolicy
{
    private readonly TimeSpan _retryDelay;

    public LinearRetryPolicy(int maxRetryCount, TimeSpan retryDelay) : base(maxRetryCount)
    {
        _retryDelay = retryDelay;
    }

    public override TimeSpan NextRetry() => _retryDelay;
}