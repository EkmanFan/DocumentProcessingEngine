namespace DocumentProcessing.Core.Documents;

public sealed record DocumentTypeDetectionResult(
    DocumentFormatId? Format,
    string? DetectedMediaType,
    bool IsSupported)
{
    public static DocumentTypeDetectionResult Unknown { get; } =
        new(
            Format: null,
            DetectedMediaType: null,
            IsSupported: false);
}
