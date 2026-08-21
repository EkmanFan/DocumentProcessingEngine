namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Concrete engine action for one source visual occurrence.
///
/// The action is independent from the text execution mode. In particular,
/// visual analysis does not imply OCR.
/// </summary>
public enum VisualExecutionAction
{
    /// <summary>
    /// No additional semantic visual processing is required.
    ///
    /// This does not authorize deletion of source bytes or fidelity assets.
    /// </summary>
    NoAdditionalSemanticProcessing,

    /// <summary>
    /// Preserve the visual as meaningful documentary content without requiring
    /// layout/OCR merely for text verification.
    /// </summary>
    PreserveMeaningfulVisual,

    /// <summary>
    /// Preserve the visual while reporting that the Engine could not qualify
    /// it as meaningful or presentational.
    /// </summary>
    PreserveUnqualifiedVisual,

    /// <summary>
    /// The visual remains unresolved and requires visual analysis.
    ///
    /// Source evidence must remain available until the analysis is resolved.
    /// </summary>
    AnalyzeVisual
}
