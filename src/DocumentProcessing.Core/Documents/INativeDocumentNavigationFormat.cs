namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Optional format capability for inspecting publisher-supplied navigation
/// without running document processing.
/// </summary>
public interface INativeDocumentNavigationFormat
    : IDocumentFormat
{
    /// <summary>
    /// Returns native navigation when this format recognizes the source, or
    /// <see langword="null"/> when it does not recognize it.
    /// </summary>
    ValueTask<NativeDocumentNavigationInspection?> TryInspectNativeNavigationAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes publisher-supplied navigation for one recognized source.</summary>
public sealed record NativeDocumentNavigationInspection
{
    /// <summary>Gets the recognized document format.</summary>
    public DocumentFormatId Format { get; }

    /// <summary>Gets the complete source coordinate axis.</summary>
    public NativeDocumentNavigationAxis Axis { get; }

    /// <summary>Gets navigation entries in publisher order.</summary>
    public IReadOnlyList<NativeDocumentNavigationEntry> Entries { get; }

    /// <summary>Creates a validated native-navigation inspection.</summary>
    public NativeDocumentNavigationInspection(
        DocumentFormatId format,
        NativeDocumentNavigationAxis axis,
        IReadOnlyList<NativeDocumentNavigationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(
            axis);

        ArgumentNullException.ThrowIfNull(
            entries);

        var entryArray =
            entries.ToArray();

        if (entryArray.Any(
                entry =>
                    entry is null))
        {
            throw new ArgumentException(
                "Native navigation cannot contain null entries.",
                nameof(entries));
        }

        if (entryArray.Any(
                entry =>
                    !axis.Contains(
                        entry.Position)))
        {
            throw new ArgumentException(
                "Every native-navigation entry must belong to the declared source axis.",
                nameof(entries));
        }

        if (entryArray
                .Select(
                    entry =>
                        entry.SourceOrder)
                .Distinct()
                .Count() !=
            entryArray.Length)
        {
            throw new ArgumentException(
                "Native-navigation source orders must be unique.",
                nameof(entries));
        }

        Format =
            format;

        Axis =
            axis;

        Entries =
            entryArray;
    }
}

/// <summary>Describes one publisher-supplied navigation destination.</summary>
public sealed record NativeDocumentNavigationEntry
{
    /// <summary>Gets the publisher-supplied title.</summary>
    public string Title { get; }

    /// <summary>Gets the zero-based navigation hierarchy level.</summary>
    public int HierarchyLevel { get; }

    /// <summary>Gets the entry order in a depth-first traversal.</summary>
    public int SourceOrder { get; }

    /// <summary>Gets the resolved source position.</summary>
    public NativeDocumentNavigationPosition Position { get; }

    /// <summary>Creates one resolved native-navigation entry.</summary>
    public NativeDocumentNavigationEntry(
        string title,
        int hierarchyLevel,
        int sourceOrder,
        NativeDocumentNavigationPosition position)
    {
        if (string.IsNullOrWhiteSpace(
                title))
        {
            throw new ArgumentException(
                "Native-navigation title cannot be empty.",
                nameof(title));
        }

        if (hierarchyLevel <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hierarchyLevel),
                hierarchyLevel,
                "Native-navigation hierarchy level cannot be negative.");
        }

        if (sourceOrder <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceOrder),
                sourceOrder,
                "Native-navigation source order cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(
            position);

        Title =
            title.Trim();

        HierarchyLevel =
            hierarchyLevel;

        SourceOrder =
            sourceOrder;

        Position =
            position;
    }
}

/// <summary>Describes the complete ordered coordinate space of a native source.</summary>
public abstract record NativeDocumentNavigationAxis
{
    private NativeDocumentNavigationAxis()
    {
    }

    internal abstract bool Contains(
        NativeDocumentNavigationPosition position);

    /// <summary>Describes a source with authoritative physical pages.</summary>
    public sealed record PhysicalPages
        : NativeDocumentNavigationAxis
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
            NativeDocumentNavigationPosition position) =>
            position is NativeDocumentNavigationPosition.PhysicalPage page &&
            page.PhysicalPageNumber <=
            PhysicalPageCount;
    }

    /// <summary>Describes a source with stable ordered content units.</summary>
    public sealed record ContentUnits
        : NativeDocumentNavigationAxis
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
            NativeDocumentNavigationPosition position) =>
            position is NativeDocumentNavigationPosition.ContentUnit unit &&
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

/// <summary>Identifies one resolved position on a native source axis.</summary>
public abstract record NativeDocumentNavigationPosition
{
    private NativeDocumentNavigationPosition()
    {
    }

    /// <summary>Identifies one one-based physical page.</summary>
    public sealed record PhysicalPage
        : NativeDocumentNavigationPosition
    {
        /// <summary>Gets the one-based physical page number.</summary>
        public int PhysicalPageNumber { get; }

        /// <summary>Creates one physical-page navigation position.</summary>
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
        : NativeDocumentNavigationPosition
    {
        /// <summary>Gets the zero-based content-unit index.</summary>
        public int ContentUnitIndex { get; }

        /// <summary>Gets the stable native content-unit identifier.</summary>
        public string ContentUnitId { get; }

        /// <summary>Creates one ordered content-unit navigation position.</summary>
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
