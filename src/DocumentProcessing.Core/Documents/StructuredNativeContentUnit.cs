namespace DocumentProcessing.Core.Documents;

/// <summary>
/// One ordered source-native content unit, such as an EPUB spine resource.
/// </summary>
public sealed record StructuredNativeContentUnit
{
    #region Properties

    public string UnitId { get; }

    public IReadOnlyList<StructuredNativeTextBlock> TextBlocks { get; }

    #endregion

    #region ctor

    public StructuredNativeContentUnit(
        string unitId,
        IReadOnlyList<StructuredNativeTextBlock> textBlocks)
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
    }

    #endregion
}
