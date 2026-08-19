namespace DocumentProcessing.Core.Locations;

/// <summary>
/// Identifies a position within a source document without assuming that the
/// document is physically paginated.
/// </summary>
/// <remarks>
/// This is the format-neutral root of source-location provenance.
///
/// Concrete document formats provide location shapes appropriate to their
/// native structure. A paginated source can use
/// <see cref="PagedDocumentSourceLocation"/>; EPUB and DOCX strategies can
/// later provide their own location types without inventing physical pages.
///
/// C1 introduces this abstraction only. Existing PDF result contracts remain
/// unchanged until the later portable-result migration.
/// </remarks>
public abstract record DocumentSourceLocation;
