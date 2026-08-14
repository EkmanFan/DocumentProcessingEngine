namespace DocumentProcessing.Core.Extraction;

public sealed class DocumentExtractionPage
{
    private static readonly NormalizedRectangle FullPageViewport =
        new(
            0,
            0,
            1,
            1);

    public DocumentExtractionPage(
        int physicalPageNumber,
        string sourceText,
        int wordCount = 0,
        int rasterImageCount = 0,
        double largestRasterImageAreaRatio = 0,
        double sourceWidth = 0,
        double sourceHeight = 0,
        IReadOnlyList<DocumentWord>? words = null,
        IReadOnlyList<DocumentTextBlock>? blocks = null)
        : this(
            physicalPageNumber,
            sourceText,
            FullPageViewport,
            wordCount,
            rasterImageCount,
            largestRasterImageAreaRatio,
            sourceWidth,
            sourceHeight,
            words,
            blocks)
    {
    }

    public DocumentExtractionPage(
        int physicalPageNumber,
        string sourceText,
        NormalizedRectangle contentViewport,
        int wordCount = 0,
        int rasterImageCount = 0,
        double largestRasterImageAreaRatio = 0,
        double sourceWidth = 0,
        double sourceHeight = 0,
        IReadOnlyList<DocumentWord>? words = null,
        IReadOnlyList<DocumentTextBlock>? blocks = null)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be greater than zero.");
        }

        if (wordCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount));
        }

        if (rasterImageCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rasterImageCount));
        }

        if (!double.IsFinite(
                largestRasterImageAreaRatio) ||
            largestRasterImageAreaRatio < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(largestRasterImageAreaRatio));
        }

        if (!double.IsFinite(
                sourceWidth) ||
            sourceWidth < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceWidth));
        }

        if (!double.IsFinite(
                sourceHeight) ||
            sourceHeight < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceHeight));
        }

        if (contentViewport.Right -
                contentViewport.Left <= 0 ||
            contentViewport.Bottom -
                contentViewport.Top <= 0)
        {
            throw new ArgumentException(
                "Content viewport must have positive width and height.",
                nameof(contentViewport));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        SourceText =
            sourceText ??
            throw new ArgumentNullException(
                nameof(sourceText));

        ContentViewport =
            contentViewport;

        WordCount =
            wordCount;

        RasterImageCount =
            rasterImageCount;

        LargestRasterImageAreaRatio =
            largestRasterImageAreaRatio;

        SourceWidth =
            sourceWidth;

        SourceHeight =
            sourceHeight;

        Words =
            words ??
            [];

        Blocks =
            blocks ??
            [];
    }

    public int PhysicalPageNumber { get; }

    public string SourceText { get; }

    /// <summary>
    /// Effective source/content viewport represented inside canonical page
    /// coordinates.
    ///
    /// For PDF extraction this is the effective CropBox expressed in the
    /// canonical MediaBox display coordinate space. Other producers default to
    /// the full page when they have no narrower viewport semantics.
    /// </summary>
    public NormalizedRectangle ContentViewport { get; }

    public int WordCount { get; }

    public int RasterImageCount { get; }

    public double LargestRasterImageAreaRatio { get; }

    /// <summary>
    /// Width of the canonical page coordinate space used by extractor evidence.
    /// </summary>
    public double SourceWidth { get; }

    /// <summary>
    /// Height of the canonical page coordinate space used by extractor evidence.
    /// </summary>
    public double SourceHeight { get; }

    public IReadOnlyList<DocumentWord> Words { get; }

    /// <summary>
    /// Layout blocks in derived reading order.
    /// Each block retains its original SourceSequence independently.
    /// </summary>
    public IReadOnlyList<DocumentTextBlock> Blocks { get; }
}
