namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Availability and foreground state of the decoded visual observation.
/// </summary>
public enum VisualForegroundState
{
    /// <summary>
    /// Foreground analysis is unavailable or indeterminate.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The visual decoded successfully and contains no effective foreground.
    /// </summary>
    BlankCanvas,

    /// <summary>
    /// Foreground pixels were measured deterministically.
    /// </summary>
    Measured
}

/// <summary>
/// Deterministic relationship between measured foreground pixels and native
/// word boxes.
/// </summary>
public enum VisualPixelInteractionKind
{
    NotMeasured,
    NoNativeWords,
    BlankCanvas,
    NoForegroundWordIntersection,
    LowForegroundWordInteraction,
    ForegroundWordInteraction
}

/// <summary>
/// Structural evidence relating the effective visual to the nearest semantic
/// heading.
/// </summary>
public enum HeadingAssociationEvidenceKind
{
    NotMeasured,
    NoStrongAssociation,
    PossibleAdjacentVisual,
    StrongAdjacentVisual
}

/// <summary>
/// Native document structure contained by the effective visual bounds.
/// </summary>
public enum NativeTextContainmentEvidenceKind
{
    NotMeasured,
    NoContainedNativeText,
    SparseContainedText,
    HeadingDominatedContainedText,
    TextRichContainer
}

/// <summary>
/// Deterministic caption relationship from geometry plus optional generic
/// caption lexical evidence.
/// </summary>
public enum CaptionAssociationEvidenceKind
{
    NotMeasured,
    NoAssociation,
    NoStrongAssociation,
    PossibleAssociation,
    StrongAssociation
}
