namespace DocumentProcessing.Core.Documents;

public readonly record struct DocumentFormatId
{
    public DocumentFormatId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Document format identifier cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public static DocumentFormatId Pdf { get; } = new("pdf");

    public override string ToString() => Value;
}
