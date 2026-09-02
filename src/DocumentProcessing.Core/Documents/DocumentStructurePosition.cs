namespace DocumentProcessing.Core.Documents;

/// <summary>Identifies one resolved position on a source-structure axis.</summary>
public abstract record DocumentStructurePosition
{
    private DocumentStructurePosition()
    {
    }

    /// <summary>Identifies one one-based physical page.</summary>
    public sealed record PhysicalPage
        : DocumentStructurePosition
    {
        /// <summary>Gets the one-based physical page number.</summary>
        public int PhysicalPageNumber { get; }

        /// <summary>Creates one physical-page structure position.</summary>
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

    /// <summary>Identifies one zero-based ordered content unit.</summary>
    public sealed record ContentUnit
        : DocumentStructurePosition
    {
        /// <summary>Gets the zero-based content-unit index.</summary>
        public int ContentUnitIndex { get; }

        /// <summary>Gets the stable native content-unit identifier.</summary>
        public string ContentUnitId { get; }

        /// <summary>Creates one ordered content-unit structure position.</summary>
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
