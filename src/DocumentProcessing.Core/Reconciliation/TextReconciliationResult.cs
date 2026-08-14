namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Neutral reconciliation result that retains both source evidence objects and
/// makes selection, divergence, and unresolved states explicit.
/// </summary>
public sealed class TextReconciliationResult
{
    public TextReconciliationResult(
        TextReconciliationInput input,
        TextReconciliationDecision decision,
        TextSelectionOrigin selectedOrigin,
        string? selectedText,
        string? ocrText,
        bool? textsEquivalent)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        if (!Enum.IsDefined(selectedOrigin))
        {
            throw new ArgumentOutOfRangeException(nameof(selectedOrigin));
        }

        var normalizedSelectedText =
            string.IsNullOrWhiteSpace(selectedText)
                ? null
                : selectedText.Trim();

        var normalizedOcrText =
            string.IsNullOrWhiteSpace(ocrText)
                ? null
                : ocrText.Trim();

        if ((selectedOrigin == TextSelectionOrigin.None) !=
            (normalizedSelectedText is null))
        {
            throw new ArgumentException(
                "Selected origin None must correspond to no selected text, and vice versa.");
        }

        if (selectedOrigin == TextSelectionOrigin.NativePdf &&
            input.NativeBlock is null)
        {
            throw new ArgumentException(
                "NativePdf selection requires native evidence.",
                nameof(selectedOrigin));
        }

        if (selectedOrigin == TextSelectionOrigin.Ocr &&
            normalizedOcrText is null)
        {
            throw new ArgumentException(
                "OCR selection requires usable OCR text.",
                nameof(selectedOrigin));
        }

        if (textsEquivalent is not null &&
            (input.NativeBlock is null || normalizedOcrText is null))
        {
            throw new ArgumentException(
                "Text equivalence can only be reported when both native and OCR text exist.",
                nameof(textsEquivalent));
        }

        Input = input;
        Decision = decision;
        SelectedOrigin = selectedOrigin;
        SelectedText = normalizedSelectedText;
        OcrText = normalizedOcrText;
        TextsEquivalent = textsEquivalent;
    }

    public TextReconciliationInput Input { get; }

    public TextReconciliationDecision Decision { get; }

    public TextSelectionOrigin SelectedOrigin { get; }

    /// <summary>
    /// Authoritative V1 selection when reconciliation can resolve the candidate;
    /// null means the ambiguity is intentionally unresolved.
    /// </summary>
    public string? SelectedText { get; }

    /// <summary>
    /// OCR fragments composed in backend observation order for comparison and
    /// auditing. The original OcrRegionResult remains available through Input.
    /// </summary>
    public string? OcrText { get; }

    /// <summary>
    /// Conservative comparison result when both text sources are usable.
    /// Null means no two-source comparison was possible.
    /// </summary>
    public bool? TextsEquivalent { get; }

    public bool IsResolved => SelectedText is not null;

    public bool HasDivergence =>
        Decision is TextReconciliationDecision.HealthyNativePreferred or
            TextReconciliationDecision.Conflict;
}
