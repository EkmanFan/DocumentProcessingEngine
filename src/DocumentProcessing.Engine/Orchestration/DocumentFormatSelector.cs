using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Selects one neutral document format by acquiring native evidence from each
/// registered format against the same prepared, replayable source.
/// </summary>
/// <remarks>
/// <see cref="NativeEvidenceExtractionResult.Success"/> and
/// <see cref="NativeEvidenceExtractionResult.Invalid"/> both mean that a format
/// recognized the source. More than one recognition claim therefore fails
/// closed as an ambiguous format selection rather than using registration order.
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            preparedSource);

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
                    await format
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
                    invalid.Reason),

            NativeEvidenceExtractionResult.Success success =>
                new DocumentFormatSelectionResult.Success(
                    selected.Format,
                    success.Evidence),

            _ =>
                throw new InvalidOperationException(
                    "A recorded format-recognition claim must be either Invalid or Success.")
        };
    }

    #endregion
}
