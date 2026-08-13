namespace DocumentProcessing.Core.Documents;

public sealed class DocumentSource
{
    public DocumentSource(
        Stream content,
        string? fileName = null,
        string? declaredMediaType = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));

        if (!content.CanRead)
        {
            throw new ArgumentException("Document source stream must be readable.", nameof(content));
        }

        FileName = string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim();

        DeclaredMediaType = string.IsNullOrWhiteSpace(declaredMediaType)
            ? null
            : declaredMediaType.Trim();
    }

    public Stream Content { get; }
    public string? FileName { get; }
    public string? DeclaredMediaType { get; }
}
