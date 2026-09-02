namespace DocumentProcessing.Manager.Partitioning;

/// <summary>
/// Describes the complete ordered coordinate space of one source document.
/// </summary>
public abstract record DocumentPartitionAxis
{
    private DocumentPartitionAxis()
    {
    }

    internal abstract int FirstCoordinate { get; }

    internal abstract int LastCoordinate { get; }

    internal abstract bool Contains(
        DocumentPartitionPosition position);

    internal abstract DocumentPartitionPosition CreatePosition(
        int coordinate);

    /// <summary>Describes a source with authoritative physical pages.</summary>
    public sealed record PhysicalPages
        : DocumentPartitionAxis
    {
        /// <summary>Gets the complete source physical-page count.</summary>
        public int PhysicalPageCount { get; }

        internal override int FirstCoordinate =>
            1;

        internal override int LastCoordinate =>
            PhysicalPageCount;

        /// <summary>Creates a physical-page coordinate axis.</summary>
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
            DocumentPartitionPosition position) =>
            position is DocumentPartitionPosition.PhysicalPage page &&
            page.PhysicalPageNumber <=
            PhysicalPageCount;

        internal override DocumentPartitionPosition CreatePosition(
            int coordinate) =>
            coordinate >=
                FirstCoordinate &&
            coordinate <=
                LastCoordinate
                ? new DocumentPartitionPosition.PhysicalPage(
                    coordinate)
                : throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    coordinate,
                    "Physical-page coordinate is outside the source axis.");
    }

    /// <summary>Describes a source with stable ordered content units.</summary>
    public sealed record ContentUnits
        : DocumentPartitionAxis
    {
        /// <summary>Gets stable unit identifiers in authoritative reading order.</summary>
        public IReadOnlyList<string> ContentUnitIds { get; }

        internal override int FirstCoordinate =>
            0;

        internal override int LastCoordinate =>
            ContentUnitIds.Count -
            1;

        /// <summary>Creates an ordered content-unit coordinate axis.</summary>
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
            DocumentPartitionPosition position) =>
            position is DocumentPartitionPosition.ContentUnit unit &&
            unit.ContentUnitIndex >=
            FirstCoordinate &&
            unit.ContentUnitIndex <=
            LastCoordinate &&
            string.Equals(
                ContentUnitIds[unit.ContentUnitIndex],
                unit.ContentUnitId,
                StringComparison.Ordinal);

        internal override DocumentPartitionPosition CreatePosition(
            int coordinate) =>
            coordinate >=
                FirstCoordinate &&
            coordinate <=
                LastCoordinate
                ? new DocumentPartitionPosition.ContentUnit(
                    coordinate,
                    ContentUnitIds[coordinate])
                : throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    coordinate,
                    "Content-unit coordinate is outside the source axis.");
    }
}
