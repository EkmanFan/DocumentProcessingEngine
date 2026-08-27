namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Defines the immutable source scope of one atomic processing unit.
/// </summary>
public abstract record ProcessingUnitScope
{
    private ProcessingUnitScope()
    {
    }

    /// <summary>
    /// Represents processing of the complete submitted document.
    /// </summary>
    public sealed record WholeDocument
        : ProcessingUnitScope;

    /// <summary>
    /// Represents an approved inclusive range of original physical pages.
    /// </summary>
    public sealed record PageRange
        : ProcessingUnitScope
    {
        #region Properties

        /// <summary>
        /// Gets the inclusive first physical page number.
        /// </summary>
        public int StartPhysicalPageNumber { get; }

        /// <summary>
        /// Gets the inclusive last physical page number.
        /// </summary>
        public int EndPhysicalPageNumber { get; }

        /// <summary>
        /// Gets the user-visible segment title.
        /// </summary>
        public string Title { get; }

        #endregion

        #region ctor

        /// <summary>
        /// Creates one approved physical-page range.
        /// </summary>
        public PageRange(
            int startPhysicalPageNumber,
            int endPhysicalPageNumber,
            string title)
        {
            if (startPhysicalPageNumber <=
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startPhysicalPageNumber),
                    startPhysicalPageNumber,
                    "Start physical page number must be positive.");
            }

            if (endPhysicalPageNumber <
                startPhysicalPageNumber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endPhysicalPageNumber),
                    endPhysicalPageNumber,
                    "End physical page number cannot precede the start page.");
            }

            if (string.IsNullOrWhiteSpace(
                    title))
            {
                throw new ArgumentException(
                    "Processing-unit range title cannot be empty.",
                    nameof(title));
            }

            StartPhysicalPageNumber =
                startPhysicalPageNumber;

            EndPhysicalPageNumber =
                endPhysicalPageNumber;

            Title =
                title.Trim();
        }

        #endregion
    }
}
