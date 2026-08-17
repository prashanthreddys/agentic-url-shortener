namespace UrlShortener.Orchestration.Observability;

/// <summary>
/// Reliability and performance metrics for a run: success rate, retry/rollback frequency, MTTR, and
/// end-to-end latency. Thread-safe increments so parallel stage waves can update it concurrently.
/// </summary>
public sealed class ReliabilityMetrics
{
    private readonly object _lock = new();
    private readonly Dictionary<string, TimeSpan> _stageLatencies = new();
    private readonly List<TimeSpan> _recoveryDurations = new();

    public int TotalStages { get; set; }
    public int StagesAttempted { get; private set; }
    public int StagesSucceeded { get; private set; }
    public int StagesFailed { get; private set; }
    public int Retries { get; private set; }
    public int Rollbacks { get; private set; }
    public int Replans { get; private set; }
    public int ApprovalsRequested { get; private set; }
    public int ApprovalsRejected { get; private set; }
    public int GuardrailDenials { get; private set; }
    public TimeSpan EndToEndLatency { get; set; }

    public void MarkAttempted() { lock (_lock) StagesAttempted++; }
    public void MarkSucceeded() { lock (_lock) StagesSucceeded++; }
    public void MarkFailed() { lock (_lock) StagesFailed++; }
    public void MarkRetry() { lock (_lock) Retries++; }
    public void MarkRollback() { lock (_lock) Rollbacks++; }
    public void MarkReplan() { lock (_lock) Replans++; }
    public void MarkApprovalRequested() { lock (_lock) ApprovalsRequested++; }
    public void MarkApprovalRejected() { lock (_lock) ApprovalsRejected++; }
    public void MarkGuardrailDenial() { lock (_lock) GuardrailDenials++; }

    public void RecordStageLatency(string stage, TimeSpan elapsed)
    {
        lock (_lock) _stageLatencies[stage] = elapsed;
    }

    /// <summary>Time from a stage's first failure to its eventual recovery (retry/fallback success).</summary>
    public void RecordRecovery(TimeSpan duration)
    {
        lock (_lock) _recoveryDurations.Add(duration);
    }

    public IReadOnlyDictionary<string, TimeSpan> StageLatencies
    {
        get { lock (_lock) return new Dictionary<string, TimeSpan>(_stageLatencies); }
    }

    public double SuccessRate =>
        StagesAttempted == 0 ? 0 : Math.Round(StagesSucceeded / (double)StagesAttempted, 3);

    public double RetryFrequency =>
        StagesAttempted == 0 ? 0 : Math.Round(Retries / (double)StagesAttempted, 3);

    public double RollbackFrequency =>
        StagesAttempted == 0 ? 0 : Math.Round(Rollbacks / (double)StagesAttempted, 3);

    public TimeSpan MeanTimeToRecovery
    {
        get
        {
            lock (_lock)
            {
                if (_recoveryDurations.Count == 0) return TimeSpan.Zero;
                var totalTicks = _recoveryDurations.Sum(d => d.Ticks);
                return TimeSpan.FromTicks(totalTicks / _recoveryDurations.Count);
            }
        }
    }
}
