namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Policy-neutral deterministic observations for one embedded visual
/// occurrence.
///
/// This contract contains measured/derived signals only. It contains no
/// <see cref="VisualEvidenceKind"/>, <see cref="VisualDisposition"/> or
/// <see cref="PageProcessingRoute"/>.
/// </summary>
public sealed record VisualEvidenceObservation
{
    public VisualEvidenceObservation(
        int sourceVisualIndex,
        VisualForegroundState foregroundState,
        double? foregroundPixelRatio,
        VisualPixelInteractionKind pixelInteraction,
        double nativeWordsTouchedRatio,
        int? significantComponentCount,
        double? effectiveVisualAreaRatio,
        HeadingAssociationEvidenceKind headingAssociation,
        NativeTextContainmentEvidenceKind textContainment,
        CaptionAssociationEvidenceKind captionAssociation)
    {
        if (sourceVisualIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex),
                sourceVisualIndex,
                "Source visual index must be non-negative.");
        }

        ValidateDefined(
            foregroundState,
            nameof(foregroundState));

        ValidateDefined(
            pixelInteraction,
            nameof(pixelInteraction));

        ValidateDefined(
            headingAssociation,
            nameof(headingAssociation));

        ValidateDefined(
            textContainment,
            nameof(textContainment));

        ValidateDefined(
            captionAssociation,
            nameof(captionAssociation));

        ValidateRatio(
            foregroundPixelRatio,
            nameof(foregroundPixelRatio));

        ValidateRatio(
            nativeWordsTouchedRatio,
            nameof(nativeWordsTouchedRatio));

        ValidateRatio(
            effectiveVisualAreaRatio,
            nameof(effectiveVisualAreaRatio));

        if (significantComponentCount is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(significantComponentCount),
                significantComponentCount,
                "Significant component count must be non-negative when present.");
        }

        if (foregroundState ==
                VisualForegroundState.Unavailable &&
            foregroundPixelRatio is not null)
        {
            throw new ArgumentException(
                "Unavailable foreground state cannot carry a foreground ratio.",
                nameof(foregroundPixelRatio));
        }

        if (foregroundState ==
                VisualForegroundState.BlankCanvas &&
            foregroundPixelRatio is not 0)
        {
            throw new ArgumentException(
                "Blank canvas foreground ratio must be exactly zero.",
                nameof(foregroundPixelRatio));
        }

        if (foregroundState ==
                VisualForegroundState.Measured &&
            foregroundPixelRatio is null)
        {
            throw new ArgumentException(
                "Measured foreground state requires a foreground ratio.",
                nameof(foregroundPixelRatio));
        }

        SourceVisualIndex =
            sourceVisualIndex;

        ForegroundState =
            foregroundState;

        ForegroundPixelRatio =
            foregroundPixelRatio;

        PixelInteraction =
            pixelInteraction;

        NativeWordsTouchedRatio =
            nativeWordsTouchedRatio;

        SignificantComponentCount =
            significantComponentCount;

        EffectiveVisualAreaRatio =
            effectiveVisualAreaRatio;

        HeadingAssociation =
            headingAssociation;

        TextContainment =
            textContainment;

        CaptionAssociation =
            captionAssociation;
    }

    public int SourceVisualIndex { get; }

    public VisualForegroundState ForegroundState { get; }

    public double? ForegroundPixelRatio { get; }

    public VisualPixelInteractionKind PixelInteraction { get; }

    public double NativeWordsTouchedRatio { get; }

    public int? SignificantComponentCount { get; }

    public double? EffectiveVisualAreaRatio { get; }

    public HeadingAssociationEvidenceKind HeadingAssociation { get; }

    public NativeTextContainmentEvidenceKind TextContainment { get; }

    public CaptionAssociationEvidenceKind CaptionAssociation { get; }

    private static void ValidateRatio(
        double? value,
        string parameterName)
    {
        if (value is null)
        {
            return;
        }

        if (!double.IsFinite(
                value.Value) ||
            value.Value < 0 ||
            value.Value > 1)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Ratio must be finite and between zero and one.");
        }
    }

    private static void ValidateDefined<TEnum>(
        TEnum value,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(
                value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Evidence enum value must be defined.");
        }
    }
}
