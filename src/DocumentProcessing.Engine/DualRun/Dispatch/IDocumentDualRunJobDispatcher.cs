namespace DocumentProcessing.Engine.DualRun.Dispatch;

/// <summary>
/// Narrow producer-side boundary used by future Dual Run submission logic.
///
/// TryDispatch never waits for capacity.
/// </summary>
public interface IDocumentDualRunJobDispatcher
{
    DocumentDualRunDispatchOutcome TryDispatch(
        DocumentDualRunPreparedJob job);
}
