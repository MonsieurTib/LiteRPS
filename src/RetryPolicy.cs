namespace LiteRPS;

public abstract class RetryPolicy
{
    private readonly int _maxRetryCount;
    private int _retryCount;

    protected RetryPolicy(int maxRetryCount)
    {
        _maxRetryCount = maxRetryCount;
    }

    public bool ShouldRetry()
    {
        if (_retryCount >= _maxRetryCount)
        {
            return false;
        }

        _retryCount++;
        return true;
    }

    public abstract TimeSpan NextRetry();

    public virtual void Reset()
    {
        _retryCount = 0;
    }
}