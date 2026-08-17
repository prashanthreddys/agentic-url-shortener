namespace UrlShortener.Orchestration.Governance;

/// <summary>Bounded retry configuration for a stage. Backoff is kept tiny so demos stay fast.</summary>
public sealed class RetryPolicy
{
    public int MaxAttempts { get; init; } = 1;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.Zero;

    public static RetryPolicy None { get; } = new() { MaxAttempts = 1 };

    public static RetryPolicy Bounded(int maxAttempts, TimeSpan? baseDelay = null) =>
        new() { MaxAttempts = Math.Max(1, maxAttempts), BaseDelay = baseDelay ?? TimeSpan.Zero };

    /// <summary>Exponential backoff delay for a given (1-based) attempt.</summary>
    public TimeSpan DelayFor(int attempt) =>
        BaseDelay <= TimeSpan.Zero ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
}
