using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Segmentation;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Segmentation;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.EvaluationCli;

/// <summary>
/// Evaluation-only explanation of the production heading boundaries.
///
/// The diagnostic intentionally mirrors HeadingEvidenceEvaluator's v2 decision
/// rules locally, then proves parity against the actual production segmenter.
/// If the mirrored classifier and production output disagree, the report runner
/// fails rather than silently presenting stale explanations.
/// </summary>
internal static class HeadingBoundaryAnalysisCli
{
    private const string ReportSchemaVersion =
        "document-processing-heading-boundary-analysis-v1";

    private const int MaximumHeadingCharacters = 180;
    private const int MaximumHeadingWords = 24;
    private const int MinimumHeadingLetterCount = 3;

    private const double MinimumHeadingFontRatio = 1.18;
    private const double SectionFontRatio = 1.30;
    private const double MinimumExplicitFontRatio = 0.95;
    private const double MinimumUppercaseFontRatio = 1.10;

    private const int SmallSegmentCharacterThreshold = 120;
    private const int LargeSegmentCharacterThreshold = 4000;
    private const int VeryLargeCrossPageCharacterThreshold = 12000;

    private const int ConsoleSampleLimit = 12;
    private const int ReportSampleLimit = 40;
    private const int ContextCharacterLimit = 240;

