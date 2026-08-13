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
/// Evaluation-only study of native PDF outline-to-content alignment.
///
/// The PDF bookmark destination remains authoritative only as an observation.
/// This diagnostic does not promote an outline entry to a production heading.
/// </summary>
internal static class PdfOutlineAlignmentAnalysisCli
{
    private const string ReportSchemaVersion =
        "document-processing-pdf-outline-alignment-analysis-v1";

    private const int MaximumClusterSize = 3;
    private const int NearbyPageRadius = 2;
    private const int MaximumCandidatesPerEntry = 5;
    private const int ConsoleSampleLimit = 30;
    private const int MinimumContainmentCompactLength = 8;
    private const double MinimumContainmentLengthRatio = 0.20;
    private const double DominantRasterImageAreaRatio = 0.60;

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

    private static readonly Regex LeadingNumberRegex =
        new(
            @"^\s*(?:(?:chapter|part|section|book|excursus)\s+)?(?<number>\d{1,4})\b",
            RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase |
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

    private static async Task<PdfOutlineAlignmentReport>
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

        var extractionPageByNumber =
            extracted.Pages
                .ToDictionary(
                    page =>
                        page.PhysicalPageNumber);

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
                                context.Block.SourceBlock
                                    .ReadingOrder ??
                                int.MaxValue)
                            .ThenBy(context =>
                                context.Key.SourceSequence)
                            .ToArray());

        var productionHeadingKeys =
            BuildProductionHeadingKeys(
                segmented.Segments,
                blockContexts);

        var selectedOutlineEntries =
            outlineRead.Entries
                .Where(entry =>
                    entry.IsInternalDocumentDestination &&
                    entry.TargetPageNumber is >= 1 &&
                    entry.TargetPageNumber >=
                    options.FirstPage &&
                    entry.TargetPageNumber <=
                    options.LastPage)
                .ToArray();

        var observations =
            selectedOutlineEntries
                .Select(entry =>
                    AnalyzeEntry(
                        entry,
                        options.FirstPage,
                        options.LastPage,
                        extractionPageByNumber,
                        blocksByPage,
                        productionHeadingKeys))
                .ToArray();

        var bandCounts =
            Enum.GetValues<
                    DiagnosticAlignmentBand>()
                .ToDictionary(
                    band =>
                        band,
                    band =>
                        observations.Count(entry =>
                            entry.BestCandidate?.Band ==
                            band));

        var numericRelationCounts =
            Enum.GetValues<
                    NumericLabelRelation>()
                .ToDictionary(
                    relation =>
                        relation,
                    relation =>
                        observations.Count(entry =>
                            entry.BestCandidate?
                                .NumericLabelRelation ==
                            relation));

        var summary =
            new AlignmentSummary(
                selectedOutlineEntries.Length,
                observations.Count(entry =>
                    entry.TargetPage.WordCount > 0),
                observations.Count(entry =>
                    entry.TargetPage.BlockCount > 0),
                observations.Count(entry =>
                    entry.TargetPage.IsTextlessDominantRaster),
                observations.Count(entry =>
                    entry.Destination.NormalizedTop.HasValue),
                observations.Count(entry =>
                    entry.Destination.NormalizedLeft.HasValue),
                observations.Count(entry =>
                    entry.BestCandidate is not null),
                observations.Count(entry =>
                    entry.BestCandidate is not null &&
                    IsPlausibleAlignment(
                        entry.BestCandidate.Band)),
                observations.Count(entry =>
                    entry.BestCandidate is not null &&
                    !IsPlausibleAlignment(
                        entry.BestCandidate.Band)),
                observations.Count(entry =>
                    entry.BestCandidate is null),
                observations.Count(entry =>
                    entry.BestCandidate?
                        .PageOffset ==
                    0),
                observations.Count(entry =>
                    entry.BestCandidate is not null &&
                    entry.BestCandidate.PageOffset !=
                    0),
                observations.Count(entry =>
                    entry.BestCandidate is not null &&
                    IsPlausibleAlignment(
                        entry.BestCandidate.Band) &&
                    entry.BestCandidate.PageOffset ==
                    0),
                observations.Count(entry =>
                    entry.BestCandidate is not null &&
                    IsPlausibleAlignment(
                        entry.BestCandidate.Band) &&
                    entry.BestCandidate.PageOffset !=
                    0),
                observations.Count(entry =>
                    entry.BestCandidate?
                        .IsProductionHeading ==
                    true),
                observations.Count(entry =>
                    entry.BestCandidate is not null &&
                    !entry.BestCandidate
                        .IsProductionHeading),
                observations.Count(entry =>
                    entry.BestCandidate is not null &&
                    IsPlausibleAlignment(
                        entry.BestCandidate.Band) &&
                    entry.BestCandidate
                        .IsProductionHeading),
                observations.Count(entry =>
                    entry.BestCandidate is not null &&
                    IsPlausibleAlignment(
                        entry.BestCandidate.Band) &&
                    !entry.BestCandidate
                        .IsProductionHeading),
                bandCounts,
                numericRelationCounts);

        return new PdfOutlineAlignmentReport(
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
            outlineRead.RootCount,
            outlineRead.Entries.Count,
            productionHeadingKeys.Count,
            summary,
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

    private static OutlineAlignmentObservation
        AnalyzeEntry(
            RawOutlineEntry entry,
            int firstPage,
            int lastPage,
            IReadOnlyDictionary<
                int,
                DocumentExtractionPage> extractionPageByNumber,
            IReadOnlyDictionary<
                int,
                IReadOnlyList<BlockContext>> blocksByPage,
            IReadOnlySet<BlockKey> productionHeadingKeys)
    {
        if (entry.TargetPageNumber is not int targetPage ||
            !extractionPageByNumber.TryGetValue(
                targetPage,
                out var extractionPage))
        {
            throw new InvalidDataException(
                $"Outline entry {entry.Ordinal} has an unresolved target page.");
        }

        var destination =
            BuildDestinationObservation(
                entry,
                extractionPage);

        var targetPageObservation =
            new TargetPageObservation(
                extractionPage.WordCount,
                extractionPage.Blocks.Count,
                extractionPage.LargestRasterImageAreaRatio,
                extractionPage.WordCount == 0 &&
                extractionPage.LargestRasterImageAreaRatio >=
                DominantRasterImageAreaRatio);

        var candidates =
            new List<AlignmentCandidate>();

        var minimumPage =
            Math.Max(
                firstPage,
                targetPage -
                NearbyPageRadius);

        var maximumPage =
            Math.Min(
                lastPage,
                targetPage +
                NearbyPageRadius);

        for (var pageNumber = minimumPage;
             pageNumber <= maximumPage;
             pageNumber++)
        {
            if (!blocksByPage.TryGetValue(
                    pageNumber,
                    out var pageBlocks) ||
                pageBlocks.Count == 0)
            {
                continue;
            }

            AddCandidates(
                entry,
                targetPage,
                pageNumber,
                pageBlocks,
                destination,
                productionHeadingKeys,
                candidates);
        }

        var ranked =
            candidates
                .OrderByDescending(candidate =>
                    GetBandRank(
                        candidate.Band))
                .ThenByDescending(candidate =>
                    candidate.OutlineTokenCoverage)
                .ThenByDescending(candidate =>
                    candidate.CandidateTokenCoverage)
                .ThenByDescending(candidate =>
                    candidate.SharedTokenCount)
                .ThenBy(candidate =>
                    Math.Abs(
                        candidate.PageOffset))
                .ThenBy(candidate =>
                    candidate.PageOffset)
                .ThenBy(candidate =>
                    candidate.DestinationTopDistance ??
                    double.MaxValue)
                .ThenBy(candidate =>
                    candidate.ClusterSize)
                .ThenBy(candidate =>
                    candidate.FirstSourceSequence)
                .Take(
                    MaximumCandidatesPerEntry)
                .ToArray();

        return new OutlineAlignmentObservation(
            entry.Ordinal,
            entry.ParentOrdinal,
            entry.Level,
            entry.Title,
            targetPage,
            entry.DestinationType,
            destination,
            targetPageObservation,
            ranked.FirstOrDefault(),
            ranked);
    }

    private static DestinationObservation
        BuildDestinationObservation(
            RawOutlineEntry entry,
            DocumentExtractionPage page)
    {
        double? normalizedLeft =
            null;

        double? normalizedTop =
            null;

        if (entry.Left is double left &&
            double.IsFinite(left) &&
            page.SourceWidth > 0)
        {
            normalizedLeft =
                left /
                page.SourceWidth;
        }

        if (entry.Top is double top &&
            double.IsFinite(top) &&
            page.SourceHeight > 0)
        {
            // PdfPig's extracted layout uses a bottom-left PDF origin.
            // Production blocks are normalized to a top-left origin.
            normalizedTop =
                1 -
                top /
                page.SourceHeight;
        }

        return new DestinationObservation(
            entry.Left,
            entry.Top,
            entry.Right,
            entry.Bottom,
            normalizedLeft,
            normalizedTop);
    }

    private static void AddCandidates(
        RawOutlineEntry entry,
        int targetPage,
        int candidatePage,
        IReadOnlyList<BlockContext> blocks,
        DestinationObservation destination,
        IReadOnlySet<BlockKey> productionHeadingKeys,
        ICollection<AlignmentCandidate> candidates)
    {
        for (var start = 0;
             start < blocks.Count;
             start++)
        {
            for (var clusterSize = 1;
                 clusterSize <=
                 MaximumClusterSize &&
                 start + clusterSize <=
                 blocks.Count;
                 clusterSize++)
            {
                var cluster =
                    blocks
                        .Skip(
                            start)
                        .Take(
                            clusterSize)
                        .ToArray();

                var candidateText =
                    string.Join(
                        " ",
                        cluster.Select(context =>
                            context.Block.Text));

                var metrics =
                    BuildLexicalMetrics(
                        entry.Title,
                        candidateText);

                if (metrics.SharedTokenCount == 0 &&
                    metrics.Containment ==
                    TextContainmentRelation.None)
                {
                    continue;
                }

                var first =
                    cluster[0];

                var bounds =
                    CombineBounds(
                        cluster);

                var topDistance =
                    candidatePage ==
                    targetPage
                        ? DistanceToVerticalRange(
                            destination.NormalizedTop,
                            bounds.Top,
                            bounds.Bottom)
                        : null;

                var pointDistance =
                    candidatePage ==
                    targetPage
                        ? DistanceToRectangle(
                            destination.NormalizedLeft,
                            destination.NormalizedTop,
                            bounds.Left,
                            bounds.Top,
                            bounds.Right,
                            bounds.Bottom)
                        : null;

                var productionHeading =
                    cluster.Any(context =>
                        productionHeadingKeys.Contains(
                            context.Key));

                var numericRelation =
                    CompareNumericLabels(
                        entry.Title,
                        candidateText);

                candidates.Add(
                    new AlignmentCandidate(
                        candidatePage,
                        candidatePage -
                        targetPage,
                        first.Key.SourceSequence,
                        clusterSize,
                        candidateText,
                        bounds.Left,
                        bounds.Top,
                        bounds.Right,
                        bounds.Bottom,
                        metrics.SharedTokenCount,
                        metrics.OutlineTokenCount,
                        metrics.CandidateTokenCount,
                        metrics.OutlineTokenCoverage,
                        metrics.CandidateTokenCoverage,
                        metrics.Containment,
                        ClassifyBand(
                            metrics),
                        numericRelation,
                        topDistance,
                        pointDistance,
                        productionHeading));
            }
        }
    }

    private static LexicalMetrics BuildLexicalMetrics(
        string outlineTitle,
        string candidateText)
    {
        var outlineTokens =
            Tokenize(
                outlineTitle);

        var candidateTokens =
            Tokenize(
                candidateText);

        var shared =
            outlineTokens
                .Intersect(
                    candidateTokens,
                    StringComparer.Ordinal)
                .Count();

        var outlineCoverage =
            outlineTokens.Count == 0
                ? 0
                : shared /
                  (double)outlineTokens.Count;

        var candidateCoverage =
            candidateTokens.Count == 0
                ? 0
                : shared /
                  (double)candidateTokens.Count;

        var containment =
            GetContainment(
                outlineTitle,
                candidateText);

        return new LexicalMetrics(
            shared,
            outlineTokens.Count,
            candidateTokens.Count,
            Math.Round(
                outlineCoverage,
                3),
            Math.Round(
                candidateCoverage,
                3),
            containment);
    }

    private static TextContainmentRelation GetContainment(
        string outlineTitle,
        string candidateText)
    {
        var outline =
            CompactHeadingKey(
                outlineTitle);

        var candidate =
            CompactHeadingKey(
                candidateText);

        if (outline.Length == 0 ||
            candidate.Length == 0)
        {
            return TextContainmentRelation.None;
        }

        if (string.Equals(
                outline,
                candidate,
                StringComparison.Ordinal))
        {
            return TextContainmentRelation.Equal;
        }

        var shorterLength =
            Math.Min(
                outline.Length,
                candidate.Length);

        var longerLength =
            Math.Max(
                outline.Length,
                candidate.Length);

        var lengthRatio =
            longerLength == 0
                ? 0
                : shorterLength /
                  (double)longerLength;

        if (shorterLength <
            MinimumContainmentCompactLength ||
            lengthRatio <
            MinimumContainmentLengthRatio)
        {
            return TextContainmentRelation.None;
        }

        if (candidate.Contains(
                outline,
                StringComparison.Ordinal))
        {
            return TextContainmentRelation.OutlineWithinCandidate;
        }

        if (outline.Contains(
                candidate,
                StringComparison.Ordinal))
        {
            return TextContainmentRelation.CandidateWithinOutline;
        }

        return TextContainmentRelation.None;
    }

    private static DiagnosticAlignmentBand ClassifyBand(
        LexicalMetrics metrics)
    {
        if (metrics.Containment ==
            TextContainmentRelation.Equal)
        {
            return DiagnosticAlignmentBand.ExactEquivalent;
        }

        if (metrics.Containment is
            TextContainmentRelation.OutlineWithinCandidate or
            TextContainmentRelation.CandidateWithinOutline)
        {
            return DiagnosticAlignmentBand.Containment;
        }

        if (metrics.SharedTokenCount >= 3 &&
            metrics.OutlineTokenCoverage >= 0.70 &&
            metrics.CandidateTokenCoverage >= 0.50)
        {
            return DiagnosticAlignmentBand.HighOverlap;
        }

        if (metrics.SharedTokenCount >= 2 &&
            metrics.OutlineTokenCoverage >= 0.50)
        {
            return DiagnosticAlignmentBand.ModerateOverlap;
        }

        if (metrics.SharedTokenCount > 0)
        {
            return DiagnosticAlignmentBand.WeakOverlap;
        }

        return DiagnosticAlignmentBand.None;
    }

    private static int GetBandRank(
        DiagnosticAlignmentBand band) =>
        band switch
        {
            DiagnosticAlignmentBand.ExactEquivalent => 5,
            DiagnosticAlignmentBand.Containment => 4,
            DiagnosticAlignmentBand.HighOverlap => 3,
            DiagnosticAlignmentBand.ModerateOverlap => 2,
            DiagnosticAlignmentBand.WeakOverlap => 1,
            _ => 0
        };

    private static bool IsPlausibleAlignment(
        DiagnosticAlignmentBand band) =>
        band is
            DiagnosticAlignmentBand.ExactEquivalent or
            DiagnosticAlignmentBand.Containment or
            DiagnosticAlignmentBand.HighOverlap;

    private static NumericLabelRelation CompareNumericLabels(
        string outlineTitle,
        string candidateText)
    {
        var outlineNumber =
            TryGetLeadingNumber(
                outlineTitle);

        var candidateNumber =
            TryGetLeadingNumber(
                candidateText);

        if (outlineNumber is null &&
            candidateNumber is null)
        {
            return NumericLabelRelation.BothMissing;
        }

        if (outlineNumber is not null &&
            candidateNumber is null)
        {
            return NumericLabelRelation.OutlineOnly;
        }

        if (outlineNumber is null &&
            candidateNumber is not null)
        {
            return NumericLabelRelation.CandidateOnly;
        }

        return outlineNumber ==
               candidateNumber
            ? NumericLabelRelation.Same
            : NumericLabelRelation.Different;
    }

    private static int? TryGetLeadingNumber(
        string text)
    {
        var match =
            LeadingNumberRegex.Match(
                text);

        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(
                match.Groups["number"].Value,
                out var number)
            ? number
            : null;
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

    private static string CompactHeadingKey(
        string heading) =>
        new(
            heading
                .Where(
                    char.IsLetterOrDigit)
                .Select(
                    char.ToUpperInvariant)
                .ToArray());

    private static CombinedBounds CombineBounds(
        IReadOnlyList<BlockContext> cluster) =>
        new(
            cluster.Min(context =>
                context.Block.SourceBlock
                    .Bounds.Left),
            cluster.Min(context =>
                context.Block.SourceBlock
                    .Bounds.Top),
            cluster.Max(context =>
                context.Block.SourceBlock
                    .Bounds.Right),
            cluster.Max(context =>
                context.Block.SourceBlock
                    .Bounds.Bottom));

    private static double? DistanceToVerticalRange(
        double? normalizedTop,
        double blockTop,
        double blockBottom)
    {
        if (normalizedTop is not double value ||
            !double.IsFinite(value))
        {
            return null;
        }

        if (value < blockTop)
        {
            return Math.Round(
                blockTop -
                value,
                4);
        }

        if (value > blockBottom)
        {
            return Math.Round(
                value -
                blockBottom,
                4);
        }

        return 0;
    }

    private static double? DistanceToRectangle(
        double? normalizedLeft,
        double? normalizedTop,
        double left,
        double top,
        double right,
        double bottom)
    {
        if (normalizedLeft is not double x ||
            normalizedTop is not double y ||
            !double.IsFinite(x) ||
            !double.IsFinite(y))
        {
            return null;
        }

        var dx =
            x < left
                ? left - x
                : x > right
                    ? x - right
                    : 0;

        var dy =
            y < top
                ? top - y
                : y > bottom
                    ? y - bottom
                    : 0;

        return Math.Round(
            Math.Sqrt(
                dx * dx +
                dy * dy),
            4);
    }

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
                                 block.Text)))
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
        PdfOutlineAlignmentReport report)
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
                    JsonNamingPolicy.CamelCase,
                Converters =
                {
                    new System.Text.Json.Serialization
                        .JsonStringEnumConverter()
                }
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
        PdfOutlineAlignmentReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: PDF OUTLINE ALIGNMENT ANALYZED");

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
            $"Outline roots / global entries / selected internal entries: " +
            $"{report.OutlineRootCount} / " +
            $"{report.OutlineEntryCount} / " +
            $"{report.Summary.SelectedInternalEntryCount}");

        Console.WriteLine(
            $"Production headings: {report.ProductionHeadingCount}");

        Console.WriteLine(
            $"Target pages with words / blocks / textless-dominant-raster: " +
            $"{report.Summary.TargetPagesWithWords} / " +
            $"{report.Summary.TargetPagesWithBlocks} / " +
            $"{report.Summary.TargetPagesTextlessDominantRaster}");

        Console.WriteLine(
            $"Destinations with normalized left / top: " +
            $"{report.Summary.DestinationsWithNormalizedLeft} / " +
            $"{report.Summary.DestinationsWithNormalizedTop}");

        Console.WriteLine(
            $"Best candidates total / exact-page / nearby-page: " +
            $"{report.Summary.EntriesWithBestCandidate} / " +
            $"{report.Summary.BestCandidateOnTargetPage} / " +
            $"{report.Summary.BestCandidateOnNearbyPage}");

        Console.WriteLine(
            $"Plausible alignments / exploratory candidates / no candidate: " +
            $"{report.Summary.PlausibleAlignmentCount} / " +
            $"{report.Summary.ExploratoryCandidateCount} / " +
            $"{report.Summary.NoCandidateCount}");

        Console.WriteLine(
            $"Plausible alignments exact-page / nearby-page: " +
            $"{report.Summary.PlausibleAlignmentOnTargetPage} / " +
            $"{report.Summary.PlausibleAlignmentOnNearbyPage}");

        Console.WriteLine(
            $"Best candidates production-heading / non-heading: " +
            $"{report.Summary.BestCandidateProductionHeading} / " +
            $"{report.Summary.BestCandidateNonHeading}");

        Console.WriteLine(
            $"Plausible alignments production-heading / non-heading: " +
            $"{report.Summary.PlausibleAlignmentProductionHeading} / " +
            $"{report.Summary.PlausibleAlignmentNonHeading}");

        Console.WriteLine(
            "Diagnostic alignment bands:");

        foreach (var pair in report.Summary.BandCounts)
        {
            Console.WriteLine(
                $"  {pair.Key}: {pair.Value}");
        }

        Console.WriteLine(
            "Leading numeric-label relations:");

        foreach (var pair in report.Summary.NumericLabelRelationCounts)
        {
            Console.WriteLine(
                $"  {pair.Key}: {pair.Value}");
        }

        Console.WriteLine(
            "Best alignment candidates:");

        foreach (var entry in report.Entries
                     .Where(entry =>
                         entry.BestCandidate is not null)
                     .Take(
                         ConsoleSampleLimit))
        {
            var candidate =
                entry.BestCandidate!;

            Console.WriteLine(
                $"  outline p{entry.TargetPageNumber} " +
                $"L{entry.Level} '{Truncate(entry.Title, 120)}'");

            Console.WriteLine(
                $"      target words={entry.TargetPage.WordCount} " +
                $"blocks={entry.TargetPage.BlockCount} " +
                $"raster={entry.TargetPage.IsTextlessDominantRaster} " +
                $"destTop={FormatNullable(entry.Destination.NormalizedTop)}");

            Console.WriteLine(
                $"      {candidate.Band} " +
                $"p{candidate.PhysicalPageNumber} " +
                $"offset={candidate.PageOffset:+#;-#;0} " +
                $"cluster={candidate.ClusterSize} " +
                $"shared={candidate.SharedTokenCount} " +
                $"outlineCoverage={candidate.OutlineTokenCoverage:F3} " +
                $"candidateCoverage={candidate.CandidateTokenCoverage:F3}");

            Console.WriteLine(
                $"      containment={candidate.Containment} " +
                $"numeric={candidate.NumericLabelRelation} " +
                $"topDistance={FormatNullable(candidate.DestinationTopDistance)} " +
                $"pointDistance={FormatNullable(candidate.DestinationPointDistance)} " +
                $"productionHeading={candidate.IsProductionHeading}");

            Console.WriteLine(
                $"      block: {Truncate(candidate.Text, 180)}");
        }

        Console.WriteLine(
            "Entries without lexical candidate in target +/-2 pages:");

        foreach (var entry in report.Entries
                     .Where(entry =>
                         entry.BestCandidate is null)
                     .Take(
                         ConsoleSampleLimit))
        {
            Console.WriteLine(
                $"  p{entry.TargetPageNumber} " +
                $"L{entry.Level} " +
                $"words={entry.TargetPage.WordCount} " +
                $"blocks={entry.TargetPage.BlockCount} " +
                $"raster={entry.TargetPage.IsTextlessDominantRaster} " +
                $"'{Truncate(entry.Title, 150)}'");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private static string FormatNullable(
        double? value) =>
        value.HasValue
            ? value.Value.ToString("F4")
            : "n/a";

    private static string Truncate(
        string text,
        int maximumLength)
    {
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
        int RootCount,
        IReadOnlyList<RawOutlineEntry> Entries);

    private sealed record RawOutlineEntry(
        int Ordinal,
        int? ParentOrdinal,
        int Level,
        string Title,
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

    private sealed record CombinedBounds(
        double Left,
        double Top,
        double Right,
        double Bottom);

    private sealed record LexicalMetrics(
        int SharedTokenCount,
        int OutlineTokenCount,
        int CandidateTokenCount,
        double OutlineTokenCoverage,
        double CandidateTokenCoverage,
        TextContainmentRelation Containment);

    private enum TextContainmentRelation
    {
        None,
        Equal,
        OutlineWithinCandidate,
        CandidateWithinOutline
    }

    private enum DiagnosticAlignmentBand
    {
        None,
        WeakOverlap,
        ModerateOverlap,
        HighOverlap,
        Containment,
        ExactEquivalent
    }

    private enum NumericLabelRelation
    {
        BothMissing,
        OutlineOnly,
        CandidateOnly,
        Same,
        Different
    }

    private sealed record PdfOutlineAlignmentReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string SourceFileName,
        string SourceSha256,
        long SourceByteLength,
        int TotalPdfPages,
        PdfPageSelection PageSelection,
        string NormalizationProfileId,
        string SegmentationProfileId,
        int OutlineRootCount,
        int OutlineEntryCount,
        int ProductionHeadingCount,
        AlignmentSummary Summary,
        IReadOnlyList<OutlineAlignmentObservation> Entries);

    private sealed record PdfPageSelection(
        int FirstPage,
        int LastPage,
        int PageCount);

    private sealed record AlignmentSummary(
        int SelectedInternalEntryCount,
        int TargetPagesWithWords,
        int TargetPagesWithBlocks,
        int TargetPagesTextlessDominantRaster,
        int DestinationsWithNormalizedTop,
        int DestinationsWithNormalizedLeft,
        int EntriesWithBestCandidate,
        int PlausibleAlignmentCount,
        int ExploratoryCandidateCount,
        int NoCandidateCount,
        int BestCandidateOnTargetPage,
        int BestCandidateOnNearbyPage,
        int PlausibleAlignmentOnTargetPage,
        int PlausibleAlignmentOnNearbyPage,
        int BestCandidateProductionHeading,
        int BestCandidateNonHeading,
        int PlausibleAlignmentProductionHeading,
        int PlausibleAlignmentNonHeading,
        IReadOnlyDictionary<
            DiagnosticAlignmentBand,
            int> BandCounts,
        IReadOnlyDictionary<
            NumericLabelRelation,
            int> NumericLabelRelationCounts);

    private sealed record OutlineAlignmentObservation(
        int Ordinal,
        int? ParentOrdinal,
        int Level,
        string Title,
        int TargetPageNumber,
        string? DestinationType,
        DestinationObservation Destination,
        TargetPageObservation TargetPage,
        AlignmentCandidate? BestCandidate,
        IReadOnlyList<AlignmentCandidate> Candidates);

    private sealed record DestinationObservation(
        double? RawLeft,
        double? RawTop,
        double? RawRight,
        double? RawBottom,
        double? NormalizedLeft,
        double? NormalizedTop);

    private sealed record TargetPageObservation(
        int WordCount,
        int BlockCount,
        double LargestRasterImageAreaRatio,
        bool IsTextlessDominantRaster);

    private sealed record AlignmentCandidate(
        int PhysicalPageNumber,
        int PageOffset,
        int FirstSourceSequence,
        int ClusterSize,
        string Text,
        double Left,
        double Top,
        double Right,
        double Bottom,
        int SharedTokenCount,
        int OutlineTokenCount,
        int CandidateTokenCount,
        double OutlineTokenCoverage,
        double CandidateTokenCoverage,
        TextContainmentRelation Containment,
        DiagnosticAlignmentBand Band,
        NumericLabelRelation NumericLabelRelation,
        double? DestinationTopDistance,
        double? DestinationPointDistance,
        bool IsProductionHeading);
}
