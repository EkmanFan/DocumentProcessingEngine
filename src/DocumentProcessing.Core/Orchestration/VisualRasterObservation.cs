using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Policy-neutral low-level raster/geometry observations for one source visual.
///
/// This contract intentionally stops before heading association, native-text
/// containment and caption association. Those structural signals are required
/// before a complete <see cref="VisualEvidenceObservation"/> may be created.
/// </summary>
public sealed record VisualRasterObservation
{
    public VisualRasterObservation(
        int sourceVisualIndex,
        NormalizedRectangle declaredPageBounds,
        VisualRasterDecodeSource decodeSource,
        int? pixelWidth,
        int? pixelHeight,
        double? backgroundUniformity,
        VisualForegroundState foregroundState,
        double? foregroundPixelRatio,
        VisualPixelInteractionKind pixelInteraction,
        double nativeWordsTouchedRatio,
        int? significantComponentCount,
        NormalizedRectangle? effectiveVisualBounds)
    {
        if (sourceVisualIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex),
                sourceVisualIndex,
                "Source visual index must be non-negative.");
        }

        ValidateDefined(
            decodeSource,
            nameof(decodeSource));

        ValidateDefined(
            foregroundState,
            nameof(foregroundState));

        ValidateDefined(
            pixelInteraction,
            nameof(pixelInteraction));

        ValidateOptionalPositive(
            pixelWidth,
            nameof(pixelWidth));

        ValidateOptionalPositive(
            pixelHeight,
            nameof(pixelHeight));

        if ((pixelWidth is null) !=
            (pixelHeight is null))
        {
            throw new ArgumentException(
                "Decoded pixel width and height must either both be present or both be absent.");
        }

        ValidateRatio(
            backgroundUniformity,
            nameof(backgroundUniformity));

        ValidateRatio(
            foregroundPixelRatio,
            nameof(foregroundPixelRatio));

        ValidateRatio(
            nativeWordsTouchedRatio,
            nameof(nativeWordsTouchedRatio));

        if (significantComponentCount is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(significantComponentCount),
                significantComponentCount,
                "Significant component count must be non-negative when present.");
        }

        if (decodeSource ==
                VisualRasterDecodeSource.Unavailable &&
            (
                pixelWidth is not null ||
                pixelHeight is not null ||
                backgroundUniformity is not null
            ))
        {
            throw new ArgumentException(
                "Unavailable decode source cannot carry decoded pixel dimensions or background measurements.");
        }

        if (decodeSource !=
                VisualRasterDecodeSource.Unavailable &&
            (
                pixelWidth is null ||
                pixelHeight is null
            ))
        {
            throw new ArgumentException(
                "A decoded raster source requires pixel dimensions.");
        }

        if (foregroundState ==
            VisualForegroundState.Unavailable)
        {
            if (foregroundPixelRatio is not null ||
                pixelInteraction !=
                    VisualPixelInteractionKind.NotMeasured ||
                nativeWordsTouchedRatio !=
                    0 ||
                significantComponentCount is not null ||
                effectiveVisualBounds is not null)
            {
                throw new ArgumentException(
                    "Unavailable foreground state cannot carry derived foreground, interaction, component or effective-bound measurements.");
            }
        }

        if (foregroundState ==
            VisualForegroundState.BlankCanvas)
        {
            if (foregroundPixelRatio is not 0 ||
                pixelInteraction !=
                    VisualPixelInteractionKind.BlankCanvas ||
                nativeWordsTouchedRatio !=
                    0 ||
                significantComponentCount is not 0 ||
                effectiveVisualBounds is not null)
            {
                throw new ArgumentException(
                    "Blank canvas observations require zero foreground, blank interaction, zero components and no effective bounds.");
            }
        }

        if (foregroundState ==
            VisualForegroundState.Measured)
        {
            if (foregroundPixelRatio is null ||
                foregroundPixelRatio <=
                    0 ||
                pixelInteraction is
                    VisualPixelInteractionKind.NotMeasured or
                    VisualPixelInteractionKind.BlankCanvas ||
                significantComponentCount is null ||
                effectiveVisualBounds is null)
            {
                throw new ArgumentException(
                    "Measured foreground requires a positive ratio, measured interaction, component count and effective bounds.");
            }
        }

        SourceVisualIndex =
            sourceVisualIndex;

        DeclaredPageBounds =
            declaredPageBounds;

        DecodeSource =
            decodeSource;

        PixelWidth =
            pixelWidth;

        PixelHeight =
            pixelHeight;

        BackgroundUniformity =
            backgroundUniformity;

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

        EffectiveVisualBounds =
            effectiveVisualBounds;
    }

    public int SourceVisualIndex { get; }

    /// <summary>
    /// Declared source visual placement in canonical page-normalized
    /// coordinates. This remains source evidence and is not treated as the
    /// visible semantic extent.
    /// </summary>
    public NormalizedRectangle DeclaredPageBounds { get; }

    public VisualRasterDecodeSource DecodeSource { get; }

    public int? PixelWidth { get; }

    public int? PixelHeight { get; }

    public double? BackgroundUniformity { get; }

    public VisualForegroundState ForegroundState { get; }

    public double? ForegroundPixelRatio { get; }

    public VisualPixelInteractionKind PixelInteraction { get; }

    public double NativeWordsTouchedRatio { get; }

    public int? SignificantComponentCount { get; }

    /// <summary>
    /// Effective foreground extent mapped back into canonical page coordinates.
    /// Null when foreground analysis is unavailable or blank.
    /// </summary>
    public NormalizedRectangle? EffectiveVisualBounds { get; }

    /// <summary>
    /// Area of <see cref="EffectiveVisualBounds"/> in normalized page units.
    ///
    /// This remains low-level source evidence and is intentionally not clamped
    /// to [0,1]. Structural enrichment must fail closed if source geometry makes
    /// it unsuitable for the final bounded evidence contract.
    /// </summary>
    public double? EffectiveVisualAreaRatio =>
        EffectiveVisualBounds is { } bounds
            ? Math.Max(
                0,
                bounds.Right -
                bounds.Left) *
              Math.Max(
                0,
                bounds.Bottom -
                bounds.Top)
            : null;

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

    private static void ValidateOptionalPositive(
        int? value,
        string parameterName)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Pixel dimension must be positive when present.");
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
                "Enum value must be defined.");
        }
    }
}
