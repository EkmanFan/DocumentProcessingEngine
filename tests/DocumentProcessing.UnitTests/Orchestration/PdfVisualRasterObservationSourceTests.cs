using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class PdfVisualRasterObservationSourceTests
{
    private static readonly NormalizedRectangle FullPage =
        new(
            0,
            0,
            1,
            1);

    [Fact]
    public void Measure_BlankWhiteCanvas_ProducesExactBlankObservation()
    {
        var observation =
            Engine().Measure(
                sourceVisualIndex:
                    0,
                FullPage,
                VisualRasterDecodeSource.RawEmbeddedImage,
                width:
                    16,
                height:
                    16,
                Rgba(
                    16,
                    16,
                    (_, _) =>
                        White),
                nativeWords:
                    []);

        Assert.Equal(
            VisualForegroundState.BlankCanvas,
            observation.ForegroundState);

        Assert.Equal(
            0,
            observation.ForegroundPixelRatio);

        Assert.Equal(
            VisualPixelInteractionKind.BlankCanvas,
            observation.PixelInteraction);

        Assert.Equal(
            0,
            observation.NativeWordsTouchedRatio);

        Assert.Equal(
            0,
            observation.SignificantComponentCount);

        Assert.Null(
            observation.EffectiveVisualBounds);

        Assert.Null(
            observation.EffectiveVisualAreaRatio);

        Assert.Equal(
            1,
            observation.BackgroundUniformity);
    }

    [Fact]
    public void Measure_FourByFourForeground_PreservesFrozenComponentThreshold()
    {
        var observation =
            Engine().Measure(
                sourceVisualIndex:
                    0,
                FullPage,
                VisualRasterDecodeSource.RawEmbeddedImage,
                width:
                    16,
                height:
                    16,
                Rgba(
                    16,
                    16,
                    (x, y) =>
                        x is >= 6 and <= 9 &&
                        y is >= 6 and <= 9
                            ? Black
                            : White),
                nativeWords:
                    []);

        Assert.Equal(
            VisualForegroundState.Measured,
            observation.ForegroundState);

        var foregroundPixelRatio =
            Assert.IsType<double>(
                observation.ForegroundPixelRatio);

        Assert.Equal(
            16d /
            256d,
            foregroundPixelRatio,
            precision:
                12);

        Assert.Equal(
            VisualPixelInteractionKind.NoNativeWords,
            observation.PixelInteraction);

        Assert.Equal(
            1,
            observation.SignificantComponentCount);

        var effective =
            Assert.IsType<NormalizedRectangle>(
                observation.EffectiveVisualBounds);

        Assert.Equal(
            6d /
            16d,
            effective.Left,
            precision:
                12);

        Assert.Equal(
            6d /
            16d,
            effective.Top,
            precision:
                12);

        Assert.Equal(
            10d /
            16d,
            effective.Right,
            precision:
                12);

        Assert.Equal(
            10d /
            16d,
            effective.Bottom,
            precision:
                12);

        var effectiveVisualAreaRatio =
            Assert.IsType<double>(
                observation.EffectiveVisualAreaRatio);

        Assert.Equal(
            16d /
            256d,
            effectiveVisualAreaRatio,
            precision:
                12);
    }

    [Fact]
    public void Measure_ForegroundInsideNativeWord_ReportsWordInteraction()
    {
        var observation =
            Engine().Measure(
                sourceVisualIndex:
                    0,
                FullPage,
                VisualRasterDecodeSource.RawEmbeddedImage,
                width:
                    16,
                height:
                    16,
                Rgba(
                    16,
                    16,
                    (x, y) =>
                        x is >= 6 and <= 9 &&
                        y is >= 6 and <= 9
                            ? Black
                            : White),
                nativeWords:
                [
                    new DocumentWord(
                        sourceSequence:
                            0,
                        text:
                            "inside",
                        bounds:
                            new NormalizedRectangle(
                                0.375,
                                0.375,
                                0.625,
                                0.625))
                ]);

        Assert.Equal(
            VisualPixelInteractionKind.ForegroundWordInteraction,
            observation.PixelInteraction);

        Assert.Equal(
            1,
            observation.NativeWordsTouchedRatio);
    }

    [Fact]
    public void Measure_ForegroundAwayFromNativeWord_ReportsNoIntersection()
    {
        var observation =
            Engine().Measure(
                sourceVisualIndex:
                    0,
                FullPage,
                VisualRasterDecodeSource.RawEmbeddedImage,
                width:
                    16,
                height:
                    16,
                Rgba(
                    16,
                    16,
                    (x, y) =>
                        x is >= 10 and <= 13 &&
                        y is >= 10 and <= 13
                            ? Black
                            : White),
                nativeWords:
                [
                    new DocumentWord(
                        sourceSequence:
                            0,
                        text:
                            "outside",
                        bounds:
                            new NormalizedRectangle(
                                0,
                                0,
                                0.10,
                                0.10))
                ]);

        Assert.Equal(
            VisualPixelInteractionKind.NoForegroundWordIntersection,
            observation.PixelInteraction);

        Assert.Equal(
            0,
            observation.NativeWordsTouchedRatio);
    }

    [Fact]
    public void Measure_NonUniformBoundary_FailsClosedAsUnavailable()
    {
        var observation =
            Engine().Measure(
                sourceVisualIndex:
                    0,
                FullPage,
                VisualRasterDecodeSource.RawEmbeddedImage,
                width:
                    16,
                height:
                    16,
                Rgba(
                    16,
                    16,
                    (_, y) =>
                        y ==
                            0
                            ? Black
                            : White),
                nativeWords:
                    []);

        Assert.Equal(
            VisualForegroundState.Unavailable,
            observation.ForegroundState);

        Assert.Null(
            observation.ForegroundPixelRatio);

        Assert.Equal(
            VisualPixelInteractionKind.NotMeasured,
            observation.PixelInteraction);

        Assert.Null(
            observation.SignificantComponentCount);

        Assert.Null(
            observation.EffectiveVisualBounds);

        Assert.NotNull(
            observation.BackgroundUniformity);

        Assert.True(
            observation.BackgroundUniformity <
            PdfVisualRasterMeasurementEngine
                .BackgroundUniformityRequired);
    }

    [Fact]
    public void Measure_PreCancelledToken_DoesNotStartPixelAnalysis()
    {
        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () =>
                Engine().Measure(
                    sourceVisualIndex:
                        0,
                    FullPage,
                    VisualRasterDecodeSource.RawEmbeddedImage,
                    width:
                        16,
                    height:
                        16,
                    Rgba(
                        16,
                        16,
                        (_, _) =>
                            White),
                    nativeWords:
                        [],
                    cancellation.Token));
    }

    [Fact]
    public async Task Source_ObservesGeneratedPdfImage_EndToEnd()
    {
        var pdfBytes =
            BuildPdfWithEmbeddedPng();

        await using var stream =
            new MemoryStream(
                pdfBytes);

        var documentSource =
            new DocumentSource(
                stream,
                "generated.pdf",
                "application/pdf");

        var extraction =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    documentSource,
                    DocumentFormatId.Pdf);

        Assert.Equal(
            1,
            Assert.Single(
                extraction.Pages).RasterImageCount);

        stream.Position =
            7;

        var observer =
            new PdfPigVisualRasterObservationSource();

        var pages =
            await observer.ObserveAsync(
                documentSource,
                DocumentFormatId.Pdf,
                extraction);

        Assert.Equal(
            7,
            stream.Position);

        var page =
            Assert.Single(
                pages);

        Assert.Equal(
            1,
            page.PhysicalPageNumber);

        var observation =
            Assert.Single(
                page.VisualElements);

        Assert.NotEqual(
            VisualRasterDecodeSource.Unavailable,
            observation.DecodeSource);

        Assert.Equal(
            VisualForegroundState.Measured,
            observation.ForegroundState);

        Assert.Equal(
            16,
            observation.PixelWidth);

        Assert.Equal(
            16,
            observation.PixelHeight);

        var foregroundPixelRatio =
            Assert.IsType<double>(
                observation.ForegroundPixelRatio);

        Assert.Equal(
            16d /
            256d,
            foregroundPixelRatio,
            precision:
                12);

        Assert.Equal(
            VisualPixelInteractionKind.NoNativeWords,
            observation.PixelInteraction);

        Assert.Equal(
            1,
            observation.SignificantComponentCount);

        Assert.NotNull(
            observation.EffectiveVisualBounds);
    }

    [Fact]
    public void SharedPagePrimitives_ReuseOneMaterializedPdfPigPage()
    {
        var pdfBytes =
            BuildPdfWithEmbeddedPng();

        using var document =
            PdfDocument.Open(
                pdfBytes);

        var sourcePage =
            document.GetPage(
                1);

        var extractionPage =
            PdfPigDocumentExtractor
                .ExtractPage(
                    sourcePage,
                    physicalPageNumber:
                        1,
                    out var coordinateSpace,
                    out var images);

        var observationPage =
            new PdfPigVisualRasterObservationSource()
                .ObservePage(
                    physicalPageNumber:
                        1,
                    coordinateSpace,
                    images,
                    extractionPage);

        Assert.Equal(
            1,
            extractionPage.PhysicalPageNumber);

        Assert.Equal(
            1,
            extractionPage.RasterImageCount);

        Assert.Single(
            images);

        Assert.Equal(
            1,
            observationPage.PhysicalPageNumber);

        var observation =
            Assert.Single(
                observationPage.VisualElements);

        Assert.NotEqual(
            VisualRasterDecodeSource.Unavailable,
            observation.DecodeSource);

        Assert.Equal(
            VisualForegroundState.Measured,
            observation.ForegroundState);
    }

    [Fact]
    public async Task Source_RejectsImageCountDriftAgainstExtraction()
    {
        var pdfBytes =
            BuildPdfWithEmbeddedPng();

        await using var stream =
            new MemoryStream(
                pdfBytes);

        var source =
            new DocumentSource(
                stream,
                "generated.pdf",
                "application/pdf");

        var driftedExtraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    new DocumentExtractionPage(
                        physicalPageNumber:
                            1,
                        sourceText:
                            string.Empty,
                        wordCount:
                            0,
                        rasterImageCount:
                            2)
                ]);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await new PdfPigVisualRasterObservationSource()
                    .ObserveAsync(
                        source,
                        DocumentFormatId.Pdf,
                        driftedExtraction));
    }

    [Fact]
    public async Task Source_RejectsUnsupportedFormat()
    {
        await using var stream =
            new MemoryStream(
                BuildPdfWithEmbeddedPng());

        var source =
            new DocumentSource(
                stream);

        var observer =
            new PdfPigVisualRasterObservationSource();

        var unsupported =
            new DocumentFormatId(
                "text");

        await Assert.ThrowsAsync<NotSupportedException>(
            async () =>
                await observer.ObserveAsync(
                    source,
                    unsupported,
                    new DocumentExtractionResult(
                        unsupported)));
    }

    [Fact]
    public void Source_RejectsInvalidPixelBudget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PdfPigVisualRasterObservationSource(
                    maxDecodedPixels:
                        0));
    }

    [Fact]
    public void PageObservations_SnapshotCallerOwnedCollection()
    {
        var source =
            new List<VisualRasterObservation>
            {
                BlankObservation(
                    sourceVisualIndex:
                        0)
            };

        var page =
            new PageVisualRasterObservations(
                physicalPageNumber:
                    1,
                source);

        source.Add(
            BlankObservation(
                sourceVisualIndex:
                    1));

        Assert.Single(
            page.VisualElements);
    }

    [Fact]
    public void PageObservations_RejectDuplicateSourceVisualIndexes()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PageVisualRasterObservations(
                    physicalPageNumber:
                        1,
                    [
                        BlankObservation(
                            sourceVisualIndex:
                                0),
                        BlankObservation(
                            sourceVisualIndex:
                                0)
                    ]));
    }

    [Fact]
    public void RasterObservation_DoesNotCarryStructuralOrPolicyFields()
    {
        var propertyNames =
            typeof(VisualRasterObservation)
                .GetProperties()
                .Select(
                    property =>
                        property.Name)
                .ToHashSet(
                    StringComparer.Ordinal);

        Assert.DoesNotContain(
            nameof(VisualEvidenceObservation.HeadingAssociation),
            propertyNames);

        Assert.DoesNotContain(
            nameof(VisualEvidenceObservation.TextContainment),
            propertyNames);

        Assert.DoesNotContain(
            nameof(VisualEvidenceObservation.CaptionAssociation),
            propertyNames);

        Assert.DoesNotContain(
            nameof(VisualElementEvidence.Kind),
            propertyNames);
    }

    private static PdfVisualRasterMeasurementEngine Engine() =>
        new();

    private static VisualRasterObservation BlankObservation(
        int sourceVisualIndex) =>
        new(
            sourceVisualIndex,
            FullPage,
            VisualRasterDecodeSource.RawEmbeddedImage,
            pixelWidth:
                16,
            pixelHeight:
                16,
            backgroundUniformity:
                1,
            VisualForegroundState.BlankCanvas,
            foregroundPixelRatio:
                0,
            VisualPixelInteractionKind.BlankCanvas,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                0,
            effectiveVisualBounds:
                null);

    private static byte[] BuildPdfWithEmbeddedPng()
    {
        var builder =
            new PdfDocumentBuilder();

        var page =
            builder.AddPage(
                400,
                400);

        page.AddPng(
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAMElEQVR4nGP8////fwYKABMlmkcNgAAWXBKMjIwofFyRNfBeoF0YEJtAB94Lw8AAANR7Ch2xuB6GAAAAAElFTkSuQmCC"),
            new PdfRectangle(
                100,
                100,
                300,
                300));

        return builder.Build();
    }

    private static byte[] Rgba(
        int width,
        int height,
        Func<int, int, RgbaPixel> pixelFactory)
    {
        var bytes =
            new byte[
                checked(
                    width *
                    height *
                    4)];

        for (var y = 0;
             y < height;
             y++)
        {
            for (var x = 0;
                 x < width;
                 x++)
            {
                var pixel =
                    pixelFactory(
                        x,
                        y);

                var index =
                    (
                        y *
                        width +
                        x
                    ) *
                    4;

                bytes[index] =
                    pixel.R;

                bytes[index +
                    1] =
                    pixel.G;

                bytes[index +
                    2] =
                    pixel.B;

                bytes[index +
                    3] =
                    pixel.A;
            }
        }

        return bytes;
    }

    private static RgbaPixel White =>
        new(
            255,
            255,
            255,
            255);

    private static RgbaPixel Black =>
        new(
            0,
            0,
            0,
            255);

    private readonly record struct RgbaPixel(
        byte R,
        byte G,
        byte B,
        byte A);
}
