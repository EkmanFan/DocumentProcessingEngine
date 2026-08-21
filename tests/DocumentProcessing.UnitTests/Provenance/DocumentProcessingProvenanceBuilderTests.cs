using System.Security.Cryptography;
using System.Text;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Hybrid.Segmentation;
using DocumentProcessing.Engine.Provenance;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Provenance;

public sealed class DocumentProcessingProvenanceBuilderTests
{
    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Build_ProducesPortableCustodyChainAndProcessingManifest()
    {
        var segmentation =
            BuildRepresentativeSegmentation(
                SourceSha);

        var provenance =
            DocumentProcessingProvenanceBuilder
                .Build(
                    segmentation,
                    CompleteContext(
                        SourceSha));

        Assert.Equal(
            SourceSha,
            provenance.Source.Sha256);

        Assert.Equal(
            1,
            provenance.Source.PhysicalPageCount);

        Assert.Equal(
            "0.1.0-test",
            provenance.ProcessingManifest.EngineVersion);

        Assert.Equal(
            "pdfpig",
            provenance.ProcessingManifest
                .NativeExtraction.BackendId);

        Assert.Equal(
            "pdftoppm",
            provenance.ProcessingManifest
                .Rasterization!.BackendId);

        Assert.Equal(
            "pp-structurev3",
            provenance.ProcessingManifest
                .LayoutAnalysis!.BackendId);

        var ocrIdentity =
            Assert.Single(
                provenance.ProcessingManifest.Ocr);

        Assert.Equal(
            "paddleocr-general-ocr",
            ocrIdentity.BackendId);

        Assert.Equal(
            "ocr-profile-v1",
            ocrIdentity.ProfileId);

        Assert.Equal(
            "native-ocr-reconciler",
            provenance.ProcessingManifest
                .Reconciliation!.BackendId);

        Assert.Equal(
            HybridDocumentAssembler.AssemblyProfileId,
            provenance.ProcessingManifest
                .AssemblyProfileId);

        Assert.Equal(
            HybridDocumentNormalizer.NormalizationProfileId,
            provenance.ProcessingManifest
                .NormalizationProfileId);

        Assert.Equal(
            HybridDocumentSegmenter.SegmentationProfileId,
            provenance.ProcessingManifest
                .SegmentationProfileId);

        Assert.Contains(
            "visual-profile-v1",
            provenance.ProcessingManifest
                .VisualPreservationProfileIds);

        Assert.Equal(
            4,
            provenance.Elements.Count);

        var heading =
            Assert.Single(
                provenance.Elements,
                element =>
                    element.ReadingOrder ==
                    0);

        Assert.Equal(
            "p000001-e000000",
            heading.ElementId);

        Assert.Equal(
            "p000001-s000000",
            heading.SegmentId);

        Assert.Equal(
            HybridDocumentElementKind.Heading,
            heading.Kind);

        Assert.Equal(
            TextSelectionOrigin.Ocr,
            heading.TextOrigin);

        Assert.Equal(
            "Heading",
            heading.SelectedSourceText);

        Assert.Equal(
            "Heading",
            heading.NormalizedText);

        Assert.Equal(
            Hash(
                "Heading"),
            heading.SelectedSourceTextSha256);

        Assert.Equal(
            Hash(
                "Heading"),
            heading.NormalizedTextSha256);

        Assert.Equal(
            0,
            heading.LayoutObservationSequence);

        Assert.Equal(
            LayoutObservationKind.Heading,
            heading.LayoutKind);

        Assert.Equal(
            "paddleocr-general-ocr",
            heading.OcrBackendId);

        Assert.Equal(
            "ocr-profile-v1",
            heading.OcrProfileId);

        Assert.Equal(
            TextReconciliationDecision.OcrOnly,
            heading.ReconciliationDecision);

        Assert.True(
            heading.IsResolved);

        var native =
            Assert.Single(
                provenance.Elements,
                element =>
                    element.ReadingOrder ==
                    1);

        Assert.Equal(
            TextSelectionOrigin.Native,
            native.TextOrigin);

        Assert.Equal(
            7,
            native.NativeBlockSourceSequence);

        Assert.Equal(
            "Native   body.",
            native.SelectedSourceText);

        Assert.Equal(
            "Native body.",
            native.NormalizedText);

        Assert.NotEqual(
            native.SelectedSourceTextSha256,
            native.NormalizedTextSha256);

        Assert.True(
            native.NormalizationChangedText);

        Assert.Equal(
            Hash(
                "Native   body."),
            native.SelectedSourceTextSha256);

        Assert.Equal(
            Hash(
                "Native body."),
            native.NormalizedTextSha256);

        var visual =
            Assert.Single(
                provenance.Elements,
                element =>
                    element.ReadingOrder ==
                    2);

        Assert.Equal(
            HybridDocumentElementKind.Visual,
            visual.Kind);

        Assert.Equal(
            LayoutObservationKind.Figure,
            visual.LayoutKind);

        Assert.Null(
            visual.SelectedSourceText);

        Assert.Null(
            visual.NormalizedText);

        Assert.NotNull(
            visual.PreservedVisual);

        Assert.Equal(
            "visual-profile-v1",
            visual.PreservedVisual!.ProfileId);

        Assert.Equal(
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            visual.PreservedVisual.ContentSha256);

        var deferred =
            Assert.Single(
                provenance.Elements,
                element =>
                    element.ReadingOrder ==
                    3);

        Assert.Equal(
            HybridDocumentElementKind.Deferred,
            deferred.Kind);

        Assert.False(
            deferred.IsResolved);

        Assert.Equal(
            LayoutObservationKind.Unknown,
            deferred.LayoutKind);

        var segment =
            Assert.Single(
                provenance.Segments);

        Assert.Equal(
            SourceSha,
            segment.SourceDocumentSha256);

        Assert.Equal(
            "p000001-s000000",
            segment.SegmentId);

        Assert.Equal(
            "Heading\n\nNative body.",
            segment.Text);

        Assert.Equal(
            Hash(
                "Heading\n\nNative body."),
            segment.TextSha256);

        Assert.Equal(
            new[]
            {
                "p000001-e000000",
                "p000001-e000001",
                "p000001-e000002",
                "p000001-e000003"
            },
            segment.SourceElementIds);

        Assert.Equal(
            new[]
            {
                TextSelectionOrigin.Ocr,
                TextSelectionOrigin.Native
            },
            segment.TextOrigins);

        Assert.True(
            segment.IsMixedTextOrigin);

        Assert.True(
            segment.HasUnresolvedEvidence);
    }

