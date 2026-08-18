namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Receives non-authoritative document Dual Run planning reports.
///
/// Observer failures are isolated by the Dual Run runner and cannot change authoritative
/// runtime execution. Caller-requested cancellation still propagates.
/// </summary>
public interface IDocumentDualRunPlanningObserver
{
    ValueTask ObserveAsync(
        DocumentDualRunPlanningReport report,
        CancellationToken cancellationToken = default);
}
