using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Result of one authoritative native-extraction pass that optionally acquired
/// complete low-level raster observations for the same materialized pages.
///
/// Native extraction is always present. Raster acquisition is either complete
/// or represented by one sanitized ordinary failure; partial raster evidence is
/// never exposed as complete.
/// </summary>
public sealed record DocumentExtractionWithRasterObservationsResult
{
    public DocumentExtractionWithRasterObservationsResult(
        DocumentExtractionResult extraction,
        IEnumerable<PageVisualRasterObservations>? rasterObservations,
        RasterObservationAcquisitionFailure? rasterObservationFailure)
    {
        Extraction =
            extraction ??
            throw new ArgumentNullException(
                nameof(extraction));

        if ((rasterObservations is null) ==
            (rasterObservationFailure is null))
        {
            throw new ArgumentException(
                "Exactly one raster-observation outcome must be supplied: " +
                "complete observations or one acquisition failure.");
        }

        if (rasterObservations is not null)
        {
            var materialized =
                rasterObservations.ToArray();

            if (materialized.Length !=
                extraction.Pages.Count)
            {
                throw new ArgumentException(
                    $"Raster-observation page count {materialized.Length} does not " +
                    $"match extraction page count {extraction.Pages.Count}.",
                    nameof(rasterObservations));
            }

            for (var index = 0;
                 index <
                 materialized.Length;
                 index++)
            {
                var observationPage =
                    materialized[index] ??
                    throw new ArgumentException(
                        "Raster observations cannot contain null pages.",
                        nameof(rasterObservations));

                var expectedPhysicalPageNumber =
                    extraction.Pages[index]
                        .PhysicalPageNumber;

                if (observationPage.PhysicalPageNumber !=
                    expectedPhysicalPageNumber)
                {
                    throw new ArgumentException(
                        $"Raster observation at index {index} refers to physical page " +
                        $"{observationPage.PhysicalPageNumber}; expected " +
                        $"{expectedPhysicalPageNumber}.",
                        nameof(rasterObservations));
                }
            }

            RasterObservations =
                Array.AsReadOnly(
                    materialized);
        }

        RasterObservationFailure =
            rasterObservationFailure;
    }

    public DocumentExtractionResult Extraction { get; }

    public IReadOnlyList<PageVisualRasterObservations>? RasterObservations { get; }

    public RasterObservationAcquisitionFailure? RasterObservationFailure { get; }
}
