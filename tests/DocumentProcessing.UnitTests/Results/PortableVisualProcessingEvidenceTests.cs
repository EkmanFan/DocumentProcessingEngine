using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.UnitTests.Results;

/// <summary>
/// Verifies that portable visual elements can retain processing evidence needed
/// for a lossless migration of the current PDF result.
/// </summary>
public sealed class PortableVisualProcessingEvidenceTests
{
    #region Variables and Constants

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string VisualSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    #endregion

    #region Methods Tests

    [Fact]
    public void ProcessingResult_AllowsVisualElementProcessingEvidence()
    {
        var visualElement =
            CreateVisualElement();

        var visualEvidence =
            new DocumentElementProcessingEvidence(
                elementId:
                    visualElement.ElementId,
                DocumentTextSourceKind.None,
                selectedSourceText:
                    null,
                selectedSourceTextSha256:
                    null,
                nativeCandidateSequence:
                    null,
                layoutCandidateSequence:
                    7,
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
                    LayoutObservationKind.Figure);

        var result =
            new DocumentProcessingResult(
                CreateSource(),
                CreateManifest(),
                [visualElement],
                [visualEvidence],
                structuralSegments:
                    [],
                segmentProcessingEvidence:
                    [],
                visualAssets:
                    [CreateVisualAsset()],
                DocumentProcessingQualityObservations.Empty,
                sourceStructure:
                    null);

        var retained =
            Assert.Single(
                result.ElementProcessingEvidence);

        Assert.Equal(
            visualElement.ElementId,
            retained.ElementId);

        Assert.Equal(
            7,
            retained.LayoutCandidateSequence);

        Assert.Equal(
            LayoutObservationKind.Figure,
            retained.LayoutKind);

        Assert.True(
            retained.IsResolved);
    }

    [Fact]
    public void ProcessingResult_StillAllowsDirectVisualWithoutProcessingEvidence()
    {
        var visualElement =
            CreateVisualElement();

        var result =
            new DocumentProcessingResult(
                CreateSource(),
                CreateManifest(),
                [visualElement],
                elementProcessingEvidence:
                    [],
                structuralSegments:
                    [],
                segmentProcessingEvidence:
                    [],
                visualAssets:
                    [CreateVisualAsset()],
                DocumentProcessingQualityObservations.Empty,
                sourceStructure:
                    null);

        Assert.Empty(
            result.ElementProcessingEvidence);
    }

    #endregion

    #region Methods Fixtures

    private static DocumentSourceDescriptor CreateSource() =>
        new(
            new DocumentFormatId(
                "epub"),
            SourceSha,
            byteLength:
                128,
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
                new ProcessingComponentIdentity(
                    "visual-source",
                    "visual-source-v1"),
            layoutAnalysis:
                new ProcessingComponentIdentity(
                    "layout",
                    "layout-v1"),
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

    private static DocumentElement CreateVisualElement() =>
        new(
            elementId:
                "visual-1",
            ordinal:
                0,
            DocumentElementKind.Visual,
            new TestNonPagedLocation(
                "images/figure-1.png"),
            segmentId:
                null,
            text:
                null,
            textSha256:
                null);

    private static DocumentVisualAsset CreateVisualAsset() =>
        new(
            assetId:
                "visual-1:asset",
            elementId:
                "visual-1",
            preservationProfileId:
                "embedded-image-v1",
            mediaType:
                "image/png",
            contentLength:
                42,
            VisualSha);

    #endregion

    #region Test Types

    private sealed record TestNonPagedLocation(
        string ResourceId)
        : DocumentSourceLocation;

    #endregion
}
