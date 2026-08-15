namespace DocumentProcessing.Core.Layout;

/// <summary>
/// Narrow external-capability boundary for analyzing one already-rasterized
/// physical page into neutral layout evidence.
/// </summary>
public interface IPageLayoutAnalyzer
{
    ValueTask<LayoutAnalysisResult> AnalyzeAsync(
        Stream rasterImage,
        int physicalPageNumber,
        int pixelWidth,
        int pixelHeight,
        CancellationToken cancellationToken = default);
}
