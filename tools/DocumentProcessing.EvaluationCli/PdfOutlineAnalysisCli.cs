using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Segmentation;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Outline;

namespace DocumentProcessing.EvaluationCli;

/// <summary>
/// Evaluation-only inspection of PDF outline/bookmark evidence.
///
/// This diagnostic reads the complete native PDF outline, then compares
/// document-local destinations that fall inside the selected evaluation range
/// against normalized blocks and the current production heading boundaries.
///
/// It does not promote outline entries to production structure.
/// </summary>
internal static class PdfOutlineAnalysisCli
{
    private const string ReportSchemaVersion =
        "document-processing-pdf-outline-analysis-v1";

    private const int ConsoleOutlineSampleLimit = 40;
    private const int ConsoleDiagnosticSampleLimit = 20;
    private const int CandidateLimit = 3;

    private static readonly Regex WhitespaceRegex =
        new(
            @"\s+",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly Regex TokenRegex =
        new(
            @"[\p{L}\p{Nd}]+",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    public static async Task<int> RunAsync(
        string[] args)
    {
        var options =
            AnalysisOptions.Parse(
                args);

        var report =
            await AnalyzeAsync(
                options);

        await WriteReportAsync(
            options.ReportPath,
            report);

        WriteSummary(
            report,
            options.ReportPath);

        return 0;
    }

    private static async Task<PdfOutlineAnalysisReport>
        AnalyzeAsync(
            AnalysisOptions options)
    {
        var sourcePath =
            Path.GetFullPath(
                options.SourcePath);

        var fileInfo =
            new FileInfo(
                sourcePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "PDF source was not found.",
                sourcePath);
        }

        var sourceSha256 =
            await ComputeSha256Async(
                sourcePath);

        var outlineRead =
            ReadOutline(
                sourcePath);

        await using var sourceStream =
            File.OpenRead(
                sourcePath);

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

        if (outlineRead.TotalPdfPages !=
            extracted.Pages.Count)
        {
            throw new InvalidDataException(
                "PdfPig outline page count differs from native extraction page count.");
        }

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

        var blockContexts =
            BuildBlockContexts(
                normalized.Pages);

        var blocksByPage =
            blockContexts
                .GroupBy(context =>
                    context.Key.PhysicalPageNumber)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        (IReadOnlyList<BlockContext>)
                        group
                            .OrderBy(context =>
                                context.Key.SourceSequence)
                            .ToArray());

        var productionHeadingKeys =
            BuildProductionHeadingKeys(
                segmented.Segments,
                blockContexts);

        var observations =
            outlineRead.Entries
                .Select(entry =>
                    BuildEntryObservation(
                        entry,
                        options.FirstPage,
                        options.LastPage,
                        blocksByPage,
                        productionHeadingKeys))
                .ToArray();

        var selectedInternal =
            observations
                .Where(entry =>
                    entry.IsInternalDocumentDestination &&
                    entry.IsTargetInSelectedRange)
                .ToArray();

        var matched =
            selectedInternal
                .Where(entry =>
                    entry.Match is not null)
                .ToArray();

        var supportedProductionHeadingKeys =
            matched
                .Where(entry =>
                    entry.Match!.IsProductionHeading)
                .Select(entry =>
                    new BlockKey(
                        entry.Match!.PhysicalPageNumber,
                        entry.Match.SourceSequence))
                .ToHashSet();

        var matchSummary =
            new OutlineMatchSummary(
                selectedInternal.Length,
                selectedInternal.Count(entry =>
                    entry.Match?.Kind ==
                    OutlineMatchKind.ExactText),
                selectedInternal.Count(entry =>
                    entry.Match?.Kind ==
                    OutlineMatchKind.NormalizedText),
                selectedInternal.Count(entry =>
                    entry.Match?.Kind ==
                    OutlineMatchKind.CompactText),
                selectedInternal.Count(entry =>
                    entry.Match is null),
                matched.Count(entry =>
                    entry.Match!.IsProductionHeading),
                matched.Count(entry =>
                    !entry.Match!.IsProductionHeading),
                supportedProductionHeadingKeys.Count,
                productionHeadingKeys.Count -
                supportedProductionHeadingKeys.Count);

        var outlineSummary =
            new OutlineSummary(
                outlineRead.HasOutline,
                outlineRead.RootCount,
                observations.Length,
                observations.Length == 0
                    ? 0
                    : observations.Max(entry =>
                        entry.Level),
                observations.Count(entry =>
                    entry.IsInternalDocumentDestination),
                observations.Count(entry =>
                    !entry.IsInternalDocumentDestination),
                observations.Count(entry =>
                    entry.IsInternalDocumentDestination &&
                    entry.TargetPageNumber is <= 0),
                observations.Count(entry =>
                    entry.IsInternalDocumentDestination &&
                    entry.HasCoordinates),
                observations.Count(entry =>
                    entry.IsInternalDocumentDestination &&
                    entry.IsTargetInSelectedRange));

        return new PdfOutlineAnalysisReport(
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
            outlineSummary,
            new ProductionHeadingSummary(
                productionHeadingKeys.Count),
            matchSummary,
            observations);
    }

