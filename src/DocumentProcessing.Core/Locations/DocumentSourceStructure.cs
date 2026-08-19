namespace DocumentProcessing.Core.Locations;

/// <summary>
/// Optional structural description of the source artifact.
/// </summary>
/// <remarks>
/// The portable result does not require one universal structural model for all
/// formats. A format processor can provide a specialized source structure when
/// that structure contains documentary facts that cannot be derived from
/// element locations alone.
///
/// EPUB and DOCX are not required to use a paged structure.
/// </remarks>
public abstract record DocumentSourceStructure;
