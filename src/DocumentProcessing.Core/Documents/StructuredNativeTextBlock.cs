using DocumentProcessing.Core.Locations;

namespace DocumentProcessing.Core.Documents;

/// <summary>
/// One ordered native text block acquired from a structured, non-paged source.
/// </summary>
public sealed record StructuredNativeTextBlock
{
    #region Properties

    public StructuredNativeTextBlockKind Kind { get; }

    public DocumentSourceLocation Location { get; }

    /// <summary>
    /// Gets source text before Engine-owned whitespace normalization.
    /// </summary>
    public string SourceText { get; }

    #endregion

    #region ctor

    public StructuredNativeTextBlock(
        StructuredNativeTextBlockKind kind,
        DocumentSourceLocation location,
        string sourceText)
    {
        Location =
            location ??
            throw new ArgumentNullException(
                nameof(location));

        if (string.IsNullOrWhiteSpace(
                sourceText))
        {
            throw new ArgumentException(
                "Structured native text cannot be empty.",
                nameof(sourceText));
        }

        Kind =
            kind;

        SourceText =
            sourceText;
    }

    #endregion
}
