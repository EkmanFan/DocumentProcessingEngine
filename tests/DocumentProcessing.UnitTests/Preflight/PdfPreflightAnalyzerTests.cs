using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.UnitTests.Preflight;

public sealed class PdfPreflightAnalyzerTests
{
    private readonly PdfPreflightAnalyzer analyzer = new();

    [Fact]
    public void Analyze_ClassifiesBornDigitalHybridRasterAndProblematicCases()
    {
        var bornDigital = analyzer.Analyze(Create(Page(1, 10), Page(2, 8)));
        var hybrid = analyzer.Analyze(Create(Page(1, 10), Page(2, 0, 0.95)));
        var raster = analyzer.Analyze(Create(Page(1, 0, 0.90), Page(2, 0, 0.75)));
        var problematic = analyzer.Analyze(Create(Page(1, 0), Page(2, 0, 0.20)));

        Assert.Equal(DocumentPreflightClassification.HealthyBornDigital, bornDigital.Classification);
        Assert.Equal(DocumentPreflightClassification.Hybrid, hybrid.Classification);
        Assert.Equal(DocumentPreflightClassification.RasterOrScanned, raster.Classification);
        Assert.Equal(DocumentPreflightClassification.Problematic, problematic.Classification);

        Assert.Equal(50, hybrid.TextLayerCoveragePercent);
        Assert.Equal([2], hybrid.TextlessPageNumbers);
        Assert.Equal([2], hybrid.TextlessDominantRasterPageNumbers);
    }

    [Fact]
    public void Analyze_UsesApologiaBaselineDominantRasterThreshold()
    {
        var result = analyzer.Analyze(Create(Page(1, 0, 0.60)));

        Assert.Equal(DocumentPreflightClassification.RasterOrScanned, result.Classification);
        Assert.Equal(0.60, PdfPreflightAnalyzer.DominantRasterImageAreaRatio);
    }

    [Fact]
    public void Analyze_RejectsNonPdfExtraction()
    {
        var extraction = new DocumentExtractionResult(
            new DocumentFormatId("epub"),
            [Page(1, 1)]);

        Assert.Throws<NotSupportedException>(() => analyzer.Analyze(extraction));
    }

    private static DocumentExtractionResult Create(params DocumentExtractionPage[] pages) =>
        new(DocumentFormatId.Pdf, pages);

    private static DocumentExtractionPage Page(
        int pageNumber,
        int wordCount,
        double rasterRatio = 0) =>
        new(
            pageNumber,
            wordCount > 0 ? "native text" : string.Empty,
            wordCount,
            rasterImageCount: rasterRatio > 0 ? 1 : 0,
            largestRasterImageAreaRatio: rasterRatio);
}
