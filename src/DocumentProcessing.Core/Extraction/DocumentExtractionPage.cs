namespace DocumentProcessing.Core.Extraction;

public sealed class DocumentExtractionPage
{
    public DocumentExtractionPage(
        int physicalPageNumber,
        string sourceText,
        int wordCount = 0,
        int rasterImageCount = 0,
        double largestRasterImageAreaRatio = 0,
        double sourceWidth = 0,
        double sourceHeight = 0,
        IReadOnlyList<DocumentWord>? words = null)
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
            throw new ArgumentOutOfRangeException(nameof(wordCount));
        }

        if (rasterImageCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rasterImageCount));
        }

        if (!double.IsFinite(largestRasterImageAreaRatio) ||
            largestRasterImageAreaRatio < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(largestRasterImageAreaRatio));
        }

        if (!double.IsFinite(sourceWidth) || sourceWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        }

        if (!double.IsFinite(sourceHeight) || sourceHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceHeight));
        }

        PhysicalPageNumber = physicalPageNumber;
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        WordCount = wordCount;
        RasterImageCount = rasterImageCount;
        LargestRasterImageAreaRatio = largestRasterImageAreaRatio;
        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
        Words = words ?? [];
    }

    public int PhysicalPageNumber { get; }
    public string SourceText { get; }
    public int WordCount { get; }
    public int RasterImageCount { get; }
    public double LargestRasterImageAreaRatio { get; }

    /// <summary>
    /// Width of the page in the source extractor coordinate space.
    /// </summary>
    public double SourceWidth { get; }

    /// <summary>
    /// Height of the page in the source extractor coordinate space.
    /// </summary>
    public double SourceHeight { get; }

    public IReadOnlyList<DocumentWord> Words { get; }
}
