using UrlShortener.Orchestration.Observability;

namespace UrlShortener.Orchestration.Execution;

/// <summary>
/// Context passed to a stage agent for one execution attempt. Exposes the shared blackboard, the
/// current attempt number, and the audit sink so agents can record their reasoning.
/// </summary>
public sealed class StageContext
{
    public string StageId { get; }
    public Blackboard Blackboard { get; }
    public int Attempt { get; }
    public AuditLog Audit { get; }
    public CancellationToken CancellationToken { get; }

    public StageContext(string stageId, Blackboard blackboard, int attempt, AuditLog audit, CancellationToken ct)
    {
        StageId = stageId;
        Blackboard = blackboard;
        Attempt = attempt;
        Audit = audit;
        CancellationToken = ct;
    }
}

/// <summary>
/// A unit of SDLC work (requirements analysis, design, implementation, testing, ...). Agents run
/// under governance: their outcome is only committed after exit gates and, where required, approval.
/// </summary>
public interface IStageAgent
{
    Task<StageOutcome> ExecuteAsync(StageContext context);
}

/// <summary>Compensating action used to undo a stage's effects during rollback.</summary>
public interface IRollbackAction
{
    Task RollbackAsync(StageContext context);
}

/// <summary>A rollback action defined inline.</summary>
public sealed class DelegateRollback : IRollbackAction
{
    private readonly Func<StageContext, Task> _action;
    public DelegateRollback(Func<StageContext, Task> action) => _action = action;
    public DelegateRollback(Action<StageContext> action) => _action = ctx => { action(ctx); return Task.CompletedTask; };
    public Task RollbackAsync(StageContext context) => _action(context);
}
