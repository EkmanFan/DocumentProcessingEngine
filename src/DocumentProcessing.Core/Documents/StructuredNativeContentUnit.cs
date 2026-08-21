namespace DocumentProcessing.Core.Documents;

/// <summary>
/// One ordered source-native content unit, such as an EPUB spine resource.
/// </summary>
public sealed record StructuredNativeContentUnit
{
    #region Properties

    public string UnitId { get; }

    public IReadOnlyList<StructuredNativeTextBlock> TextBlocks { get; }

    /// <summary>
    /// Gets whether deterministic format context identifies this complete unit
    /// as publication presentation rather than documentary content.
    /// </summary>
    public bool IsPresentationOnly { get; }

    #endregion

    #region ctor

    public StructuredNativeContentUnit(
        string unitId,
        IReadOnlyList<StructuredNativeTextBlock> textBlocks,
        bool isPresentationOnly = false)
    {
        if (string.IsNullOrWhiteSpace(
                unitId))
        {
            throw new ArgumentException(
                "Structured native content-unit ID cannot be empty.",
                nameof(unitId));
        }

        ArgumentNullException.ThrowIfNull(
            textBlocks);

        if (textBlocks.Any(
                block =>
                    block is null))
        {
            throw new ArgumentException(
                "Structured native content units cannot contain null text blocks.",
                nameof(textBlocks));
        }

        UnitId =
            unitId.Trim();

        TextBlocks =
            textBlocks.ToArray();

        IsPresentationOnly =
            isPresentationOnly;
    }

    #endregion
}
