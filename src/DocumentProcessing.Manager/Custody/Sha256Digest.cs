namespace DocumentProcessing.Manager.Custody;

/// <summary>
/// Canonical lowercase SHA-256 digest of immutable artifact bytes.
/// </summary>
public readonly record struct Sha256Digest
{
    #region Properties

    /// <summary>
    /// Gets the 64-character lowercase hexadecimal digest.
    /// </summary>
    public string Value { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a canonical SHA-256 digest.
    /// </summary>
    public Sha256Digest(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "SHA-256 digest cannot be empty.",
                nameof(value));
        }

        var normalized =
            value.Trim().ToLowerInvariant();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    character is not (>= '0' and <= '9') &&
                    character is not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "SHA-256 digest must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        Value =
            normalized;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public override string ToString() =>
        Value;

    #endregion
}
