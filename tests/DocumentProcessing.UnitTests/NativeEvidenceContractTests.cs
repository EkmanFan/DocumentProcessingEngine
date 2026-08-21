using Xunit;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.UnitTests.DualRun.InProcess;

public sealed class NativeEvidenceContractTests
{
    #region Methods

    [Fact]
    public void NativeDocumentEvidence_DelegatesExistingCoordinatedEvidence()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf);

        var coordinated =
            new DocumentExtractionWithRasterObservationsResult(
                extraction,
                Array.Empty<PageVisualRasterObservations>(),
                rasterObservationFailure:
                    null);

        var evidence =
            new NativeDocumentEvidence(
                coordinated);

        Assert.Same(
            extraction,
            evidence.Extraction);

        Assert.NotNull(
            evidence.RasterObservations);

        Assert.Empty(
            evidence.RasterObservations);

        Assert.Null(
            evidence.RasterObservationFailure);
    }

    [Fact]
    public void NativeDocumentEvidence_RejectsNullCurrentEvidence()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new NativeDocumentEvidence(
                    null!));
    }

    [Fact]
    public void Invalid_RejectsBlankReason()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new NativeEvidenceExtractionResult.Invalid(
                    " "));
    }

    [Fact]
    public void Success_RejectsNullEvidence()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new NativeEvidenceExtractionResult.Success(
                    null!));
    }

    [Fact]
    public void Unavailable_RejectsBlankReason()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new NativeEvidenceExtractionResult.Unavailable(
                    " "));
    }

    [Fact]
    public void FunctionalOutcomes_AreStructurallyDistinct()
    {
        NativeEvidenceExtractionResult notRecognized =
            new NativeEvidenceExtractionResult.NotRecognized();

        NativeEvidenceExtractionResult invalid =
            new NativeEvidenceExtractionResult.Invalid(
                "recognized but invalid");

        NativeEvidenceExtractionResult unavailable =
            new NativeEvidenceExtractionResult.Unavailable(
                "validation unavailable");

        Assert.IsType<
            NativeEvidenceExtractionResult.NotRecognized>(
            notRecognized);

        Assert.IsType<
            NativeEvidenceExtractionResult.Invalid>(
            invalid);

        Assert.IsType<
            NativeEvidenceExtractionResult.Unavailable>(
            unavailable);
    }

    #endregion
}
