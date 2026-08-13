using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.EvaluationCli;

internal static class NormalizedPdfAnalysisCli
{
    private const string ReportSchemaVersion =
        "document-processing-normalized-pdf-analysis-v1";

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

    private static async Task<NormalizedPdfAnalysisReport>
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

        var extractor =
            new PdfPigDocumentExtractor();

        var extracted =
            await extractor.ExtractAsync(
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

        var extractionMetrics =
            BuildExtractionMetrics(
                selectedPages);

        var normalizationMetrics =
            BuildNormalizationMetrics(
                normalized.Pages);

        var layoutMetrics =
            BuildLayoutMetrics(
                normalized.Pages);

        var probes =
            options.Probes
                .Select(probe =>
                    BuildProbeDiagnostic(
                        probe,
                        selectedPages,
                        normalized.Pages))
                .ToArray();

        return new NormalizedPdfAnalysisReport(
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
            normalized.NormalizationProfileId,
            normalizationMetrics,
            layoutMetrics,
            probes);
    }

    private static PdfExtractionMetrics
        BuildExtractionMetrics(
            IReadOnlyCollection<DocumentExtractionPage> pages)
    {
        var pagesWithWords =
            pages.Count(page =>
                page.Words.Count > 0);

        return new PdfExtractionMetrics(
            pages.Sum(page =>
                page.Words.Count),
            pages.Sum(page =>
                page.Blocks.Count),
            pagesWithWords,
            pages.Count - pagesWithWords,
            pages.Count == 0
                ? 0
                : Math.Round(
                    pagesWithWords * 100.0 /
                    pages.Count,
                    1));
    }

    private static PdfNormalizationMetrics
        BuildNormalizationMetrics(
            IReadOnlyCollection<NormalizedDocumentPage> pages)
    {
        var blocks =
            pages.SelectMany(page =>
                    page.Blocks)
                .ToArray();

        return new PdfNormalizationMetrics(
            blocks.Length,
            blocks.Count(block =>
                !block.IsExcluded),
            blocks.Count(block =>
                block.ExclusionReason ==
                DocumentBlockExclusionReason.RepeatedHeader),
            blocks.Count(block =>
                block.ExclusionReason ==
                DocumentBlockExclusionReason.RepeatedFooter));
    }

    private static PdfLayoutMetrics
        BuildLayoutMetrics(
            IReadOnlyCollection<NormalizedDocumentPage> pages)
    {
        var candidates =
            new List<PdfPageLayoutDiagnostic>();

        foreach (var page in pages)
        {
            var blocks = page.Blocks
                .Where(block =>
                    !block.IsExcluded &&
                    !string.IsNullOrWhiteSpace(
                        block.Text))
                .OrderBy(block =>
                    block.SourceBlock.ReadingOrder ??
                    int.MaxValue)
                .ThenBy(block =>
                    block.SourceBlock.SourceSequence)
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

            var narrow =
                classified
                    .Where(item =>
                        item.Column is "L" or "R")
                    .ToArray();

            var switchCount =
                CountColumnSwitches(
                    narrow);

            var verticalReversalCount =
                CountVerticalReversals(
                    page.SourcePage.SourceHeight,
                    narrow,
                    "L") +
                CountVerticalReversals(
                    page.SourcePage.SourceHeight,
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

        return new PdfLayoutMetrics(
            candidates.Count,
            candidates.Count(page =>
                page.InterleavedColumns),
            candidates.Count(page =>
                page.HasVerticalReversal));
    }

    private static string ClassifyColumn(
        NormalizedDocumentTextBlock block)
    {
        var bounds =
            block.SourceBlock.Bounds;

        var width =
            bounds.Right -
            bounds.Left;

        if (width >= 0.55)
        {
            return "W";
        }

        var center =
            (bounds.Left +
             bounds.Right) / 2.0;

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
            if (previousTop is not null &&
                item.Block.SourceBlock.Bounds.Top <
                previousTop.Value -
                normalizedTolerance)
            {
                reversals++;
            }

            previousTop =
                item.Block.SourceBlock.Bounds.Top;
        }

        return reversals;
    }

    private static PdfProbeDiagnostic
        BuildProbeDiagnostic(
            string probe,
            IReadOnlyCollection<DocumentExtractionPage> sourcePages,
            IReadOnlyCollection<NormalizedDocumentPage> normalizedPages)
    {
        var wordStreamPages =
            sourcePages
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

        var blockPages =
            normalizedPages
                .SelectMany(page =>
                    page.Blocks
                        .Where(block =>
                            !block.IsExcluded &&
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
        NormalizedPdfAnalysisReport report)
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
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void WriteSummary(
        NormalizedPdfAnalysisReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: NORMALIZED");

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
            $"Words: {report.Extraction.WordCount}");

        Console.WriteLine(
            $"Blocks: {report.Extraction.BlockCount}");

        Console.WriteLine(
            $"Normalization profile: " +
            $"{report.NormalizationProfileId}");

        Console.WriteLine(
            $"Included blocks: " +
            $"{report.Normalization.IncludedBlocks}");

        Console.WriteLine(
            $"Excluded recurring headers: " +
            $"{report.Normalization.ExcludedHeaderBlocks}");

        Console.WriteLine(
            $"Excluded recurring footers: " +
            $"{report.Normalization.ExcludedFooterBlocks}");

        Console.WriteLine(
            $"Multi-column candidate pages: " +
            $"{report.Layout.MultiColumnCandidatePages}");

        Console.WriteLine(
            $"Interleaved multi-column pages: " +
            $"{report.Layout.InterleavedColumnPages}");

        Console.WriteLine(
            $"Vertical reading-order reversal pages: " +
            $"{report.Layout.VerticalReversalPages}");

        foreach (var probe in report.Probes)
        {
            Console.WriteLine(
                $"Probe '{probe.Probe}': " +
                $"{probe.WordStreamMatches} page-word-stream match(es), " +
                $"{probe.BlockMatches} normalized block match(es)");
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

    private sealed record NormalizedPdfAnalysisReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string SourceFileName,
        string SourceSha256,
        long SourceByteLength,
        int TotalPdfPages,
        PdfPageSelection PageSelection,
        PdfExtractionMetrics Extraction,
        string NormalizationProfileId,
        PdfNormalizationMetrics Normalization,
        PdfLayoutMetrics Layout,
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
        double TextLayerCoveragePercent);

    private sealed record PdfNormalizationMetrics(
        int BlockCount,
        int IncludedBlocks,
        int ExcludedHeaderBlocks,
        int ExcludedFooterBlocks);

    private sealed record PdfLayoutMetrics(
        int MultiColumnCandidatePages,
        int InterleavedColumnPages,
        int VerticalReversalPages);

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
        NormalizedDocumentTextBlock Block,
        string Column);
}
