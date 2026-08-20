using DocumentProcessing.Core.Documents;
using UglyToad.PdfPig.Core;

namespace DocumentProcessing.Pdf;

/// <summary>
/// PDF implementation of the neutral document-format acquisition contract.
/// </summary>
/// <remarks>
/// This boundary deliberately reuses the current validated PDF extraction
/// implementation. It recognizes the source, acquires native text/structure
/// evidence and low-level deterministic visual raster observations, and stops
/// before Engine assessment, planning, OCR, layout, reconciliation or policy.
///
/// Existing PDF processor/validator/extractor contracts remain in place during
/// this migration increment.
/// </remarks>
public sealed class PdfDocumentFormat
    : IDocumentFormat
{
    #region Variables and Constants

    private readonly PdfFormatValidator _validator;
    private readonly PdfPigDocumentExtractor _extractor;
    private readonly PdfPigVisualRasterObservationSource
        _visualRasterObservationSource;

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
    }

    #endregion

    #region Properties

    public DocumentFormatId Format =>
        DocumentFormatId.Pdf;

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
                        currentEvidence));
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
