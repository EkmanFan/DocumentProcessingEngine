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

        #endregion

        #region ctor

        public Invalid(
            string reason)
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

    #endregion
}
