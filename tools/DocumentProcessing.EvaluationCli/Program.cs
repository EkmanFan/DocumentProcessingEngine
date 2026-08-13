using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.EvaluationCli;

internal static class Program
{
    private const string ReportSchemaVersion =
        "document-processing-native-pdf-analysis-v1";

    private const double DominantRasterImageAreaRatio = 0.60;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 ||
                args.Contains("--help", StringComparer.Ordinal))
            {
                WriteUsage();
                return args.Length == 0 ? 2 : 0;
            }

            if (string.Equals(
                    args[0],
                    "analyze-segmented-pdf",
                    StringComparison.Ordinal))
            {
                return await SegmentedPdfAnalysisCli.RunAsync(
                    args[1..]);
            }

            if (string.Equals(
                    args[0],
                    "analyze-normalized-pdf",
                    StringComparison.Ordinal))
            {
                return await NormalizedPdfAnalysisCli.RunAsync(
                    args[1..]);
            }

            if (!string.Equals(
                    args[0],
                    "analyze-pdf",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Unknown command '{args[0]}'. " +
                    "Expected 'analyze-pdf', 'analyze-normalized-pdf', " +
                    "or 'analyze-segmented-pdf'.");
            }

            var options = AnalysisOptions.Parse(args[1..]);

            var report = await AnalyzeAsync(options);

            await WriteReportAsync(
                options.ReportPath,
                report);

            WriteSummary(
                report,
                options.ReportPath);

            return 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException)
        {
            Console.Error.WriteLine(
                $"ERROR: {exception.Message}");
            return 2;
        }
    }

    private static async Task<PdfNativeAnalysisReport> AnalyzeAsync(
        AnalysisOptions options)
    {
        var sourcePath = Path.GetFullPath(options.SourcePath);
        var fileInfo = new FileInfo(sourcePath);

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

        var source = new DocumentSource(
            sourceStream,
            fileInfo.Name,
            "application/pdf");

        var extractor =
            new PdfPigDocumentExtractor();

        var extracted =
            await extractor.ExtractAsync(
                source,
                DocumentFormatId.Pdf);

        var selectedPages = SelectPages(
            extracted.Pages,
            options.FirstPage,
            options.LastPage);

        var pagesWithWords =
            selectedPages.Count(page =>
                page.Words.Count > 0);

        var textlessDominantRasterPages =
            selectedPages
                .Where(page =>
                    page.Words.Count == 0 &&
                    page.LargestRasterImageAreaRatio >=
                    DominantRasterImageAreaRatio)
                .Select(page =>
                    page.PhysicalPageNumber)
                .OrderBy(pageNumber =>
                    pageNumber)
                .ToArray();

        var extractionMetrics =
            new PdfExtractionMetrics(
                selectedPages.Sum(page =>
                    page.Words.Count),
                selectedPages.Sum(page =>
                    page.Blocks.Count),
                pagesWithWords,
                selectedPages.Count - pagesWithWords,
                selectedPages.Count(page =>
                    page.Blocks.Count == 0),
                selectedPages.Count == 0
                    ? 0
                    : Math.Round(
                        pagesWithWords * 100.0 /
                        selectedPages.Count,
                        1),
                DominantRasterImageAreaRatio,
                textlessDominantRasterPages.Length);

        var layoutMetrics =
            BuildRawLayoutMetrics(selectedPages);

        var probes = options.Probes
            .Select(probe =>
                BuildProbeDiagnostic(
                    probe,
                    selectedPages))
            .ToArray();

        return new PdfNativeAnalysisReport(
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
            extractionMetrics,
            layoutMetrics,
            probes);
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
                page.PhysicalPageNumber >= firstPage &&
                page.PhysicalPageNumber <= lastPage)
            .ToArray();
    }

    private static PdfRawLayoutMetrics
        BuildRawLayoutMetrics(
            IReadOnlyList<DocumentExtractionPage> pages)
    {
        var candidates =
            new List<PdfPageLayoutDiagnostic>();

        foreach (var page in pages)
        {
            var blocks = page.Blocks
                .Where(block =>
                    !string.IsNullOrWhiteSpace(block.Text))
                .OrderBy(block =>
                    block.ReadingOrder ??
                    int.MaxValue)
                .ThenBy(block =>
                    block.SourceSequence)
                .ToArray();

            var classified = blocks
                .Select(block =>
                    new ClassifiedBlock(
                        block,
                        ClassifyColumn(block)))
                .ToArray();

            var leftCount =
                classified.Count(item =>
                    item.Column == "L");

            var rightCount =
                classified.Count(item =>
                    item.Column == "R");

            if (leftCount < 2 ||
                rightCount < 2)
            {
                continue;
            }

            var narrow = classified
                .Where(item =>
                    item.Column is "L" or "R")
                .ToArray();

            var switchCount =
                CountColumnSwitches(narrow);

            var verticalReversalCount =
                CountVerticalReversals(
                    page.SourceHeight,
                    narrow,
                    "L") +
                CountVerticalReversals(
                    page.SourceHeight,
                    narrow,
                    "R");

            candidates.Add(
                new PdfPageLayoutDiagnostic(
                    page.PhysicalPageNumber,
                    switchCount,
                    verticalReversalCount,
                    switchCount > 1,
                    verticalReversalCount > 0));
        }

        return new PdfRawLayoutMetrics(
            candidates.Count,
            candidates.Count(page =>
                page.InterleavedColumns),
            candidates.Count(page =>
                page.HasVerticalReversal),
            candidates
                .Select(page =>
                    page.PhysicalPageNumber)
                .ToArray(),
            candidates
                .Where(page =>
                    page.InterleavedColumns)
                .Select(page =>
                    page.PhysicalPageNumber)
                .ToArray(),
            candidates
                .Where(page =>
                    page.HasVerticalReversal)
                .Select(page =>
                    page.PhysicalPageNumber)
                .ToArray());
    }

    private static string ClassifyColumn(
        DocumentTextBlock block)
    {
        var width =
            block.Bounds.Right -
            block.Bounds.Left;

        if (width >= 0.55)
        {
            return "W";
        }

        var center =
            (block.Bounds.Left +
             block.Bounds.Right) / 2.0;

        return center < 0.5
            ? "L"
            : "R";
    }

    private static int CountColumnSwitches(
        IReadOnlyList<ClassifiedBlock> blocks)
    {
        if (blocks.Count <= 1)
        {
            return 0;
        }

        var switches = 0;
        var previous =
            blocks[0].Column;

        for (var index = 1;
             index < blocks.Count;
             index++)
        {
            if (blocks[index].Column ==
                previous)
            {
                continue;
            }

            switches++;
            previous =
                blocks[index].Column;
        }

        return switches;
    }

    private static int CountVerticalReversals(
        double sourceHeight,
        IReadOnlyCollection<ClassifiedBlock> blocks,
        string column)
    {
        var normalizedTolerance =
            sourceHeight > 0
                ? Math.Max(
                    2.0 / sourceHeight,
                    0.03)
                : 0.03;

        double? previousTop = null;
        var reversals = 0;

        foreach (var item in blocks.Where(item =>
                     item.Column == column))
        {
            // ApologiaStudio used PdfPig's bottom-left coordinate system:
            // current.Top > previous.Top + tolerance means a move upward.
            // Core uses a normalized top-left origin, so the equivalent
            // comparison is inverted.
            if (previousTop is not null &&
                item.Block.Bounds.Top <
                previousTop.Value -
                normalizedTolerance)
            {
                reversals++;
            }

            previousTop =
                item.Block.Bounds.Top;
        }

        return reversals;
    }

    private static PdfProbeDiagnostic
        BuildProbeDiagnostic(
            string probe,
            IReadOnlyList<DocumentExtractionPage> pages)
    {
        var wordStreamPages = pages
            .Where(page =>
                string.Join(
                        ' ',
                        page.Words
                            .OrderBy(word =>
                                word.SourceSequence)
                            .Select(word =>
                                word.Text))
                    .Contains(
                        probe,
                        StringComparison.OrdinalIgnoreCase))
            .Select(page =>
                page.PhysicalPageNumber)
            .ToArray();

        var blockPages = pages
            .SelectMany(page =>
                page.Blocks
                    .Where(block =>
                        block.Text.Contains(
                            probe,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(_ =>
                        page.PhysicalPageNumber))
            .ToArray();

        return new PdfProbeDiagnostic(
            probe,
            wordStreamPages.Length,
            blockPages.Length,
            wordStreamPages,
            blockPages
                .Distinct()
                .OrderBy(pageNumber =>
                    pageNumber)
                .ToArray());
    }

    private static async Task<string>
        ComputeSha256Async(string sourcePath)
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
        PdfNativeAnalysisReport report)
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
        PdfNativeAnalysisReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: ANALYZED");

        Console.WriteLine(
            $"Source: {report.SourceFileName}");

        Console.WriteLine(
            $"Source SHA-256: {report.SourceSha256}");

        Console.WriteLine(
            $"Source bytes: {report.SourceByteLength}");

        Console.WriteLine(
            $"PDF pages total: {report.TotalPdfPages}");

        Console.WriteLine(
            $"PDF pages selected: " +
            $"{report.PageSelection.FirstPage}-" +
            $"{report.PageSelection.LastPage} " +
            $"({report.PageSelection.PageCount})");

        Console.WriteLine(
            $"Words: {report.Extraction.WordCount}");

        Console.WriteLine(
            $"Blocks: {report.Extraction.BlockCount}");

        Console.WriteLine(
            $"Text-layer coverage: " +
            $"{report.Extraction.TextLayerCoveragePercent:F1}% " +
            $"({report.Extraction.PagesWithWords}/" +
            $"{report.PageSelection.PageCount} selected pages)");

        Console.WriteLine(
            $"Textless pages with dominant raster image: " +
            $"{report.Extraction.TextlessPagesWithDominantRasterImage}");

        Console.WriteLine(
            $"Raw multi-column candidate pages: " +
            $"{report.RawLayout.MultiColumnCandidatePages}");

        Console.WriteLine(
            $"Raw interleaved multi-column pages: " +
            $"{report.RawLayout.InterleavedColumnPages}");

        Console.WriteLine(
            $"Raw vertical reading-order reversal pages: " +
            $"{report.RawLayout.VerticalReversalPages}");

        foreach (var probe in report.Probes)
        {
            Console.WriteLine(
                $"Probe '{probe.Probe}': " +
                $"{probe.WordStreamMatches} page-word-stream match(es), " +
                $"{probe.BlockMatches} block match(es)");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private static void WriteUsage()
    {
        Console.WriteLine(
            """
            Usage:
              dotnet run --project tools/DocumentProcessing.EvaluationCli -- analyze-pdf --source /absolute/path/document.pdf --report /absolute/path/report.json --pages 512-561 [--probe "text"]

              dotnet run --project tools/DocumentProcessing.EvaluationCli -- analyze-normalized-pdf --source /absolute/path/document.pdf --report /absolute/path/report.json --pages 512-561 [--probe "text"]
              dotnet run --project tools/DocumentProcessing.EvaluationCli -- analyze-segmented-pdf --source /absolute/path/document.pdf --report /absolute/path/report.json --pages 512-561 [--probe "text"]

            These commands are evaluation-only. They do not modify the PDF,
            persist document content, create retrieval chunks, or run OCR.
            """);
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
                        source = ReadValue(
                            args,
                            ref index,
                            option);
                        break;

                    case "--report":
                        report = ReadValue(
                            args,
                            ref index,
                            option);
                        break;

                    case "--pages":
                        pages = ReadValue(
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
            if (index + 1 >= args.Count)
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

    private sealed record PdfNativeAnalysisReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string SourceFileName,
        string SourceSha256,
        long SourceByteLength,
        int TotalPdfPages,
        PdfPageSelection PageSelection,
        PdfExtractionMetrics Extraction,
        PdfRawLayoutMetrics RawLayout,
        IReadOnlyList<PdfProbeDiagnostic> Probes);

    private sealed record PdfPageSelection(
        int FirstPage,
        int LastPage,
        int PageCount);

    private sealed record PdfExtractionMetrics(
        int WordCount,
        int BlockCount,
        int PagesWithWords,
        int PagesWithoutWords,
        int PagesWithoutBlocks,
        double TextLayerCoveragePercent,
        double DominantRasterImageAreaThreshold,
        int TextlessPagesWithDominantRasterImage);

    private sealed record PdfRawLayoutMetrics(
        int MultiColumnCandidatePages,
        int InterleavedColumnPages,
        int VerticalReversalPages,
        IReadOnlyList<int> MultiColumnCandidatePageNumbers,
        IReadOnlyList<int> InterleavedColumnPageNumbers,
        IReadOnlyList<int> VerticalReversalPageNumbers);

    private sealed record PdfPageLayoutDiagnostic(
        int PhysicalPageNumber,
        int ColumnSwitchCount,
        int VerticalReversalCount,
        bool InterleavedColumns,
        bool HasVerticalReversal);

    private sealed record PdfProbeDiagnostic(
        string Probe,
        int WordStreamMatches,
        int BlockMatches,
        IReadOnlyList<int> WordStreamPages,
        IReadOnlyList<int> BlockPages);

    private sealed record ClassifiedBlock(
        DocumentTextBlock Block,
        string Column);
}
