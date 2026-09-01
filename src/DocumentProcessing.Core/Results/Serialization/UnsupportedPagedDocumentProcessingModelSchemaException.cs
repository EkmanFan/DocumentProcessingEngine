namespace DocumentProcessing.Core.Results.Serialization;

/// <summary>
/// Raised when a syntactically readable paged-model JSON payload declares
/// a schema that this reader does not support.
/// </summary>
public sealed class UnsupportedPagedDocumentProcessingModelSchemaException
    : NotSupportedException
{
    public UnsupportedPagedDocumentProcessingModelSchemaException(
        string? schemaVersion)
        : base(
            $"Unsupported paged document processing model schema '{schemaVersion ?? "<null>"}'. " +
            $"Supported schema: '{PagedDocumentProcessingModel.SchemaVersionId}'.")
    {
        SchemaVersion =
            schemaVersion;
    }

    public string? SchemaVersion { get; }
}
