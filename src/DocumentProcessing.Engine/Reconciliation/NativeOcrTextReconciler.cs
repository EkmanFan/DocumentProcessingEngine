using System.Text;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Engine.Reconciliation;

/// <summary>
/// Deterministic V1 native/OCR reconciliation policy.
///
/// Policy:
/// - healthy native text is preferred;
/// - missing native text may be recovered by OCR;
/// - suspicious native text requires OCR agreement before becoming selected;
/// - disagreement involving suspicious native evidence remains unresolved;
/// - no confidence threshold, fuzzy similarity score, or LLM arbitration is used.
/// </summary>
public static class NativeOcrTextReconciler
{
    public static TextReconciliationResult Reconcile(
        TextReconciliationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var nativeText =
            input.NativeBlock?.Text;

        var ocrText =
            ComposeOcrText(input);

        var hasOcrText =
            ocrText is not null;

        if (input.NativeStatus == NativeTextStatus.Missing)
        {
            return hasOcrText
                ? Result(
                    input,
                    TextReconciliationDecision.OcrOnly,
                    TextSelectionOrigin.Ocr,
                    ocrText,
                    ocrText,
                    textsEquivalent: null)
                : Result(
                    input,
                    TextReconciliationDecision.NoTextRecovered,
                    TextSelectionOrigin.None,
                    selectedText: null,
                    ocrText: null,
                    textsEquivalent: null);
        }

        if (!hasOcrText)
        {
            return input.NativeStatus == NativeTextStatus.Healthy
                ? Result(
                    input,
                    TextReconciliationDecision.NativeOnly,
                    TextSelectionOrigin.NativePdf,
                    nativeText,
                    ocrText: null,
                    textsEquivalent: null)
                : Result(
                    input,
                    TextReconciliationDecision.SuspiciousNativeUnverified,
                    TextSelectionOrigin.None,
                    selectedText: null,
                    ocrText: null,
                    textsEquivalent: null);
        }

        var textsEquivalent =
            AreConservativelyEquivalent(
                nativeText!,
                ocrText!);

        if (textsEquivalent)
        {
            return Result(
                input,
                TextReconciliationDecision.Agreement,
                TextSelectionOrigin.NativePdf,
                nativeText,
                ocrText,
                textsEquivalent: true);
        }

        if (input.NativeStatus == NativeTextStatus.Healthy)
        {
            return Result(
                input,
                TextReconciliationDecision.HealthyNativePreferred,
                TextSelectionOrigin.NativePdf,
                nativeText,
                ocrText,
                textsEquivalent: false);
        }

        return Result(
            input,
            TextReconciliationDecision.Conflict,
            TextSelectionOrigin.None,
            selectedText: null,
            ocrText,
            textsEquivalent: false);
    }

    /// <summary>
    /// V1 intentionally uses conservative equality, not fuzzy matching.
    /// Unicode compatibility normalization, discretionary soft-hyphen removal,
    /// and whitespace collapsing are allowed; case and punctuation differences
    /// remain meaningful disagreements until real evaluation justifies more.
    /// </summary>
    public static bool AreConservativelyEquivalent(
        string left,
        string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.Equals(
            NormalizeForComparison(left),
            NormalizeForComparison(right),
            StringComparison.Ordinal);
    }

    private static TextReconciliationResult Result(
        TextReconciliationInput input,
        TextReconciliationDecision decision,
        TextSelectionOrigin selectedOrigin,
        string? selectedText,
        string? ocrText,
        bool? textsEquivalent) =>
        new(
            input,
            decision,
            selectedOrigin,
            selectedText,
            ocrText,
            textsEquivalent);

    private static string? ComposeOcrText(
        TextReconciliationInput input)
    {
        if (input.OcrRegion is null ||
            input.OcrRegion.TextObservations.Count == 0)
        {
            return null;
        }

        var fragments =
            input.OcrRegion.TextObservations
                .OrderBy(
                    observation =>
                        observation.ObservationSequence)
                .Select(
                    observation =>
                        observation.Text.Trim())
                .Where(
                    text =>
                        text.Length > 0)
                .ToArray();

        return fragments.Length == 0
            ? null
            : string.Join(" ", fragments);
    }

    private static string NormalizeForComparison(
        string value)
    {
        var normalized =
            value.Normalize(NormalizationForm.FormKC);

        var builder =
            new StringBuilder(normalized.Length);

        var pendingWhitespace =
            false;

        foreach (var character in normalized)
        {
            if (character == '\u00AD')
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0)
                {
                    pendingWhitespace = true;
                }

                continue;
            }

            if (pendingWhitespace)
            {
                builder.Append(' ');
                pendingWhitespace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
