namespace DocumentProcessing.Core.Results;

/// <summary>
/// Format-neutral semantic kind of one processed document element.
/// </summary>
/// <remarks>
/// These values describe the portable result model and deliberately avoid the
/// runtime-specific "Hybrid" terminology used by the current PDF pipeline.
/// Format-specific processors map their internal element kinds to this contract.
/// </remarks>
public enum DocumentElementKind
{
    Text = 0,
    Heading = 1,
    Caption = 2,
    Visual = 3,
    UnresolvedText = 4,
    Deferred = 5
}
