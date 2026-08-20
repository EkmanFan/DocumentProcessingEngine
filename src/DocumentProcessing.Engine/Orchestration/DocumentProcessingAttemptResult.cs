using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Internal functional result of the configured Engine processing pipeline.
/// </summary>
/// <remarks>
/// Format-recognition outcomes remain data, not technical exceptions. This type
/// stays internal until the consumer-facing Host projection is deliberately
/// chosen.
/// </remarks>
internal abstract record DocumentProcessingAttemptResult
{
    #region ctor

    private protected DocumentProcessingAttemptResult()
    {
    }

    #endregion

    #region Result Types

    internal sealed record NotRecognized
        : DocumentProcessingAttemptResult
    {
    }

    internal sealed record Invalid
        : DocumentProcessingAttemptResult
    {
        #region ctor

        public Invalid(
            DocumentFormatId format,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(
                    format.Value))
            {
                throw new ArgumentException(
                    "Recognized invalid document format cannot be empty.",
                    nameof(format));
            }

            if (string.IsNullOrWhiteSpace(
                    reason))
            {
                throw new ArgumentException(
                    "Invalid-document reason cannot be empty.",
                    nameof(reason));
            }

            Format =
                format;

            Reason =
                reason.Trim();
        }

        #endregion

        #region Properties

        public DocumentFormatId Format { get; }

        public string Reason { get; }

        #endregion
    }

    internal sealed record Ambiguous
        : DocumentProcessingAttemptResult
    {
        #region Variables and Constants

        private readonly IReadOnlyList<DocumentFormatId>
            _formats;

        #endregion

        #region ctor

        public Ambiguous(
            IReadOnlyList<DocumentFormatId> formats)
        {
            ArgumentNullException.ThrowIfNull(
                formats);

            if (formats.Count <
                2)
            {
                throw new ArgumentException(
                    "Ambiguous document processing requires at least two recognized formats.",
                    nameof(formats));
            }

            _formats =
                formats
                    .OrderBy(
                        format =>
                            format.Value,
                        StringComparer.Ordinal)
                    .ToArray();
        }

        #endregion

        #region Properties

        public IReadOnlyList<DocumentFormatId> Formats =>
            _formats;

        #endregion
    }

    internal sealed record Success
        : DocumentProcessingAttemptResult
    {
        #region ctor

        public Success(
            DocumentFormatId format,
            DocumentProcessingResult result)
        {
            if (string.IsNullOrWhiteSpace(
                    format.Value))
            {
                throw new ArgumentException(
                    "Processed document format cannot be empty.",
                    nameof(format));
            }

            Format =
                format;

            Result =
                result ??
                throw new ArgumentNullException(
                    nameof(result));
        }

        #endregion

        #region Properties

        public DocumentFormatId Format { get; }

        public DocumentProcessingResult Result { get; }

        #endregion
    }

    #endregion
}
