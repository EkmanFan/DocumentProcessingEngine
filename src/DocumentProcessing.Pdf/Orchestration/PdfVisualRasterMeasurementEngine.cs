using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Pure deterministic measurement engine for an already-decoded RGBA source
/// visual.
///
/// Thresholds are promoted unchanged from the frozen raster-measurement diagnostic
/// algorithm. They describe low-level measurement, not visual semantic policy.
/// </summary>
internal sealed class PdfVisualRasterMeasurementEngine
{
    internal const double BackgroundDistance =
        18.0;

    internal const double BackgroundUniformityRequired =
        0.95;

    internal const int WordBoxExpansionPixels =
        2;

    internal const int SignificantComponentMinimumPixels =
        16;

    public VisualRasterObservation Measure(
        int sourceVisualIndex,
        NormalizedRectangle declaredPageBounds,
        VisualRasterDecodeSource decodeSource,
        int width,
        int height,
        byte[] rgba,
        IReadOnlyList<DocumentWord> nativeWords,
        CancellationToken cancellationToken = default)
    {
        if (decodeSource ==
            VisualRasterDecodeSource.Unavailable)
        {
            throw new ArgumentException(
                "Measurement requires a decoded raster source.",
                nameof(decodeSource));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height));
        }

        ArgumentNullException.ThrowIfNull(
            rgba);

        ArgumentNullException.ThrowIfNull(
            nativeWords);

        var expectedLength =
            checked(
                width *
                height *
                4);

        if (rgba.Length !=
            expectedLength)
        {
            throw new ArgumentException(
                $"RGBA byte length {rgba.Length} does not match " +
                $"{width} x {height} x 4 = {expectedLength}.",
                nameof(rgba));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var foreground =
            AnalyzeForeground(
                width,
                height,
                rgba,
                cancellationToken);

        if (foreground.State ==
            VisualForegroundState.Unavailable)
        {
            return new VisualRasterObservation(
                sourceVisualIndex,
                declaredPageBounds,
                decodeSource,
                width,
                height,
                foreground.BackgroundUniformity,
                VisualForegroundState.Unavailable,
                foregroundPixelRatio:
                    null,
                VisualPixelInteractionKind.NotMeasured,
                nativeWordsTouchedRatio:
                    0,
                significantComponentCount:
                    null,
                effectiveVisualBounds:
                    null);
        }

        if (foreground.State ==
            VisualForegroundState.BlankCanvas)
        {
            return new VisualRasterObservation(
                sourceVisualIndex,
                declaredPageBounds,
                decodeSource,
                width,
                height,
                foreground.BackgroundUniformity,
                VisualForegroundState.BlankCanvas,
                foregroundPixelRatio:
                    0,
                VisualPixelInteractionKind.BlankCanvas,
                nativeWordsTouchedRatio:
                    0,
                significantComponentCount:
                    0,
                effectiveVisualBounds:
                    null);
        }

        var mask =
            foreground.Mask ??
            throw new InvalidDataException(
                "Measured foreground did not retain its deterministic mask.");

        var pixelInteraction =
            AnalyzeWordPixelInteraction(
                width,
                height,
                mask,
                declaredPageBounds,
                nativeWords,
                cancellationToken);

        var components =
            AnalyzeComponents(
                width,
                height,
                mask,
                declaredPageBounds,
                cancellationToken);

        return new VisualRasterObservation(
            sourceVisualIndex,
            declaredPageBounds,
            decodeSource,
            width,
            height,
            foreground.BackgroundUniformity,
            VisualForegroundState.Measured,
            foreground.ForegroundPixelRatio,
            pixelInteraction.Kind,
            pixelInteraction.NativeWordsTouchedRatio,
            components.SignificantComponentCount,
            components.EffectiveVisualBounds);
    }

    private static ForegroundMeasurement AnalyzeForeground(
        int width,
        int height,
        byte[] rgba,
        CancellationToken cancellationToken)
    {
        var boundary =
            SampleBoundary(
                width,
                height,
                rgba);

        if (boundary.Count ==
            0)
        {
            return ForegroundMeasurement.Unavailable(
                backgroundUniformity:
                    null);
        }

        var background =
            new Pixel(
                Median(
                    boundary.Select(
                        pixel =>
                            pixel.R)),
                Median(
                    boundary.Select(
                        pixel =>
                            pixel.G)),
                Median(
                    boundary.Select(
                        pixel =>
                            pixel.B)),
                255);

        var uniformity =
            boundary.Count(
                pixel =>
                    pixel.A <=
                        16 ||
                    ColorDistance(
                        pixel,
                        background) <=
                    BackgroundDistance) /
            (double)boundary.Count;

        if (uniformity <
            BackgroundUniformityRequired)
        {
            return ForegroundMeasurement.Unavailable(
                uniformity);
        }

        var mask =
            new bool[
                checked(
                    width *
                    height)];

        long foregroundCount =
            0;

        for (var y = 0;
             y < height;
             y++)
        {
            if ((y &
                 63) ==
                0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            for (var x = 0;
                 x < width;
                 x++)
            {
                var pixel =
                    ReadPixel(
                        width,
                        rgba,
                        x,
                        y);

                if (pixel.A <=
                    16)
                {
                    continue;
                }

                if (ColorDistance(
                        pixel,
                        background) <=
                    BackgroundDistance)
                {
                    continue;
                }

                mask[
                    y *
                    width +
                    x] =
                    true;

                foregroundCount++;
            }
        }

        var totalPixels =
            (long)width *
            height;

        var ratio =
            totalPixels ==
                0
                ? 0
                : foregroundCount /
                  (double)totalPixels;

        return foregroundCount ==
                0
            ? ForegroundMeasurement.Blank(
                uniformity)
            : ForegroundMeasurement.Measured(
                uniformity,
                ratio,
                mask);
    }

    private static IReadOnlyList<Pixel> SampleBoundary(
        int width,
        int height,
        byte[] rgba)
    {
        var result =
            new List<Pixel>();

        if (width <=
                0 ||
            height <=
                0)
        {
            return result;
        }

        var stride =
            Math.Max(
                1,
                Math.Min(
                    width,
                    height) /
                128);

        for (var x = 0;
             x < width;
             x += stride)
        {
            result.Add(
                ReadPixel(
                    width,
                    rgba,
                    x,
                    0));

            if (height >
                1)
            {
                result.Add(
                    ReadPixel(
                        width,
                        rgba,
                        x,
                        height -
                        1));
            }
        }

        for (var y = stride;
             y < height -
                 1;
             y += stride)
        {
            result.Add(
                ReadPixel(
                    width,
                    rgba,
                    0,
                    y));

            if (width >
                1)
            {
                result.Add(
                    ReadPixel(
                        width,
                        rgba,
                        width -
                        1,
                    y));
            }
        }

        return result;
    }

    private static PixelInteractionMeasurement AnalyzeWordPixelInteraction(
        int width,
        int height,
        IReadOnlyList<bool> foregroundMask,
        NormalizedRectangle declaredImageBounds,
        IReadOnlyList<DocumentWord> words,
        CancellationToken cancellationToken)
    {
        if (words.Count ==
            0)
        {
            return new PixelInteractionMeasurement(
                VisualPixelInteractionKind.NoNativeWords,
                0);
        }

        var expandedCoverage =
            new bool[
                checked(
                    width *
                    height)];

        var wordsTouched =
            0;

        for (var wordIndex = 0;
             wordIndex <
             words.Count;
             wordIndex++)
        {
            if ((wordIndex &
                 255) ==
                0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var rectangle =
                MapWordToImagePixels(
                    words[wordIndex].Bounds,
                    declaredImageBounds,
                    width,
                    height,
                    WordBoxExpansionPixels);

            if (rectangle is null)
            {
                continue;
            }

            MarkCoverage(
                expandedCoverage,
                width,
                rectangle);

            if (ContainsForeground(
                    foregroundMask,
                    width,
                    rectangle))
            {
                wordsTouched++;
            }
        }

        long foregroundCount =
            0;

        long inside =
            0;

        for (var index = 0;
             index <
             foregroundMask.Count;
             index++)
        {
            if ((index &
                 262143) ==
                0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!foregroundMask[index])
            {
                continue;
            }

            foregroundCount++;

            if (expandedCoverage[index])
            {
                inside++;
            }
        }

        var insideRatio =
            foregroundCount ==
                0
                ? 0
                : inside /
                  (double)foregroundCount;

        var wordsTouchedRatio =
            wordsTouched /
            (double)words.Count;

        var kind =
            foregroundCount ==
                0
                ? VisualPixelInteractionKind.BlankCanvas
                : inside ==
                    0
                    ? VisualPixelInteractionKind.NoForegroundWordIntersection
                    : insideRatio <=
                        0.01 &&
                      wordsTouchedRatio <=
                        0.01
                        ? VisualPixelInteractionKind.LowForegroundWordInteraction
                        : VisualPixelInteractionKind.ForegroundWordInteraction;

        return new PixelInteractionMeasurement(
            kind,
            wordsTouchedRatio);
    }

    private static ComponentMeasurement AnalyzeComponents(
        int width,
        int height,
        IReadOnlyList<bool> foreground,
        NormalizedRectangle declaredPageBounds,
        CancellationToken cancellationToken)
    {
        var total =
            checked(
                width *
                height);

        var visited =
            new bool[
                total];

        var queue =
            new int[
                total];

        var components =
            new List<Component>();

        for (var start = 0;
             start <
             total;
             start++)
        {
            if ((start &
                 262143) ==
                0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!foreground[start] ||
                visited[start])
            {
                continue;
            }

            var head =
                0;

            var tail =
                0;

            queue[tail++] =
                start;

            visited[start] =
                true;

            var count =
                0;

            var minX =
                width;

            var minY =
                height;

            var maxX =
                -1;

            var maxY =
                -1;

            while (head <
                   tail)
            {
                if ((head &
                     262143) ==
                    0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var current =
                    queue[head++];

                var x =
                    current %
                    width;

                var y =
                    current /
                    width;

                count++;

                minX =
                    Math.Min(
                        minX,
                        x);

                minY =
                    Math.Min(
                        minY,
                        y);

                maxX =
                    Math.Max(
                        maxX,
                        x);

                maxY =
                    Math.Max(
                        maxY,
                        y);

                for (var dy = -1;
                     dy <= 1;
                     dy++)
                {
                    for (var dx = -1;
                         dx <= 1;
                         dx++)
                    {
                        if (dx ==
                                0 &&
                            dy ==
                                0)
                        {
                            continue;
                        }

                        var nx =
                            x +
                            dx;

                        var ny =
                            y +
                            dy;

                        if (nx <
                                0 ||
                            nx >=
                                width ||
                            ny <
                                0 ||
                            ny >=
                                height)
                        {
                            continue;
                        }

                        var next =
                            ny *
                            width +
                            nx;

                        if (!foreground[next] ||
                            visited[next])
                        {
                            continue;
                        }

                        visited[next] =
                            true;

                        queue[tail++] =
                            next;
                    }
                }
            }

            components.Add(
                new Component(
                    count,
                    new PixelRect(
                        minX,
                        minY,
                        maxX +
                            1,
                        maxY +
                            1)));
        }

        if (components.Count ==
            0)
        {
            throw new InvalidDataException(
                "Measured non-blank foreground produced no connected component.");
        }

        var significant =
            components
                .Where(
                    component =>
                        component.PixelCount >=
                        SignificantComponentMinimumPixels)
                .ToArray();

        var largest =
            components
                .OrderByDescending(
                    component =>
                        component.PixelCount)
                .First();

        var effectivePixelBounds =
            significant.Length >
                0
                ? Union(
                    significant.Select(
                        component =>
                            component.Bounds))
                : largest.Bounds;

        var effectivePageBounds =
            MapPixelBoundsToPage(
                declaredPageBounds,
                effectivePixelBounds,
                width,
                height);

        return new ComponentMeasurement(
            significant.Length,
            effectivePageBounds);
    }

    private static PixelRect? MapWordToImagePixels(
        NormalizedRectangle word,
        NormalizedRectangle image,
        int pixelWidth,
        int pixelHeight,
        int expansionPixels)
    {
        var imageWidth =
            image.Right -
            image.Left;

        var imageHeight =
            image.Bottom -
            image.Top;

        if (imageWidth <=
                0 ||
            imageHeight <=
                0)
        {
            return null;
        }

        var left =
            Math.Max(
                word.Left,
                image.Left);

        var top =
            Math.Max(
                word.Top,
                image.Top);

        var right =
            Math.Min(
                word.Right,
                image.Right);

        var bottom =
            Math.Min(
                word.Bottom,
                image.Bottom);

        if (right <=
                left ||
            bottom <=
                top)
        {
            return null;
        }

        var x0 =
            (int)Math.Floor(
                (left -
                 image.Left) /
                imageWidth *
                pixelWidth) -
            expansionPixels;

        var y0 =
            (int)Math.Floor(
                (top -
                 image.Top) /
                imageHeight *
                pixelHeight) -
            expansionPixels;

        var x1 =
            (int)Math.Ceiling(
                (right -
                 image.Left) /
                imageWidth *
                pixelWidth) +
            expansionPixels;

        var y1 =
            (int)Math.Ceiling(
                (bottom -
                 image.Top) /
                imageHeight *
                pixelHeight) +
            expansionPixels;

        x0 =
            Math.Clamp(
                x0,
                0,
                pixelWidth);

        y0 =
            Math.Clamp(
                y0,
                0,
                pixelHeight);

        x1 =
            Math.Clamp(
                x1,
                0,
                pixelWidth);

        y1 =
            Math.Clamp(
                y1,
                0,
                pixelHeight);

        return x1 <=
                   x0 ||
               y1 <=
                   y0
            ? null
            : new PixelRect(
                x0,
                y0,
                x1,
                y1);
    }

    private static void MarkCoverage(
        IList<bool> coverage,
        int width,
        PixelRect rectangle)
    {
        for (var y = rectangle.Top;
             y < rectangle.Bottom;
             y++)
        {
            var offset =
                y *
                width;

            for (var x = rectangle.Left;
                 x < rectangle.Right;
                 x++)
            {
                coverage[
                    offset +
                    x] =
                    true;
            }
        }
    }

    private static bool ContainsForeground(
        IReadOnlyList<bool> foreground,
        int width,
        PixelRect rectangle)
    {
        for (var y = rectangle.Top;
             y < rectangle.Bottom;
             y++)
        {
            var offset =
                y *
                width;

            for (var x = rectangle.Left;
                 x < rectangle.Right;
                 x++)
            {
                if (foreground[
                    offset +
                    x])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static PixelRect Union(
        IEnumerable<PixelRect> rectangles)
    {
        var items =
            rectangles.ToArray();

        if (items.Length ==
            0)
        {
            throw new InvalidDataException(
                "Cannot union an empty rectangle set.");
        }

        return new PixelRect(
            items.Min(
                item =>
                    item.Left),
            items.Min(
                item =>
                    item.Top),
            items.Max(
                item =>
                    item.Right),
            items.Max(
                item =>
                    item.Bottom));
    }

    private static NormalizedRectangle MapPixelBoundsToPage(
        NormalizedRectangle declared,
        PixelRect pixels,
        int imageWidth,
        int imageHeight)
    {
        var declaredWidth =
            declared.Right -
            declared.Left;

        var declaredHeight =
            declared.Bottom -
            declared.Top;

        return new NormalizedRectangle(
            declared.Left +
            declaredWidth *
            pixels.Left /
            imageWidth,
            declared.Top +
            declaredHeight *
            pixels.Top /
            imageHeight,
            declared.Left +
            declaredWidth *
            pixels.Right /
            imageWidth,
            declared.Top +
            declaredHeight *
            pixels.Bottom /
            imageHeight);
    }

    private static Pixel ReadPixel(
        int width,
        byte[] rgba,
        int x,
        int y)
    {
        var index =
            checked(
                (y *
                 width +
                 x) *
                4);

        return new Pixel(
            rgba[index],
            rgba[index +
                1],
            rgba[index +
                2],
            rgba[index +
                3]);
    }

    private static double ColorDistance(
        Pixel left,
        Pixel right)
    {
        var red =
            left.R -
            right.R;

        var green =
            left.G -
            right.G;

        var blue =
            left.B -
            right.B;

        return Math.Sqrt(
            red *
            red +
            green *
            green +
            blue *
            blue);
    }

    private static byte Median(
        IEnumerable<byte> values)
    {
        var ordered =
            values
                .Order()
                .ToArray();

        if (ordered.Length ==
            0)
        {
            throw new InvalidDataException(
                "Cannot compute median of an empty sequence.");
        }

        return ordered[
            ordered.Length /
            2];
    }

    private readonly record struct Pixel(
        byte R,
        byte G,
        byte B,
        byte A);

    private sealed record PixelRect(
        int Left,
        int Top,
        int Right,
        int Bottom);

    private sealed record Component(
        int PixelCount,
        PixelRect Bounds);

    private sealed record PixelInteractionMeasurement(
        VisualPixelInteractionKind Kind,
        double NativeWordsTouchedRatio);

    private sealed record ComponentMeasurement(
        int SignificantComponentCount,
        NormalizedRectangle EffectiveVisualBounds);

    private sealed record ForegroundMeasurement(
        VisualForegroundState State,
        double? BackgroundUniformity,
        double? ForegroundPixelRatio,
        IReadOnlyList<bool>? Mask)
    {
        public static ForegroundMeasurement Unavailable(
            double? backgroundUniformity) =>
            new(
                VisualForegroundState.Unavailable,
                backgroundUniformity,
                ForegroundPixelRatio:
                    null,
                Mask:
                    null);

        public static ForegroundMeasurement Blank(
            double backgroundUniformity) =>
            new(
                VisualForegroundState.BlankCanvas,
                backgroundUniformity,
                ForegroundPixelRatio:
                    0,
                Mask:
                    null);

        public static ForegroundMeasurement Measured(
            double backgroundUniformity,
            double foregroundPixelRatio,
            IReadOnlyList<bool> mask) =>
            new(
                VisualForegroundState.Measured,
                backgroundUniformity,
                foregroundPixelRatio,
                mask);
    }
}
