using System.Text;
using System.Text.RegularExpressions;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Engine.Normalization;

/// <summary>
/// Applies deterministic text-only normalization without changing source evidence.
/// Margin detection and structural segmentation are separate stages.
/// </summary>
public sealed partial class DocumentTextNormalizer
{
    public const string NormalizationProfileId =
        "unicode-nfc-whitespace-dehyphenation-v1";

    public DocumentTextNormalizationResult Normalize(
        DocumentExtractionResult extraction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        cancellationToken.ThrowIfCancellationRequested();

        var pages = new List<NormalizedDocumentPage>(
            extraction.Pages.Count);

        foreach (var page in extraction.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var blocks = page.Blocks
                .Select(block =>
                    new NormalizedDocumentTextBlock(
                        block,
                        NormalizeText(block.Text)))
                .ToArray();

            pages.Add(
                new NormalizedDocumentPage(
                    page,
                    blocks));
        }

        return new DocumentTextNormalizationResult(
            extraction,
            NormalizationProfileId,
            pages);
    }

    private static string NormalizeText(
        string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        var normalized = sourceText
            .Normalize(NormalizationForm.FormC)
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                '\r',
                '\n');

        normalized =
            DehyphenationRegex()
                .Replace(
                    normalized,
                    string.Empty);

        return WhitespaceRegex()
            .Replace(
                normalized,
                " ")
            .Trim();
    }

    [GeneratedRegex(
        @"(?<=\p{L})-[\t ]*\n[\t ]*(?=\p{Ll})",
        RegexOptions.CultureInvariant)]
    private static partial Regex DehyphenationRegex();

    [GeneratedRegex(
        @"\s+",
        RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
