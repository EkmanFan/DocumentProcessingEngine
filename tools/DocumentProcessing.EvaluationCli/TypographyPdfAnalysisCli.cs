using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Segmentation;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Segmentation;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.EvaluationCli;

internal static class TypographyPdfAnalysisCli
{
    private const string ReportSchemaVersion =
        "document-processing-typography-pdf-analysis-v1";

    // Historical generic font-hierarchy thresholds used by ApologiaStudio.
    private const int MaximumHeadingCharacters = 180;
    private const int MaximumHeadingWords = 24;
    private const double MinimumHeadingFontRatio = 1.18;
    private const double SectionFontRatio = 1.30;
    private const double ChapterFontRatio = 1.55;
    private const int SampleLimit = 30;

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

    private static async Task<TypographyAnalysisReport>
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

        var allWords =
            selectedPages
                .SelectMany(page =>
                    page.Words)
                .ToArray();

        var rawBlocks =
            selectedPages
                .SelectMany(page =>
                    page.Blocks)
                .ToArray();

        var includedContexts =
            normalized.Pages
                .SelectMany(page =>
                    page.Blocks
                        .Where(block =>
                            !block.IsExcluded &&
                            !string.IsNullOrWhiteSpace(
                                block.Text))
                        .Select(block =>
                            new BlockContext(
                                page.PhysicalPageNumber,
                                block)))
                .ToArray();

        var bodyFontSize =
            GetWeightedMedianFontSize(
                includedContexts);

        var currentHeadingKeys =
            segmented.Segments
                .Where(segment =>
                    segment.HeadingText is not null &&
                    segment.SourceBlocks.Count > 0)
                .Select(segment =>
                    new BlockKey(
                        segment.FirstPhysicalPageNumber,
                        segment.SourceBlocks[0]
                            .SourceBlock
                            .SourceSequence))
                .ToHashSet();

        var fontCandidates =
            includedContexts
                .Select(context =>
                    EvaluateFontCandidate(
                        context,
                        bodyFontSize))
                .Where(candidate =>
                    candidate.IsCandidate)
                .ToArray();

        var fontCandidateKeys =
            fontCandidates
                .Select(candidate =>
                    candidate.Key)
                .ToHashSet();

        var currentHeadingContexts =
            includedContexts
                .Where(context =>
                    currentHeadingKeys.Contains(
                        context.Key))
                .ToArray();

        var textOnlyHeadings =
            currentHeadingContexts
                .Where(context =>
                    !fontCandidateKeys.Contains(
                        context.Key))
                .Select(context =>
                    ToHeadingEvidenceSample(
                        context,
                        bodyFontSize))
                .Take(SampleLimit)
                .ToArray();

        var fontOnlyHeadings =
            fontCandidates
                .Where(candidate =>
                    !currentHeadingKeys.Contains(
                        candidate.Key))
                .OrderByDescending(candidate =>
                    candidate.FontRatio)
                .ThenBy(candidate =>
                    candidate.PageNumber)
                .Select(candidate =>
                    candidate.Sample)
                .Take(SampleLimit)
                .ToArray();

        var strongestFontCandidates =
            fontCandidates
                .OrderByDescending(candidate =>
                    candidate.FontRatio)
                .ThenBy(candidate =>
                    candidate.PageNumber)
                .Select(candidate =>
                    candidate.Sample)
                .Take(SampleLimit)
                .ToArray();

        var pointSizeDistribution =
            includedContexts
                .Where(context =>
                    context.Block.SourceBlock
                        .MedianPointSize is > 0)
                .GroupBy(context =>
                    Math.Round(
                        context.Block.SourceBlock
                            .MedianPointSize!.Value,
                        3))
                .Select(group =>
                    new PointSizeDistribution(
                        group.Key,
                        group.Count(),
                        group.Sum(context =>
                            context.Block.SourceBlock
                                .WordCount)))
                .OrderBy(item =>
                    item.PointSize)
                .ToArray();

        var topFonts =
            includedContexts
                .Where(context =>
                    !string.IsNullOrWhiteSpace(
                        context.Block.SourceBlock
                            .DominantFontName))
                .GroupBy(
                    context =>
                        context.Block.SourceBlock
                            .DominantFontName!,
                    StringComparer.Ordinal)
                .Select(group =>
                    new FontDistribution(
                        group.Key,
                        group.Count(),
                        group.Sum(context =>
                            context.Block.SourceBlock
                                .WordCount)))
                .OrderByDescending(item =>
                    item.WeightedWordCount)
                .ThenByDescending(item =>
                    item.BlockCount)
                .ThenBy(item =>
                    item.FontName,
                    StringComparer.Ordinal)
                .Take(20)
                .ToArray();

