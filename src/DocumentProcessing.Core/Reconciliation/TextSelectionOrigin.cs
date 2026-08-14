namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Origin of the text selected by reconciliation.
///
/// V1 deliberately has no Merged value because Phase 17A never synthesizes a
/// third text from native and OCR strings.
/// </summary>
public enum TextSelectionOrigin
{
    None,
    NativePdf,
    Ocr
}