    private static OutlineReadResult ReadOutline(
        string sourcePath)
    {
        using var document =
            PdfDocument.Open(
                sourcePath);

        if (!document.TryGetBookmarks(
                out var bookmarks,
                allowContainerNode: true))
        {
            return new OutlineReadResult(
                document.NumberOfPages,
                false,
                0,
                Array.Empty<RawOutlineEntry>());
        }

        var entries =
            new List<RawOutlineEntry>();

        var ordinal = 0;

        foreach (var root in bookmarks.Roots)
        {
            AddNode(
                root,
                parentOrdinal: null,
                entries,
                ref ordinal);
        }

        return new OutlineReadResult(
            document.NumberOfPages,
            true,
            bookmarks.Roots.Count,
            entries);
    }

    private static void AddNode(
        BookmarkNode node,
        int? parentOrdinal,
        ICollection<RawOutlineEntry> entries,
        ref int ordinal)
    {
        var currentOrdinal =
            ordinal++;

        var documentNode =
            node as DocumentBookmarkNode;

        var coordinates =
            documentNode?
                .Destination
                .Coordinates;

        entries.Add(
            new RawOutlineEntry(
                currentOrdinal,
                parentOrdinal,
                node.Level,
                node.Title,
                node.GetType().Name,
                documentNode is not null,
                documentNode?.PageNumber,
                documentNode?
                    .Destination
                    .Type
                    .ToString(),
                coordinates?.Left,
                coordinates?.Top,
                coordinates?.Right,
                coordinates?.Bottom));

        foreach (var child in node.Children)
        {
            AddNode(
                child,
                currentOrdinal,
                entries,
                ref ordinal);
        }
    }

    private static OutlineEntryObservation
        BuildEntryObservation(
            RawOutlineEntry entry,
            int firstPage,
            int lastPage,
            IReadOnlyDictionary<
                int,
                IReadOnlyList<BlockContext>> blocksByPage,
            IReadOnlySet<BlockKey> productionHeadingKeys)
    {
        var inSelectedRange =
            entry.IsInternalDocumentDestination &&
            entry.TargetPageNumber is >= 1 &&
            entry.TargetPageNumber >=
            firstPage &&
            entry.TargetPageNumber <=
            lastPage;

        OutlineBlockMatch? match =
            null;

        IReadOnlyList<OutlineCandidateBlock>
            candidates =
                Array.Empty<OutlineCandidateBlock>();

        if (inSelectedRange &&
            entry.TargetPageNumber is int pageNumber &&
            blocksByPage.TryGetValue(
                pageNumber,
                out var pageBlocks))
        {
            match =
                FindDeterministicMatch(
                    entry.Title,
                    pageBlocks,
                    productionHeadingKeys);

            if (match is null)
            {
                candidates =
                    FindLexicalCandidates(
                        entry.Title,
                        pageBlocks,
                        productionHeadingKeys);
            }
        }

        return new OutlineEntryObservation(
            entry.Ordinal,
            entry.ParentOrdinal,
            entry.Level,
            entry.Title,
            entry.NodeType,
            entry.IsInternalDocumentDestination,
            entry.TargetPageNumber,
            entry.DestinationType,
            entry.Left,
            entry.Top,
            entry.Right,
            entry.Bottom,
            entry.Left.HasValue ||
            entry.Top.HasValue ||
            entry.Right.HasValue ||
            entry.Bottom.HasValue,
            inSelectedRange,
            match,
            candidates);
    }

