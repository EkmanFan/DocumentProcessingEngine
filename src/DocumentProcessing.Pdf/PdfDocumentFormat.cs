using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using UglyToad.PdfPig.Core;

namespace DocumentProcessing.Pdf;

/// <summary>
/// PDF implementation of the neutral document-format acquisition contract and
/// the PDF-specific technical capabilities currently used by the Engine.
/// </summary>
/// <remarks>
/// This boundary recognizes PDF, acquires native evidence and exposes operations
/// that exist because the source is PDF. It does not decide whether native
/// evidence is sufficient, whether enrichment is required, or which processing
/// route is authoritative.
/// </remarks>
public sealed class PdfDocumentFormat
    : IDocumentFormat,
      IDocumentRasterizer,
      IVisualRasterObservationSource
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity
        NativeExtractionIdentity =
            new(
                "pdfpig",
                "pdfpig-native-v1");

    private readonly PdfFormatValidator _validator;
    private readonly PdfPigDocumentExtractor _extractor;
    private readonly PdfPigVisualRasterObservationSource
        _visualRasterObservationSource;
    private readonly PdftoppmDocumentRasterizer
        _documentRasterizer;

    #endregion

    #region Properties

    public DocumentFormatId Format =>
        DocumentFormatId.Pdf;

    #endregion

    #region ctor

    public PdfDocumentFormat()
    {
        _validator =
            new PdfFormatValidator();

        _extractor =
            new PdfPigDocumentExtractor();

        _visualRasterObservationSource =
            new PdfPigVisualRasterObservationSource();

        _documentRasterizer =
            new PdftoppmDocumentRasterizer(
                dpi:
                    300);
    }

    #endregion

    #region Methods Acquisition

    public async ValueTask<NativeEvidenceExtractionResult>
        TryExtractNativeEvidenceAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        var recognized =
            await _validator
                .ValidateAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!recognized)
        {
            return new NativeEvidenceExtractionResult
                .NotRecognized();
        }

        try
        {
            var currentEvidence =
                await _extractor
                    .ExtractWithRasterObservationsAsync(
                        source,
                        DocumentFormatId.Pdf,
                        _visualRasterObservationSource,
                        cancellationToken)
                    .ConfigureAwait(false);

            return new NativeEvidenceExtractionResult
                .Success(
                    new NativeDocumentEvidence(
                        currentEvidence,
                        NativeExtractionIdentity));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (PdfDocumentFormatException exception)
        {
            return Invalid(
                exception);
        }
        catch (InvalidDataException exception)
        {
            return Invalid(
                exception);
        }
    }

    #endregion

    #region Methods Rasterization Capability

    public bool CanRasterize(
        DocumentFormatId format) =>
        _documentRasterizer
            .CanRasterize(
                format);

    public ValueTask<IDocumentRasterizationSession> OpenAsync(
        DocumentSource source,
        DocumentFormatId format,
        CancellationToken cancellationToken = default) =>
        _documentRasterizer
            .OpenAsync(
                source,
                format,
                cancellationToken);

    #endregion

    #region Methods Native Visual Observation Capability

    public bool CanObserve(
        DocumentFormatId format) =>
        _visualRasterObservationSource
            .CanObserve(
                format);

    public ValueTask<IReadOnlyList<PageVisualRasterObservations>>
        ObserveAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            CancellationToken cancellationToken = default) =>
        _visualRasterObservationSource
            .ObserveAsync(
                source,
                format,
                extraction,
                cancellationToken);

    #endregion

    #region Methods Classification

    private static NativeEvidenceExtractionResult.Invalid Invalid(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        var detail =
            string.IsNullOrWhiteSpace(
                exception.Message)
                ? "The recognized PDF cannot be parsed as a valid document."
                : exception.Message.Trim();

        return new NativeEvidenceExtractionResult.Invalid(
            detail);
    }

    #endregion
}
