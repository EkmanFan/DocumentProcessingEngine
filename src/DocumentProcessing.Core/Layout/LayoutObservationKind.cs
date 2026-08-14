namespace DocumentProcessing.Core.Layout;

/// <summary>
/// Neutral document-layout role inferred by a layout backend.
/// This is intentionally smaller than any backend-specific label vocabulary.
/// </summary>
public enum LayoutObservationKind
{
    Unknown = 0,
    Text = 1,
    Heading = 2,
    Caption = 3,
    Figure = 4,
    Table = 5
}