    private static readonly Regex ExplicitStructuralHeadingRegex =
        new(
            @"^(?:(?:CHAPTER|PART|SECTION|BOOK)\b|(?:\d+\.\d+(?:\.\d+)*|\d+[.)]|[IVXLCDM]+[.)])\s+\S+)",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly Regex NumberedStructuralHeadingRegex =
        new(
            @"^(?:\d+\.\d+(?:\.\d+)*|\d+[.)]|[IVXLCDM]+[.)])\s+\S+",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex =
        new(
            @"\s+",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    public static async Task<int> RunAsync(
        string[] args)
    {
        var options =
            AnalysisOptions.Parse(args);

        var report =
            await AnalyzeAsync(options);

        await WriteReportAsync(
            options.ReportPath,
            report);

        WriteSummary(
            report,
            options.ReportPath);

        return 0;
    }

    private static async Task<HeadingBoundaryAnalysisReport>
        AnalyzeAsync(
            AnalysisOptions options)
    {
        var sourcePath =
            Path.GetFullPath(
                options.SourcePath);

        var fileInfo =
            new FileInfo(sourcePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "PDF source was not found.",
                sourcePath);
        }

        var sourceSha256 =
            await ComputeSha256Async(
                sourcePath);

        await using var sourceStream =
            File.OpenRead(sourcePath);

        var source =
            new DocumentSource(
                sourceStream,
                fileInfo.Name,
                "application/pdf");

        var extracted =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    source,
                    DocumentFormatId.Pdf);

        var selectedPages =
            SelectPages(
                extracted.Pages,
                options.FirstPage,
                options.LastPage);

        var selectedExtraction =
            new DocumentExtractionResult(
                extracted.Format,
                selectedPages);

        var normalized =
            new DocumentTextNormalizer()
                .Normalize(
                    selectedExtraction);

        var segmented =
            new HeuristicDocumentSegmenter()
                .Segment(
                    normalized);

        var bodyFontSize =
            GetWeightedMedianFontSize(
                normalized.Pages
                    .SelectMany(page =>
                        page.Blocks)
                    .Where(block =>
                        !block.IsExcluded &&
                        !string.IsNullOrWhiteSpace(
                            block.Text))
                    .ToArray());

        var contexts =
            BuildBlockContexts(
                normalized.Pages);

        var contextByBlock =
            new Dictionary<
                NormalizedDocumentTextBlock,
                BlockContext>(
                ReferenceEqualityComparer.Instance);

        foreach (var context in contexts)
        {
            contextByBlock.Add(
                context.Block,
                context);
        }

        var headingSegments =
            segmented.Segments
                .Where(segment =>
                    segment.HeadingText is not null)
                .ToArray();

        var actualHeadingKeys =
            headingSegments
                .Select(segment =>
                {
                    var headingBlock =
                        segment.SourceBlocks[0];

                    var context =
                        contextByBlock[headingBlock];

                    return context.Key;
                })
                .ToHashSet();

        var diagnosticDecisions =
            contexts
                .Select(context =>
                    new ContextDecision(
                        context,
                        EvaluateHeading(
                            context.Block,
                            bodyFontSize)))
                .ToArray();

        var diagnosticHeadingKeys =
            diagnosticDecisions
                .Where(item =>
                    item.Decision.IsHeading)
                .Select(item =>
                    item.Context.Key)
                .ToHashSet();

        var productionOnly =
            actualHeadingKeys
                .Except(
                    diagnosticHeadingKeys)
                .OrderBy(key =>
                    key.PhysicalPageNumber)
                .ThenBy(key =>
                    key.SourceSequence)
                .ToArray();

        var diagnosticOnly =
            diagnosticHeadingKeys
                .Except(
                    actualHeadingKeys)
                .OrderBy(key =>
                    key.PhysicalPageNumber)
                .ThenBy(key =>
                    key.SourceSequence)
                .ToArray();

        var headingSegmentByBlock =
            new Dictionary<
                NormalizedDocumentTextBlock,
                DocumentSegment>(
                ReferenceEqualityComparer.Instance);

        foreach (var segment in headingSegments)
        {
            headingSegmentByBlock.Add(
                segment.SourceBlocks[0],
                segment);
        }

        var repeatedCounts =
            headingSegments
                .GroupBy(
                    segment =>
                        NormalizeHeadingKey(
                            segment.HeadingText!),
                    StringComparer.Ordinal)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.Count(),
                    StringComparer.Ordinal);

        var boundaries =
            diagnosticDecisions
                .Where(item =>
                    item.Decision.IsHeading)
                .Select(item =>
                {
                    var context =
                        item.Context;

                    var segment =
                        headingSegmentByBlock[
                            context.Block];

                    var headingKey =
                        NormalizeHeadingKey(
                            segment.HeadingText!);

                    var repeatedCount =
                        repeatedCounts[headingKey];

                    return BuildBoundaryDiagnostic(
                        contexts,
                        context,
                        item.Decision,
                        segment,
                        repeatedCount);
                })
                .OrderBy(boundary =>
                    boundary.SegmentOrdinal)
                .ToArray();

        var originCounts =
            boundaries
                .GroupBy(
                    boundary =>
                        boundary.DecisionOrigin,
                    StringComparer.Ordinal)
                .Select(group =>
                    new HeadingOriginCount(
                        group.Key,
                        group.Count()))
                .OrderByDescending(item =>
                    item.Count)
                .ThenBy(item =>
                    item.Origin,
                    StringComparer.Ordinal)
                .ToArray();

        var repeatedGroups =
            boundaries
                .Where(boundary =>
                    boundary.RepeatedHeadingCount > 1)
                .GroupBy(
                    boundary =>
                        NormalizeHeadingKey(
                            boundary.HeadingText),
                    StringComparer.Ordinal)
                .Select(group =>
                    new RepeatedHeadingGroup(
                        group.First()
                            .HeadingText,
                        group.Count(),
                        group.Select(boundary =>
                                boundary.PhysicalPageNumber)
                            .Distinct()
                            .OrderBy(pageNumber =>
                                pageNumber)
                            .ToArray(),
                        group.Select(boundary =>
                                boundary.DecisionOrigin)
                            .Distinct(
                                StringComparer.Ordinal)
                            .OrderBy(origin =>
                                origin,
                                StringComparer.Ordinal)
                            .ToArray()))
                .OrderByDescending(group =>
                    group.Count)
                .ThenBy(group =>
                    group.HeadingText,
                    StringComparer.Ordinal)
                .Take(ReportSampleLimit)
                .ToArray();

        var numbered =
            boundaries
                .Where(boundary =>
                    boundary.IsNumberedStructural)
                .OrderBy(boundary =>
                    boundary.PhysicalPageNumber)
                .Take(ReportSampleLimit)
                .ToArray();

        var weakTypography =
            boundaries
                .Where(boundary =>
                    boundary.FontRatio is not null &&
                    boundary.FontRatio <
                    MinimumHeadingFontRatio)
                .OrderBy(boundary =>
                    boundary.FontRatio)
                .ThenBy(boundary =>
                    boundary.PhysicalPageNumber)
                .Take(ReportSampleLimit)
                .ToArray();

        var smallSegments =
            boundaries
                .Where(boundary =>
                    boundary.SegmentCharacterCount <=
                    SmallSegmentCharacterThreshold)
                .OrderBy(boundary =>
                    boundary.SegmentCharacterCount)
                .ThenBy(boundary =>
                    boundary.SegmentOrdinal)
                .Take(ReportSampleLimit)
                .ToArray();

        var largeSegments =
            boundaries
                .Where(boundary =>
                    boundary.SegmentCharacterCount >=
                    LargeSegmentCharacterThreshold)
                .OrderByDescending(boundary =>
                    boundary.SegmentCharacterCount)
                .ThenBy(boundary =>
                    boundary.SegmentOrdinal)
                .Take(ReportSampleLimit)
                .ToArray();

        var veryLargeCrossPage =
            boundaries
                .Where(boundary =>
                    boundary.IsCrossPage &&
                    boundary.SegmentCharacterCount >=
                    VeryLargeCrossPageCharacterThreshold)
                .OrderByDescending(boundary =>
                    boundary.SegmentCharacterCount)
                .ThenBy(boundary =>
                    boundary.SegmentOrdinal)
                .Take(ReportSampleLimit)
                .ToArray();

        var flagCounts =
            new BoundaryFlagCounts(
                boundaries.Count(boundary =>
                    boundary.IsNumberedStructural),
                boundaries.Count(boundary =>
                    boundary.FontRatio is not null &&
                    boundary.FontRatio <
                    MinimumHeadingFontRatio),
                boundaries.Count(boundary =>
                    boundary.RepeatedHeadingCount > 1),
                boundaries.Count(boundary =>
                    boundary.SegmentCharacterCount <=
                    SmallSegmentCharacterThreshold),
                boundaries.Count(boundary =>
                    boundary.SegmentCharacterCount >=
                    LargeSegmentCharacterThreshold),
                boundaries.Count(boundary =>
                    boundary.IsCrossPage &&
                    boundary.SegmentCharacterCount >=
                    VeryLargeCrossPageCharacterThreshold));

        return new HeadingBoundaryAnalysisReport(
            ReportSchemaVersion,
            DateTimeOffset.UtcNow,
            fileInfo.Name,
            sourceSha256,
            fileInfo.Length,
            extracted.Pages.Count,
            new PdfPageSelection(
                options.FirstPage,
                options.LastPage,
                selectedPages.Count),
            normalized.NormalizationProfileId,
            segmented.SegmentationProfileId,
            bodyFontSize,
            new SegmentationSnapshot(
                segmented.Segments.Count,
                headingSegments.Length,
                segmented.Segments.Count(segment =>
                    segment.HeadingText is null),
                segmented.Segments.Count(segment =>
                    segment.FirstPhysicalPageNumber !=
                    segment.LastPhysicalPageNumber)),
            new DiagnosticParity(
                actualHeadingKeys.Count,
                diagnosticHeadingKeys.Count,
                productionOnly.Length,
                diagnosticOnly.Length,
                productionOnly,
                diagnosticOnly),
            originCounts,
            flagCounts,
            repeatedGroups,
            numbered,
            weakTypography,
            smallSegments,
            largeSegments,
            veryLargeCrossPage,
            boundaries);
    }

