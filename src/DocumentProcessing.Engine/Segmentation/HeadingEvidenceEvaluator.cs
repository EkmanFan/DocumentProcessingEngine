using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Engine.Segmentation;

/// <summary>
/// Deterministic automatic heading decision over neutral normalized native
/// block evidence.
///
/// Automatic heading inference requires typography. Textual shape alone is not
/// promoted to structural truth. Source- or caller-supplied editorial heading
/// hints are a separate concern and are intentionally not handled here.
/// </summary>
internal sealed class HeadingEvidenceEvaluator
{
    private readonly NativeHeadingEvidenceRules _rules;

    public HeadingEvidenceEvaluator(
        DocumentTextNormalizationResult document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        _rules =
            new NativeHeadingEvidenceRules(
                document.Pages
                    .SelectMany(
                        page =>
                            page.Blocks)
                    .Where(
                        block =>
                            !block.IsExcluded &&
                            !string.IsNullOrWhiteSpace(
                                block.Text))
                    .Select(
                        block =>
                            block.SourceBlock)
                    .ToArray());
    }

    public bool IsHeading(
        NormalizedDocumentTextBlock block)
    {
        ArgumentNullException.ThrowIfNull(
            block);

        return _rules.IsHeading(
            block.SourceBlock,
            block.Text);
    }
}
