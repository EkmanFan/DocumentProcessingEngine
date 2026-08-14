using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Quality;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Results.Serialization;

namespace DocumentProcessing.UnitTests.Results;

public sealed class DocumentIngestionResultJsonTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string VisualSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void RoundTrip_PreservesPortableResultAndProducesStableUtf8Json()
    {
        var original =
            CreateRepresentativeResult();

        var firstBytes =
            DocumentIngestionResultJson
                .SerializeToUtf8Bytes(
                    original);

        var roundTripped =
            DocumentIngestionResultJson
                .Deserialize(
                    firstBytes);

        var secondBytes =
            DocumentIngestionResultJson
                .SerializeToUtf8Bytes(
                    roundTripped);

        Assert.Equal(
            firstBytes,
            secondBytes);

        Assert.Equal(
            DocumentIngestionResult.SchemaVersionId,
            roundTripped.SchemaVersion);

        Assert.Equal(
            SourceSha,
            roundTripped.Source.Sha256);

        Assert.Equal(
            "pdf",
            roundTripped.Source.Format.Value);

        AssertProcessingManifestEquivalent(
            original.ProcessingManifest,
            roundTripped.ProcessingManifest);

        Assert.Equal(
            original.Pages.Count,
            roundTripped.Pages.Count);

        Assert.Equal(
            original.Pages[0].ContentViewport,
            roundTripped.Pages[0].ContentViewport);

        Assert.Equal(
            original.Pages[0].OrderedElementIds,
            roundTripped.Pages[0].OrderedElementIds);

        Assert.Equal(
            original.Elements.Count,
            roundTripped.Elements.Count);

        Assert.Equal(
            original.Elements
                .Select(
                    element =>
                        (
                            element.ElementId,
                            element.SelectedSourceTextSha256,
                            element.NormalizedTextSha256,
                            element.TextOrigin,
                            element.ReconciliationDecision,
                            element.PreservedVisual?.ContentSha256
                        )),
            roundTripped.Elements
                .Select(
                    element =>
                        (
                            element.ElementId,
                            element.SelectedSourceTextSha256,
                            element.NormalizedTextSha256,
                            element.TextOrigin,
                            element.ReconciliationDecision,
                            element.PreservedVisual?.ContentSha256
                        )));

        Assert.Equal(
            original.StructuralSegments
                .Select(
                    segment =>
                        (
                            segment.SegmentId,
                            segment.TextSha256,
                            segment.HasUnresolvedEvidence
                        )),
            roundTripped.StructuralSegments
                .Select(
                    segment =>
                        (
                            segment.SegmentId,
                            segment.TextSha256,
                            segment.HasUnresolvedEvidence
                        )));

        var confidence =
            Assert.Single(
                roundTripped.QualityObservations
                    .OcrConfidenceObservations);

        Assert.Equal(
            "p000001-e000001",
            confidence.ElementId);

        Assert.Equal(
            0.91d,
            confidence.Confidence.ArithmeticMean);
    }

    [Fact]
    public void Serialize_UsesExplicitV1ShapeAndOmitsDerivedDuplicateProperties()
    {
        var bytes =
            DocumentIngestionResultJson
                .SerializeToUtf8Bytes(
                    CreateRepresentativeResult());

        using var document =
            JsonDocument.Parse(
                bytes);

        var root =
            document.RootElement;

        Assert.Equal(
            "document-ingestion-result-v1",
            root.GetProperty(
                    "schemaVersion")
                .GetString());

        Assert.Equal(
            JsonValueKind.String,
            root.GetProperty(
                    "source")
                .GetProperty(
                    "format")
                .ValueKind);

        Assert.Equal(
            "pdf",
            root.GetProperty(
                    "source")
                .GetProperty(
                    "format")
                .GetString());

        var page =
            root.GetProperty(
                    "pages")
                [0];

        Assert.False(
            page.TryGetProperty(
                "pageId",
                out _));

        var elements =
            root.GetProperty(
                "elements");

        var native =
            FindById(
                elements,
                "p000001-e000000");

        Assert.Equal(
            "text",
            native.GetProperty(
                    "kind")
                .GetString());

        Assert.Equal(
            "nativePdf",
            native.GetProperty(
                    "textOrigin")
                .GetString());

        Assert.False(
            native.TryGetProperty(
                "ocrBackendId",
                out _));

        Assert.False(
            native.TryGetProperty(
                "reconciliationDecision",
                out _));

        Assert.False(
            native.TryGetProperty(
                "isExcluded",
                out _));

        var ocr =
            FindById(
                elements,
                "p000001-e000001");

        Assert.Equal(
            "ocr",
            ocr.GetProperty(
                    "textOrigin")
                .GetString());

        Assert.Equal(
            "ocrOnly",
            ocr.GetProperty(
                    "reconciliationDecision")
                .GetString());

        Assert.Equal(
            "heading",
            ocr.GetProperty(
                    "layoutKind")
                .GetString());

        var visual =
            FindById(
                elements,
                "p000001-e000002");

        var crop =
            visual.GetProperty(
                    "preservedVisual")
                .GetProperty(
                    "crop");

        Assert.False(
            crop.TryGetProperty(
                "width",
                out _));

        Assert.False(
            crop.TryGetProperty(
                "height",
                out _));

        var segment =
            root.GetProperty(
                    "structuralSegments")
                [0];

        Assert.False(
            segment.TryGetProperty(
                "isMixedTextOrigin",
                out _));

        var json =
            Encoding.UTF8.GetString(
                bytes);

        Assert.DoesNotContain(
            "contentBase64",
            json,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "rawLabel",
            json,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "temporaryPath",
            json,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "backendResponse",
            json,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_WritesRequiredEmptyCollectionsAsArrays()
    {
        var bytes =
            DocumentIngestionResultJson
                .SerializeToUtf8Bytes(
                    CreateNativeOnlyResult());

        using var document =
            JsonDocument.Parse(
                bytes);

        var root =
            document.RootElement;

        Assert.Equal(
            0,
            root.GetProperty(
                    "processingManifest")
                .GetProperty(
                    "ocr")
                .GetArrayLength());

        Assert.Equal(
            0,
            root.GetProperty(
                    "processingManifest")
                .GetProperty(
                    "visualPreservationProfileIds")
                .GetArrayLength());

        Assert.Equal(
            0,
            root.GetProperty(
                    "qualityObservations")
                .GetProperty(
                    "ocrConfidenceObservations")
                .GetArrayLength());

        Assert.Equal(
            1,
            root.GetProperty(
                    "pages")
                .GetArrayLength());

        Assert.Equal(
            1,
            root.GetProperty(
                    "elements")
                .GetArrayLength());

        Assert.Equal(
            1,
            root.GetProperty(
                    "structuralSegments")
                .GetArrayLength());
    }

    [Fact]
    public void Deserialize_ToleratesUnknownProperties()
    {
        var root =
            ParseMutable(
                CreateRepresentativeResult());

        root["futureRoot"] =
            new JsonObject
            {
                ["nested"] =
                    true
            };

        root["source"]!
            .AsObject()["futureSourceProperty"] =
            42;

        root["elements"]!
            .AsArray()[0]!
            .AsObject()["futureElementProperty"] =
            "ignored";

        var result =
            Deserialize(
                root);

        Assert.Equal(
            SourceSha,
            result.Source.Sha256);

        Assert.Equal(
            4,
            result.Elements.Count);
    }

    [Fact]
    public void Deserialize_RejectsUnsupportedSchemaVersion()
    {
        var root =
            ParseMutable(
                CreateRepresentativeResult());

        root["schemaVersion"] =
            "document-ingestion-result-v2";

        var error =
            Assert.Throws<
                UnsupportedDocumentIngestionResultSchemaException>(
                () =>
                    Deserialize(
                        root));

        Assert.Equal(
            "document-ingestion-result-v2",
            error.SchemaVersion);
    }

    [Fact]
    public void Deserialize_RejectsMissingSchemaVersion()
    {
        var root =
            ParseMutable(
                CreateRepresentativeResult());

        Assert.True(
            root.Remove(
                "schemaVersion"));

        Assert.Throws<JsonException>(
            () =>
                Deserialize(
                    root));
    }

    [Fact]
    public void Deserialize_RejectsNumericEnumRepresentation()
    {
        var root =
            ParseMutable(
                CreateRepresentativeResult());

        root["elements"]!
            .AsArray()[0]!
            .AsObject()["textOrigin"] =
            1;

        Assert.Throws<JsonException>(
            () =>
                Deserialize(
                    root));
    }

    [Fact]
    public void Deserialize_RejectsUnknownEnumValue()
    {
        var root =
            ParseMutable(
                CreateRepresentativeResult());

        root["elements"]!
            .AsArray()[0]!
            .AsObject()["textOrigin"] =
            "futureOrigin";

        Assert.Throws<JsonException>(
            () =>
                Deserialize(
                    root));
    }

    [Fact]
    public void Deserialize_RejectsTamperedTextWithOriginalCustodyHash()
    {
        var root =
            ParseMutable(
                CreateRepresentativeResult());

        root["elements"]!
            .AsArray()[0]!
            .AsObject()["normalizedText"] =
            "tampered text";

        var error =
            Assert.Throws<JsonException>(
                () =>
                    Deserialize(
                        root));

        Assert.NotNull(
            error.InnerException);

        Assert.Contains(
            "SHA-256",
            error.InnerException!.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsNullRequiredCollection()
    {
        var root =
            ParseMutable(
                CreateRepresentativeResult());

        root["pages"] =
            null;

        Assert.Throws<JsonException>(
            () =>
                Deserialize(
                    root));
    }

    [Fact]
    public void Deserialize_RejectsDuplicateJsonProperties()
    {
        var bytes =
            DocumentIngestionResultJson
                .SerializeToUtf8Bytes(
                    CreateRepresentativeResult());

        var json =
            Encoding.UTF8.GetString(
                bytes);

        const string marker =
            "\"schemaVersion\":\"document-ingestion-result-v1\",";

        var duplicated =
            json.Replace(
                marker,
                marker +
                marker,
                StringComparison.Ordinal);

        Assert.NotEqual(
            json,
            duplicated);

        Assert.Throws<JsonException>(
            () =>
                DocumentIngestionResultJson
                    .Deserialize(
                        Encoding.UTF8.GetBytes(
                            duplicated)));
    }

    [Fact]
    public void Deserialize_RequiresExactCamelCasePropertyNames()
    {
        var root =
            ParseMutable(
                CreateRepresentativeResult());

        var source =
            root["source"]!
                .DeepClone();

        Assert.True(
            root.Remove(
                "source"));

        root["Source"] =
            source;

        Assert.Throws<JsonException>(
            () =>
                Deserialize(
                    root));
    }

    private static void AssertProcessingManifestEquivalent(
        DocumentProcessingManifest expected,
        DocumentProcessingManifest actual)
    {
        Assert.Equal(
            expected.EngineVersion,
            actual.EngineVersion);

        Assert.Equal(
            expected.NativeExtraction,
            actual.NativeExtraction);

        Assert.Equal(
            expected.Rasterization,
            actual.Rasterization);

        Assert.Equal(
            expected.LayoutAnalysis,
            actual.LayoutAnalysis);

        Assert.Equal(
            expected.Ocr,
            actual.Ocr);

        Assert.Equal(
            expected.Reconciliation,
            actual.Reconciliation);

        Assert.Equal(
            expected.VisualPreservationProfileIds,
            actual.VisualPreservationProfileIds);

        Assert.Equal(
            expected.AssemblyProfileId,
            actual.AssemblyProfileId);

        Assert.Equal(
            expected.NormalizationProfileId,
            actual.NormalizationProfileId);

        Assert.Equal(
            expected.SegmentationProfileId,
            actual.SegmentationProfileId);
    }

    private static JsonObject ParseMutable(
        DocumentIngestionResult result)
    {
        var bytes =
            DocumentIngestionResultJson
                .SerializeToUtf8Bytes(
                    result);

        return JsonNode.Parse(
                bytes)!
            .AsObject();
    }

    private static DocumentIngestionResult Deserialize(
        JsonObject root) =>
        DocumentIngestionResultJson
            .Deserialize(
                Encoding.UTF8.GetBytes(
                    root.ToJsonString()));

    private static JsonElement FindById(
        JsonElement array,
        string elementId)
    {
        foreach (var item in
                 array.EnumerateArray())
        {
            if (string.Equals(
                    item.GetProperty(
                            "elementId")
                        .GetString(),
                    elementId,
                    StringComparison.Ordinal))
            {
                return item;
            }
        }

        throw new InvalidOperationException(
            $"Element '{elementId}' was not found in JSON.");
    }

    private static DocumentIngestionResult CreateRepresentativeResult()
    {
        var nativeText =
            "Native body.";

        var ocrSourceText =
            "OCR head-\ning";

        var ocrNormalizedText =
            "OCR heading";

        var segmentText =
            $"{nativeText}\n\n{ocrNormalizedText}";

        const string segmentId =
            "p000001-s000000";

        var native =
            new DocumentElementProvenance(
                SourceSha,
                "p000001-e000000",
                physicalPageNumber:
                    1,
                readingOrder:
                    0,
                HybridDocumentElementKind.Text,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.90,
                    0.25),
                segmentId,
                nativeText,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        nativeText),
                nativeText,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        nativeText),
                TextSelectionOrigin.NativePdf,
                nativeBlockSourceSequence:
                    7,
                layoutObservationSequence:
                    null,
                layoutKind:
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
                preservedVisual:
                    null);

        var ocr =
            new DocumentElementProvenance(
                SourceSha,
                "p000001-e000001",
                physicalPageNumber:
                    1,
                readingOrder:
                    1,
                HybridDocumentElementKind.Heading,
                new NormalizedRectangle(
                    0.10,
                    0.30,
                    0.90,
                    0.40),
                segmentId,
                ocrSourceText,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        ocrSourceText),
                ocrNormalizedText,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        ocrNormalizedText),
                TextSelectionOrigin.Ocr,
                nativeBlockSourceSequence:
                    null,
                layoutObservationSequence:
                    10,
                layoutKind:
                    LayoutObservationKind.Heading,
                ocrBackendId:
                    "paddleocr-general-ocr",
                ocrProfileId:
                    "ocr-profile-v1",
                reconciliationDecision:
                    TextReconciliationDecision.OcrOnly,
                textsEquivalent:
                    null,
                hasReconciliationDivergence:
                    false,
                selectedTextPreparation:
                    new TextDehyphenationProvenance(
                        softHyphenRemovalCount:
                            0,
                        boundaryJoinCount:
                            1),
                normalizationDehyphenation:
                    new TextDehyphenationProvenance(
                        softHyphenRemovalCount:
                            0,
                        boundaryJoinCount:
                            1),
                normalizationChangedText:
                    true,
                exclusionReason:
                    null,
                isResolved:
                    true,
                preservedVisual:
                    null);

        var visual =
            new DocumentElementProvenance(
                SourceSha,
                "p000001-e000002",
                physicalPageNumber:
                    1,
                readingOrder:
                    2,
                HybridDocumentElementKind.Visual,
                new NormalizedRectangle(
                    0.10,
                    0.45,
                    0.90,
                    0.70),
                segmentId:
                    null,
                selectedSourceText:
                    null,
                selectedSourceTextSha256:
                    null,
                normalizedText:
                    null,
                normalizedTextSha256:
                    null,
                TextSelectionOrigin.None,
                nativeBlockSourceSequence:
                    null,
                layoutObservationSequence:
                    20,
                layoutKind:
                    LayoutObservationKind.Figure,
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
                preservedVisual:
                    new PreservedVisualProvenance(
                        "visual-profile-v1",
                        "image/png",
                        sourceRasterPixelWidth:
                            1200,
                        sourceRasterPixelHeight:
                            1600,
                        new PixelRectangle(
                            100,
                            700,
                            1100,
                            1200),
                        contentLength:
                            12345,
                        VisualSha));

        var deferred =
            new DocumentElementProvenance(
                SourceSha,
                "p000001-e000003",
                physicalPageNumber:
                    1,
                readingOrder:
                    3,
                HybridDocumentElementKind.Deferred,
                new NormalizedRectangle(
                    0.10,
                    0.75,
                    0.90,
                    0.80),
                segmentId:
                    null,
                selectedSourceText:
                    null,
                selectedSourceTextSha256:
                    null,
                normalizedText:
                    null,
                normalizedTextSha256:
                    null,
                TextSelectionOrigin.None,
                nativeBlockSourceSequence:
                    null,
                layoutObservationSequence:
                    30,
                layoutKind:
                    LayoutObservationKind.Unknown,
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
                    false,
                preservedVisual:
                    null);

        var segment =
            new DocumentSegmentProvenance(
                SourceSha,
                segmentId,
                ordinal:
                    0,
                segmentText,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        segmentText),
                headingText:
                    ocrNormalizedText,
                firstPhysicalPageNumber:
                    1,
                lastPhysicalPageNumber:
                    1,
                sourceElementIds:
                    [
                        native.ElementId,
                        ocr.ElementId
                    ],
                textOrigins:
                    [
                        TextSelectionOrigin.NativePdf,
                        TextSelectionOrigin.Ocr
                    ],
                hasUnresolvedEvidence:
                    false);

        var source =
            new DocumentSourceIdentity(
                DocumentFormatId.Pdf,
                SourceSha,
                byteLength:
                    987654,
                physicalPageCount:
                    1,
                fileName:
                    "fixture.pdf",
                declaredMediaType:
                    "application/pdf");

        var manifest =
            new DocumentProcessingManifest(
                engineVersion:
                    "0.1.0-test",
                nativeExtraction:
                    new ProcessingComponentIdentity(
                        "pdfpig",
                        "pdfpig-native-v1"),
                rasterization:
                    new ProcessingComponentIdentity(
                        "pdftoppm",
                        "pdftoppm-300dpi-v1"),
                layoutAnalysis:
                    new ProcessingComponentIdentity(
                        "pp-structurev3",
                        "pp-structurev3-v1"),
                ocr:
                    [
                        new ProcessingComponentIdentity(
                            "paddleocr-general-ocr",
                            "ocr-profile-v1")
                    ],
                reconciliation:
                    new ProcessingComponentIdentity(
                        "native-ocr-reconciler",
                        "native-ocr-reconciliation-v1"),
                visualPreservationProfileIds:
                    [
                        "visual-profile-v1"
                    ],
                assemblyProfileId:
                    "assembly-v1",
                normalizationProfileId:
                    "normalization-v1",
                segmentationProfileId:
                    "segmentation-v1");

        var page =
            new DocumentIngestionPage(
                physicalPageNumber:
                    1,
                new NormalizedRectangle(
                    0.05,
                    0.05,
                    0.95,
                    0.95),
                [
                    native.ElementId,
                    ocr.ElementId,
                    visual.ElementId,
                    deferred.ElementId
                ]);

        var quality =
            new DocumentIngestionQualityObservations(
                [
                    new DocumentElementOcrQualityObservation(
                        ocr.ElementId,
                        new OcrConfidenceSummary(
                            observationCount:
                                2,
                            minimum:
                                0.88,
                            arithmeticMean:
                                0.91,
                            maximum:
                                0.94))
                ]);

        return new DocumentIngestionResult(
            source,
            manifest,
            [page],
            [
                native,
                ocr,
                visual,
                deferred
            ],
            [segment],
            quality);
    }

    private static DocumentIngestionResult CreateNativeOnlyResult()
    {
        var text =
            "Native only.";

        const string segmentId =
            "p000001-s000000";

        var element =
            new DocumentElementProvenance(
                SourceSha,
                "p000001-e000000",
                1,
                0,
                HybridDocumentElementKind.Text,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.90,
                    0.20),
                segmentId,
                text,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        text),
                text,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        text),
                TextSelectionOrigin.NativePdf,
                nativeBlockSourceSequence:
                    0,
                layoutObservationSequence:
                    null,
                layoutKind:
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
                preservedVisual:
                    null);

        var segment =
            new DocumentSegmentProvenance(
                SourceSha,
                segmentId,
                0,
                text,
                ProvenanceTextHashing
                    .ComputeUtf8Sha256(
                        text),
                headingText:
                    null,
                1,
                1,
                [element.ElementId],
                [TextSelectionOrigin.NativePdf],
                hasUnresolvedEvidence:
                    false);

        return new DocumentIngestionResult(
            new DocumentSourceIdentity(
                DocumentFormatId.Pdf,
                SourceSha,
                byteLength:
                    100,
                physicalPageCount:
                    1,
                fileName:
                    null,
                declaredMediaType:
                    null),
            new DocumentProcessingManifest(
                "0.1.0-test",
                new ProcessingComponentIdentity(
                    "pdfpig",
                    "pdfpig-native-v1"),
                rasterization:
                    null,
                layoutAnalysis:
                    null,
                ocr:
                    Array.Empty<ProcessingComponentIdentity>(),
                reconciliation:
                    null,
                visualPreservationProfileIds:
                    Array.Empty<string>(),
                assemblyProfileId:
                    "assembly-v1",
                normalizationProfileId:
                    "normalization-v1",
                segmentationProfileId:
                    "segmentation-v1"),
            [
                new DocumentIngestionPage(
                    1,
                    new NormalizedRectangle(
                        0d,
                        0d,
                        1d,
                        1d),
                    [element.ElementId])
            ],
            [element],
            [segment],
            DocumentIngestionQualityObservations.Empty);
    }
}