    private static IReadOnlyList<BlockContext>
        BuildBlockContexts(
            IReadOnlyCollection<NormalizedDocumentPage> pages)
    {
        var contexts =
            new List<BlockContext>();

        foreach (var page in pages)
        {
            foreach (var block in page.Blocks)
            {
                if (block.IsExcluded ||
                    string.IsNullOrWhiteSpace(
                        block.Text))
                {
                    continue;
                }

                contexts.Add(
                    new BlockContext(
                        contexts.Count,
                        page.PhysicalPageNumber,
                        block));
            }
        }

        return contexts;
    }

    private static BoundaryDiagnostic
        BuildBoundaryDiagnostic(
            IReadOnlyList<BlockContext> contexts,
            BlockContext context,
            HeadingDecision decision,
            DocumentSegment segment,
            int repeatedHeadingCount)
    {
        var previous =
            context.GlobalIndex > 0
                ? contexts[
                    context.GlobalIndex - 1]
                : null;

        var next =
            context.GlobalIndex + 1 <
            contexts.Count
                ? contexts[
                    context.GlobalIndex + 1]
                : null;

        return new BoundaryDiagnostic(
            segment.Id,
            segment.Ordinal,
            context.PhysicalPageNumber,
            context.Block.SourceBlock
                .SourceSequence,
            segment.FirstPhysicalPageNumber,
            segment.LastPhysicalPageNumber,
            segment.FirstPhysicalPageNumber !=
            segment.LastPhysicalPageNumber,
            segment.Text.Length,
            segment.SourceBlocks.Count,
            segment.HeadingText!,
            decision.Origin!,
            context.Block.SourceBlock
                .DominantFontName,
            context.Block.SourceBlock
                .MedianPointSize,
            decision.FontRatio,
            context.Block.SourceBlock
                .WordCount,
            context.Block.SourceBlock
                .LineCount,
            NumberedStructuralHeadingRegex
                .IsMatch(
                    context.Block.Text.Trim()),
            repeatedHeadingCount,
            previous?.PhysicalPageNumber,
            previous is null
                ? null
                : Truncate(
                    previous.Block.Text),
            next?.PhysicalPageNumber,
            next is null
                ? null
                : Truncate(
                    next.Block.Text));
    }

