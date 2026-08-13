using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Normalization;

/// <summary>
/// A deterministic textual projection of an extracted layout block.
/// The original block remains available as source evidence.
/// </summary>
public sealed class NormalizedDocumentTextBlock
{
    public NormalizedDocumentTextBlock(
        DocumentTextBlock sourceBlock,
        string text)
    {
        SourceBlock =
            sourceBlock ??
            throw new ArgumentNullException(nameof(sourceBlock));

        Text =
            text ??
            throw new ArgumentNullException(nameof(text));
    }

    public DocumentTextBlock SourceBlock { get; }

    public string SourceText => SourceBlock.Text;

    public string Text { get; }
}
