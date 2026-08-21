namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Policy vocabulary for what the engine should do with visual evidence.
///
/// Deterministic visual-evidence policy assigns this vocabulary for planning.
/// The enum itself carries no execution authority.
/// </summary>
public enum VisualDisposition
{
    /// <summary>
    /// No visual element requires processing.
    /// </summary>
    NoVisual,

    /// <summary>
    /// The visual is presentation-only for document understanding.
    ///
    /// This does not mean that source bytes must be physically deleted. A
    /// fidelity-oriented consumer may still retain source assets.
    /// </summary>
    PresentationOnly,

    /// <summary>
    /// The visual carries documentary meaning and should remain preservable.
    /// </summary>
    PreserveMeaningfulVisual,

    /// <summary>
    /// The visual could not be qualified confidently after analysis, but must
    /// remain available to the consumer rather than being discarded.
    /// </summary>
    PreserveUnqualifiedVisual,

    /// <summary>
    /// Deterministic evidence is insufficient for a safe disposition.
    /// Processing must remain conservative.
    /// </summary>
    RequiresVisualAnalysis
}
