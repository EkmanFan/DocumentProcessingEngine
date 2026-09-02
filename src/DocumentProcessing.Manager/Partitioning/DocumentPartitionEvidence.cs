namespace DocumentProcessing.Manager.Partitioning;

/// <summary>Identifies the neutral source of one partition boundary.</summary>
public enum DocumentPartitionEvidenceOrigin
{
    /// <summary>A native hierarchical navigation structure.</summary>
    NativeNavigation = 0,

    /// <summary>A reconciled structural heading.</summary>
    StructuralHeading = 1,

    /// <summary>An explicitly enabled mechanical fallback.</summary>
    MechanicalFallback = 2
}

/// <summary>Describes one potential partition boundary.</summary>
public sealed record DocumentPartitionBoundary
{
    /// <summary>Gets the exact source position.</summary>
    public DocumentPartitionPosition Position { get; }

    /// <summary>Gets the suggested segment title.</summary>
    public string Title { get; }

    /// <summary>Gets the zero-based hierarchy level.</summary>
    public int HierarchyLevel { get; }

    /// <summary>Gets the evidence order before coordinate reconciliation.</summary>
    public int SourceOrder { get; }

    /// <summary>Gets the neutral evidence origin.</summary>
    public DocumentPartitionEvidenceOrigin Origin { get; }

    /// <summary>Creates one neutral partition-boundary observation.</summary>
    public DocumentPartitionBoundary(
        DocumentPartitionPosition position,
        string title,
        int hierarchyLevel,
        int sourceOrder,
        DocumentPartitionEvidenceOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(
            position);

        if (string.IsNullOrWhiteSpace(
                title))
        {
            throw new ArgumentException(
                "Partition-boundary title cannot be empty.",
                nameof(title));
        }

        if (hierarchyLevel <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hierarchyLevel),
                hierarchyLevel,
                "Hierarchy level cannot be negative.");
        }

        if (sourceOrder <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceOrder),
                sourceOrder,
                "Boundary source order cannot be negative.");
        }

        if (!Enum.IsDefined(
                origin))
        {
            throw new ArgumentOutOfRangeException(
                nameof(origin),
                origin,
                "Unknown partition-evidence origin.");
        }

        Position =
            position;

        Title =
            title.Trim();

        HierarchyLevel =
            hierarchyLevel;

        SourceOrder =
            sourceOrder;

        Origin =
            origin;
    }
}

/// <summary>
/// Contains format-neutral structural observations for one complete source.
/// </summary>
public sealed record DocumentPartitionEvidence
{
    /// <summary>Gets the authoritative source coordinate axis.</summary>
    public DocumentPartitionAxis Axis { get; }

    /// <summary>Gets the observed partition boundaries.</summary>
    public IReadOnlyList<DocumentPartitionBoundary> Boundaries { get; }

    /// <summary>Creates validated neutral partition evidence.</summary>
    public DocumentPartitionEvidence(
        DocumentPartitionAxis axis,
        IReadOnlyList<DocumentPartitionBoundary> boundaries)
    {
        ArgumentNullException.ThrowIfNull(
            axis);

        ArgumentNullException.ThrowIfNull(
            boundaries);

        var boundaryArray =
            boundaries.ToArray();

        if (boundaryArray.Any(
                boundary =>
                    boundary is null))
        {
            throw new ArgumentException(
                "Partition evidence cannot contain null boundaries.",
                nameof(boundaries));
        }

        if (boundaryArray.Any(
                boundary =>
                    !axis.Contains(
                        boundary.Position)))
        {
            throw new ArgumentException(
                "Every partition boundary must belong to the declared source axis.",
                nameof(boundaries));
        }

        if (boundaryArray
                .Select(
                    boundary =>
                        boundary.SourceOrder)
                .Distinct()
                .Count() !=
            boundaryArray.Length)
        {
            throw new ArgumentException(
                "Partition-boundary source orders must be unique.",
                nameof(boundaries));
        }

        Axis =
            axis;

        Boundaries =
            boundaryArray;
    }
}
