namespace DocumentProcessing.Engine.Layout;

/// <summary>
/// Processing action selected from neutral layout evidence.
///
/// The values are intentionally operational rather than semantic:
/// layout classification does not decide what an image means.
/// </summary>
public enum LayoutTreatment
{
    Deferred = 0,
    RecognizeText = 1,
    PreserveVisualWithoutOcr = 2
}
