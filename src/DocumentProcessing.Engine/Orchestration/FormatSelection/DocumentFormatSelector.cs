using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Selects one neutral document format by acquiring native evidence from each
/// registered format against the same prepared, replayable source.
/// </summary>
/// <remarks>
/// Every result other than <see cref="NativeEvidenceExtractionResult.NotRecognized"/>
/// means that a format recognized the source. More than one recognition claim
/// therefore fails closed as an ambiguous format selection rather than using
/// registration order.
/// </remarks>
internal sealed class DocumentFormatSelector
{
    #region Variables and Constants

    private readonly IReadOnlyList<IDocumentFormat>
        _formats;

    #endregion

    #region ctor

    public DocumentFormatSelector(
        IEnumerable<IDocumentFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(
            formats);

        var materialized =
            formats
                .ToArray();

        if (materialized.Length ==
            0)
        {
            throw new ArgumentException(
                "At least one document format must be registered.",
                nameof(formats));
        }

        if (materialized.Any(
                format =>
                    format is null))
        {
            throw new ArgumentException(
                "Registered document formats cannot contain null entries.",
                nameof(formats));
        }

        var duplicateFormat =
            materialized
                .GroupBy(
                    format =>
                        format.Format)
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (duplicateFormat is not null)
        {
            throw new ArgumentException(
                $"Document format '{duplicateFormat.Key}' is registered more than once.",
                nameof(formats));
        }

        _formats =
            materialized;
    }

    #endregion

    #region Methods Selection

    public async ValueTask<DocumentFormatSelectionResult> SelectAsync(
        PreparedDocumentSource preparedSource,
        PhysicalPageRange? physicalPageRange = null,
        ContentUnitRange? contentUnitRange = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            preparedSource);

        if (physicalPageRange is not null &&
            contentUnitRange is not null)
        {
            throw new ArgumentException(
                "Format selection cannot combine physical-page and content-unit ranges.",
                nameof(contentUnitRange));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var recognitionClaims =
            new List<
                (
                    IDocumentFormat Format,
                    NativeEvidenceExtractionResult Outcome
                )>();

        try
        {
            foreach (var format in
                     _formats)
            {
                cancellationToken.ThrowIfCancellationRequested();

                preparedSource.ResetForRead();

                var outcome =
                    physicalPageRange is not null &&
                    format is IPhysicalPageRangeDocumentFormat pagedRangeFormat
                        ? await pagedRangeFormat
                            .TryExtractNativeEvidenceAsync(
                                preparedSource.Source,
                                physicalPageRange,
                                cancellationToken)
                            .ConfigureAwait(false)
                        : contentUnitRange is not null &&
                          format is IContentUnitRangeDocumentFormat contentRangeFormat
                            ? await contentRangeFormat
                                .TryExtractNativeEvidenceAsync(
                                    preparedSource.Source,
                                    contentUnitRange,
                                    cancellationToken)
                                .ConfigureAwait(false)
                        : await format
                            .TryExtractNativeEvidenceAsync(
                                preparedSource.Source,
                                cancellationToken)
                            .ConfigureAwait(false);

                if (outcome is null)
                {
                    throw new InvalidDataException(
                        $"Document format '{format.Format}' returned no native-evidence outcome.");
                }

                switch (outcome)
                {
                    case NativeEvidenceExtractionResult.NotRecognized:
                        break;

                    case NativeEvidenceExtractionResult.Invalid:
                    case NativeEvidenceExtractionResult.Unavailable:
                    case NativeEvidenceExtractionResult.Success:
                        recognitionClaims.Add(
                            (
                                format,
                                outcome
                            ));
                        break;

                    default:
                        throw new InvalidDataException(
                            $"Document format '{format.Format}' returned unsupported native-evidence outcome " +
                            $"'{outcome.GetType().FullName}'.");
                }
            }
        }
        finally
        {
            preparedSource.ResetForRead();
        }

        if (recognitionClaims.Count ==
            0)
        {
            return new DocumentFormatSelectionResult
                .NotRecognized();
        }

        if (recognitionClaims.Count >
            1)
        {
            return new DocumentFormatSelectionResult
                .Ambiguous(
                    recognitionClaims
                        .Select(
                            claim =>
                                claim.Format.Format)
                        .ToArray());
        }

        var selected =
            recognitionClaims[0];

        return selected.Outcome switch
        {
            NativeEvidenceExtractionResult.Invalid invalid =>
                new DocumentFormatSelectionResult.Invalid(
                    selected.Format,
                    invalid.Reason,
                    invalid.IsConsumerSafeReason),

            NativeEvidenceExtractionResult.Unavailable unavailable =>
                new DocumentFormatSelectionResult.Unavailable(
                    selected.Format,
                    unavailable.Reason),

            NativeEvidenceExtractionResult.Success
                when physicalPageRange is not null &&
                     selected.Format is not IPhysicalPageRangeDocumentFormat =>
                new DocumentFormatSelectionResult.Invalid(
                    selected.Format,
                    $"Document format '{selected.Format.Format}' does not support physical-page ranges.",
                    true),

            NativeEvidenceExtractionResult.Success
                when contentUnitRange is not null &&
                     selected.Format is not IContentUnitRangeDocumentFormat =>
                new DocumentFormatSelectionResult.Invalid(
                    selected.Format,
                    $"Document format '{selected.Format.Format}' does not support content-unit ranges.",
                    true),

            NativeEvidenceExtractionResult.Success success =>
                new DocumentFormatSelectionResult.Success(
                    selected.Format,
                    success.Evidence),
            _ =>
                throw new InvalidOperationException(
                    "A recorded format-recognition claim must be Invalid, Unavailable or a supported Success shape.")
        };
    }

    #endregion
}
