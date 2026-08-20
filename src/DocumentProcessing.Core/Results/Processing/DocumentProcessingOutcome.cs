namespace DocumentProcessing.Core.Results;

/// <summary>
/// Consumer-facing outcome of one document-processing request.
/// </summary>
/// <remarks>
/// Unsupported or ambiguous formats are functional failures represented by an
/// error message. Technical failures and cancellation remain exceptional.
/// </remarks>
public sealed record DocumentProcessingOutcome
{
    #region ctor

    private DocumentProcessingOutcome(
        DocumentProcessingResult? result,
        string? errorMessage)
    {
        Result =
            result;

        ErrorMessage =
            errorMessage;
    }

    #endregion

    #region Properties

    public bool IsSuccess =>
        Result is not null;

    public DocumentProcessingResult? Result { get; }

    public string? ErrorMessage { get; }

    #endregion

    #region Methods Factories

    public static DocumentProcessingOutcome Success(
        DocumentProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        return new DocumentProcessingOutcome(
            result,
            errorMessage:
                null);
    }

    public static DocumentProcessingOutcome Failure(
        string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(
                errorMessage))
        {
            throw new ArgumentException(
                "A failed document-processing outcome requires an error message.",
                nameof(errorMessage));
        }

        return new DocumentProcessingOutcome(
            result:
                null,
            errorMessage.Trim());
    }

    #endregion
}
