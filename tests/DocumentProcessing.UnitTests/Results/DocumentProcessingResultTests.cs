using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.UnitTests.Results;

/// <summary>
/// Verifies the canonical format-neutral document-processing result.
/// </summary>
public sealed class DocumentProcessingResultTests
{
    #region Variables and Constants

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string VisualSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    #endregion

    #region Methods Tests

    [Fact]
    public void Constructor_AllowsNonPagedDocumentAndDirectVisualAsset()
    {
        var result =
            CreatePortableResult();

        Assert.Equal(
            DocumentProcessingResult.SchemaVersionId,
            result.SchemaVersion);

        Assert.Equal(
            "epub",
            result.Source.Format.Value);

        Assert.Equal(
            2,
            result.Elements.Count);

        Assert.Single(
            result.VisualAssets);

        Assert.Null(
            result.VisualAssets[0].RasterDerivation);
    }

    [Fact]
    public void Contract_HasNoRequiredPageCollection()
    {
        Assert.Null(
            typeof(DocumentProcessingResult)
                .GetProperty(
                    "Pages"));

        Assert.Null(
            typeof(DocumentProcessingResult)
                .GetProperty(
                    "PhysicalPageCount"));
    }

    [Fact]
    public void Constructor_RejectsMissingNonVisualProcessingEvidence()
    {
        var source =
            CreateSource();

        var manifest =
            CreateManifest();

        var textElement =
            CreateTextElement();

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentProcessingResult(
                    source,
                    manifest,
                    [textElement],
                    elementProcessingEvidence:
                        [],
                    structuralSegments:
                        [],
                    segmentProcessingEvidence:
                        [],
                    visualAssets:
                        [],
                    DocumentProcessingQualityObservations.Empty));
    }

    [Fact]
    public void Constructor_RejectsBrokenBidirectionalSegmentMembership()
    {
        const string text =
            "Portable text.";

        var textElement =
            new DocumentElement(
                elementId:
                    "element-1",
                ordinal:
                    0,
                DocumentElementKind.Text,
                new TestNonPagedLocation(
                    "chapter.xhtml",
                    "p1"),
                segmentId:
                    "segment-1",
                text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text));

        var segment =
            new DocumentStructuralSegment(
                segmentId:
                    "segment-1",
                ordinal:
                    0,
                text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text),
                headingText:
                    null,
                sourceElementIds:
                    ["different-element"]);

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentProcessingResult(
                    CreateSource(),
                    CreateManifest(),
                    [textElement],
                    [CreateTextEvidence()],
                    [segment],
                    [
                        new DocumentSegmentProcessingEvidence(
                            "segment-1",
                            [DocumentTextSourceKind.Native],
                            hasUnresolvedEvidence:
                                false)
                    ],
                    visualAssets:
                        [],
                    DocumentProcessingQualityObservations.Empty));
    }

    [Fact]
    public void Constructor_RejectsVisualAssetAttachedToTextElement()
    {
        var textElement =
            CreateTextElement();

        var visualAsset =
            new DocumentVisualAsset(
                assetId:
                    "asset-1",
                elementId:
                    textElement.ElementId,
                preservationProfileId:
                    "embedded-image-v1",
                mediaType:
                    "image/png",
                contentLength:
                    42,
                VisualSha);

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentProcessingResult(
                    CreateSource(),
                    CreateManifest(),
                    [textElement],
                    [CreateTextEvidence()],
                    [CreateSegment()],
                    [CreateSegmentEvidence()],
                    [visualAsset],
                    DocumentProcessingQualityObservations.Empty));
    }

    [Fact]
    public void Constructor_RejectsVisualProfileMissingFromManifest()
    {
        var source =
            CreateSource();

        var manifest =
            new DocumentProcessingManifest(
                engineVersion:
                    "test-engine",
                nativeExtraction:
                    new ProcessingComponentIdentity(
                        "epub-native",
                        "epub-native-v1"),
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
                    "segmentation-v1");

        var visualElement =
            CreateVisualElement();

        var visualAsset =
            new DocumentVisualAsset(
                assetId:
                    "asset-1",
                elementId:
                    visualElement.ElementId,
                preservationProfileId:
                    "embedded-image-v1",
                mediaType:
                    "image/png",
                contentLength:
                    42,
                VisualSha);

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentProcessingResult(
                    source,
                    manifest,
                    [
                        CreateTextElement(),
                        visualElement
                    ],
                    [CreateTextEvidence()],
                    [CreateSegment()],
                    [CreateSegmentEvidence()],
                    [visualAsset],
                    DocumentProcessingQualityObservations.Empty));
    }

    #endregion

    #region Methods Fixtures

    private static DocumentProcessingResult CreatePortableResult()
    {
        var visualElement =
            CreateVisualElement();

        var visualAsset =
            new DocumentVisualAsset(
                assetId:
                    "asset-1",
                elementId:
                    visualElement.ElementId,
                preservationProfileId:
                    "embedded-image-v1",
                mediaType:
                    "image/png",
                contentLength:
                    42,
                VisualSha);

        return new DocumentProcessingResult(
            CreateSource(),
            CreateManifest(),
            [
                CreateTextElement(),
                visualElement
            ],
            [CreateTextEvidence()],
            [CreateSegment()],
            [CreateSegmentEvidence()],
            [visualAsset],
            DocumentProcessingQualityObservations.Empty);
    }

    private static DocumentSourceDescriptor CreateSource() =>
        new(
            new DocumentFormatId(
                "epub"),
            SourceSha,
            byteLength:
                2048,
            fileName:
                "book.epub",
            declaredMediaType:
                "application/epub+zip");

    private static DocumentProcessingManifest CreateManifest() =>
        new(
            engineVersion:
                "test-engine",
            nativeExtraction:
                new ProcessingComponentIdentity(
                    "epub-native",
                    "epub-native-v1"),
            rasterization:
                null,
            layoutAnalysis:
                null,
            ocr:
                [],
            reconciliation:
                null,
            visualPreservationProfileIds:
                ["embedded-image-v1"],
            assemblyProfileId:
                "assembly-v1",
            normalizationProfileId:
                "normalization-v1",
            segmentationProfileId:
                "segmentation-v1");

    private static DocumentElement CreateTextElement()
    {
        const string text =
            "Portable text.";

        return new DocumentElement(
            elementId:
                "element-1",
            ordinal:
                0,
            DocumentElementKind.Text,
            new TestNonPagedLocation(
                "chapter.xhtml",
                "p1"),
            segmentId:
                "segment-1",
            text,
            ProvenanceTextHashing.ComputeUtf8Sha256(
                text));
    }

    private static DocumentElement CreateVisualElement() =>
        new(
            elementId:
                "element-2",
            ordinal:
                1,
            DocumentElementKind.Visual,
            new TestNonPagedLocation(
                "images/figure-1.png",
                null),
            segmentId:
                null,
            text:
                null,
            textSha256:
                null);

    private static DocumentElementProcessingEvidence CreateTextEvidence()
    {
        const string text =
            "Portable text.";

        return new DocumentElementProcessingEvidence(
            elementId:
                "element-1",
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
    }

    private static DocumentStructuralSegment CreateSegment()
    {
        const string text =
            "Portable text.";

        return new DocumentStructuralSegment(
            segmentId:
                "segment-1",
            ordinal:
                0,
            text,
            ProvenanceTextHashing.ComputeUtf8Sha256(
                text),
            headingText:
                null,
            sourceElementIds:
                ["element-1"]);
    }

    private static DocumentSegmentProcessingEvidence CreateSegmentEvidence() =>
        new(
            segmentId:
                "segment-1",
            textSources:
                [DocumentTextSourceKind.Native],
            hasUnresolvedEvidence:
                false);

    #endregion

    #region Test Types

    private sealed record TestNonPagedLocation(
        string ResourceId,
        string? FragmentId)
        : DocumentSourceLocation;

    #endregion
}
