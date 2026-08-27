namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Stable identity of one submitted source document.
/// </summary>
public readonly record struct DocumentSubmissionId
{
    #region Properties

    /// <summary>
    /// Gets the underlying identifier.
    /// </summary>
    public Guid Value { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a document-submission identity.
    /// </summary>
    public DocumentSubmissionId(
        Guid value)
    {
        if (value ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Document-submission identifier cannot be empty.",
                nameof(value));
        }

        Value =
            value;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates a new document-submission identity.
    /// </summary>
    public static DocumentSubmissionId New() =>
        new(
            Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() =>
        Value.ToString();

    #endregion
}
