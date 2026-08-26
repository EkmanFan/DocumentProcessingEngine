using Xunit;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.UnitTests.DualRun.InProcess;

public sealed class NativeEvidenceContractTests
{
    #region Methods

    [Fact]
    public void PagedNativeDocumentEvidence_DelegatesExistingCoordinatedEvidence()
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
            new PagedNativeDocumentEvidence(
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

        Assert.Empty(
            evidence.DocumentNotes);
    }

    [Fact]
    public void PagedNativeDocumentEvidence_SnapshotsConcludedNotes()
    {
        var notes =
            new List<NativeDocumentNote>
            {
                CreateNote(
                    "7")
            };

        var evidence =
            new PagedNativeDocumentEvidence(
                CreateCoordinatedEvidence(),
                nativeExtractionIdentity:
                    null,
                notes);

        notes.Clear();

        var note =
            Assert.IsType<PagedNativeDocumentNote>(
                Assert.Single(
                    evidence.DocumentNotes));

        Assert.Equal(
            "7",
            note.Label);

        Assert.Equal(
            "note payload",
            note.Text);
    }

    [Fact]
    public void PagedNativeDocumentEvidence_RejectsNullDocumentNotes()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new PagedNativeDocumentEvidence(
                    CreateCoordinatedEvidence(),
                    nativeExtractionIdentity:
                        null,
                    documentNotes:
                        null!));
    }

    [Fact]
    public void PagedNativeDocumentEvidence_RejectsNullNoteItem()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PagedNativeDocumentEvidence(
                    CreateCoordinatedEvidence(),
                    nativeExtractionIdentity:
                        null,
                    documentNotes:
                        new NativeDocumentNote[]
                        {
                            null!
                        }));
    }

    [Fact]
    public void PagedNativeDocumentEvidence_RejectsNullCurrentEvidence()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new PagedNativeDocumentEvidence(
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

    private static DocumentExtractionWithRasterObservationsResult
        CreateCoordinatedEvidence() =>
        new(
            new DocumentExtractionResult(
                DocumentFormatId.Pdf),
            Array.Empty<PageVisualRasterObservations>(),
            rasterObservationFailure:
                null);

    private static PagedNativeDocumentNote CreateNote(
        string label) =>
        new(
            label,
            [
                new PagedNativeNoteReference(
                    label,
                    physicalPageNumber:
                        1,
                    sourceBlockSequence:
                        2,
                    wordSourceSequence:
                        3,
                    new NormalizedRectangle(
                        0.1,
                        0.2,
                        0.3,
                        0.4))
            ],
            [
                new PagedNativeNotePayloadLine(
                    physicalPageNumber:
                        1,
                    text:
                        "note payload",
                    new NormalizedRectangle(
                        0.1,
                        0.8,
                        0.7,
                        0.85),
                    sourceBlockSequences:
                        [4],
                    wordSourceSequences:
                        [5, 6])
            ],
            [
                new PagedNativeNoteSourceBlock(
                    physicalPageNumber:
                        1,
                    sourceSequence:
                        4)
            ]);

    #endregion
}