    private static HeadingDecision EvaluateHeading(
        NormalizedDocumentTextBlock block,
        double? bodyFontSize)
    {
        var text =
            block.Text.Trim();

        var fontRatio =
            GetFontRatio(
                block,
                bodyFontSize);

        if (!HasAcceptableHeadingText(text))
        {
            return new HeadingDecision(
                false,
                null,
                fontRatio);
        }

        if (block.SourceBlock.WordCount >
            MaximumHeadingWords)
        {
            return new HeadingDecision(
                false,
                null,
                fontRatio);
        }

        if (ExplicitStructuralHeadingRegex
            .IsMatch(text))
        {
            if (fontRatio is null ||
                fontRatio >=
                MinimumExplicitFontRatio)
            {
                return new HeadingDecision(
                    true,
                    fontRatio is null
                        ? "ExplicitStructuralNoTypography"
                        : "ExplicitStructural",
                    fontRatio);
            }

            return new HeadingDecision(
                false,
                null,
                fontRatio);
        }

        if (fontRatio is >=
            MinimumHeadingFontRatio)
        {
            if (fontRatio <
                SectionFontRatio &&
                LooksLikeSentence(text))
            {
                return new HeadingDecision(
                    false,
                    null,
                    fontRatio);
            }

            return new HeadingDecision(
                true,
                fontRatio <
                SectionFontRatio
                    ? "TypographySubsection"
                    : "TypographyStrong",
                fontRatio);
        }

        if (IsUppercaseHeading(text))
        {
            if (fontRatio is null)
            {
                return new HeadingDecision(
                    true,
                    "UppercaseNoTypography",
                    fontRatio);
            }

            if (fontRatio >=
                MinimumUppercaseFontRatio)
            {
                return new HeadingDecision(
                    true,
                    "UppercaseModest",
                    fontRatio);
            }
        }

        return new HeadingDecision(
            false,
            null,
            fontRatio);
    }

    private static double? GetFontRatio(
        NormalizedDocumentTextBlock block,
        double? bodyFontSize)
    {
        if (bodyFontSize is null or <= 0 ||
            block.SourceBlock
                .MedianPointSize is null or <= 0)
        {
            return null;
        }

        return block.SourceBlock
                   .MedianPointSize.Value /
               bodyFontSize.Value;
    }

    private static bool HasAcceptableHeadingText(
        string text)
    {
        if (text.Length == 0 ||
            text.Length >
            MaximumHeadingCharacters ||
            text.Contains(
                '\uFFFD',
                StringComparison.Ordinal))
        {
            return false;
        }

        if (text.Any(char.IsControl))
        {
            return false;
        }

        var letterCount =
            text.Count(char.IsLetter);

        if (letterCount <
            MinimumHeadingLetterCount)
        {
            return false;
        }

        var nonWhitespaceCount =
            text.Count(character =>
                !char.IsWhiteSpace(character));

        var alphaNumericCount =
            text.Count(character =>
                char.IsLetterOrDigit(character));

        return nonWhitespaceCount > 0 &&
               alphaNumericCount * 2 >=
               nonWhitespaceCount;
    }

    private static bool IsUppercaseHeading(
        string text)
    {
        var hasLetter =
            false;

        foreach (var character in text)
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            hasLetter = true;

            if (char.IsLower(character))
            {
                return false;
            }
        }

