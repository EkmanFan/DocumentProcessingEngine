namespace DocumentProcessing.Core.Segmentation;

/// <summary>
/// Optional caller-supplied structural segmentation evidence.
///
/// Heading hints are explicit editorial evidence, not automatic inference.
/// The engine remains source-agnostic: callers decide which hints, if any,
/// belong to a document-processing profile.
/// </summary>
public sealed class DocumentSegmentationOptions
{
    public static DocumentSegmentationOptions Default { get; } =
        new();

    public DocumentSegmentationOptions(
        IEnumerable<string>? headingHints = null)
    {
        if (headingHints is null)
        {
            HeadingHints =
                Array.Empty<string>();

            return;
        }

        var seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var normalized =
            new List<string>();

        foreach (var hint in headingHints)
        {
            if (string.IsNullOrWhiteSpace(
                    hint))
            {
                throw new ArgumentException(
                    "Heading hints cannot contain empty values.",
                    nameof(headingHints));
            }

            var trimmed =
                hint.Trim();

            if (!trimmed.Any(
                    char.IsLetterOrDigit))
            {
                throw new ArgumentException(
                    "Heading hints must contain at least one letter or digit.",
                    nameof(headingHints));
            }

            if (seen.Add(
                    trimmed))
            {
                normalized.Add(
                    trimmed);
            }
        }

        HeadingHints =
            normalized.AsReadOnly();
    }

    public IReadOnlyList<string> HeadingHints { get; }
}
