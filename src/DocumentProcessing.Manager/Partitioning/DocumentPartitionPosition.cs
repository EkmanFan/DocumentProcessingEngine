namespace DocumentProcessing.Manager.Partitioning;

/// <summary>
/// Identifies one exact position on a declared document-partition axis.
/// </summary>
public abstract record DocumentPartitionPosition
{
    private DocumentPartitionPosition()
    {
    }

    internal abstract int Coordinate { get; }

    /// <summary>Identifies one one-based physical page.</summary>
    public sealed record PhysicalPage
        : DocumentPartitionPosition
    {
        /// <summary>Gets the one-based physical page number.</summary>
        public int PhysicalPageNumber { get; }

        internal override int Coordinate =>
            PhysicalPageNumber;

        /// <summary>Creates one physical-page position.</summary>
        public PhysicalPage(
            int physicalPageNumber)
        {
            if (physicalPageNumber <=
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(physicalPageNumber),
                    physicalPageNumber,
                    "Physical page number must be positive.");
            }

            PhysicalPageNumber =
                physicalPageNumber;
        }
    }

    /// <summary>Identifies one zero-based ordered source content unit.</summary>
    public sealed record ContentUnit
        : DocumentPartitionPosition
    {
        /// <summary>Gets the zero-based content-unit index.</summary>
        public int ContentUnitIndex { get; }

        /// <summary>Gets the stable format-supplied content-unit identifier.</summary>
        public string ContentUnitId { get; }

        internal override int Coordinate =>
            ContentUnitIndex;

        /// <summary>Creates one ordered content-unit position.</summary>
        public ContentUnit(
            int contentUnitIndex,
            string contentUnitId)
        {
            if (contentUnitIndex <
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contentUnitIndex),
                    contentUnitIndex,
                    "Content-unit index cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(
                    contentUnitId))
            {
                throw new ArgumentException(
                    "Content-unit identifier cannot be empty.",
                    nameof(contentUnitId));
            }

            ContentUnitIndex =
                contentUnitIndex;

            ContentUnitId =
                contentUnitId.Trim();
        }
    }
}
