namespace DocumentProcessing.Core.Documents;

/// <summary>Describes a source that supports physical-page previews.</summary>
public sealed record PhysicalPagePreviewInspection(
    /// <summary>Gets the recognized document format.</summary>
    DocumentFormatId Format,
    /// <summary>Gets the number of physical pages.</summary>
    int PhysicalPageCount);