    [Fact]
    public void Build_RejectsLayoutEvidenceWithoutRasterizationIdentity()
    {
        var segmentation =
            BuildRepresentativeSegmentation(
                SourceSha);

        var context =
            new DocumentProcessingProvenanceContext(
                Source(
                    SourceSha),
                "0.1.0-test",
                NativeExtraction(),
                rasterization:
                    null,
                layoutAnalysis:
                    LayoutAnalysis(),
                reconciliation:
                    Reconciliation());

        var error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    DocumentProcessingProvenanceBuilder
                        .Build(
                            segmentation,
                            context));

        Assert.Contains(
            "rasterization processing identity",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_RejectsLayoutEvidenceWithoutLayoutIdentity()
    {
        var segmentation =
            BuildRepresentativeSegmentation(
                SourceSha);

        var context =
            new DocumentProcessingProvenanceContext(
                Source(
                    SourceSha),
                "0.1.0-test",
                NativeExtraction(),
                rasterization:
                    Rasterization(),
                layoutAnalysis:
                    null,
                reconciliation:
                    Reconciliation());

        var error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    DocumentProcessingProvenanceBuilder
                        .Build(
                            segmentation,
                            context));

        Assert.Contains(
            "layout processing identity",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_RejectsReconciliationEvidenceWithoutReconciliationIdentity()
    {
        var segmentation =
            BuildRepresentativeSegmentation(
                SourceSha);

        var context =
            new DocumentProcessingProvenanceContext(
                Source(
                    SourceSha),
                "0.1.0-test",
                NativeExtraction(),
                Rasterization(),
                LayoutAnalysis(),
                reconciliation:
                    null);

        var error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    DocumentProcessingProvenanceBuilder
                        .Build(
                            segmentation,
                            context));

        Assert.Contains(
            "reconciliation processing identity",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_RejectsPreservedVisualFromDifferentSourceDocument()
    {
        var segmentation =
            BuildRepresentativeSegmentation(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");

        var error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    DocumentProcessingProvenanceBuilder
                        .Build(
                            segmentation,
                            CompleteContext(
                                SourceSha)));

        Assert.Contains(
            "different source document",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentElementProvenance_RejectsSelectedSourceTextHashMismatch()
    {
        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    MinimalTextElement(
                        "e1",
                        "s1",
                        selectedSourceTextSha256:
                            Hash("tampered")));

        Assert.Contains(
            "Selected source text SHA-256",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentElementProvenance_RejectsNormalizedTextHashMismatch()
    {
        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    MinimalTextElement(
                        "e1",
                        "s1",
                        normalizedTextSha256:
                            Hash("tampered")));

        Assert.Contains(
            "Normalized text SHA-256",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentElementProvenance_RejectsIncorrectNormalizationChangedFlag()
    {
        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    MinimalTextElement(
                        "e1",
                        "s1",
                        selectedSourceText:
                            "source",
                        normalizedText:
                            "normalized",
                        normalizationChangedText:
                            false));

        Assert.Contains(
            "NormalizationChangedText",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentSegmentProvenance_RejectsTextHashMismatch()
    {
        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    MinimalSegment(
                        "s1",
                        new[]
                        {
                            "e1"
                        },
                        textSha256:
                            Hash("tampered")));

        Assert.Contains(
            "Segment text SHA-256",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentSegmentProvenance_RejectsDuplicateSourceElementIds()
    {
        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    MinimalSegment(
                        "s1",
                        new[]
                        {
                            "e1",
                            "e1"
                        }));

        Assert.Contains(
            "same source element",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentProcessingProvenance_RejectsElementReferencingUnknownSegment()
    {
        var element =
            MinimalTextElement(
                "e1",
                "missing");

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    MinimalAggregate(
                        new[]
                        {
                            element
                        },
                        Array.Empty<DocumentSegmentProvenance>()));

        Assert.Contains(
            "unknown segment",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentProcessingProvenance_RejectsSegmentReferencingUnknownElement()
    {
        var segment =
            MinimalSegment(
                "s1",
                new[]
                {
                    "missing"
                });

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    MinimalAggregate(
                        Array.Empty<DocumentElementProvenance>(),
                        new[]
                        {
                            segment
                        }));

        Assert.Contains(
            "unknown element",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentProcessingProvenance_RejectsElementMissingFromDeclaredSegmentMembership()
    {
        var first =
            MinimalTextElement(
                "e1",
                "s1",
                readingOrder:
                    0);

        var second =
            MinimalTextElement(
                "e2",
                "s1",
                readingOrder:
                    1);

        var segment =
            MinimalSegment(
                "s1",
                new[]
                {
                    "e1"
                });

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    MinimalAggregate(
                        new[]
                        {
                            first,
                            second
                        },
                        new[]
                        {
                            segment
                        }));

        Assert.Contains(
            "absent from",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentProcessingProvenance_RejectsSegmentReferencingElementAssignedElsewhere()
    {
        var first =
            MinimalTextElement(
                "e1",
                "s2");

        var second =
            MinimalTextElement(
                "e2",
                "s2",
                readingOrder:
                    1);

        var firstSegment =
            MinimalSegment(
                "s1",
                new[]
                {
                    "e1"
                });

        var secondSegment =
            MinimalSegment(
                "s2",
                new[]
                {
                    "e2"
                },
                ordinal:
                    1);

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    MinimalAggregate(
                        new[]
                        {
                            first,
                            second
                        },
                        new[]
                        {
                            firstSegment,
                            secondSegment
                        }));

        Assert.Contains(
            "declares segment",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicElementProvenance_DoesNotExposeRawBackendLabelOrRawOcrFragments()
    {
        var properties =
            typeof(DocumentElementProvenance)
                .GetProperties()
                .Select(
                    property =>
                        property.Name)
                .ToHashSet(
                    StringComparer.Ordinal);

        Assert.DoesNotContain(
            "LayoutRawLabel",
            properties);

        Assert.DoesNotContain(
            "RawBackendLabel",
            properties);

        Assert.DoesNotContain(
            "OcrTextObservations",
            properties);

        Assert.DoesNotContain(
            "RawOcrPayload",
            properties);
    }

    private static DocumentElementProvenance MinimalTextElement(
        string elementId,
        string? segmentId,
        int readingOrder = 0,
        string selectedSourceText = "text",
        string normalizedText = "text",
        string? selectedSourceTextSha256 = null,
        string? normalizedTextSha256 = null,
        bool? normalizationChangedText = null) =>
        new(
            SourceSha,
            elementId,
            physicalPageNumber:
                1,
            readingOrder,
            HybridDocumentElementKind.Text,
            Rectangle(
                top:
                    0.10,
                bottom:
                    0.20),
            segmentId,
            selectedSourceText,
            selectedSourceTextSha256 ??
            Hash(selectedSourceText),
            normalizedText,
            normalizedTextSha256 ??
            Hash(normalizedText),
            TextSelectionOrigin.Native,
            nativeBlockSourceSequence:
                readingOrder,
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
                normalizationChangedText ??
                !string.Equals(
                    selectedSourceText,
                    normalizedText,
                    StringComparison.Ordinal),
            exclusionReason:
                null,
            isResolved:
                true,
            preservedVisual:
                null);

    private static DocumentSegmentProvenance MinimalSegment(
        string segmentId,
        IReadOnlyList<string> sourceElementIds,
        int ordinal = 0,
        string text = "text",
        string? textSha256 = null) =>
        new(
            SourceSha,
            segmentId,
            ordinal,
            text,
            textSha256 ??
            Hash(text),
            headingText:
                null,
            firstPhysicalPageNumber:
                1,
            lastPhysicalPageNumber:
                1,
            sourceElementIds,
            new[]
            {
                TextSelectionOrigin.Native
            },
            hasUnresolvedEvidence:
                false);

    private static DocumentProcessingProvenance MinimalAggregate(
        IReadOnlyList<DocumentElementProvenance> elements,
        IReadOnlyList<DocumentSegmentProvenance> segments) =>
        new(
            Source(SourceSha),
            new DocumentProcessingManifest(
                "0.1.0-test",
                NativeExtraction(),
                rasterization:
                    null,
                layoutAnalysis:
                    null,
                Array.Empty<ProcessingComponentIdentity>(),
                reconciliation:
                    null,
                Array.Empty<string>(),
                HybridDocumentAssembler.AssemblyProfileId,
                HybridDocumentNormalizer.NormalizationProfileId,
                HybridDocumentSegmenter.SegmentationProfileId),
            elements,
            segments);

    private static DocumentProcessingProvenanceContext
        CompleteContext(
        string sourceSha) =>
        new(
            Source(
                sourceSha),
            "0.1.0-test",
            NativeExtraction(),
            Rasterization(),
            LayoutAnalysis(),
            Reconciliation());

    private static DocumentSourceIdentity Source(
        string sourceSha) =>
        new(
            DocumentFormatId.Pdf,
            sourceSha,
            byteLength:
                12345,
            physicalPageCount:
                1,
            fileName:
                "source.pdf",
            declaredMediaType:
                "application/pdf");

    private static ProcessingComponentIdentity
        NativeExtraction() =>
        new(
            "pdfpig",
            "pdfpig-native-v1");

    private static ProcessingComponentIdentity
        Rasterization() =>
        new(
            "pdftoppm",
            "pdftoppm-300dpi-v1");

    private static ProcessingComponentIdentity
        LayoutAnalysis() =>
        new(
            "pp-structurev3",
            "pp-structurev3-v1");

    private static ProcessingComponentIdentity
        Reconciliation() =>
        new(
            "native-ocr-reconciler",
            "native-ocr-reconciliation-v1");

    private static DocumentProcessing.Core.Hybrid.Segmentation
        .HybridDocumentSegmentationResult BuildRepresentativeSegmentation(
        string sourceShaForVisual)
    {
        var headingLayout =
            Layout(
                sequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Heading,
                top:
                    0.10,
                bottom:
                    0.16,
                rawLabel:
                    "doc_title");

        var ocrRegion =
            new OcrRegionResult(
                "paddleocr-general-ocr",
                "ocr-profile-v1",
                headingLayout,
                new[]
                {
                    new OcrTextObservation(
                        physicalPageNumber:
                            1,
                        sourceLayoutObservationSequence:
                            0,
                        observationSequence:
                            0,
                        text:
                            "Heading",
                        confidence:
                            0.98,
                        bounds:
                            headingLayout.Bounds)
                });

        var reconciliation =
            NativeOcrTextReconciler
                .Reconcile(
                    new TextReconciliationInput(
                        physicalPageNumber:
                            1,
                        NativeTextStatus.Missing,
                        nativeBlock:
                            null,
                        ocrRegion));

        var heading =
            HybridDocumentElementFactory
                .FromReconciliation(
                    reconciliation);

        var nativeBlock =
            new DocumentTextBlock(
                sourceSequence:
                    7,
                readingOrder:
                    1,
                text:
                    "Native   body.",
                bounds:
                    Rectangle(
                        top:
                            0.20,
                        bottom:
                            0.45));

        var native =
            HybridDocumentElementFactory
                .FromNative(
                    1,
                    nativeBlock);

        var figureLayout =
            Layout(
                sequence:
                    2,
                readingOrder:
                    2,
                LayoutObservationKind.Figure,
                top:
                    0.50,
                bottom:
                    0.70,
                rawLabel:
                    "image");

        var visual =
            HybridDocumentElementFactory
                .FromPreservedVisual(
                    new PreservedVisualEvidence(
                        sourceShaForVisual,
                        "visual-profile-v1",
                        "image/png",
                        figureLayout,
                        sourceRasterPixelWidth:
                            1000,
                        sourceRasterPixelHeight:
                            1000,
                        new PixelRectangle(
                            100,
                            500,
                            900,
                            700),
                        contentLength:
                            500,
                        contentSha256:
                            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));

        var deferred =
            HybridDocumentElementFactory
                .FromDeferred(
                    Layout(
                        sequence:
                            3,
                        readingOrder:
                            3,
                        LayoutObservationKind.Unknown,
                        top:
                            0.75,
                        bottom:
                            0.80,
                        rawLabel:
                            "number"));

        var assembly =
            HybridDocumentAssembler
                .AssembleDocument(
                    new[]
                    {
                        HybridDocumentAssembler
                            .AssemblePage(
                                1,
                                new[]
                                {
                                    heading,
                                    native,
                                    visual,
                                    deferred
                                })
                    });

        var normalization =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        return new HybridDocumentSegmenter()
            .Segment(
                normalization);
    }

    private static LayoutObservation Layout(
        int sequence,
        int readingOrder,
        LayoutObservationKind kind,
        double top,
        double bottom,
        string rawLabel) =>
        new(
            physicalPageNumber:
                1,
            observationSequence:
                sequence,
            readingOrder,
            kind,
            Rectangle(
                top,
                bottom),
            rawLabel);

    private static NormalizedRectangle Rectangle(
        double top,
        double bottom) =>
        new(
            left:
                0.10,
            top,
            right:
                0.90,
            bottom);

    private static string Hash(
        string text) =>
        Convert
            .ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        text)))
            .ToLowerInvariant();
}
