using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Engine.Orchestration;
using Xunit;

namespace DocumentProcessing.UnitTests;

public sealed class DocumentFormatProcessingBindingTests
{
    #region Variables and Constants

    private static readonly DocumentFormatId AlternateFormat =
        new(
            "binding-alternate");

    #endregion

    #region Methods Tests

    [Fact]
    public void ctor_MatchingFormat_BindsExactInstances()
    {
        var documentFormat =
            new StubDocumentFormat(
                DocumentFormatId.Pdf);

        var processor =
            CreateProcessor(
                DocumentFormatId.Pdf);

        var binding =
            new DocumentFormatProcessingBinding(
                documentFormat,
                processor);

        Assert.Equal(
            DocumentFormatId.Pdf,
            binding.Format);

        Assert.Same(
            documentFormat,
            binding.DocumentFormat);

        Assert.Same(
            processor,
            binding.Processor);
    }

    [Fact]
    public void ctor_MismatchedProcessorFormat_RejectsBinding()
    {
        var documentFormat =
            new StubDocumentFormat(
                DocumentFormatId.Pdf);

        var processor =
            CreateProcessor(
                AlternateFormat);

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentFormatProcessingBinding(
                        documentFormat,
                        processor));

        Assert.Contains(
            DocumentFormatId.Pdf.Value,
            exception.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            AlternateFormat.Value,
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ctor_NullDocumentFormat_RejectsBinding()
    {
        var processor =
            CreateProcessor(
                DocumentFormatId.Pdf);

        Assert.Throws<ArgumentNullException>(
            () =>
                new DocumentFormatProcessingBinding(
                    null!,
                    processor));
    }

    [Fact]
    public void ctor_NullProcessor_RejectsBinding()
    {
        var documentFormat =
            new StubDocumentFormat(
                DocumentFormatId.Pdf);

        Assert.Throws<ArgumentNullException>(
            () =>
                new DocumentFormatProcessingBinding(
                    documentFormat,
                    null!));
    }

    #endregion

    #region Methods Fixtures

    private static DocumentProcessor CreateProcessor(
        DocumentFormatId format) =>
        new(
            format,
            new StubDocumentExtractor(),
            new StubPreflightAnalyzer(),
            "format-processing-binding-test",
            new ProcessingComponentIdentity(
                "binding-native-extractor",
                "test-v1"));

    #endregion

    #region Types

    private sealed class StubDocumentFormat
        : IDocumentFormat
    {
        #region ctor

        public StubDocumentFormat(
            DocumentFormatId format)
        {
            Format =
                format;
        }

        #endregion

        #region Properties

        public DocumentFormatId Format { get; }

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

            return ValueTask.FromResult<
                NativeEvidenceExtractionResult>(
                new NativeEvidenceExtractionResult
                    .NotRecognized());
        }

        #endregion
    }

    private sealed class StubDocumentExtractor
        : IDocumentExtractor
    {
        #region Methods Extraction

        public bool CanExtract(
            DocumentFormatId format) =>
            true;

        public ValueTask<DocumentExtractionResult> ExtractAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Binding tests must not perform native extraction.");

        #endregion
    }

    private sealed class StubPreflightAnalyzer
        : IDocumentPreflightAnalyzer
    {
        #region Methods Preflight

        public bool CanAnalyze(
            DocumentFormatId format) =>
            true;

        public DocumentPreflightResult Analyze(
            DocumentExtractionResult extraction) =>
            throw new InvalidOperationException(
                "Binding tests must not perform preflight analysis.");

        #endregion
    }

    #endregion
}
