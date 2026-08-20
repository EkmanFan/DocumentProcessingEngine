using System.Text;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Engine.Reconciliation;

/// <summary>
/// Deterministic V1 native/OCR reconciliation policy.
///
/// Policy:
/// - healthy native text is preferred;
/// - missing native text may be recovered by OCR;
/// - suspicious/unverified native text requires OCR agreement before becoming
///   selected;
/// - disagreement involving suspicious/unverified native evidence remains
///   unresolved;
/// - no confidence threshold, fuzzy similarity score, or LLM arbitration is
///   used.
/// </summary>
public static class NativeOcrTextReconciler
{
    #region Methods

    /// <summary>
    /// Raw block-level reconciliation retained for Phase 17A compatibility.
    /// </summary>
    public static TextReconciliationResult Reconcile(
        TextReconciliationInput input)
    {
        ArgumentNullException.ThrowIfNull(
            input);

        return ReconcileCore(
            input,
            input.NativeBlock?.Text,
            ComposeOcrText(
                input),
            comparableNativeExtent: null,
            comparableNativeEvidence: null,
            nativeTextPreparation: null,
            ocrTextPreparation: null);
    }

    /// <summary>
    /// Legacy single-source-block comparable reconciliation.
    /// </summary>
    public static TextReconciliationResult ReconcileComparable(
        TextReconciliationInput input,
        ComparableNativeTextExtent comparableNativeExtent)
    {
        ArgumentNullException.ThrowIfNull(
            input);

        ArgumentNullException.ThrowIfNull(
            comparableNativeExtent);

        if (input.NativeStatus ==
                NativeTextStatus.Missing ||
            input.NativeBlock is null)
        {
            throw new ArgumentException(
                "Comparable reconciliation requires native evidence.",
                nameof(input));
        }

        if (input.OcrRegion is null)
        {
            throw new ArgumentException(
                "Comparable reconciliation requires OCR evidence.",
                nameof(input));
        }

        if (!ReferenceEquals(
                comparableNativeExtent.SourceBlock,
                input.NativeBlock))
        {
            throw new ArgumentException(
                "Comparable native extent must originate from the input native block.",
                nameof(comparableNativeExtent));
        }

        if (!ReferenceEquals(
                comparableNativeExtent.SourceLayoutObservation,
                input.OcrRegion.SourceLayoutObservation))
        {
            throw new ArgumentException(
                "Comparable native extent must originate from the input OCR layout observation.",
                nameof(comparableNativeExtent));
        }

        var nativeTextPreparation =
            ReconciliationTextDehyphenator
                .DehyphenateNative(
                    comparableNativeExtent);

        var ocrTextPreparation =
            ReconciliationTextDehyphenator
                .DehyphenateOcr(
                    input.OcrRegion);

        return ReconcileCore(
            input,
            nativeTextPreparation.Text,
            string.IsNullOrWhiteSpace(
                ocrTextPreparation.Text)
                ? null
                : ocrTextPreparation.Text,
            comparableNativeExtent,
            comparableNativeEvidence: null,
            nativeTextPreparation,
            ocrTextPreparation);
    }

    /// <summary>
    /// Reconciles one target-centric native aggregate against OCR evidence for
    /// the exact same layout target.
    /// </summary>
    public static TextReconciliationResult ReconcileComparable(
        TextReconciliationInput input,
        ComparableNativeTextEvidence comparableNativeEvidence)
    {
        ArgumentNullException.ThrowIfNull(
            input);

        ArgumentNullException.ThrowIfNull(
            comparableNativeEvidence);

        if (input.NativeStatus ==
                NativeTextStatus.Missing ||
            !input.HasNativeEvidence)
        {
            throw new ArgumentException(
                "Target-centric comparable reconciliation requires native evidence.",
                nameof(input));
        }

        if (input.OcrRegion is null)
        {
            throw new ArgumentException(
                "Target-centric comparable reconciliation requires OCR evidence.",
                nameof(input));
        }

        if (!ReferenceEquals(
                input.ComparableNativeEvidence,
                comparableNativeEvidence))
        {
            throw new ArgumentException(
                "Comparable native evidence must be the exact evidence attached " +
                "to the reconciliation input.",
                nameof(comparableNativeEvidence));
        }

        if (!ReferenceEquals(
                comparableNativeEvidence.SourceLayoutObservation,
                input.OcrRegion.SourceLayoutObservation))
        {
            throw new ArgumentException(
                "Comparable native evidence must originate from the input OCR layout observation.",
                nameof(comparableNativeEvidence));
        }

        var nativeTextPreparation =
            ReconciliationTextDehyphenator
                .DehyphenateNative(
                    comparableNativeEvidence);

        var ocrTextPreparation =
            ReconciliationTextDehyphenator
                .DehyphenateOcr(
                    input.OcrRegion);

        var legacySingleExtent =
            comparableNativeEvidence.ExtentCount ==
                    1
                ? comparableNativeEvidence.Extents[0]
                : null;

        return ReconcileCore(
            input,
            nativeTextPreparation.Text,
            string.IsNullOrWhiteSpace(
                ocrTextPreparation.Text)
                ? null
                : ocrTextPreparation.Text,
            legacySingleExtent,
            comparableNativeEvidence,
            nativeTextPreparation,
            ocrTextPreparation);
    }

