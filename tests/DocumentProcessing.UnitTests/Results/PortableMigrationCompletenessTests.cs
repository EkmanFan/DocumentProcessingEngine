using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.UnitTests.Results;

/// <summary>
/// Verifies the portable-result facts required for a lossless PDF migration
/// without making pagination mandatory for other formats.
/// </summary>
public sealed class PortableMigrationCompletenessTests
{
    #region Variables and Constants

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    #endregion

    #region Methods Tests

    [Fact]
    public void PagedSourceStructure_PreservesEmptyPhysicalPageAndViewport()
    {
        var structure =
            CreatePagedStructure();

        Assert.Equal(
            2,
            structure.PhysicalPageCount);

        Assert.Equal(
            2,
            structure.Pages[1].PhysicalPageNumber);

        Assert.Equal(
            new NormalizedRectangle(
                0.05,
                0.10,
                0.95,
                0.90),
            structure.Pages[1].ContentViewport);
    }

    [Fact]
    public void ProcessingResult_AcceptsOptionalPagedSourceStructure()
    {
        var result =
            CreatePagedResult(
                physicalPageNumber:
                    2);

        var structure =
            Assert.IsType<PagedDocumentSourceStructure>(
                result.SourceStructure);

        Assert.Equal(
            2,
            structure.PhysicalPageCount);

        var location =
            Assert.IsType<PagedDocumentSourceLocation>(
                result.Elements[0].Location);

        Assert.Equal(
            2,
            location.PhysicalPageNumber);
    }

    [Fact]
    public void ProcessingResult_RejectsElementOutsidePagedSourceStructure()
    {
        Assert.Throws<ArgumentException>(
            () =>
                CreatePagedResult(
                    physicalPageNumber:
                        3));
    }

    [Fact]
    public void ElementProcessingEvidence_RetainsNeutralLayoutKind()
    {
        const string text =
            "OCR text.";

        var evidence =
            new DocumentElementProcessingEvidence(
                elementId:
                    "element-1",
                DocumentTextSourceKind.Ocr,
                selectedSourceText:
                    text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text),
                nativeCandidateSequence:
                    null,
                layoutCandidateSequence:
                    7,
                ocrBackendId:
                    "ocr",
                ocrProfileId:
                    "ocr-v1",
                reconciliationDecision:
                    null,
                textsEquivalent:
                    null,
                hasReconciliationDivergence:
                    false,
                selectedTextPreparation:
                    null,
                normalizationDehyphenation:
                    null,
                normalizationChangedText:
                    false,
                exclusionReason:
                    null,
                isResolved:
                    true,
                layoutKind:
                    LayoutObservationKind.Caption);

        Assert.Equal(
            LayoutObservationKind.Caption,
            evidence.LayoutKind);
    }

    [Fact]
    public void ElementProcessingEvidence_RejectsLayoutSequenceWithoutKind()
    {
        const string text =
            "OCR text.";

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentElementProcessingEvidence(
                    elementId:
                        "element-1",
                    DocumentTextSourceKind.Ocr,
                    selectedSourceText:
                        text,
                    ProvenanceTextHashing.ComputeUtf8Sha256(
                        text),
                    nativeCandidateSequence:
                        null,
                    layoutCandidateSequence:
                        7,
                    ocrBackendId:
                        "ocr",
                    ocrProfileId:
                        "ocr-v1",
                    reconciliationDecision:
                        null,
                    textsEquivalent:
                        null,
                    hasReconciliationDivergence:
                        false,
                    selectedTextPreparation:
                        null,
                    normalizationDehyphenation:
                        null,
                    normalizationChangedText:
                        false,
                    exclusionReason:
                        null,
                    isResolved:
                        true,
                    layoutKind:
                        null));
    }

    #endregion

    #region Methods Fixtures

    private static PagedDocumentSourceStructure CreatePagedStructure() =>
        new(
            [
                new PagedDocumentPageDescriptor(
                    physicalPageNumber:
                        1,
                    new NormalizedRectangle(
                        0,
                        0,
                        1,
                        1)),
                new PagedDocumentPageDescriptor(
                    physicalPageNumber:
                        2,
                    new NormalizedRectangle(
                        0.05,
                        0.10,
                        0.95,
                        0.90))
            ]);

    private static DocumentProcessingResult CreatePagedResult(
        int physicalPageNumber)
    {
        const string text =
            "PDF text.";

        var element =
            new DocumentElement(
                elementId:
                    "element-1",
                ordinal:
                    0,
                DocumentElementKind.Text,
                new PagedDocumentSourceLocation(
                    physicalPageNumber,
                    new NormalizedRectangle(
                        0.10,
                        0.10,
                        0.80,
                        0.20)),
                segmentId:
                    null,
                text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text));

        var evidence =
            new DocumentElementProcessingEvidence(
                elementId:
                    element.ElementId,
                DocumentTextSourceKind.Native,
                selectedSourceText:
                    text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text),
                nativeCandidateSequence:
                    0,
                layoutCandidateSequence:
                    null,
                ocrBackendId:
                    null,
                ocrProfileId:
                    null,
                reconciliationDecision:
                    null,
                textsEquivalent:
                    null,
                hasReconciliationDivergence:
                    false,
                selectedTextPreparation:
                    null,
                normalizationDehyphenation:
                    null,
                normalizationChangedText:
                    false,
                exclusionReason:
                    null,
                isResolved:
                    true,
                layoutKind:
                    null);

        return new DocumentProcessingResult(
            new DocumentSourceDescriptor(
                DocumentFormatId.Pdf,
                SourceSha,
                byteLength:
                    100),
            new DocumentProcessingManifest(
                engineVersion:
                    "test-engine",
                nativeExtraction:
                    new ProcessingComponentIdentity(
                        "native",
                        "native-v1"),
                rasterization:
                    null,
                layoutAnalysis:
                    null,
                ocr:
                    [],
                reconciliation:
                    null,
                visualPreservationProfileIds:
                    [],
                assemblyProfileId:
                    "assembly-v1",
                normalizationProfileId:
                    "normalization-v1",
                segmentationProfileId:
                    "segmentation-v1"),
            [element],
            [evidence],
            structuralSegments:
                [],
            segmentProcessingEvidence:
                [],
            visualAssets:
                [],
            DocumentProcessingQualityObservations.Empty,
            sourceStructure:
                CreatePagedStructure());
    }

    #endregion
}
