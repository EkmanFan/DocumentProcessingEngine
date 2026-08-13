namespace DocumentProcessing.Core.Extraction;

public sealed class DocumentExtractionPage
{
    public DocumentExtractionPage(
        int physicalPageNumber,
        string sourceText,
        int wordCount = 0,
        int rasterImageCount = 0,
        double largestRasterImageAreaRatio = 0)
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

        PhysicalPageNumber = physicalPageNumber;
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        WordCount = wordCount;
        RasterImageCount = rasterImageCount;
        LargestRasterImageAreaRatio = largestRasterImageAreaRatio;
    }

    public int PhysicalPageNumber { get; }
    public string SourceText { get; }
    public int WordCount { get; }
    public int RasterImageCount { get; }
    public double LargestRasterImageAreaRatio { get; }
}
