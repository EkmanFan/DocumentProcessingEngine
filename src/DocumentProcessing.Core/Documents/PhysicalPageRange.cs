namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Defines an inclusive range of original physical pages requested by a
/// document-processing consumer.
/// </summary>
public sealed record PhysicalPageRange
{
    #region Properties

    /// <summary>Gets the inclusive first physical page number.</summary>
    public int StartPhysicalPageNumber { get; }

    /// <summary>Gets the inclusive last physical page number.</summary>
    public int EndPhysicalPageNumber { get; }

    #endregion

    #region ctor

    /// <summary>Creates an inclusive original physical-page range.</summary>
    public PhysicalPageRange(int startPhysicalPageNumber, int endPhysicalPageNumber)
    {
        if (startPhysicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startPhysicalPageNumber), startPhysicalPageNumber,
                "Start physical page number must be positive.");
        }

        if (endPhysicalPageNumber < startPhysicalPageNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(endPhysicalPageNumber), endPhysicalPageNumber,
                "End physical page number cannot precede the start page.");
        }

        StartPhysicalPageNumber = startPhysicalPageNumber;
        EndPhysicalPageNumber = endPhysicalPageNumber;
    }

    #endregion
}
