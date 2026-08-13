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

internal static class SegmentedPdfAnalysisCli
{
    private const string ReportSchemaVersion =
        "document-processing-segmented-pdf-analysis-v1";

    private const int SmallSegmentCharacterThreshold = 120;
    private const int LargeSegmentCharacterThreshold = 4000;
    private const int OutlierListLimit = 10;

    public static async Task<int> RunAsync(
        string[] args)
    {
        var options = AnalysisOptions.Parse(args);
        var report = await AnalyzeAsync(options);

        await WriteReportAsync(
            options.ReportPath,
            report);

        WriteSummary(
            report,
            options.ReportPath);

        return 0;
    }

    private static async Task<SegmentedPdfAnalysisReport>
        AnalyzeAsync(
            AnalysisOptions options)
    {
        var sourcePath =
            Path.GetFullPath(options.SourcePath);

        var fileInfo =
            new FileInfo(sourcePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "PDF source was not found.",
                sourcePath);
        }

        var sourceSha256 =
            await ComputeSha256Async(sourcePath);

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
                .Normalize(selectedExtraction);

        var segmented =
            new HeuristicDocumentSegmenter()
                .Segment(normalized);

        var preprocessing =
            BuildPreprocessingMetrics(
                selectedPages,
                normalized.Pages);

        var segmentation =
            BuildSegmentationMetrics(
                selectedPages,
                segmented.Segments);

        var headings =
            segmented.Segments
                .Where(segment =>
                    segment.HeadingText is not null)
                .Select(segment =>
                    new HeadingDiagnostic(
                        segment.Id,
                        segment.FirstPhysicalPageNumber,
                        segment.HeadingText!))
                .ToArray();

        var smallestSegments =
            segmented.Segments
                .OrderBy(segment =>
                    segment.Text.Length)
                .ThenBy(segment =>
                    segment.Ordinal)
                .Take(OutlierListLimit)
                .Select(ToSegmentDiagnostic)
                .ToArray();

        var largestSegments =
            segmented.Segments
                .OrderByDescending(segment =>
                    segment.Text.Length)
                .ThenBy(segment =>
                    segment.Ordinal)
                .Take(OutlierListLimit)
                .Select(ToSegmentDiagnostic)
                .ToArray();

        var probes =
            options.Probes
                .Select(probe =>
                    BuildProbeDiagnostic(
                        probe,
                        segmented.Segments))
                .ToArray();