    private static TextReconciliationResult ReconcileCore(
        TextReconciliationInput input,
        string? nativeText,
        string? ocrText,
        ComparableNativeTextExtent? comparableNativeExtent,
        ComparableNativeTextEvidence? comparableNativeEvidence,
        TextDehyphenationResult? nativeTextPreparation,
        TextDehyphenationResult? ocrTextPreparation)
    {
        var hasOcrText =
            !string.IsNullOrWhiteSpace(
                ocrText);

        if (input.NativeStatus ==
            NativeTextStatus.Missing)
        {
            return hasOcrText
                ? Result(
                    input,
                    TextReconciliationDecision.OcrOnly,
                    TextSelectionOrigin.Ocr,
                    ocrText,
                    ocrText,
                    textsEquivalent: null,
                    comparableNativeExtent,
                    comparableNativeEvidence,
                    nativeTextPreparation,
                    ocrTextPreparation)
                : Result(
                    input,
                    TextReconciliationDecision.NoTextRecovered,
                    TextSelectionOrigin.None,
                    selectedText: null,
                    ocrText: null,
                    textsEquivalent: null,
                    comparableNativeExtent,
                    comparableNativeEvidence,
                    nativeTextPreparation,
                    ocrTextPreparation);
        }

        if (!hasOcrText)
        {
            return input.NativeStatus ==
                   NativeTextStatus.Healthy
                ? Result(
                    input,
                    TextReconciliationDecision.NativeOnly,
                    TextSelectionOrigin.NativePdf,
                    nativeText,
                    ocrText: null,
                    textsEquivalent: null,
                    comparableNativeExtent,
                    comparableNativeEvidence,
                    nativeTextPreparation,
                    ocrTextPreparation)
                : Result(
                    input,
                    TextReconciliationDecision.SuspiciousNativeUnverified,
                    TextSelectionOrigin.None,
                    selectedText: null,
                    ocrText: null,
                    textsEquivalent: null,
                    comparableNativeExtent,
                    comparableNativeEvidence,
                    nativeTextPreparation,
                    ocrTextPreparation);
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
                textsEquivalent: true,
                comparableNativeExtent,
                comparableNativeEvidence,
                nativeTextPreparation,
                ocrTextPreparation);
        }

        if (input.NativeStatus ==
            NativeTextStatus.Healthy)
        {
            return Result(
                input,
                TextReconciliationDecision.HealthyNativePreferred,
                TextSelectionOrigin.NativePdf,
                nativeText,
                ocrText,
                textsEquivalent: false,
                comparableNativeExtent,
                comparableNativeEvidence,
                nativeTextPreparation,
                ocrTextPreparation);
        }

        return Result(
            input,
            TextReconciliationDecision.Conflict,
            TextSelectionOrigin.None,
            selectedText: null,
            ocrText,
            textsEquivalent: false,
            comparableNativeExtent,
            comparableNativeEvidence,
            nativeTextPreparation,
            ocrTextPreparation);
    }

    public static bool AreConservativelyEquivalent(
        string left,
        string right)
    {
        ArgumentNullException.ThrowIfNull(
            left);

        ArgumentNullException.ThrowIfNull(
            right);

        return string.Equals(
            NormalizeForComparison(
                left),
            NormalizeForComparison(
                right),
            StringComparison.Ordinal);
    }

    private static TextReconciliationResult Result(
        TextReconciliationInput input,
        TextReconciliationDecision decision,
        TextSelectionOrigin selectedOrigin,
        string? selectedText,
        string? ocrText,
        bool? textsEquivalent,
        ComparableNativeTextExtent? comparableNativeExtent = null,
        ComparableNativeTextEvidence? comparableNativeEvidence = null,
        TextDehyphenationResult? nativeTextPreparation = null,
        TextDehyphenationResult? ocrTextPreparation = null) =>
        new(
            input,
            decision,
            selectedOrigin,
            selectedText,
            ocrText,
            textsEquivalent,
            comparableNativeExtent,
            nativeTextPreparation,
            ocrTextPreparation,
            comparableNativeEvidence);

    private static string? ComposeOcrText(
        TextReconciliationInput input)
    {
        if (input.OcrRegion is null ||
            input.OcrRegion.TextObservations.Count ==
            0)
        {
            return null;
        }

        var fragments =
            input.OcrRegion
                .TextObservations
                .OrderBy(
                    observation =>
                        observation.ObservationSequence)
                .Select(
                    observation =>
                        observation.Text.Trim())
                .Where(
                    text =>
                        text.Length >
                        0)
                .ToArray();

        return fragments.Length ==
               0
            ? null
            : string.Join(
                " ",
                fragments);
    }

    private static string NormalizeForComparison(
        string value)
    {
        var normalized =
            value.Normalize(
                NormalizationForm.FormKC);

        var builder =
            new StringBuilder(
                normalized.Length);

        var pendingWhitespace =
            false;

        foreach (var character in normalized)
        {
            if (character ==
                '\u00AD')
            {
                continue;
            }

            if (char.IsWhiteSpace(
                    character))
            {
                if (builder.Length >
                    0)
                {
                    pendingWhitespace =
                        true;
                }

                continue;
            }

            if (pendingWhitespace)
            {
                builder.Append(
                    ' ');

                pendingWhitespace =
                    false;
            }

            builder.Append(
                character);
        }

        return builder.ToString();
    }

    #endregion
}
