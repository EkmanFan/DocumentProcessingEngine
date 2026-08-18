namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Receives non-authoritative document shadow-planning reports.
///
/// Observer failures are isolated by the shadow runner and cannot change legacy
/// runtime execution. Caller-requested cancellation still propagates.
/// </summary>
public interface IDocumentDualRunPlanningObserver
{
    ValueTask ObserveAsync(
        DocumentDualRunPlanningReport report,
        CancellationToken cancellationToken = default);
}
