namespace DocumentProcessing.Core.Processing;

/// <summary>
/// Stable DPEngine capability requested during document processing.
///
/// This identifies what the Engine asked to be done, independently from the
/// concrete software that performed it.
/// </summary>
public enum ProcessingCapability
{
    LayoutAnalysis = 1,
    TextRecognition = 2
}
