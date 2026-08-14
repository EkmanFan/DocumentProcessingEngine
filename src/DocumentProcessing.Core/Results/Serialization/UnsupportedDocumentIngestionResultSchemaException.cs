namespace DocumentProcessing.Core.Results.Serialization;

/// <summary>
/// Raised when a syntactically readable document-ingestion JSON payload declares
/// a schema that this reader does not support.
/// </summary>
public sealed class UnsupportedDocumentIngestionResultSchemaException
    : NotSupportedException
{
    public UnsupportedDocumentIngestionResultSchemaException(
        string? schemaVersion)
        : base(
            $"Unsupported document ingestion result schema '{schemaVersion ?? "<null>"}'. " +
            $"Supported schema: '{DocumentIngestionResult.SchemaVersionId}'.")
    {
        SchemaVersion =
            schemaVersion;
    }

    public string? SchemaVersion { get; }
}
