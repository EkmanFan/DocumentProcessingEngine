using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Neutral unresolved evidence produced for one AnalyzeVisual source occurrence.
///
/// H.4D.4B.1 retains the page raster/layout evidence without converting the
/// source occurrence into a semantic Figure or other final disposition.
/// </summary>
public sealed record DocumentControlledCandidateVisualAnalysisProvenance
{
    public DocumentControlledCandidateVisualAnalysisProvenance(
        string sourceDocumentSha256,
        int sourceVisualIndex,
        RasterRenderResult pageRaster,
        LayoutAnalysisResult layout)
    {
        if (sourceVisualIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex));
        }

        SourceDocumentSha256 =
            NormalizeSha256(
                sourceDocumentSha256);

        PageRaster =
            pageRaster ??
            throw new ArgumentNullException(
                nameof(pageRaster));

        Layout =
            layout ??
            throw new ArgumentNullException(
                nameof(layout));

        if (!PageRaster.IsFullPage)
        {
            throw new ArgumentException(
                "AnalyzeVisual provenance requires a full-page raster.",
                nameof(pageRaster));
        }

        if (PageRaster.PhysicalPageNumber !=
            Layout.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "AnalyzeVisual raster and layout evidence must belong to the same page.");
        }

        SourceVisualIndex =
            sourceVisualIndex;
    }

    public string SourceDocumentSha256 { get; }

    public int PhysicalPageNumber =>
        PageRaster.PhysicalPageNumber;

    public int SourceVisualIndex { get; }

    public RasterRenderResult PageRaster { get; }

    public LayoutAnalysisResult Layout { get; }

    public int LayoutObservationCount =>
        Layout.Observations.Count;

    public int FigureObservationCount =>
        Layout.Observations.Count(
            observation =>
                observation.Kind ==
                LayoutObservationKind.Figure);

    public bool IsResolved =>
        false;

    private static string NormalizeSha256(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Source SHA-256 cannot be empty.",
                nameof(value));
        }

        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "Source SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        return normalized;
    }
}
