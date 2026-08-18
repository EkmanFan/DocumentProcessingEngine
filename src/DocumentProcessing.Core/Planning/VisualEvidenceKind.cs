namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Neutral deterministic evidence class for one embedded visual element.
///
/// Values describe observed structural/visual evidence. They are not storage
/// instructions and are not themselves authority to delete or preserve bytes.
/// A later policy maps evidence to a <see cref="VisualDisposition"/>.
/// </summary>
public enum VisualEvidenceKind
{
    /// <summary>
    /// Available deterministic evidence is insufficient for a safe visual
    /// interpretation. Downstream policy must fail closed.
    /// </summary>
    Unknown,

    /// <summary>
    /// The decoded visual is effectively a blank canvas.
    /// </summary>
    BlankCanvas,

    /// <summary>
    /// Effective foreground is tiny or noise-like under the validated
    /// deterministic evidence profile.
    /// </summary>
    TinyOrNoise,

    /// <summary>
    /// A small visual is structurally associated with a semantic heading.
    /// This is evidence consistent with a heading ornament, not a claim that
    /// the heading itself is decorative.
    /// </summary>
    SmallHeadingAssociatedVisual,

    /// <summary>
    /// The visual behaves like presentation behind or around contained
    /// heading text.
    /// </summary>
    HeadingBackplateOrPresentation,

    /// <summary>
    /// The visual bounds contain substantial native document structure and are
    /// consistent with a frame, box, background or other presentation
    /// container.
    /// </summary>
    NativeTextContainerOrFrame,

    /// <summary>
    /// A strong caption association identifies a meaningful visual candidate.
    /// </summary>
    CaptionedMeaningfulVisual,

    /// <summary>
    /// A substantial visual is spatially independent from native text and is a
    /// meaningful-visual candidate.
    /// </summary>
    LargeIndependentVisual
}
