namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Functional outcome of attempting to recognize a source and acquire its
/// native document evidence.
/// </summary>
public abstract record NativeEvidenceExtractionResult
{
    #region ctor

    private protected NativeEvidenceExtractionResult()
    {
    }

    #endregion

    #region Result Types

    /// <summary>
    /// The source is not recognized as the concrete format.
    /// </summary>
    public sealed record NotRecognized : NativeEvidenceExtractionResult
    {
    }

    /// <summary>
    /// The source is recognized as the concrete format but cannot be accepted
    /// as a valid document of that format.
    /// </summary>
    public sealed record Invalid : NativeEvidenceExtractionResult
    {
        #region Properties

        public string Reason { get; }

        /// <summary>
        /// Gets whether the reason is already a complete consumer-facing
        /// message and must not receive technical format-selection context.
        /// </summary>
        public bool IsConsumerSafeReason { get; }

        #endregion

        #region ctor

        public Invalid(
            string reason,
            bool isConsumerSafeReason = false)
        {
            if (string.IsNullOrWhiteSpace(
                    reason))
            {
                throw new ArgumentException(
                    "Invalid-document reason cannot be empty.",
                    nameof(reason));
            }

            Reason =
                reason;

            IsConsumerSafeReason =
                isConsumerSafeReason;
        }

        #endregion
    }

    /// <summary>
    /// The source is recognized as the concrete format, but the format cannot
    /// currently acquire its native evidence because a required capability is
    /// unavailable.
    /// </summary>
    public sealed record Unavailable : NativeEvidenceExtractionResult
    {
        #region Properties

        /// <summary>
        /// Gets the stable consumer-safe reason for the unavailability.
        /// </summary>
        public string Reason { get; }

        #endregion

        #region ctor

        public Unavailable(
            string reason)
        {
            if (string.IsNullOrWhiteSpace(
                    reason))
            {
                throw new ArgumentException(
                    "Unavailable-format reason cannot be empty.",
                    nameof(reason));
            }

            Reason =
                reason.Trim();
        }

        #endregion
    }

    /// <summary>
    /// The source is recognized and its native evidence was acquired.
    /// </summary>
    public sealed record Success : NativeEvidenceExtractionResult
    {
        #region Properties

        public NativeDocumentEvidence Evidence { get; }

        #endregion

        #region ctor

        public Success(
            NativeDocumentEvidence evidence)
        {
            Evidence =
                evidence ??
                throw new ArgumentNullException(
                    nameof(evidence));
        }

        #endregion
    }

    /// <summary>
    /// The source is recognized and its structured, non-paged native evidence
    /// was acquired.
    /// </summary>
    public sealed record StructuredSuccess : NativeEvidenceExtractionResult
    {
        #region Properties

        public StructuredNativeDocumentEvidence Evidence { get; }

        #endregion

        #region ctor

        public StructuredSuccess(
            StructuredNativeDocumentEvidence evidence)
        {
            Evidence =
                evidence ??
                throw new ArgumentNullException(
                    nameof(evidence));
        }

        #endregion
    }

    #endregion
}