        var bands =
            BuildRatioBands(
                includedContexts,
                bodyFontSize);

        var fontCandidateCounts =
            new FontCandidateCounts(
                fontCandidates.Length,
                fontCandidates.Count(candidate =>
                    candidate.Kind ==
                    "Subsection"),
                fontCandidates.Count(candidate =>
                    candidate.Kind ==
                    "Section"),
                fontCandidates.Count(candidate =>
                    candidate.Kind ==
                    "Chapter"));

        var overlap =
            new HeadingComparison(
                currentHeadingKeys.Count,
                fontCandidateKeys.Count,
                currentHeadingKeys
                    .Intersect(
                        fontCandidateKeys)
                    .Count(),
                currentHeadingKeys
                    .Except(
                        fontCandidateKeys)
                    .Count(),
                fontCandidateKeys
                    .Except(
                        currentHeadingKeys)
                    .Count());

        return new TypographyAnalysisReport(
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
            new TypographyCoverage(
                allWords.Length,
                allWords.Count(word =>
                    !string.IsNullOrWhiteSpace(
                        word.FontName)),
                allWords.Count(word =>
                    word.MedianPointSize is > 0),
                rawBlocks.Length,
                rawBlocks.Count(block =>
                    !string.IsNullOrWhiteSpace(
                        block.DominantFontName)),
                rawBlocks.Count(block =>
                    block.MedianPointSize is > 0),
                rawBlocks.Count(block =>
                    block.LineCount > 0),
                includedContexts.Length,
                includedContexts.Count(context =>
                    !string.IsNullOrWhiteSpace(
                        context.Block.SourceBlock
                            .DominantFontName)),
                includedContexts.Count(context =>
                    context.Block.SourceBlock
                        .MedianPointSize is > 0)),
            bodyFontSize,
            new HistoricalThresholds(
                MaximumHeadingCharacters,
                MaximumHeadingWords,
                MinimumHeadingFontRatio,
                SectionFontRatio,
                ChapterFontRatio),
            bands,
            fontCandidateCounts,
            overlap,
            pointSizeDistribution,
            topFonts,
            strongestFontCandidates,
            textOnlyHeadings,
            fontOnlyHeadings);
    }

    private static IReadOnlyList<FontRatioBand>
        BuildRatioBands(
            IReadOnlyCollection<BlockContext> contexts,
            double? bodyFontSize)
    {
        if (bodyFontSize is null or <= 0)
        {
            return
            [
                new FontRatioBand(
                    "unknown",
                    null,
                    null,
                    contexts.Count)
            ];
        }

        var ratios =
            contexts
                .Where(context =>
                    context.Block.SourceBlock
                        .MedianPointSize is > 0)
                .Select(context =>
                    context.Block.SourceBlock
                        .MedianPointSize!.Value /
                    bodyFontSize.Value)
                .ToArray();

        return
        [
            new FontRatioBand(
                "<1.18",
                null,
                MinimumHeadingFontRatio,
                ratios.Count(ratio =>
                    ratio <
                    MinimumHeadingFontRatio)),
            new FontRatioBand(
                "1.18-1.30",
                MinimumHeadingFontRatio,
                SectionFontRatio,
                ratios.Count(ratio =>
                    ratio >=
                    MinimumHeadingFontRatio &&
                    ratio <
                    SectionFontRatio)),
            new FontRatioBand(
                "1.30-1.55",
                SectionFontRatio,
                ChapterFontRatio,
                ratios.Count(ratio =>
                    ratio >=
                    SectionFontRatio &&
                    ratio <
                    ChapterFontRatio)),
            new FontRatioBand(
                ">=1.55",
                ChapterFontRatio,
                null,
                ratios.Count(ratio =>
                    ratio >=
                    ChapterFontRatio))
        ];
    }

    private static FontCandidateEvaluation
        EvaluateFontCandidate(
            BlockContext context,
            double? bodyFontSize)
    {
        var block =
            context.Block.SourceBlock;

        var sample =
            ToHeadingEvidenceSample(
                context,
                bodyFontSize);

        if (bodyFontSize is null or <= 0 ||
            block.MedianPointSize is null or <= 0 ||
            context.Block.Text.Length >
            MaximumHeadingCharacters ||
            block.WordCount >
            MaximumHeadingWords)
        {
            return new FontCandidateEvaluation(
                context.Key,
                context.PageNumber,
                false,
                null,
                sample.FontRatio,
                sample);
        }

        var fontRatio =
            block.MedianPointSize.Value /
            bodyFontSize.Value;

        if (fontRatio <
            MinimumHeadingFontRatio)
        {
            return new FontCandidateEvaluation(
                context.Key,
                context.PageNumber,
                false,
                null,
                fontRatio,
                sample);
        }

        if (fontRatio <
            SectionFontRatio &&
            LooksLikeSentence(
                context.Block.Text))
        {
            return new FontCandidateEvaluation(
                context.Key,
                context.PageNumber,
                false,
                null,
                fontRatio,
                sample);
        }

        var kind =
            fontRatio >=
            ChapterFontRatio
                ? "Chapter"
                : fontRatio >=
                  SectionFontRatio
                    ? "Section"
                    : "Subsection";

        return new FontCandidateEvaluation(
            context.Key,
            context.PageNumber,
            true,
            kind,
            fontRatio,
            sample with
            {
                HistoricalCandidateKind =
                    kind
            });
    }

    private static HeadingEvidenceSample
        ToHeadingEvidenceSample(
            BlockContext context,
            double? bodyFontSize)
    {
        var block =
            context.Block.SourceBlock;

        var ratio =
            bodyFontSize is > 0 &&
            block.MedianPointSize is > 0
                ? block.MedianPointSize.Value /
                  bodyFontSize.Value
                : (double?)null;

        return new HeadingEvidenceSample(
            context.PageNumber,
            block.SourceSequence,
            context.Block.Text,
            block.DominantFontName,
            block.MedianPointSize,
            ratio,
            block.WordCount,
            block.LineCount,
            LooksLikeSentence(
                context.Block.Text),
            null);
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
            IReadOnlyCollection<BlockContext> blocks)
    {
        var samples =
            blocks
                .Where(context =>
                    context.Block.SourceBlock
                        .MedianPointSize is > 0 &&
                    context.Block.SourceBlock
                        .WordCount > 0)
                .Select(context =>
                    new FontSample(
                        context.Block.SourceBlock
                            .MedianPointSize!.Value,
                        Math.Max(
                            1,
                            context.Block.SourceBlock
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

        return samples[^1].PointSize;
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
        TypographyAnalysisReport report)
    {
        var fullPath =
            Path.GetFullPath(
                reportPath);

        var directory =
            Path.GetDirectoryName(
                fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
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
        TypographyAnalysisReport report,
        string reportPath)
    {
        var coverage =
            report.Coverage;

        Console.WriteLine(
            "RESULT: TYPOGRAPHY ANALYZED");

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
            $"Words: {coverage.WordCount}; " +
            $"font-name={Percent(coverage.WordsWithFontName, coverage.WordCount):F1}% " +
            $"point-size={Percent(coverage.WordsWithPointSize, coverage.WordCount):F1}%");

        Console.WriteLine(
            $"Raw blocks: {coverage.RawBlockCount}; " +
            $"font-name={Percent(coverage.RawBlocksWithFontName, coverage.RawBlockCount):F1}% " +
            $"point-size={Percent(coverage.RawBlocksWithPointSize, coverage.RawBlockCount):F1}% " +
            $"line-count={Percent(coverage.RawBlocksWithLineCount, coverage.RawBlockCount):F1}%");

        Console.WriteLine(
            $"Included blocks: {coverage.IncludedBlockCount}; " +
            $"font-name={Percent(coverage.IncludedBlocksWithFontName, coverage.IncludedBlockCount):F1}% " +
            $"point-size={Percent(coverage.IncludedBlocksWithPointSize, coverage.IncludedBlockCount):F1}%");

        Console.WriteLine(
            $"Weighted median body font size: " +
            $"{FormatNullable(report.WeightedMedianBodyFontSize)}");

        Console.WriteLine(
            "Historical font-ratio bands:");

        foreach (var band in report.RatioBands)
        {
            Console.WriteLine(
                $"  {band.Label}: {band.BlockCount}");
        }

        Console.WriteLine(
            $"Historical font candidates: " +
            $"{report.FontCandidates.Total} " +
            $"(subsection={report.FontCandidates.Subsection}, " +
            $"section={report.FontCandidates.Section}, " +
            $"chapter={report.FontCandidates.Chapter})");

        Console.WriteLine(
            $"Current text headings: {report.HeadingComparison.CurrentTextHeadings}");

        Console.WriteLine(
            $"Overlap text/font: {report.HeadingComparison.Overlap}");

        Console.WriteLine(
            $"Text-only headings: {report.HeadingComparison.TextOnly}");

        Console.WriteLine(
            $"Font-only candidates: {report.HeadingComparison.FontOnly}");

        Console.WriteLine(
            "Text-only heading samples:");

        foreach (var sample in report.TextOnlyHeadingSamples.Take(12))
        {
            Console.WriteLine(
                $"  p{sample.PhysicalPageNumber} " +
                $"ratio={FormatNullable(sample.FontRatio)} " +
                $"size={FormatNullable(sample.MedianPointSize)} " +
                $"{sample.Text}");
        }

        Console.WriteLine(
            "Strongest font-candidate samples:");

        foreach (var sample in report.StrongestFontCandidateSamples.Take(12))
        {
            Console.WriteLine(
                $"  p{sample.PhysicalPageNumber} " +
                $"{sample.HistoricalCandidateKind} " +
                $"ratio={FormatNullable(sample.FontRatio)} " +
                $"size={FormatNullable(sample.MedianPointSize)} " +
                $"{sample.Text}");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private static double Percent(
        int numerator,
        int denominator) =>
        denominator == 0
            ? 0
            : numerator * 100.0 /
              denominator;

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

            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException(
                    "--source is required.");
            }

            if (string.IsNullOrWhiteSpace(report))
            {
                throw new ArgumentException(
                    "--report is required.");
            }

            if (string.IsNullOrWhiteSpace(pages))
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

            if (string.IsNullOrWhiteSpace(value) ||
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

    private sealed record TypographyAnalysisReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string SourceFileName,
        string SourceSha256,
        long SourceByteLength,
        int TotalPdfPages,
        PdfPageSelection PageSelection,
        string NormalizationProfileId,
        string SegmentationProfileId,
        TypographyCoverage Coverage,
        double? WeightedMedianBodyFontSize,
        HistoricalThresholds HistoricalThresholds,
        IReadOnlyList<FontRatioBand> RatioBands,
        FontCandidateCounts FontCandidates,
        HeadingComparison HeadingComparison,
        IReadOnlyList<PointSizeDistribution> PointSizeDistribution,
        IReadOnlyList<FontDistribution> TopFonts,
        IReadOnlyList<HeadingEvidenceSample> StrongestFontCandidateSamples,
        IReadOnlyList<HeadingEvidenceSample> TextOnlyHeadingSamples,
        IReadOnlyList<HeadingEvidenceSample> FontOnlyHeadingSamples);

    private sealed record PdfPageSelection(
        int FirstPage,
        int LastPage,
        int PageCount);

    private sealed record TypographyCoverage(
        int WordCount,
        int WordsWithFontName,
        int WordsWithPointSize,
        int RawBlockCount,
        int RawBlocksWithFontName,
        int RawBlocksWithPointSize,
        int RawBlocksWithLineCount,
        int IncludedBlockCount,
        int IncludedBlocksWithFontName,
        int IncludedBlocksWithPointSize);

    private sealed record HistoricalThresholds(
        int MaximumHeadingCharacters,
        int MaximumHeadingWords,
        double MinimumHeadingFontRatio,
        double SectionFontRatio,
        double ChapterFontRatio);

    private sealed record FontRatioBand(
        string Label,
        double? MinimumInclusive,
        double? MaximumExclusive,
        int BlockCount);

    private sealed record FontCandidateCounts(
        int Total,
        int Subsection,
        int Section,
        int Chapter);

    private sealed record HeadingComparison(
        int CurrentTextHeadings,
        int HistoricalFontCandidates,
        int Overlap,
        int TextOnly,
        int FontOnly);

    private sealed record PointSizeDistribution(
        double PointSize,
        int BlockCount,
        int WeightedWordCount);

    private sealed record FontDistribution(
        string FontName,
        int BlockCount,
        int WeightedWordCount);

    private sealed record HeadingEvidenceSample(
        int PhysicalPageNumber,
        int SourceSequence,
        string Text,
        string? DominantFontName,
        double? MedianPointSize,
        double? FontRatio,
        int WordCount,
        int LineCount,
        bool LooksLikeSentence,
        string? HistoricalCandidateKind);

    private sealed record BlockContext(
        int PageNumber,
        NormalizedDocumentTextBlock Block)
    {
        public BlockKey Key =>
            new(
                PageNumber,
                Block.SourceBlock
                    .SourceSequence);
    }

    private readonly record struct BlockKey(
        int PageNumber,
        int SourceSequence);

    private sealed record FontSample(
        double PointSize,
        int Weight);

    private sealed record FontCandidateEvaluation(
        BlockKey Key,
        int PageNumber,
        bool IsCandidate,
        string? Kind,
        double? FontRatio,
        HeadingEvidenceSample Sample);
}
