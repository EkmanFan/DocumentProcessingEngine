namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Policy vocabulary for what the engine should do with visual evidence.
///
/// Phase 21E.1H.1 defines the vocabulary only. No production assessor or route
/// currently assigns these values.
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
    /// Deterministic evidence is insufficient for a safe disposition.
    /// Processing must remain conservative.
    /// </summary>
    RequiresVisualAnalysis
}