        return hasLetter;
    }

    private static bool LooksLikeSentence(
        string text)
    {
        var trimmed =
            text.TrimEnd();

        return trimmed.EndsWith(
                   ".",
                   StringComparison.Ordinal) ||
               trimmed.EndsWith(
                   ";",
                   StringComparison.Ordinal) ||
               trimmed.EndsWith(
                   ",",
                   StringComparison.Ordinal);
    }

    private static double?
        GetWeightedMedianFontSize(
            IReadOnlyCollection<NormalizedDocumentTextBlock> blocks)
    {
        var samples =
            blocks
                .Where(block =>
                    block.SourceBlock
                        .MedianPointSize is > 0 &&
                    block.SourceBlock
                        .WordCount > 0)
                .Select(block =>
                    new FontSample(
                        block.SourceBlock
                            .MedianPointSize!.Value,
                        Math.Max(
                            1,
                            block.SourceBlock
                                .WordCount)))
                .OrderBy(sample =>
                    sample.PointSize)
                .ToArray();

        if (samples.Length == 0)
        {
            return null;
        }

        var totalWeight =
            samples.Sum(sample =>
                (long)sample.Weight);

        var medianPosition =
            (totalWeight + 1) / 2;

        long accumulatedWeight = 0;

        foreach (var sample in samples)
        {
            accumulatedWeight +=
                sample.Weight;

            if (accumulatedWeight >=
                medianPosition)
            {
                return sample.PointSize;
            }
        }

        return samples[^1]
            .PointSize;
    }

    private static string NormalizeHeadingKey(
        string text) =>
        WhitespaceRegex
            .Replace(
                text.Trim(),
                " ")
            .ToUpperInvariant();

    private static string Truncate(
        string text)
    {
        var normalized =
            WhitespaceRegex.Replace(
                text.Trim(),
                " ");

        return normalized.Length <=
               ContextCharacterLimit
            ? normalized
            : normalized[
                ..ContextCharacterLimit] +
              "…";
    }

    private static IReadOnlyList<DocumentExtractionPage>
        SelectPages(
            IReadOnlyList<DocumentExtractionPage> pages,
            int firstPage,
            int lastPage)
    {
        if (firstPage < 1 ||
            lastPage < firstPage ||
            lastPage > pages.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstPage),
                $"Invalid page range {firstPage}-{lastPage}. " +
                $"The PDF contains {pages.Count} pages.");
        }

        return pages
            .Where(page =>
                page.PhysicalPageNumber >=
                firstPage &&
                page.PhysicalPageNumber <=
                lastPage)
            .ToArray();
    }

    private static async Task<string>
        ComputeSha256Async(
            string sourcePath)
    {
        await using var stream =
            File.OpenRead(sourcePath);

        using var sha256 =
            SHA256.Create();

        var hash =
            await sha256.ComputeHashAsync(
                stream);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }

    private static async Task WriteReportAsync(
        string reportPath,
        HeadingBoundaryAnalysisReport report)
    {
        var fullPath =
            Path.GetFullPath(
                reportPath);

        var directory =
            Path.GetDirectoryName(
                fullPath);

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        var serializerOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase
            };

        var json =
            JsonSerializer.Serialize(
                report,
                serializerOptions);

        var temporaryPath =
            fullPath +
            ".tmp-" +
            Guid.NewGuid().ToString("N");

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                    false));

            File.Move(
                temporaryPath,
                fullPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }
        }
    }

    private static void WriteSummary(
        HeadingBoundaryAnalysisReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: HEADING BOUNDARIES ANALYZED");

        Console.WriteLine(
            $"Source: {report.SourceFileName}");

        Console.WriteLine(
            $"Source SHA-256: {report.SourceSha256}");

        Console.WriteLine(
            $"PDF pages selected: " +
            $"{report.PageSelection.FirstPage}-" +
            $"{report.PageSelection.LastPage} " +
            $"({report.PageSelection.PageCount})");

        Console.WriteLine(
            $"Segmentation profile: " +
            $"{report.SegmentationProfileId}");

        Console.WriteLine(
            $"Weighted median body font: " +
            $"{FormatNullable(report.WeightedMedianBodyFontSize)}");

        Console.WriteLine(
            $"Segments: {report.Segmentation.SegmentCount} " +
            $"(heading={report.Segmentation.HeadingSegmentCount}, " +
            $"fallback={report.Segmentation.FallbackSegmentCount}, " +
            $"cross-page={report.Segmentation.CrossPageSegmentCount})");

        Console.WriteLine(
            $"Diagnostic/production heading parity: " +
            $"production={report.Parity.ProductionHeadingCount}, " +
            $"diagnostic={report.Parity.DiagnosticHeadingCount}, " +
            $"production-only={report.Parity.ProductionOnlyCount}, " +
            $"diagnostic-only={report.Parity.DiagnosticOnlyCount}");

        Console.WriteLine(
            "Decision origins:");

        foreach (var origin in report.OriginCounts)
        {
            Console.WriteLine(
                $"  {origin.Origin}: {origin.Count}");
        }

        Console.WriteLine(
            "Review flags:");

        Console.WriteLine(
            $"  numbered structural: {report.Flags.NumberedStructural}");

        Console.WriteLine(
            $"  weak typography (<1.18): {report.Flags.WeakTypography}");

        Console.WriteLine(
            $"  repeated heading instances: {report.Flags.RepeatedHeadingInstances}");

        Console.WriteLine(
            $"  small segments (<=120): {report.Flags.SmallSegments}");

        Console.WriteLine(
            $"  large segments (>=4000): {report.Flags.LargeSegments}");

        Console.WriteLine(
            $"  very large cross-page (>=12000): {report.Flags.VeryLargeCrossPageSegments}");

        WriteBoundarySamples(
            "Numbered structural samples:",
            report.NumberedStructuralSamples);

        WriteBoundarySamples(
            "Weak-typography accepted samples:",
            report.WeakTypographySamples);

        WriteBoundarySamples(
            "Small-segment boundary samples:",
            report.SmallSegmentSamples);

        WriteBoundarySamples(
            "Largest-segment boundary samples:",
            report.LargeSegmentSamples);

        Console.WriteLine(
            "Most repeated headings:");

        foreach (var group in report.RepeatedHeadingGroups
                     .Take(ConsoleSampleLimit))
        {
            Console.WriteLine(
                $"  x{group.Count} " +
                $"{group.HeadingText} " +
                $"[pages {string.Join(",", group.PhysicalPages.Take(12))}]");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private static void WriteBoundarySamples(
        string title,
        IReadOnlyList<BoundaryDiagnostic> samples)
    {
        Console.WriteLine(title);

        foreach (var sample in samples
                     .Take(ConsoleSampleLimit))
        {
            Console.WriteLine(
                $"  p{sample.PhysicalPageNumber} " +
                $"{sample.DecisionOrigin} " +
                $"ratio={FormatNullable(sample.FontRatio)} " +
                $"chars={sample.SegmentCharacterCount} " +
                $"pages={sample.FirstPhysicalPageNumber}-{sample.LastPhysicalPageNumber} " +
                $"{sample.HeadingText}");

            if (!string.IsNullOrWhiteSpace(
                    sample.PreviousBlockText))
            {
                Console.WriteLine(
                    $"      prev p{sample.PreviousBlockPage}: " +
                    $"{sample.PreviousBlockText}");
            }

            if (!string.IsNullOrWhiteSpace(
                    sample.NextBlockText))
            {
                Console.WriteLine(
                    $"      next p{sample.NextBlockPage}: " +
                    $"{sample.NextBlockText}");
            }
        }
    }

    private static string FormatNullable(
        double? value) =>
        value.HasValue
            ? value.Value.ToString(
                "F3",
                System.Globalization.CultureInfo.InvariantCulture)
            : "unknown";

    private sealed record AnalysisOptions(
        string SourcePath,
        string ReportPath,
        int FirstPage,
        int LastPage)
    {
        public static AnalysisOptions Parse(
            string[] args)
        {
            string? source = null;
            string? report = null;
            string? pages = null;

            for (var index = 0;
                 index < args.Length;
                 index++)
            {
                var option =
                    args[index];

                switch (option)
                {
                    case "--source":
                        source =
                            ReadValue(
                                args,
                                ref index,
                                option);
                        break;

                    case "--report":
                        report =
                            ReadValue(
                                args,
                                ref index,
                                option);
                        break;

                    case "--pages":
                        pages =
                            ReadValue(
                                args,
                                ref index,
                                option);
                        break;

                    default:
                        throw new ArgumentException(
                            $"Unknown option '{option}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(
                    source))
            {
                throw new ArgumentException(
                    "--source is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    report))
            {
                throw new ArgumentException(
                    "--report is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    pages))
            {
                throw new ArgumentException(
                    "--pages is required.");
            }

            var range =
                pages.Split(
                    '-',
                    2,
                    StringSplitOptions.TrimEntries);

            if (range.Length != 2 ||
                !int.TryParse(
                    range[0],
                    out var firstPage) ||
                !int.TryParse(
                    range[1],
                    out var lastPage))
            {
                throw new ArgumentException(
                    $"Invalid page range '{pages}'. Expected FIRST-LAST.");
            }

            return new AnalysisOptions(
                Path.GetFullPath(source),
                Path.GetFullPath(report),
                firstPage,
                lastPage);
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            ref int index,
            string option)
        {
            if (index + 1 >=
                args.Count)
            {
                throw new ArgumentException(
                    $"Missing value for {option}.");
            }

            index++;

            var value =
                args[index];

            if (string.IsNullOrWhiteSpace(
                    value) ||
                value.StartsWith(
                    "--",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Missing value for {option}.");
            }

            return value;
        }
    }

    private sealed record HeadingBoundaryAnalysisReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string SourceFileName,
        string SourceSha256,
        long SourceByteLength,
        int TotalPdfPages,
        PdfPageSelection PageSelection,
        string NormalizationProfileId,
        string SegmentationProfileId,
        double? WeightedMedianBodyFontSize,
        SegmentationSnapshot Segmentation,
        DiagnosticParity Parity,
        IReadOnlyList<HeadingOriginCount> OriginCounts,
        BoundaryFlagCounts Flags,
        IReadOnlyList<RepeatedHeadingGroup> RepeatedHeadingGroups,
        IReadOnlyList<BoundaryDiagnostic> NumberedStructuralSamples,
        IReadOnlyList<BoundaryDiagnostic> WeakTypographySamples,
        IReadOnlyList<BoundaryDiagnostic> SmallSegmentSamples,
        IReadOnlyList<BoundaryDiagnostic> LargeSegmentSamples,
        IReadOnlyList<BoundaryDiagnostic> VeryLargeCrossPageSamples,
        IReadOnlyList<BoundaryDiagnostic> Boundaries);

    private sealed record PdfPageSelection(
        int FirstPage,
        int LastPage,
        int PageCount);

    private sealed record SegmentationSnapshot(
        int SegmentCount,
        int HeadingSegmentCount,
        int FallbackSegmentCount,
        int CrossPageSegmentCount);

    private sealed record DiagnosticParity(
        int ProductionHeadingCount,
        int DiagnosticHeadingCount,
        int ProductionOnlyCount,
        int DiagnosticOnlyCount,
        IReadOnlyList<BlockKey> ProductionOnly,
        IReadOnlyList<BlockKey> DiagnosticOnly);

    private sealed record HeadingOriginCount(
        string Origin,
        int Count);

    private sealed record BoundaryFlagCounts(
        int NumberedStructural,
        int WeakTypography,
        int RepeatedHeadingInstances,
        int SmallSegments,
        int LargeSegments,
        int VeryLargeCrossPageSegments);

    private sealed record RepeatedHeadingGroup(
        string HeadingText,
        int Count,
        IReadOnlyList<int> PhysicalPages,
        IReadOnlyList<string> DecisionOrigins);

    private sealed record BoundaryDiagnostic(
        string SegmentId,
        int SegmentOrdinal,
        int PhysicalPageNumber,
        int SourceSequence,
        int FirstPhysicalPageNumber,
        int LastPhysicalPageNumber,
        bool IsCrossPage,
        int SegmentCharacterCount,
        int SegmentBlockCount,
        string HeadingText,
        string DecisionOrigin,
        string? DominantFontName,
        double? MedianPointSize,
        double? FontRatio,
        int WordCount,
        int LineCount,
        bool IsNumberedStructural,
        int RepeatedHeadingCount,
        int? PreviousBlockPage,
        string? PreviousBlockText,
        int? NextBlockPage,
        string? NextBlockText);

    private sealed record BlockContext(
        int GlobalIndex,
        int PhysicalPageNumber,
        NormalizedDocumentTextBlock Block)
    {
        public BlockKey Key =>
            new(
                PhysicalPageNumber,
                Block.SourceBlock
                    .SourceSequence);
    }

    private readonly record struct BlockKey(
        int PhysicalPageNumber,
        int SourceSequence);

    private sealed record HeadingDecision(
        bool IsHeading,
        string? Origin,
        double? FontRatio);

    private sealed record ContextDecision(
        BlockContext Context,
        HeadingDecision Decision);

    private sealed record FontSample(
        double PointSize,
        int Weight);
}
