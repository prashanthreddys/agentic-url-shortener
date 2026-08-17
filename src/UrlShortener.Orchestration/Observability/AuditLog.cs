using System.Collections.Concurrent;

namespace UrlShortener.Orchestration.Observability;

/// <summary>
/// Append-only, thread-safe audit trail. Provides audit-grade observability and decision lineage:
/// every gate, guardrail, approval, retry, rollback, and re-plan is recorded with a rationale and a
/// correlation id so the full run can be reconstructed.
/// </summary>
public sealed class AuditLog
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    public string CorrelationId { get; }

    public AuditLog(string correlationId) => CorrelationId = correlationId;

    public void Record(string stage, AuditEventType type, string category, string message, int? attempt = null) =>
        _entries.Enqueue(new AuditEntry(DateTimeOffset.UtcNow, CorrelationId, stage, type, category, message, attempt));

    /// <summary>Records an explicit governance decision with its rationale (decision lineage).</summary>
    public void RecordDecision(string stage, string decision, string rationale) =>
        Record(stage, AuditEventType.Decision, "Decision", $"{decision} :: because {rationale}");

    public IReadOnlyList<AuditEntry> Entries => _entries.ToList();

    public IReadOnlyList<AuditEntry> Decisions =>
        _entries.Where(e => e.Type == AuditEventType.Decision).ToList();
}
