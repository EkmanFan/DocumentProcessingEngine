namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Stable identity of one atomic queued processing unit.
/// </summary>
public readonly record struct ProcessingUnitId
{
    #region Properties

    /// <summary>
    /// Gets the underlying identifier.
    /// </summary>
    public Guid Value { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a processing-unit identity.
    /// </summary>
    public ProcessingUnitId(
        Guid value)
    {
        if (value ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-unit identifier cannot be empty.",
                nameof(value));
        }

        Value =
            value;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates a new processing-unit identity.
    /// </summary>
    public static ProcessingUnitId New() =>
        new(
            Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() =>
        Value.ToString();

    #endregion
}
