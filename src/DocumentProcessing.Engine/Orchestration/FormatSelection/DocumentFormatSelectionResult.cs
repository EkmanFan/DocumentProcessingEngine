using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Engine-owned functional result of selecting a document format from native
/// evidence acquisition outcomes.
/// </summary>
internal abstract record DocumentFormatSelectionResult
{
    #region ctor

    private protected DocumentFormatSelectionResult()
    {
    }

    #endregion

    #region Result Types

    internal sealed record NotRecognized
        : DocumentFormatSelectionResult
    {
    }

    internal sealed record Invalid
        : DocumentFormatSelectionResult
    {
        #region ctor

        public Invalid(
            IDocumentFormat documentFormat,
            string reason)
        {
            DocumentFormat =
                documentFormat ??
                throw new ArgumentNullException(
                    nameof(documentFormat));

            if (string.IsNullOrWhiteSpace(
                    reason))
            {
                throw new ArgumentException(
                    "Invalid-document reason cannot be empty.",
                    nameof(reason));
            }

            Reason =
                reason;
        }

        #endregion

        #region Properties

        public IDocumentFormat DocumentFormat { get; }

        public string Reason { get; }

        #endregion
    }

    internal sealed record Success
        : DocumentFormatSelectionResult
    {
        #region ctor

        public Success(
            IDocumentFormat documentFormat,
            NativeDocumentEvidence evidence)
        {
            DocumentFormat =
                documentFormat ??
                throw new ArgumentNullException(
                    nameof(documentFormat));

            Evidence =
                evidence ??
                throw new ArgumentNullException(
                    nameof(evidence));
        }

        #endregion

        #region Properties

        public IDocumentFormat DocumentFormat { get; }

        public NativeDocumentEvidence Evidence { get; }

        #endregion
    }

    internal sealed record Ambiguous
        : DocumentFormatSelectionResult
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
                    "Ambiguous format selection requires at least two recognition claims.",
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

    #endregion
}