        return new SegmentedPdfAnalysisReport(
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
            preprocessing,
            segmentation,
            headings,
            smallestSegments,
            largestSegments,
            probes);
    }

    private static PreprocessingMetrics
        BuildPreprocessingMetrics(
            IReadOnlyCollection<DocumentExtractionPage> pages,
            IReadOnlyCollection<NormalizedDocumentPage> normalizedPages)
    {
        var normalizedBlocks =
            normalizedPages
                .SelectMany(page =>
                    page.Blocks)
                .ToArray();

        return new PreprocessingMetrics(
            pages.Sum(page =>
                page.Words.Count),
            pages.Sum(page =>
                page.Blocks.Count),
            normalizedBlocks.Count(block =>
                !block.IsExcluded),
            normalizedBlocks.Count(block =>
                block.ExclusionReason ==
                DocumentBlockExclusionReason.RepeatedHeader),
            normalizedBlocks.Count(block =>
                block.ExclusionReason ==
                DocumentBlockExclusionReason.RepeatedFooter));
    }

    private static SegmentationMetrics
        BuildSegmentationMetrics(
            IReadOnlyCollection<DocumentExtractionPage> pages,
            IReadOnlyList<DocumentSegment> segments)
    {
        var selectedPageNumbers =
            pages.Select(page =>
                    page.PhysicalPageNumber)
                .OrderBy(pageNumber =>
                    pageNumber)
                .ToArray();

        var groups =
            segments
                .GroupBy(segment =>
                    segment.FirstPhysicalPageNumber)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.Count());

        var pageNumbersWithSegments =
            groups.Keys
                .OrderBy(pageNumber =>
                    pageNumber)
                .ToArray();

        var pageNumbersWithoutSegments =
            selectedPageNumbers
                .Where(pageNumber =>
                    !groups.ContainsKey(pageNumber))
                .ToArray();

        var pageNumbersWithMultipleSegments =
            groups
                .Where(pair =>
                    pair.Value > 1)
                .OrderBy(pair =>
                    pair.Key)
                .Select(pair =>
                    pair.Key)
                .ToArray();

        var characterCounts =
            segments
                .Select(segment =>
                    segment.Text.Length)
                .OrderBy(value =>
                    value)
                .ToArray();

        var blockCounts =
            segments
                .Select(segment =>
                    segment.SourceBlocks.Count)
                .OrderBy(value =>
                    value)
                .ToArray();

        return new SegmentationMetrics(
            segments.Count,
            segments.Count(segment =>
                segment.HeadingText is not null),
            segments.Count(segment =>
                segment.HeadingText is null),
            pageNumbersWithSegments.Length,
            pageNumbersWithoutSegments.Length,
            pageNumbersWithMultipleSegments.Length,
            groups.Count == 0
                ? 0
                : groups.Values.Max(),
            segments.Count(segment =>
                segment.FirstPhysicalPageNumber !=
                segment.LastPhysicalPageNumber),
            MinOrZero(characterCounts),
            MedianOrZero(characterCounts),
            AverageOrZero(characterCounts),
            MaxOrZero(characterCounts),
            MinOrZero(blockCounts),
            MedianOrZero(blockCounts),
            AverageOrZero(blockCounts),
            MaxOrZero(blockCounts),
            characterCounts.Count(value =>
                value <= SmallSegmentCharacterThreshold),
            characterCounts.Count(value =>
                value >= LargeSegmentCharacterThreshold),
            pageNumbersWithSegments,
            pageNumbersWithoutSegments,
            pageNumbersWithMultipleSegments);
    }

    private static int MinOrZero(
        IReadOnlyList<int> values) =>
        values.Count == 0
            ? 0
            : values[0];

    private static int MaxOrZero(
        IReadOnlyList<int> values) =>
        values.Count == 0
            ? 0
            : values[^1];

    private static double AverageOrZero(
        IReadOnlyList<int> values) =>
        values.Count == 0
            ? 0
            : Math.Round(
                values.Average(),
                1);

    private static double MedianOrZero(
        IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var middle =
            values.Count / 2;

        if (values.Count % 2 == 1)
        {
            return values[middle];
        }

        return Math.Round(
            (values[middle - 1] +
             values[middle]) / 2.0,
            1);
    }

    private static SegmentDiagnostic
        ToSegmentDiagnostic(
            DocumentSegment segment) =>
        new(
            segment.Id,
            segment.Ordinal,
            segment.FirstPhysicalPageNumber,
            segment.LastPhysicalPageNumber,
            segment.Text.Length,
            segment.SourceBlocks.Count,
            segment.HeadingText);

    private static SegmentProbeDiagnostic
        BuildProbeDiagnostic(
            string probe,
            IReadOnlyCollection<DocumentSegment> segments)
    {
        var matches =
            segments
                .Where(segment =>
                    segment.Text.Contains(
                        probe,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        return new SegmentProbeDiagnostic(
            probe,
            matches.Length,
            matches
                .Select(segment =>
                    segment.Id)
                .ToArray(),
            matches
                .Select(segment =>
                    segment.FirstPhysicalPageNumber)
                .Distinct()
                .OrderBy(pageNumber =>
                    pageNumber)
                .ToArray());
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
            await sha256.ComputeHashAsync(stream);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }

    private static async Task WriteReportAsync(
        string reportPath,
        SegmentedPdfAnalysisReport report)
    {
        var fullPath =
            Path.GetFullPath(reportPath);

        var directory =
            Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
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
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void WriteSummary(
        SegmentedPdfAnalysisReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: SEGMENTATION ANALYZED");

        Console.WriteLine(
            $"Source: {report.SourceFileName}");

        Console.WriteLine(
            $"Source SHA-256: {report.SourceSha256}");

        Console.WriteLine(
            $"PDF pages total: {report.TotalPdfPages}");

        Console.WriteLine(
            $"PDF pages selected: " +
            $"{report.PageSelection.FirstPage}-" +
            $"{report.PageSelection.LastPage} " +
            $"({report.PageSelection.PageCount})");

        Console.WriteLine(
            $"Normalization profile: " +
            $"{report.NormalizationProfileId}");

        Console.WriteLine(
            $"Segmentation profile: " +
            $"{report.SegmentationProfileId}");

        Console.WriteLine(
            $"Words / raw blocks / included blocks: " +
            $"{report.Preprocessing.WordCount} / " +
            $"{report.Preprocessing.RawBlockCount} / " +
            $"{report.Preprocessing.IncludedBlockCount}");

        Console.WriteLine(
            $"Excluded recurring headers / footers: " +
            $"{report.Preprocessing.ExcludedHeaderBlocks} / " +
            $"{report.Preprocessing.ExcludedFooterBlocks}");

        Console.WriteLine(
            $"Segments: {report.Segmentation.SegmentCount} " +
            $"(heading={report.Segmentation.HeadingSegmentCount}, " +
            $"fallback={report.Segmentation.FallbackSegmentCount})");

        Console.WriteLine(
            $"Pages with / without segments: " +
            $"{report.Segmentation.PagesWithSegments} / " +
            $"{report.Segmentation.PagesWithoutSegments}");

        Console.WriteLine(
            $"Pages with multiple segments: " +
            $"{report.Segmentation.PagesWithMultipleSegments}; " +
            $"max segments/page: " +
            $"{report.Segmentation.MaximumSegmentsOnPage}");

        Console.WriteLine(
            $"Cross-page segments: " +
            $"{report.Segmentation.CrossPageSegmentCount}");

        Console.WriteLine(
            $"Segment chars min/median/avg/max: " +
            $"{report.Segmentation.MinimumCharacterCount} / " +
            $"{report.Segmentation.MedianCharacterCount:F1} / " +
            $"{report.Segmentation.AverageCharacterCount:F1} / " +
            $"{report.Segmentation.MaximumCharacterCount}");

        Console.WriteLine(
            $"Segment blocks min/median/avg/max: " +
            $"{report.Segmentation.MinimumBlockCount} / " +
            $"{report.Segmentation.MedianBlockCount:F1} / " +
            $"{report.Segmentation.AverageBlockCount:F1} / " +
            $"{report.Segmentation.MaximumBlockCount}");

        Console.WriteLine(
            $"Small segments (<= {SmallSegmentCharacterThreshold} chars): " +
            $"{report.Segmentation.SmallSegmentCount}");

        Console.WriteLine(
            $"Large segments (>= {LargeSegmentCharacterThreshold} chars): " +
            $"{report.Segmentation.LargeSegmentCount}");

        foreach (var probe in report.Probes)
        {
            Console.WriteLine(
                $"Probe '{probe.Probe}': " +
                $"{probe.SegmentMatches} segment match(es)");
        }

        Console.WriteLine(
            $"Detected headings: {report.Headings.Count}");

        foreach (var heading in report.Headings.Take(20))
        {
            Console.WriteLine(
                $"  p{heading.PhysicalPageNumber}: " +
                $"{heading.Text}");
        }

        if (report.Headings.Count > 20)
        {
            Console.WriteLine(
                $"  ... {report.Headings.Count - 20} more heading(s) in JSON report");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private sealed record AnalysisOptions(
        string SourcePath,
        string ReportPath,
        int FirstPage,
        int LastPage,
        IReadOnlyList<string> Probes)
    {
        public static AnalysisOptions Parse(
            string[] args)
        {
            string? source = null;
            string? report = null;
            string? pages = null;

            var probes =
                new List<string>();

            for (var index = 0;
                 index < args.Length;
                 index++)
            {
                var option = args[index];

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

                    case "--probe":
                        probes.Add(
                            ReadValue(
                                args,
                                ref index,
                                option));
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
                lastPage,
                probes);
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

            var value = args[index];

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

    private sealed record SegmentedPdfAnalysisReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string SourceFileName,
        string SourceSha256,
        long SourceByteLength,
        int TotalPdfPages,
        PdfPageSelection PageSelection,
        string NormalizationProfileId,
        string SegmentationProfileId,
        PreprocessingMetrics Preprocessing,
        SegmentationMetrics Segmentation,
        IReadOnlyList<HeadingDiagnostic> Headings,
        IReadOnlyList<SegmentDiagnostic> SmallestSegments,
        IReadOnlyList<SegmentDiagnostic> LargestSegments,
        IReadOnlyList<SegmentProbeDiagnostic> Probes);

    private sealed record PdfPageSelection(
        int FirstPage,
        int LastPage,
        int PageCount);

    private sealed record PreprocessingMetrics(
        int WordCount,
        int RawBlockCount,
        int IncludedBlockCount,
        int ExcludedHeaderBlocks,
        int ExcludedFooterBlocks);

    private sealed record SegmentationMetrics(
        int SegmentCount,
        int HeadingSegmentCount,
        int FallbackSegmentCount,
        int PagesWithSegments,
        int PagesWithoutSegments,
        int PagesWithMultipleSegments,
        int MaximumSegmentsOnPage,
        int CrossPageSegmentCount,
        int MinimumCharacterCount,
        double MedianCharacterCount,
        double AverageCharacterCount,
        int MaximumCharacterCount,
        int MinimumBlockCount,
        double MedianBlockCount,
        double AverageBlockCount,
        int MaximumBlockCount,
        int SmallSegmentCount,
        int LargeSegmentCount,
        IReadOnlyList<int> PageNumbersWithSegments,
        IReadOnlyList<int> PageNumbersWithoutSegments,
        IReadOnlyList<int> PageNumbersWithMultipleSegments);

    private sealed record HeadingDiagnostic(
        string SegmentId,
        int PhysicalPageNumber,
        string Text);

    private sealed record SegmentDiagnostic(
        string SegmentId,
        int Ordinal,
        int FirstPhysicalPageNumber,
        int LastPhysicalPageNumber,
        int CharacterCount,
        int BlockCount,
        string? HeadingText);

    private sealed record SegmentProbeDiagnostic(
        string Probe,
        int SegmentMatches,
        IReadOnlyList<string> SegmentIds,
        IReadOnlyList<int> PhysicalPageNumbers);
}
