namespace DocumentProcessing.Core.Documents;

/// <summary>Describes the complete ordered coordinate space of a source structure.</summary>
public abstract record DocumentStructureAxis
{
    private DocumentStructureAxis()
    {
    }

    internal abstract bool Contains(
        DocumentStructurePosition position);

    /// <summary>Describes a source with authoritative physical pages.</summary>
    public sealed record PhysicalPages
        : DocumentStructureAxis
    {
        /// <summary>Gets the complete physical-page count.</summary>
        public int PhysicalPageCount { get; }

        /// <summary>Creates a physical-page source axis.</summary>
        public PhysicalPages(
            int physicalPageCount)
        {
            if (physicalPageCount <=
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(physicalPageCount),
                    physicalPageCount,
                    "Physical-page count must be positive.");
            }

            PhysicalPageCount =
                physicalPageCount;
        }

        internal override bool Contains(
            DocumentStructurePosition position) =>
            position is DocumentStructurePosition.PhysicalPage page &&
            page.PhysicalPageNumber <=
            PhysicalPageCount;
    }

    /// <summary>Describes a source with stable ordered content units.</summary>
    public sealed record ContentUnits
        : DocumentStructureAxis
    {
        /// <summary>Gets stable unit identifiers in authoritative reading order.</summary>
        public IReadOnlyList<string> ContentUnitIds { get; }

        /// <summary>Creates an ordered content-unit source axis.</summary>
        public ContentUnits(
            IReadOnlyList<string> contentUnitIds)
        {
            ArgumentNullException.ThrowIfNull(
                contentUnitIds);

            var normalizedIds =
                contentUnitIds
                    .Select(
                        id =>
                            string.IsNullOrWhiteSpace(
                                id)
                                ? null
                                : id.Trim())
                    .ToArray();

            if (normalizedIds.Length ==
                    0 ||
                normalizedIds.Any(
                    id =>
                        id is null))
            {
                throw new ArgumentException(
                    "Content-unit axes require at least one non-empty unit identifier.",
                    nameof(contentUnitIds));
            }

            if (normalizedIds
                    .Distinct(
                        StringComparer.Ordinal)
                    .Count() !=
                normalizedIds.Length)
            {
                throw new ArgumentException(
                    "Content-unit identifiers must be unique within one source axis.",
                    nameof(contentUnitIds));
            }

            ContentUnitIds =
                normalizedIds
                    .Select(
                        id =>
                            id!)
                    .ToArray();
        }

        internal override bool Contains(
            DocumentStructurePosition position) =>
            position is DocumentStructurePosition.ContentUnit unit &&
            unit.ContentUnitIndex >=
            0 &&
            unit.ContentUnitIndex <
            ContentUnitIds.Count &&
            string.Equals(
                ContentUnitIds[unit.ContentUnitIndex],
                unit.ContentUnitId,
                StringComparison.Ordinal);
    }
}
