namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Format-neutral origin of authoritative text retained in the portable result.
/// </summary>
/// <remarks>
/// <see cref="Native"/> means text obtained from the document's native
/// structure or text layer. It deliberately does not encode a PDF-specific
/// origin such as "NativePdf".
/// </remarks>
public enum DocumentTextSourceKind
{
    None = 0,
    Native = 1,
    Ocr = 2
}
