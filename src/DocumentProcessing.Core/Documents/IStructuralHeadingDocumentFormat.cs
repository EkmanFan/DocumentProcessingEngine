namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Optional format capability for inspecting deterministic native heading
/// evidence without running the complete processing pipeline.
/// </summary>
public interface IStructuralHeadingDocumentFormat
    : IDocumentFormat
{
    /// <summary>
    /// Returns structural heading evidence when this format recognizes the
    /// source, or <see langword="null"/> when it does not recognize it.
    /// </summary>
    ValueTask<StructuralHeadingInspection?> TryInspectStructuralHeadingsAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes deterministic heading evidence for one recognized source.</summary>
public sealed record StructuralHeadingInspection
{
    /// <summary>Gets the recognized document format.</summary>
    public DocumentFormatId Format { get; }

    /// <summary>Gets the complete source coordinate axis.</summary>
    public DocumentStructureAxis Axis { get; }

    /// <summary>Gets structural headings in source reading order.</summary>
    public IReadOnlyList<StructuralHeadingEntry> Entries { get; }

    /// <summary>Creates one validated structural-heading inspection.</summary>
    public StructuralHeadingInspection(
        DocumentFormatId format,
        DocumentStructureAxis axis,
        IReadOnlyList<StructuralHeadingEntry> entries)
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
                "Structural heading evidence cannot contain null entries.",
                nameof(entries));
        }

        if (entryArray.Any(
                entry =>
                    !axis.Contains(
                        entry.Position)))
        {
            throw new ArgumentException(
                "Every structural heading must belong to the declared source axis.",
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
                "Structural-heading source orders must be unique.",
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

/// <summary>Describes one deterministic structural-heading observation.</summary>
public sealed record StructuralHeadingEntry
{
    /// <summary>Gets the source heading text.</summary>
    public string Title { get; }

    /// <summary>Gets the zero-based structural hierarchy level.</summary>
    public int HierarchyLevel { get; }

    /// <summary>Gets the heading order in source reading order.</summary>
    public int SourceOrder { get; }

    /// <summary>Gets the resolved source position.</summary>
    public DocumentStructurePosition Position { get; }

    /// <summary>Creates one validated structural-heading observation.</summary>
    public StructuralHeadingEntry(
        string title,
        int hierarchyLevel,
        int sourceOrder,
        DocumentStructurePosition position)
    {
        if (string.IsNullOrWhiteSpace(
                title))
        {
            throw new ArgumentException(
                "Structural-heading title cannot be empty.",
                nameof(title));
        }

        if (hierarchyLevel <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hierarchyLevel),
                hierarchyLevel,
                "Structural-heading hierarchy level cannot be negative.");
        }

        if (sourceOrder <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceOrder),
                sourceOrder,
                "Structural-heading source order cannot be negative.");
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
