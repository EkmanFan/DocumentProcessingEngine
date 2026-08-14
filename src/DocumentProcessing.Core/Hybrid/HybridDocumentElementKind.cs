namespace DocumentProcessing.Core.Hybrid;

/// <summary>
/// Neutral element kinds in the unified hybrid page stream.
///
/// Textual kinds contain authoritative selected text. UnresolvedText and
/// Deferred deliberately contain no authoritative text. Visual contains
/// preserved raster evidence and never narrative text.
/// </summary>
public enum HybridDocumentElementKind
{
    Text = 0,
    Heading = 1,
    Caption = 2,
    Visual = 3,
    UnresolvedText = 4,
    Deferred = 5
}
