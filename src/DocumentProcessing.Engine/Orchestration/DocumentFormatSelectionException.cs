namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Functional failure to select exactly one usable document format for a
/// processing request.
/// </summary>
/// <remarks>
/// Recognition failure, recognized-but-invalid input, and ambiguous recognition
/// are document-processing outcomes. Technical failures and cancellation remain
/// exceptional and are not wrapped by this type.
/// </remarks>
public sealed class DocumentFormatSelectionException
    : Exception
{
    #region ctor

    public DocumentFormatSelectionException(
        string message)
        : base(
            NormalizeMessage(
                message))
    {
    }

    #endregion

    #region Methods Validation

    private static string NormalizeMessage(
        string message)
    {
        if (string.IsNullOrWhiteSpace(
                message))
        {
            throw new ArgumentException(
                "A document-format selection failure requires a message.",
                nameof(message));
        }

        return message.Trim();
    }

    #endregion
}
