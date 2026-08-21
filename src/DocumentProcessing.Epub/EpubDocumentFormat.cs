using DocumentProcessing.Core.Documents;
using DocumentProcessing.Epub.Extraction;
using DocumentProcessing.Epub.Recognition;
using DocumentProcessing.Epub.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentProcessing.Epub;

/// <summary>
/// EPUB recognition, official conformance validation and native package/XHTML
/// acquisition boundary.
/// </summary>
public sealed class EpubDocumentFormat
    : IDocumentFormat
{
    #region Variables and Constants

    private const string SourceTooLargeMessage =
        "Le fichier EPUB dépasse la taille maximale prise en charge.";

    private const string ProcessingUnavailableMessage =
        "Le traitement EPUB est temporairement indisponible.";

    private readonly EpubDocumentFormatOptions _options;
    private readonly EpubFormatRecognizer _recognizer;
    private readonly EpubCheckConformanceValidator _validator;
    private readonly EpubPackageExtractor _extractor;
    private readonly ILogger<EpubDocumentFormat> _logger;

    #endregion

    #region Properties

    public DocumentFormatId Format =>
        DocumentFormatId.Epub;

    #endregion

    #region ctor

    public EpubDocumentFormat(
        EpubDocumentFormatOptions? options = null,
        ILoggerFactory? loggerFactory = null)
    {
        _options =
            options ??
            new EpubDocumentFormatOptions();

        _recognizer =
            new EpubFormatRecognizer();

        _validator =
            new EpubCheckConformanceValidator(
                _options.EpubCheck,
                logger:
                    loggerFactory?
                        .CreateLogger<EpubCheckConformanceValidator>());

        _extractor =
            new EpubPackageExtractor();

        _logger =
            loggerFactory?
                .CreateLogger<EpubDocumentFormat>() ??
            NullLogger<EpubDocumentFormat>
                .Instance;
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

        if (!source.Content.CanSeek)
        {
            throw new InvalidOperationException(
                "EPUB acquisition requires the Engine-prepared seekable source.");
        }

        var originalPosition =
            source.Content.Position;

        try
        {
            if (!_recognizer.IsRecognized(
                    source))
            {
                return new NativeEvidenceExtractionResult
                    .NotRecognized();
            }

            if (source.Content.Length >
                _options.MaximumSourceBytes)
            {
                return new NativeEvidenceExtractionResult.Invalid(
                    SourceTooLargeMessage,
                    isConsumerSafeReason:
                        true);
            }

            await using var materialized =
                await EpubSourceMaterialization
                    .CreateAsync(
                        source.Content,
                        _options.MaximumSourceBytes,
                        cancellationToken)
                    .ConfigureAwait(false);

            var conformance =
                await _validator
                    .ValidateAsync(
                        materialized.Path,
                        cancellationToken)
                    .ConfigureAwait(false);

            var conformanceFailure =
                EpubConformanceOutcomeMapper.MapFailure(
                    conformance.Status);

            if (conformanceFailure is not null)
            {
                return conformanceFailure;
            }

            source.Content.Position =
                0;

            var evidence =
                _extractor.Extract(
                    source.Content,
                    _options,
                    cancellationToken);

            return new NativeEvidenceExtractionResult.StructuredSuccess(
                evidence);
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
        catch (Exception exception)
            when (exception is InvalidDataException or
                  IOException or
                  UnauthorizedAccessException or
                  System.Xml.XmlException or
                  UriFormatException or
                  OverflowException)
        {
            _logger.LogError(
                exception,
                "Native EPUB acquisition failed after format recognition and conformance validation.");

            return new NativeEvidenceExtractionResult.Unavailable(
                ProcessingUnavailableMessage);
        }
        finally
        {
            try
            {
                source.Content.Position =
                    originalPosition;
            }
            catch (Exception exception)
                when (exception is IOException or
                      ObjectDisposedException or
                      NotSupportedException)
            {
            }
        }
    }

    #endregion
}
