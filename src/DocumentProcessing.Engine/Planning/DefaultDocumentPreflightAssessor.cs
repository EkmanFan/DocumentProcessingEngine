using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Preflight;

namespace DocumentProcessing.Engine.Planning;

/// <summary>
/// Applies the current deterministic paged-document preflight assessment policy
/// to already acquired native extraction evidence.
/// </summary>
/// <remarks>
/// Raw word counts and raster-area ratios are format-provided evidence. The
/// dominant-raster threshold and document classification are Engine policy.
/// </remarks>
public sealed class DefaultDocumentPreflightAssessor
    : IDocumentPreflightAnalyzer
{
    #region Variables and Constants

    public const double DominantRasterImageAreaRatio =
        0.60;

    private readonly DocumentFormatId _format;

    #endregion

    #region ctor

    public DefaultDocumentPreflightAssessor(
        DocumentFormatId format)
    {
        if (string.IsNullOrWhiteSpace(
                format.Value))
        {
            throw new ArgumentException(
                "Document format cannot be empty.",
                nameof(format));
        }

        _format =
            format;
    }

    #endregion

    #region Methods Assessment

    public bool CanAnalyze(
        DocumentFormatId format) =>
        format ==
        _format;

    public DocumentPreflightResult Analyze(
        DocumentExtractionResult extraction)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        if (!CanAnalyze(
                extraction.Format))
        {
            throw new NotSupportedException(
                $"Format '{extraction.Format}' is not supported by the configured document preflight assessor.");
        }

        var pages =
            extraction.Pages;

        var pageCount =
            pages.Count;

        var pagesWithNativeText =
            pages.Count(
                page =>
                    page.WordCount >
                    0);

        var pagesWithoutNativeText =
            pageCount -
            pagesWithNativeText;

        var textlessPageNumbers =
            pages
                .Where(
                    page =>
                        page.WordCount ==
                        0)
                .Select(
                    page =>
                        page.PhysicalPageNumber)
                .OrderBy(
                    pageNumber =>
                        pageNumber)
                .ToArray();

        var textlessDominantRasterPageNumbers =
            pages
                .Where(
                    page =>
                        page.WordCount ==
                            0 &&
                        page.LargestRasterImageAreaRatio >=
                            DominantRasterImageAreaRatio)
                .Select(
                    page =>
                        page.PhysicalPageNumber)
                .OrderBy(
                    pageNumber =>
                        pageNumber)
                .ToArray();

        var coverage =
            pageCount ==
            0
                ? 0
                : Math.Round(
                    pagesWithNativeText *
                    100.0 /
                    pageCount,
                    1);

        var classification =
            pageCount ==
            0
                ? DocumentPreflightClassification.Problematic
                : pagesWithNativeText ==
                  pageCount
                    ? DocumentPreflightClassification.HealthyBornDigital
                    : pagesWithNativeText >
                      0
                        ? DocumentPreflightClassification.Hybrid
                        : textlessDominantRasterPageNumbers.Length ==
                          pageCount
                            ? DocumentPreflightClassification.RasterOrScanned
                            : DocumentPreflightClassification.Problematic;

        return new DocumentPreflightResult(
            extraction.Format,
            pageCount,
            pagesWithNativeText,
            pagesWithoutNativeText,
            coverage,
            textlessPageNumbers,
            textlessDominantRasterPageNumbers,
            classification);
    }

    #endregion
}
