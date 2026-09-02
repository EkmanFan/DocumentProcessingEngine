namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Defines an inclusive range of stable ordered source-native content units.
/// </summary>
public sealed record ContentUnitRange
{
    #region Properties

    /// <summary>Gets the zero-based inclusive first content-unit index.</summary>
    public int StartContentUnitIndex { get; }

    /// <summary>Gets the stable identifier of the first content unit.</summary>
    public string StartContentUnitId { get; }

    /// <summary>Gets the zero-based inclusive last content-unit index.</summary>
    public int EndContentUnitIndex { get; }

    /// <summary>Gets the stable identifier of the last content unit.</summary>
    public string EndContentUnitId { get; }

    #endregion

    #region ctor

    /// <summary>Creates an inclusive source-native content-unit range.</summary>
    public ContentUnitRange(
        int startContentUnitIndex,
        string startContentUnitId,
        int endContentUnitIndex,
        string endContentUnitId)
    {
        if (startContentUnitIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startContentUnitIndex),
                startContentUnitIndex,
                "Start content-unit index cannot be negative.");
        }

        if (endContentUnitIndex <
            startContentUnitIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endContentUnitIndex),
                endContentUnitIndex,
                "End content-unit index cannot precede the start unit.");
        }

        StartContentUnitId =
            NormalizeRequired(
                startContentUnitId,
                nameof(startContentUnitId));

        EndContentUnitId =
            NormalizeRequired(
                endContentUnitId,
                nameof(endContentUnitId));

        if (startContentUnitIndex ==
                endContentUnitIndex &&
            !string.Equals(
                StartContentUnitId,
                EndContentUnitId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Equal content-unit indexes must identify the same unit.",
                nameof(endContentUnitId));
        }

        StartContentUnitIndex =
            startContentUnitIndex;

        EndContentUnitIndex =
            endContentUnitIndex;
    }

    #endregion

    #region Methods Validation

    private static string NormalizeRequired(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Content-unit identifier cannot be empty.",
                parameterName);
        }

        return value.Trim();
    }

    #endregion
}
