using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class ConfiguredDocumentProcessingEngineTests
{
    #region Variables and Constants

    private static readonly DocumentFormatId FormatA =
        new(
            "configured-a");

    private static readonly DocumentFormatId FormatB =
        new(
            "configured-b");

    #endregion

    #region Methods Tests

    [Fact]
    public async Task ProcessConfiguredDocumentAsync_NotRecognized_RemainsFunctionalOutcome()
    {
        var format =
            new StubDocumentFormat(
                FormatA,
                new NativeEvidenceExtractionResult
                    .NotRecognized());

        var extractor =
            new ThrowingDocumentExtractor();

        var engine =
            CreateEngine(
                format,
                extractor);

        var result =
            await engine
                .ProcessConfiguredDocumentAsync(
                    CreateSource(),
                    CancellationToken.None);

        Assert.IsType<
            DocumentProcessingAttemptResult.NotRecognized>(
            result);

        Assert.Equal(
            1,
            format.AcquisitionCalls);

        Assert.Equal(
            0,
            extractor.ExtractCalls);
    }

    [Fact]
    public async Task ProcessConfiguredDocumentAsync_Invalid_RemainsFunctionalOutcome()
    {
        const string reason =
            "recognized but malformed";

        var format =
            new StubDocumentFormat(
                FormatA,
                new NativeEvidenceExtractionResult
                    .Invalid(
                        reason));

        var extractor =
            new ThrowingDocumentExtractor();

        var engine =
            CreateEngine(
                format,
                extractor);

        var result =
            Assert.IsType<
                DocumentProcessingAttemptResult.Invalid>(
                await engine
                    .ProcessConfiguredDocumentAsync(
                        CreateSource(),
                        CancellationToken.None));

        Assert.Equal(
            FormatA,
            result.Format);

        Assert.Equal(
            reason,
            result.Reason);

        Assert.Equal(
            0,
            extractor.ExtractCalls);
    }

    [Fact]
    public async Task ProcessConfiguredDocumentAsync_Ambiguous_RemainsFunctionalOutcome()
    {
        var formatA =
            new StubDocumentFormat(
                FormatA,
                new NativeEvidenceExtractionResult
                    .Invalid(
                        "a"));

        var formatB =
            new StubDocumentFormat(
                FormatB,
                new NativeEvidenceExtractionResult
                    .Invalid(
                        "b"));

        var extractorA =
            new ThrowingDocumentExtractor();

        var extractorB =
            new ThrowingDocumentExtractor();

        var engine =
            new DocumentProcessingEngine(
                [
                    CreateBinding(
                        formatA,
                        extractorA),
                    CreateBinding(
                        formatB,
                        extractorB)
                ]);

        var result =
            Assert.IsType<
                DocumentProcessingAttemptResult.Ambiguous>(
                await engine
                    .ProcessConfiguredDocumentAsync(
                        CreateSource(),
                        CancellationToken.None));

        Assert.Equal(
            [
                FormatA,
                FormatB
            ],
            result.Formats);

        Assert.Equal(
            0,
            extractorA.ExtractCalls);

        Assert.Equal(
            0,
            extractorB.ExtractCalls);
    }

    [Fact]
    public async Task ProcessConfiguredDocumentAsync_SuccessUsesPreacquiredEvidenceWithoutExtractor()
    {
        var extractor =
            new ThrowingDocumentExtractor();

        var evidence =
            CreateInvalidProcessingEvidence(
                FormatA);

        var format =
            new StubDocumentFormat(
                FormatA,
                new NativeEvidenceExtractionResult
                    .Success(
                        evidence));

        var engine =
            CreateEngine(
                format,
                extractor);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await engine
                    .ProcessConfiguredDocumentAsync(
                        CreateSource(),
                        CancellationToken.None));

        Assert.Equal(
            1,
            format.AcquisitionCalls);

        Assert.Equal(
            0,
            extractor.ExtractCalls);
    }

    [Fact]
    public async Task ProcessConfiguredDocumentAsync_ParameterlessEngineRejectsMissingConfiguration()
    {
        var engine =
            new DocumentProcessingEngine();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await engine
                    .ProcessConfiguredDocumentAsync(
                        CreateSource(),
                        CancellationToken.None));
    }

    [Fact]
    public void ctor_DuplicateFormatBindings_FailsClosed()
    {
        var first =
            new StubDocumentFormat(
                FormatA,
                new NativeEvidenceExtractionResult
                    .NotRecognized());

        var second =
            new StubDocumentFormat(
                FormatA,
                new NativeEvidenceExtractionResult
                    .NotRecognized());

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentProcessingEngine(
                    [
                        CreateBinding(
                            first,
                            new ThrowingDocumentExtractor()),
                        CreateBinding(
                            second,
                            new ThrowingDocumentExtractor())
                    ]));
    }

    #endregion

    #region Methods Fixtures

    private static DocumentProcessingEngine CreateEngine(
        StubDocumentFormat format,
        ThrowingDocumentExtractor extractor) =>
        new(
            [
                CreateBinding(
                    format,
                    extractor)
            ]);

    private static DocumentFormatProcessingBinding CreateBinding(
        StubDocumentFormat format,
        ThrowingDocumentExtractor extractor) =>
        new(
            format,
            new DocumentProcessor(
                format.Format,
                extractor,
                new ThrowingPreflightAnalyzer(),
                "configured-engine-test",
                new ProcessingComponentIdentity(
                    "configured-engine-native",
                    "test-v1")));

    private static NativeDocumentEvidence CreateInvalidProcessingEvidence(
        DocumentFormatId format)
    {
        var extraction =
            new DocumentExtractionResult(
                format);

        var coordinated =
            new DocumentExtractionWithRasterObservationsResult(
                extraction,
                Array.Empty<PageVisualRasterObservations>(),
                rasterObservationFailure:
                    null);

        return new NativeDocumentEvidence(
            coordinated);
    }

    private static DocumentSource CreateSource() =>
        new(
            new MemoryStream(
                "configured engine fixture"u8.ToArray()),
            fileName:
                "fixture.bin",
            declaredMediaType:
                "application/octet-stream");

    #endregion

    #region Types

    private sealed class StubDocumentFormat
        : IDocumentFormat
    {
        #region Variables and Constants

        private readonly NativeEvidenceExtractionResult
            _outcome;

        #endregion

        #region ctor

        public StubDocumentFormat(
            DocumentFormatId format,
            NativeEvidenceExtractionResult outcome)
        {
            Format =
                format;

            _outcome =
                outcome ??
                throw new ArgumentNullException(
                    nameof(outcome));
        }

        #endregion

        #region Properties

        public DocumentFormatId Format { get; }

        public int AcquisitionCalls { get; private set; }

        #endregion

        #region Methods Acquisition

        public ValueTask<NativeEvidenceExtractionResult>
            TryExtractNativeEvidenceAsync(
                DocumentSource source,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            AcquisitionCalls++;

            return ValueTask.FromResult(
                _outcome);
        }

        #endregion
    }

    private sealed class ThrowingDocumentExtractor
        : IDocumentExtractor
    {
        #region Properties

        public int ExtractCalls { get; private set; }

        #endregion

        #region Methods Extraction

        public bool CanExtract(
            DocumentFormatId format) =>
            true;

        public ValueTask<DocumentExtractionResult> ExtractAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default)
        {
            ExtractCalls++;

            throw new InvalidOperationException(
                "Configured Engine processing must consume preacquired native evidence.");
        }

        #endregion
    }

    private sealed class ThrowingPreflightAnalyzer
        : IDocumentPreflightAnalyzer
    {
        #region Methods Preflight

        public bool CanAnalyze(
            DocumentFormatId format) =>
            true;

        public DocumentPreflightResult Analyze(
            DocumentExtractionResult extraction) =>
            throw new InvalidOperationException(
                "The invalid success fixture must fail extraction validation before preflight.");

        #endregion
    }

    #endregion
}