    private static OutlineBlockMatch?
        FindDeterministicMatch(
            string outlineTitle,
            IReadOnlyList<BlockContext> blocks,
            IReadOnlySet<BlockKey> productionHeadingKeys)
    {
        var trimmedTitle =
            outlineTitle.Trim();

        var exact =
            blocks
                .Where(context =>
                    string.Equals(
                        context.Block.Text.Trim(),
                        trimmedTitle,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(context =>
                    context.Key.SourceSequence)
                .FirstOrDefault();

        if (exact is not null)
        {
            return ToMatch(
                OutlineMatchKind.ExactText,
                exact,
                productionHeadingKeys);
        }

        var normalizedTitle =
            NormalizeHeadingKey(
                outlineTitle);

        if (normalizedTitle.Length > 0)
        {
            var normalized =
                blocks
                    .Where(context =>
                        string.Equals(
                            NormalizeHeadingKey(
                                context.Block.Text),
                            normalizedTitle,
                            StringComparison.Ordinal))
                    .OrderBy(context =>
                        context.Key.SourceSequence)
                    .FirstOrDefault();

            if (normalized is not null)
            {
                return ToMatch(
                    OutlineMatchKind.NormalizedText,
                    normalized,
                    productionHeadingKeys);
            }
        }

        var compactTitle =
            CompactHeadingKey(
                outlineTitle);

        if (compactTitle.Length > 0)
        {
            var compact =
                blocks
                    .Where(context =>
                        string.Equals(
                            CompactHeadingKey(
                                context.Block.Text),
                            compactTitle,
                            StringComparison.Ordinal))
                    .OrderBy(context =>
                        context.Key.SourceSequence)
                    .FirstOrDefault();

            if (compact is not null)
            {
                return ToMatch(
                    OutlineMatchKind.CompactText,
                    compact,
                    productionHeadingKeys);
            }
        }

        return null;
    }

    private static OutlineBlockMatch ToMatch(
        OutlineMatchKind kind,
        BlockContext context,
        IReadOnlySet<BlockKey> productionHeadingKeys) =>
        new(
            kind,
            context.Key.PhysicalPageNumber,
            context.Key.SourceSequence,
            context.Block.Text,
            productionHeadingKeys.Contains(
                context.Key));

    private static IReadOnlyList<OutlineCandidateBlock>
        FindLexicalCandidates(
            string outlineTitle,
            IReadOnlyList<BlockContext> blocks,
            IReadOnlySet<BlockKey> productionHeadingKeys)
    {
        var titleTokens =
            Tokenize(
                outlineTitle);

        if (titleTokens.Count == 0)
        {
            return Array.Empty<OutlineCandidateBlock>();
        }

        return blocks
            .Select(context =>
            {
                var blockTokens =
                    Tokenize(
                        context.Block.Text);

                var shared =
                    titleTokens
                        .Intersect(
                            blockTokens,
                            StringComparer.Ordinal)
                        .Count();

                var titleCoverage =
                    shared /
                    (double)titleTokens.Count;

                return new OutlineCandidateBlock(
                    context.Key.PhysicalPageNumber,
                    context.Key.SourceSequence,
                    context.Block.Text,
                    productionHeadingKeys.Contains(
                        context.Key),
                    shared,
                    Math.Round(
                        titleCoverage,
                        3));
            })
            .Where(candidate =>
                candidate.SharedTokenCount > 0)
            .OrderByDescending(candidate =>
                candidate.TitleTokenCoverage)
            .ThenByDescending(candidate =>
                candidate.SharedTokenCount)
            .ThenByDescending(candidate =>
                candidate.IsProductionHeading)
            .ThenBy(candidate =>
                candidate.SourceSequence)
            .Take(
                CandidateLimit)
            .ToArray();
    }

    private static HashSet<string> Tokenize(
        string text) =>
        TokenRegex
            .Matches(
                text.ToUpperInvariant())
            .Select(match =>
                match.Value)
            .Where(token =>
                token.Length >= 2)
            .ToHashSet(
                StringComparer.Ordinal);

    private static string NormalizeHeadingKey(
        string heading)
    {
        var normalized =
            WhitespaceRegex
                .Replace(
                    heading,
                    " ")
                .Trim();

        var start = 0;

        while (start <
               normalized.Length &&
               !char.IsLetterOrDigit(
                   normalized[start]))
        {
            start++;
        }

        var end =
            normalized.Length -
            1;

        while (end >= start &&
               !char.IsLetterOrDigit(
                   normalized[end]))
        {
            end--;
        }

        if (start > end)
        {
            return string.Empty;
        }

        return normalized[
                start..(end + 1)]
            .ToUpperInvariant();
    }

    private static string CompactHeadingKey(
        string heading) =>
        new(
            heading
                .Where(
                    char.IsLetterOrDigit)
                .Select(
                    char.ToUpperInvariant)
                .ToArray());

    private static IReadOnlySet<BlockKey>
        BuildProductionHeadingKeys(
            IReadOnlyList<
                DocumentProcessing.Core.Segmentation.DocumentSegment> segments,
            IReadOnlyList<BlockContext> contexts)
    {
        var keyByBlock =
            new Dictionary<
                NormalizedDocumentTextBlock,
                BlockKey>(
                ReferenceEqualityComparer.Instance);

        foreach (var context in contexts)
        {
            keyByBlock.Add(
                context.Block,
                context.Key);
        }

        return segments
            .Where(segment =>
                segment.HeadingText is not null)
            .Select(segment =>
                keyByBlock[
                    segment.SourceBlocks[0]])
            .ToHashSet();
    }

    private static IReadOnlyList<BlockContext>
        BuildBlockContexts(
            IReadOnlyList<NormalizedDocumentPage> pages)
    {
        var contexts =
            new List<BlockContext>();

        foreach (var page in pages)
        {
            foreach (var block in page.Blocks
                         .Where(block =>
                             !block.IsExcluded &&
                             !string.IsNullOrWhiteSpace(
                                 block.Text))
                         .OrderBy(block =>
                             block.SourceBlock
                                 .SourceSequence))
            {
                contexts.Add(
                    new BlockContext(
                        new BlockKey(
                            page.PhysicalPageNumber,
                            block.SourceBlock
                                .SourceSequence),
                        block));
            }
        }

        return contexts;
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
            File.OpenRead(
                sourcePath);

        using var sha256 =
            SHA256.Create();

        var hash =
            await sha256.ComputeHashAsync(
                stream);

        return Convert
            .ToHexString(
                hash)
            .ToLowerInvariant();
    }

    private static async Task WriteReportAsync(
        string reportPath,
        PdfOutlineAnalysisReport report)
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
        PdfOutlineAnalysisReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: PDF OUTLINE ANALYZED");

        Console.WriteLine(
            $"Source: {report.SourceFileName}");

        Console.WriteLine(
            $"Source SHA-256: {report.SourceSha256}");

        Console.WriteLine(
            $"PDF pages total: {report.TotalPdfPages}");

        Console.WriteLine(
            $"Comparison pages: " +
            $"{report.PageSelection.FirstPage}-" +
            $"{report.PageSelection.LastPage} " +
            $"({report.PageSelection.PageCount})");

        Console.WriteLine(
            $"Outline present: {report.Outline.HasOutline}");

        Console.WriteLine(
            $"Outline roots / entries / max level: " +
            $"{report.Outline.RootCount} / " +
            $"{report.Outline.EntryCount} / " +
            $"{report.Outline.MaximumLevel}");

        Console.WriteLine(
            $"Internal / non-internal entries: " +
            $"{report.Outline.InternalDocumentEntryCount} / " +
            $"{report.Outline.NonInternalEntryCount}");

        Console.WriteLine(
            $"Internal destinations with coordinates: " +
            $"{report.Outline.InternalEntriesWithCoordinates}");

        Console.WriteLine(
            $"Internal destinations with invalid page: " +
            $"{report.Outline.InternalEntriesWithInvalidPage}");

        Console.WriteLine(
            $"Internal outline entries in comparison range: " +
            $"{report.Matches.SelectedInternalEntryCount}");

        Console.WriteLine(
            $"Matches exact / normalized / compact / unmatched: " +
            $"{report.Matches.ExactTextMatchCount} / " +
            $"{report.Matches.NormalizedTextMatchCount} / " +
            $"{report.Matches.CompactTextMatchCount} / " +
            $"{report.Matches.UnmatchedCount}");

        Console.WriteLine(
            $"Matched outline entries already production headings / outline-only: " +
            $"{report.Matches.MatchedProductionHeadingEntryCount} / " +
            $"{report.Matches.OutlineOnlyMatchedEntryCount}");

        Console.WriteLine(
            $"Production headings supported by outline / unsupported: " +
            $"{report.Matches.SupportedProductionHeadingCount} / " +
            $"{report.Matches.UnsupportedProductionHeadingCount}");

        Console.WriteLine(
            "Outline tree sample:");

        foreach (var entry in report.Entries
                     .Take(
                         ConsoleOutlineSampleLimit))
        {
            var indent =
                new string(
                    ' ',
                    Math.Max(
                        0,
                        entry.Level) * 2);

            var target =
                entry.TargetPageNumber.HasValue
                    ? $" -> p{entry.TargetPageNumber}"
                    : string.Empty;

            Console.WriteLine(
                $"  {indent}[{entry.Ordinal}] " +
                $"{entry.Title}{target} " +
                $"({entry.NodeType})");
        }

        Console.WriteLine(
            "Outline-only matched structural candidates:");

        foreach (var entry in report.Entries
                     .Where(entry =>
                         entry.IsTargetInSelectedRange &&
                         entry.Match is not null &&
                         !entry.Match.IsProductionHeading)
                     .Take(
                         ConsoleDiagnosticSampleLimit))
        {
            Console.WriteLine(
                $"  outline p{entry.TargetPageNumber} " +
                $"L{entry.Level} '{entry.Title}'");

            Console.WriteLine(
                $"      {entry.Match!.Kind} -> " +
                $"block #{entry.Match.SourceSequence}: " +
                $"{Truncate(entry.Match.BlockText)}");
        }

        Console.WriteLine(
            "Unmatched outline entries with best same-page lexical candidates:");

        foreach (var entry in report.Entries
                     .Where(entry =>
                         entry.IsTargetInSelectedRange &&
                         entry.Match is null)
                     .Take(
                         ConsoleDiagnosticSampleLimit))
        {
            Console.WriteLine(
                $"  outline p{entry.TargetPageNumber} " +
                $"L{entry.Level} '{entry.Title}'");

            foreach (var candidate in entry.Candidates)
            {
                Console.WriteLine(
                    $"      coverage={candidate.TitleTokenCoverage:F3} " +
                    $"shared={candidate.SharedTokenCount} " +
                    $"heading={candidate.IsProductionHeading} " +
                    $"block #{candidate.SourceSequence}: " +
                    $"{Truncate(candidate.BlockText)}");
            }
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private static string Truncate(
        string text)
    {
        const int maximumLength = 180;

        var normalized =
            WhitespaceRegex
                .Replace(
                    text,
                    " ")
                .Trim();

        return normalized.Length <=
               maximumLength
            ? normalized
            : normalized[
                  ..maximumLength] +
              "…";
    }

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
                Path.GetFullPath(
                    source),
                Path.GetFullPath(
                    report),
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

    private sealed record OutlineReadResult(
        int TotalPdfPages,
        bool HasOutline,
        int RootCount,
        IReadOnlyList<RawOutlineEntry> Entries);

    private sealed record RawOutlineEntry(
        int Ordinal,
        int? ParentOrdinal,
        int Level,
        string Title,
        string NodeType,
        bool IsInternalDocumentDestination,
        int? TargetPageNumber,
        string? DestinationType,
        double? Left,
        double? Top,
        double? Right,
        double? Bottom);

    private sealed record BlockContext(
        BlockKey Key,
        NormalizedDocumentTextBlock Block);

    private readonly record struct BlockKey(
        int PhysicalPageNumber,
        int SourceSequence);

    private enum OutlineMatchKind
    {
        ExactText,
        NormalizedText,
        CompactText
    }

    private sealed record PdfOutlineAnalysisReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string SourceFileName,
        string SourceSha256,
        long SourceByteLength,
        int TotalPdfPages,
        PdfPageSelection PageSelection,
        string NormalizationProfileId,
        string SegmentationProfileId,
        OutlineSummary Outline,
        ProductionHeadingSummary Production,
        OutlineMatchSummary Matches,
        IReadOnlyList<OutlineEntryObservation> Entries);

    private sealed record PdfPageSelection(
        int FirstPage,
        int LastPage,
        int PageCount);

    private sealed record OutlineSummary(
        bool HasOutline,
        int RootCount,
        int EntryCount,
        int MaximumLevel,
        int InternalDocumentEntryCount,
        int NonInternalEntryCount,
        int InternalEntriesWithInvalidPage,
        int InternalEntriesWithCoordinates,
        int InternalEntriesInSelectedRange);

    private sealed record ProductionHeadingSummary(
        int HeadingCount);

    private sealed record OutlineMatchSummary(
        int SelectedInternalEntryCount,
        int ExactTextMatchCount,
        int NormalizedTextMatchCount,
        int CompactTextMatchCount,
        int UnmatchedCount,
        int MatchedProductionHeadingEntryCount,
        int OutlineOnlyMatchedEntryCount,
        int SupportedProductionHeadingCount,
        int UnsupportedProductionHeadingCount);

    private sealed record OutlineEntryObservation(
        int Ordinal,
        int? ParentOrdinal,
        int Level,
        string Title,
        string NodeType,
        bool IsInternalDocumentDestination,
        int? TargetPageNumber,
        string? DestinationType,
        double? Left,
        double? Top,
        double? Right,
        double? Bottom,
        bool HasCoordinates,
        bool IsTargetInSelectedRange,
        OutlineBlockMatch? Match,
        IReadOnlyList<OutlineCandidateBlock> Candidates);

    private sealed record OutlineBlockMatch(
        OutlineMatchKind Kind,
        int PhysicalPageNumber,
        int SourceSequence,
        string BlockText,
        bool IsProductionHeading);

    private sealed record OutlineCandidateBlock(
        int PhysicalPageNumber,
        int SourceSequence,
        string BlockText,
        bool IsProductionHeading,
        int SharedTokenCount,
        double TitleTokenCoverage);
}
