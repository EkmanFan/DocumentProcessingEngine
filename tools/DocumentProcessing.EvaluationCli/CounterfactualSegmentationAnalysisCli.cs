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
/// Evaluation-only comparison of alternative heading policies over one exact
/// normalized block stream.
///
/// Production behavior is measured by the real HeuristicDocumentSegmenter.
/// Counterfactual policies reuse the production cross-page/page-fallback flow
/// locally but never change production classes.
/// </summary>
internal static class CounterfactualSegmentationAnalysisCli
{
    private const string ReportSchemaVersion =
        "document-processing-counterfactual-segmentation-analysis-v3";

    private const int MaximumHeadingCharacters = 180;
    private const int MaximumHeadingWords = 24;
    private const int MinimumHeadingLetterCount = 3;

    // Historical Stage 2 quality gate, evaluated counterfactually only.
    private const int StrictMinimumHeadingLetterCount = 4;
    private const double StrictMinimumAlphaNumericRatio = 0.55;

    private const double MinimumHeadingFontRatio = 1.18;
    private const double SectionFontRatio = 1.30;
    private const double MinimumStrongExplicitFontRatio = 0.95;

    private const int SmallSegmentCharacterThreshold = 120;
    private const int LargeSegmentCharacterThreshold = 4000;
    private const int SampleLimit = 30;
    private const int ContextCharacterLimit = 220;

    private static readonly Regex StrongExplicitHeadingRegex =
        new(
            @"^(?:CHAPTER|PART|SECTION|BOOK)\b",
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

    private static async Task<CounterfactualReport>
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

        var bodyFontSize =
            GetWeightedMedianFontSize(
                contexts.Select(context =>
                        context.Block)
                    .ToArray());

        var productionResult =
            new HeuristicDocumentSegmenter()
                .Segment(
                    normalized);

        var productionSegments =
            productionResult.Segments
                .Select(segment =>
                    ToEvaluationSegment(
                        segment,
                        contextByBlock))
                .ToArray();

        var productionHintedResult =
            new HeuristicDocumentSegmenter()
                .Segment(
                    normalized,
                    new DocumentSegmentationOptions(
                        options.HeadingHints));

        var productionHintedSegments =
            productionHintedResult.Segments
                .Select(segment =>
                    ToEvaluationSegment(
                        segment,
                        contextByBlock))
                .ToArray();

        ValidateCoverage(
            contexts,
            productionSegments,
            "ProductionStrictTypographyV4");

        ValidateCoverage(
            contexts,
            productionHintedSegments,
            "ProductionStrictTypographyPlusHintsV4");

        var productionHeadingKeys =
            productionSegments
                .Where(segment =>
                    segment.HeadingText is not null)
                .Select(segment =>
                    segment.Blocks[0].Key)
                .ToHashSet();

        var productionHintedHeadingKeys =
            productionHintedSegments
                .Where(segment =>
                    segment.HeadingText is not null)
                .Select(segment =>
                    segment.Blocks[0].Key)
                .ToHashSet();

        var typographyHeadingKeys =
            contexts
                .Where(context =>
                    IsTypographyHeading(
                        context.Block,
                        bodyFontSize))
                .Select(context =>
                    context.Key)
                .ToHashSet();

        var strictTypographyHeadingKeys =
            contexts
                .Where(context =>
                    IsStrictTypographyHeading(
                        context.Block,
                        bodyFontSize))
                .Select(context =>
                    context.Key)
                .ToHashSet();

        var strongExplicitHeadingKeys =
            contexts
                .Where(context =>
                    IsTypographyHeading(
                        context.Block,
                        bodyFontSize) ||
                    IsStrongExplicitHeading(
                        context.Block,
                        bodyFontSize))
                .Select(context =>
                    context.Key)
                .ToHashSet();

        var hintMatcher =
            new HeadingHintMatcher(
                options.HeadingHints);

        var typographyPlusHintsHeadingKeys =
            contexts
                .Where(context =>
                    IsTypographyHeading(
                        context.Block,
                        bodyFontSize) ||
                    hintMatcher.IsMatch(
                        context.Block))
                .Select(context =>
                    context.Key)
                .ToHashSet();

        var strictTypographyPlusHintsHeadingKeys =
            contexts
                .Where(context =>
                    IsStrictTypographyHeading(
                        context.Block,
                        bodyFontSize) ||
                    hintMatcher.IsMatch(
                        context.Block))
                .Select(context =>
                    context.Key)
                .ToHashSet();

        if (!productionHeadingKeys.SetEquals(
                strictTypographyHeadingKeys))
        {
            throw new InvalidDataException(
                "Production default heading identities differ from the independent strict-typography policy.");
        }

        if (!productionHintedHeadingKeys.SetEquals(
                strictTypographyPlusHintsHeadingKeys))
        {
            throw new InvalidDataException(
                "Production hinted heading identities differ from the independent strict-typography-plus-hints policy.");
        }

        var policies =
            new List<PolicyResult>
            {
                BuildPolicyResult(
                    "A-ProductionStrictTypographyV4",
                    "Current production strict typography policy using default empty heading hints.",
                    contexts,
                    productionSegments,
                    productionHeadingKeys,
                    productionHeadingKeys,
                    bodyFontSize,
                    options.Probes,
                    options.HistoricalSegmentCount,
                    headingOriginResolver:
                        key => "Production"),
                BuildCounterfactualPolicyResult(
                    "B-TypographyOnly",
                    "Current quality gate plus automatic font hierarchy only.",
                    normalized,
                    contexts,
                    typographyHeadingKeys,
                    productionHeadingKeys,
                    bodyFontSize,
                    options.Probes,
                    options.HistoricalSegmentCount,
                    key => "Typography"),
                BuildCounterfactualPolicyResult(
                    "C-TypographyPlusStrongExplicit",
                    "Typography plus only Chapter/Part/Section/Book textual markers.",
                    normalized,
                    contexts,
                    strongExplicitHeadingKeys,
                    productionHeadingKeys,
                    bodyFontSize,
                    options.Probes,
                    options.HistoricalSegmentCount,
                    key =>
                        typographyHeadingKeys.Contains(key)
                            ? "Typography"
                            : "StrongExplicit"),
                BuildCounterfactualPolicyResult(
                    "D-TypographyPlusHints",
                    "Typography plus configured editorial heading hints using generic decorated/compact matching.",
                    normalized,
                    contexts,
                    typographyPlusHintsHeadingKeys,
                    productionHeadingKeys,
                    bodyFontSize,
                    options.Probes,
                    options.HistoricalSegmentCount,
                    key =>
                        typographyHeadingKeys.Contains(key)
                            ? "Typography"
                            : "EditorialHint"),
                BuildCounterfactualPolicyResult(
                    "E-StrictTypographyOnly",
                    "Font hierarchy with the historical minimum textual-signal gate: >=4 letters and >=0.55 alphanumeric ratio.",
                    normalized,
                    contexts,
                    strictTypographyHeadingKeys,
                    productionHeadingKeys,
                    bodyFontSize,
                    options.Probes,
                    options.HistoricalSegmentCount,
                    key => "StrictTypography"),
                BuildCounterfactualPolicyResult(
                    "F-StrictTypographyPlusHints",
                    "Strict typography plus configured editorial hints; hints remain independent of the automatic text-quality gate.",
                    normalized,
                    contexts,
                    strictTypographyPlusHintsHeadingKeys,
                    productionHeadingKeys,
                    bodyFontSize,
                    options.Probes,
                    options.HistoricalSegmentCount,
                    key =>
                        strictTypographyHeadingKeys.Contains(key)
                            ? "StrictTypography"
                            : "EditorialHint"),
                BuildPolicyResult(
                    "G-ProductionStrictTypographyPlusHintsV4",
                    "Real production segmenter with caller-provided heading hints.",
                    contexts,
                    productionHintedSegments,
                    productionHintedHeadingKeys,
                    productionHeadingKeys,
                    bodyFontSize,
                    options.Probes,
                    options.HistoricalSegmentCount,
                    key =>
                        productionHeadingKeys.Contains(key)
                            ? "ProductionTypography"
                            : "ProductionEditorialHint")
            };

        var strictGateComparisons =
            new[]
            {
                BuildHeadingSetComparison(
                    "B-TypographyOnly",
                    "E-StrictTypographyOnly",
                    typographyHeadingKeys,
                    strictTypographyHeadingKeys,
                    contexts,
                    bodyFontSize),
                BuildHeadingSetComparison(
                    "D-TypographyPlusHints",
                    "F-StrictTypographyPlusHints",
                    typographyPlusHintsHeadingKeys,
                    strictTypographyPlusHintsHeadingKeys,
                    contexts,
                    bodyFontSize)
            };

        return new CounterfactualReport(
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
            productionResult.SegmentationProfileId,
            bodyFontSize,
            options.HistoricalSegmentCount,
            options.HeadingHints,
            options.Probes,
            contexts.Count,
            StrictMinimumHeadingLetterCount,
            StrictMinimumAlphaNumericRatio,
            policies,
            strictGateComparisons);
    }

    private static PolicyResult
        BuildCounterfactualPolicyResult(
            string name,
            string description,
            DocumentTextNormalizationResult normalized,
            IReadOnlyList<BlockContext> contexts,
            IReadOnlySet<BlockKey> headingKeys,
            IReadOnlySet<BlockKey> productionHeadingKeys,
            double? bodyFontSize,
            IReadOnlyList<string> probes,
            int historicalSegmentCount,
            Func<BlockKey, string> headingOriginResolver)
    {
        var segments =
            BuildSegments(
                normalized.Pages,
                headingKeys);

        ValidateCoverage(
            contexts,
            segments,
            name);

        return BuildPolicyResult(
            name,
            description,
            contexts,
            segments,
            headingKeys,
            productionHeadingKeys,
            bodyFontSize,
            probes,
            historicalSegmentCount,
            headingOriginResolver);
    }

    private static PolicyResult BuildPolicyResult(
        string name,
        string description,
        IReadOnlyList<BlockContext> contexts,
        IReadOnlyList<EvaluationSegment> segments,
        IReadOnlySet<BlockKey> headingKeys,
        IReadOnlySet<BlockKey> productionHeadingKeys,
        double? bodyFontSize,
        IReadOnlyList<string> probes,
        int historicalSegmentCount,
        Func<BlockKey, string> headingOriginResolver)
    {
        var removedKeys =
            productionHeadingKeys
                .Except(headingKeys)
                .OrderBy(key =>
                    key.PhysicalPageNumber)
                .ThenBy(key =>
                    key.SourceSequence)
                .ToArray();

        var addedKeys =
            headingKeys
                .Except(productionHeadingKeys)
                .OrderBy(key =>
                    key.PhysicalPageNumber)
                .ThenBy(key =>
                    key.SourceSequence)
                .ToArray();

        var contextByKey =
            contexts.ToDictionary(
                context =>
                    context.Key);

        var headingOriginCounts =
            headingKeys
                .GroupBy(
                    headingOriginResolver,
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

        var metrics =
            BuildMetrics(
                segments);

        var probeDiagnostics =
            probes
                .Select(probe =>
                    BuildProbeDiagnostic(
                        probe,
                        segments))
                .ToArray();

        var removedSamples =
            removedKeys
                .Select(key =>
                    BuildBoundarySample(
                        contextByKey[key],
                        contexts,
                        bodyFontSize))
                .Take(SampleLimit)
                .ToArray();

        var addedSamples =
            addedKeys
                .Select(key =>
                    BuildBoundarySample(
                        contextByKey[key],
                        contexts,
                        bodyFontSize))
                .Take(SampleLimit)
                .ToArray();

        var smallestSegments =
            segments
                .OrderBy(segment =>
                    segment.Text.Length)
                .ThenBy(segment =>
                    segment.Ordinal)
                .Take(SampleLimit)
                .Select(ToSegmentSample)
                .ToArray();

        var largestSegments =
            segments
                .OrderByDescending(segment =>
                    segment.Text.Length)
                .ThenBy(segment =>
                    segment.Ordinal)
                .Take(SampleLimit)
                .Select(ToSegmentSample)
                .ToArray();

        return new PolicyResult(
            name,
            description,
            metrics,
            historicalSegmentCount,
            metrics.SegmentCount -
            historicalSegmentCount,
            headingOriginCounts,
            probeDiagnostics,
            removedKeys.Length,
            addedKeys.Length,
            removedSamples,
            addedSamples,
            smallestSegments,
            largestSegments);
    }

    private static HeadingSetComparison
        BuildHeadingSetComparison(
            string fromPolicy,
            string toPolicy,
            IReadOnlySet<BlockKey> fromHeadingKeys,
            IReadOnlySet<BlockKey> toHeadingKeys,
            IReadOnlyList<BlockContext> contexts,
            double? bodyFontSize)
    {
        var contextByKey =
            contexts.ToDictionary(
                context =>
                    context.Key);

        var removed =
            fromHeadingKeys
                .Except(toHeadingKeys)
                .OrderBy(key =>
                    key.PhysicalPageNumber)
                .ThenBy(key =>
                    key.SourceSequence)
                .ToArray();

        var added =
            toHeadingKeys
                .Except(fromHeadingKeys)
                .OrderBy(key =>
                    key.PhysicalPageNumber)
                .ThenBy(key =>
                    key.SourceSequence)
                .ToArray();

        return new HeadingSetComparison(
            fromPolicy,
            toPolicy,
            removed.Length,
            added.Length,
            removed
                .Select(key =>
                    BuildBoundarySample(
                        contextByKey[key],
                        contexts,
                        bodyFontSize))
                .Take(SampleLimit)
                .ToArray(),
            added
                .Select(key =>
                    BuildBoundarySample(
                        contextByKey[key],
                        contexts,
                        bodyFontSize))
                .Take(SampleLimit)
                .ToArray());
    }

    private static IReadOnlyList<EvaluationSegment>
        BuildSegments(
            IReadOnlyList<NormalizedDocumentPage> pages,
            IReadOnlySet<BlockKey> headingKeys)
    {
        var output =
            new List<EvaluationSegment>();

        EvaluationAccumulator? structured =
            null;

        foreach (var page in pages)
        {
            var eligible =
                page.Blocks
                    .Where(block =>
                        !block.IsExcluded &&
                        !string.IsNullOrWhiteSpace(
                            block.Text))
                    .Select(block =>
                        new BlockContext(
                            GlobalIndex: -1,
                            page.PhysicalPageNumber,
                            block))
                    .ToArray();

            if (eligible.Length == 0)
            {
                continue;
            }

            var fallback =
                new List<BlockContext>();

            foreach (var context in eligible)
            {
                if (headingKeys.Contains(
                        context.Key))
                {
                    if (structured is not null)
                    {
                        FlushStructured(
                            output,
                            ref structured);
                    }
                    else if (fallback.Count > 0)
                    {
                        AddFallback(
                            output,
                            page.PhysicalPageNumber,
                            fallback);

                        fallback.Clear();
                    }

                    structured =
                        new EvaluationAccumulator(
                            context.Block.Text);

                    structured.Add(context);
                    continue;
                }

                if (structured is not null)
                {
                    structured.Add(context);
                }
                else
                {
                    fallback.Add(context);
                }
            }

            if (structured is null &&
                fallback.Count > 0)
            {
                AddFallback(
                    output,
                    page.PhysicalPageNumber,
                    fallback);
            }
        }

        FlushStructured(
            output,
            ref structured);

        return output;
    }

    private static void AddFallback(
        ICollection<EvaluationSegment> output,
        int physicalPageNumber,
        IReadOnlyList<BlockContext> blocks)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        AddEvaluationSegment(
            output,
            physicalPageNumber,
            physicalPageNumber,
            headingText: null,
            blocks);
    }

    private static void FlushStructured(
        ICollection<EvaluationSegment> output,
        ref EvaluationAccumulator? accumulator)
    {
        if (accumulator is null ||
            accumulator.Blocks.Count == 0)
        {
            accumulator = null;
            return;
        }

        AddEvaluationSegment(
            output,
            accumulator.FirstPhysicalPageNumber,
            accumulator.LastPhysicalPageNumber,
            accumulator.HeadingText,
            accumulator.Blocks);

        accumulator = null;
    }

    private static void AddEvaluationSegment(
        ICollection<EvaluationSegment> output,
        int firstPhysicalPageNumber,
        int lastPhysicalPageNumber,
        string? headingText,
        IReadOnlyList<BlockContext> blocks)
    {
        var ordinal =
            output.Count;

        output.Add(
            new EvaluationSegment(
                ordinal,
                firstPhysicalPageNumber,
                lastPhysicalPageNumber,
                headingText,
                string.Join(
                    "\n\n",
                    blocks.Select(block =>
                        block.Block.Text)),
                blocks.ToArray()));
    }

    private static EvaluationSegment
        ToEvaluationSegment(
            DocumentSegment segment,
            IReadOnlyDictionary<
                NormalizedDocumentTextBlock,
                BlockContext> contextByBlock) =>
        new(
            segment.Ordinal,
            segment.FirstPhysicalPageNumber,
            segment.LastPhysicalPageNumber,
            segment.HeadingText,
            segment.Text,
            segment.SourceBlocks
                .Select(block =>
                    contextByBlock[block])
                .ToArray());

    private static void ValidateCoverage(
        IReadOnlyList<BlockContext> contexts,
        IReadOnlyList<EvaluationSegment> segments,
        string policyName)
    {
        var observed =
            segments
                .SelectMany(segment =>
                    segment.Blocks)
                .Select(block =>
                    block.Key)
                .ToArray();

        if (observed.Length !=
            contexts.Count)
        {
            throw new InvalidDataException(
                $"{policyName} does not cover every included normalized block exactly once.");
        }

        if (observed.Distinct().Count() !=
            observed.Length)
        {
            throw new InvalidDataException(
                $"{policyName} duplicates at least one normalized block.");
        }

        var expected =
            contexts
                .Select(context =>
                    context.Key)
                .ToHashSet();

        if (!expected.SetEquals(
                observed))
        {
            throw new InvalidDataException(
                $"{policyName} block coverage differs from the normalized source.");
        }
    }

    private static PolicyMetrics BuildMetrics(
        IReadOnlyList<EvaluationSegment> segments)
    {
        var characterCounts =
            segments
                .Select(segment =>
                    segment.Text.Length)
                .OrderBy(value =>
                    value)
                .ToArray();

        return new PolicyMetrics(
            segments.Count,
            segments.Count(segment =>
                segment.HeadingText is not null),
            segments.Count(segment =>
                segment.HeadingText is null),
            segments.Count(segment =>
                segment.FirstPhysicalPageNumber !=
                segment.LastPhysicalPageNumber),
            characterCounts.Count(value =>
                value <=
                SmallSegmentCharacterThreshold),
            characterCounts.Count(value =>
                value >=
                LargeSegmentCharacterThreshold),
            MinOrZero(characterCounts),
            MedianOrZero(characterCounts),
            AverageOrZero(characterCounts),
            MaxOrZero(characterCounts));
    }

    private static ProbeDiagnostic
        BuildProbeDiagnostic(
            string probe,
            IReadOnlyList<EvaluationSegment> segments)
    {
        var headingMatches =
            segments.Count(segment =>
                segment.HeadingText is not null &&
                segment.HeadingText.Contains(
                    probe,
                    StringComparison.OrdinalIgnoreCase));

        var textMatches =
            segments.Count(segment =>
                segment.Text.Contains(
                    probe,
                    StringComparison.OrdinalIgnoreCase));

        return new ProbeDiagnostic(
            probe,
            headingMatches,
            textMatches);
    }

    private static BoundarySample
        BuildBoundarySample(
            BlockContext context,
            IReadOnlyList<BlockContext> contexts,
            double? bodyFontSize)
    {
        BlockContext? previous =
            context.GlobalIndex > 0
                ? contexts[
                    context.GlobalIndex - 1]
                : null;

        BlockContext? next =
            context.GlobalIndex + 1 <
            contexts.Count
                ? contexts[
                    context.GlobalIndex + 1]
                : null;

        var textQuality =
            GetTextQualityMetrics(
                context.Block.Text);

        return new BoundarySample(
            context.PhysicalPageNumber,
            context.Block.SourceBlock
                .SourceSequence,
            context.Block.Text,
            context.Block.SourceBlock
                .DominantFontName,
            context.Block.SourceBlock
                .MedianPointSize,
            GetFontRatio(
                context.Block,
                bodyFontSize),
            context.Block.SourceBlock
                .WordCount,
            textQuality.LetterCount,
            textQuality.NonWhitespaceCount,
            textQuality.AlphaNumericCount,
            textQuality.AlphaNumericRatio,
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

    private static SegmentSample ToSegmentSample(
        EvaluationSegment segment) =>
        new(
            segment.Ordinal,
            segment.FirstPhysicalPageNumber,
            segment.LastPhysicalPageNumber,
            segment.HeadingText,
            segment.Text.Length,
            segment.Blocks.Count,
            Truncate(
                segment.Text));

    private static bool IsTypographyHeading(
        NormalizedDocumentTextBlock block,
        double? bodyFontSize)
    {
        var text =
            block.Text.Trim();

        if (!HasAcceptableHeadingText(
                text) ||
            block.SourceBlock.WordCount >
            MaximumHeadingWords)
        {
            return false;
        }

        var fontRatio =
            GetFontRatio(
                block,
                bodyFontSize);

        if (fontRatio is not >=
            MinimumHeadingFontRatio)
        {
            return false;
        }

        if (fontRatio <
            SectionFontRatio &&
            LooksLikeSentence(
                text))
        {
            return false;
        }

        return true;
    }

    private static bool IsStrictTypographyHeading(
        NormalizedDocumentTextBlock block,
        double? bodyFontSize)
    {
        if (!IsTypographyHeading(
                block,
                bodyFontSize))
        {
            return false;
        }

        return HasStrictHeadingTextQuality(
            block.Text);
    }

    private static bool IsStrongExplicitHeading(
        NormalizedDocumentTextBlock block,
        double? bodyFontSize)
    {
        var text =
            block.Text.Trim();

        if (!HasAcceptableHeadingText(
                text) ||
            block.SourceBlock.WordCount >
            MaximumHeadingWords ||
            !StrongExplicitHeadingRegex
                .IsMatch(text))
        {
            return false;
        }

        var ratio =
            GetFontRatio(
                block,
                bodyFontSize);

        return ratio is null ||
               ratio >=
               MinimumStrongExplicitFontRatio;
    }

    private static bool HasAcceptableHeadingText(
        string text)
    {
        if (text.Length == 0 ||
            text.Length >
            MaximumHeadingCharacters ||
            text.Contains(
                '\uFFFD',
                StringComparison.Ordinal) ||
            text.Any(
                char.IsControl))
        {
            return false;
        }

        var letterCount =
            text.Count(
                char.IsLetter);

        if (letterCount <
            MinimumHeadingLetterCount)
        {
            return false;
        }

        var nonWhitespaceCount =
            text.Count(character =>
                !char.IsWhiteSpace(
                    character));

        var alphaNumericCount =
            text.Count(character =>
                char.IsLetterOrDigit(
                    character));

        return nonWhitespaceCount > 0 &&
               alphaNumericCount * 2 >=
               nonWhitespaceCount;
    }

    private static bool HasStrictHeadingTextQuality(
        string text)
    {
        var quality =
            GetTextQualityMetrics(
                text);

        return quality.NonWhitespaceCount > 0 &&
               quality.LetterCount >=
               StrictMinimumHeadingLetterCount &&
               quality.AlphaNumericRatio >=
               StrictMinimumAlphaNumericRatio;
    }

    private static TextQualityMetrics
        GetTextQualityMetrics(
            string text)
    {
        var letterCount =
            text.Count(
                char.IsLetter);

        var nonWhitespaceCount =
            text.Count(character =>
                !char.IsWhiteSpace(
                    character));

        var alphaNumericCount =
            text.Count(character =>
                char.IsLetterOrDigit(
                    character));

        var alphaNumericRatio =
            nonWhitespaceCount == 0
                ? 0
                : alphaNumericCount /
                  (double)nonWhitespaceCount;

        return new TextQualityMetrics(
            letterCount,
            nonWhitespaceCount,
            alphaNumericCount,
            alphaNumericRatio);
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
            (totalWeight + 1) /
            2;

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

    private static IReadOnlyList<BlockContext>
        BuildBlockContexts(
            IReadOnlyList<NormalizedDocumentPage> pages)
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
        CounterfactualReport report)
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
        CounterfactualReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: COUNTERFACTUAL SEGMENTATION ANALYZED");

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
            $"Included normalized blocks: " +
            $"{report.IncludedBlockCount}");

        Console.WriteLine(
            $"Weighted median body font: " +
            $"{FormatNullable(report.WeightedMedianBodyFontSize)}");

        Console.WriteLine(
            $"Historical comparison segments: " +
            $"{report.HistoricalSegmentCount}");

        Console.WriteLine(
            $"Configured hints: " +
            $"{(report.HeadingHints.Count == 0 ? "(none)" : string.Join(" | ", report.HeadingHints))}");

        Console.WriteLine(
            $"Strict automatic text gate: " +
            $"letters>={report.StrictMinimumHeadingLetterCount}, " +
            $"alphanumeric_ratio>={report.StrictMinimumAlphaNumericRatio:F2}");

        foreach (var policy in report.Policies)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"{policy.Name}:");

            Console.WriteLine(
                $"  segments={policy.Metrics.SegmentCount} " +
                $"delta_historical={policy.DeltaFromHistorical:+#;-#;0} " +
                $"heading={policy.Metrics.HeadingSegmentCount} " +
                $"fallback={policy.Metrics.FallbackSegmentCount} " +
                $"cross_page={policy.Metrics.CrossPageSegmentCount}");

            Console.WriteLine(
                $"  small={policy.Metrics.SmallSegmentCount} " +
                $"large={policy.Metrics.LargeSegmentCount} " +
                $"chars min/med/avg/max=" +
                $"{policy.Metrics.MinimumCharacters}/" +
                $"{policy.Metrics.MedianCharacters:F1}/" +
                $"{policy.Metrics.AverageCharacters:F1}/" +
                $"{policy.Metrics.MaximumCharacters}");

            Console.WriteLine(
                $"  vs production: removed_boundaries={policy.RemovedProductionBoundaryCount} " +
                $"added_boundaries={policy.AddedBoundaryCount}");

            if (policy.HeadingOrigins.Count > 0)
            {
                Console.WriteLine(
                    "  origins: " +
                    string.Join(
                        ", ",
                        policy.HeadingOrigins.Select(item =>
                            $"{item.Origin}={item.Count}")));
            }

            foreach (var probe in policy.Probes)
            {
                Console.WriteLine(
                    $"  probe '{probe.Probe}': " +
                    $"heading={probe.HeadingMatches} " +
                    $"segment_text={probe.SegmentTextMatches}");
            }

            if (policy.RemovedProductionBoundarySamples.Count > 0)
            {
                Console.WriteLine(
                    "  removed production boundary samples:");

                foreach (var sample in policy.RemovedProductionBoundarySamples
                             .Take(8))
                {
                    Console.WriteLine(
                        $"    p{sample.PhysicalPageNumber} " +
                        $"ratio={FormatNullable(sample.FontRatio)} " +
                        $"{sample.Text}");
                }
            }

            if (policy.AddedBoundarySamples.Count > 0)
            {
                Console.WriteLine(
                    "  added boundary samples:");

                foreach (var sample in policy.AddedBoundarySamples
                             .Take(8))
                {
                    Console.WriteLine(
                        $"    p{sample.PhysicalPageNumber} " +
                        $"ratio={FormatNullable(sample.FontRatio)} " +
                        $"{sample.Text}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Strict quality-gate comparisons:");

        foreach (var comparison in report.StrictGateComparisons)
        {
            Console.WriteLine(
                $"  {comparison.FromPolicy} -> {comparison.ToPolicy}: " +
                $"removed={comparison.RemovedBoundaryCount} " +
                $"added={comparison.AddedBoundaryCount}");

            foreach (var sample in comparison.RemovedBoundarySamples
                         .Take(12))
            {
                Console.WriteLine(
                    $"    p{sample.PhysicalPageNumber} " +
                    $"ratio={FormatNullable(sample.FontRatio)} " +
                    $"letters={sample.LetterCount} " +
                    $"alnum={sample.AlphaNumericRatio:F3} " +
                    $"{sample.Text}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
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
            : values.Average();

    private static double MedianOrZero(
        IReadOnlyList<int> sortedValues)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var middle =
            sortedValues.Count /
            2;

        return sortedValues.Count % 2 == 0
            ? (sortedValues[middle - 1] +
               sortedValues[middle]) /
              2.0
            : sortedValues[middle];
    }

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
        int LastPage,
        int HistoricalSegmentCount,
        IReadOnlyList<string> HeadingHints,
        IReadOnlyList<string> Probes)
    {
        public static AnalysisOptions Parse(
            string[] args)
        {
            string? source = null;
            string? report = null;
            string? pages = null;
            int? historicalSegments = null;

            var hints =
                new List<string>();

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

                    case "--historical-segments":
                        var historicalValue =
                            ReadValue(
                                args,
                                ref index,
                                option);

                        if (!int.TryParse(
                                historicalValue,
                                out var parsedHistorical) ||
                            parsedHistorical <= 0)
                        {
                            throw new ArgumentException(
                                "--historical-segments must be a positive integer.");
                        }

                        historicalSegments =
                            parsedHistorical;
                        break;

                    case "--hint":
                        hints.Add(
                            ReadValue(
                                args,
                                ref index,
                                option));
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

            if (historicalSegments is null)
            {
                throw new ArgumentException(
                    "--historical-segments is required.");
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
                historicalSegments.Value,
                hints
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                probes
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray());
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

    private sealed class HeadingHintMatcher
    {
        private readonly HintKey[] _hints;

        public HeadingHintMatcher(
            IReadOnlyList<string> hints)
        {
            _hints =
                hints
                    .Select(hint =>
                        new HintKey(
                            hint,
                            NormalizeHeadingKey(
                                hint),
                            CompactHeadingKey(
                                hint)))
                    .Where(hint =>
                        hint.Normalized.Length > 0 &&
                        hint.Compact.Length > 0)
                    .ToArray();
        }

        public bool IsMatch(
            NormalizedDocumentTextBlock block)
        {
            if (_hints.Length == 0)
            {
                return false;
            }

            var normalizedCandidate =
                NormalizeHeadingKey(
                    block.Text);

            var sourceFirstLine =
                block.SourceText
                    .Replace(
                        "\r\n",
                        "\n",
                        StringComparison.Ordinal)
                    .Replace(
                        '\r',
                        '\n')
                    .Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .FirstOrDefault() ??
                block.Text;

            var compactFirstLine =
                CompactHeadingKey(
                    sourceFirstLine);

            foreach (var hint in _hints)
            {
                if (string.Equals(
                        normalizedCandidate,
                        hint.Normalized,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                if (MatchesDecoratedSuffix(
                        normalizedCandidate,
                        hint.Normalized))
                {
                    return true;
                }

                if (string.Equals(
                        compactFirstLine,
                        hint.Compact,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesDecoratedSuffix(
            string candidate,
            string hint)
        {
            if (!candidate.EndsWith(
                    hint,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var prefixLength =
                candidate.Length -
                hint.Length;

            if (prefixLength <= 0)
            {
                return false;
            }

            var prefix =
                candidate[
                    ..prefixLength]
                    .Trim();

            return prefix.Length is > 0 and <= 3 &&
                   prefix.All(character =>
                       !char.IsLetter(character) ||
                       char.IsUpper(character));
        }

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

        private sealed record HintKey(
            string Original,
            string Normalized,
            string Compact);
    }

    private sealed record CounterfactualReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string SourceFileName,
        string SourceSha256,
        long SourceByteLength,
        int TotalPdfPages,
        PdfPageSelection PageSelection,
        string NormalizationProfileId,
        string ProductionSegmentationProfileId,
        double? WeightedMedianBodyFontSize,
        int HistoricalSegmentCount,
        IReadOnlyList<string> HeadingHints,
        IReadOnlyList<string> Probes,
        int IncludedBlockCount,
        int StrictMinimumHeadingLetterCount,
        double StrictMinimumAlphaNumericRatio,
        IReadOnlyList<PolicyResult> Policies,
        IReadOnlyList<HeadingSetComparison> StrictGateComparisons);

    private sealed record PdfPageSelection(
        int FirstPage,
        int LastPage,
        int PageCount);

    private sealed record PolicyResult(
        string Name,
        string Description,
        PolicyMetrics Metrics,
        int HistoricalSegmentCount,
        int DeltaFromHistorical,
        IReadOnlyList<HeadingOriginCount> HeadingOrigins,
        IReadOnlyList<ProbeDiagnostic> Probes,
        int RemovedProductionBoundaryCount,
        int AddedBoundaryCount,
        IReadOnlyList<BoundarySample> RemovedProductionBoundarySamples,
        IReadOnlyList<BoundarySample> AddedBoundarySamples,
        IReadOnlyList<SegmentSample> SmallestSegments,
        IReadOnlyList<SegmentSample> LargestSegments);

    private sealed record HeadingSetComparison(
        string FromPolicy,
        string ToPolicy,
        int RemovedBoundaryCount,
        int AddedBoundaryCount,
        IReadOnlyList<BoundarySample> RemovedBoundarySamples,
        IReadOnlyList<BoundarySample> AddedBoundarySamples);

    private sealed record PolicyMetrics(
        int SegmentCount,
        int HeadingSegmentCount,
        int FallbackSegmentCount,
        int CrossPageSegmentCount,
        int SmallSegmentCount,
        int LargeSegmentCount,
        int MinimumCharacters,
        double MedianCharacters,
        double AverageCharacters,
        int MaximumCharacters);

    private sealed record HeadingOriginCount(
        string Origin,
        int Count);

    private sealed record ProbeDiagnostic(
        string Probe,
        int HeadingMatches,
        int SegmentTextMatches);

    private sealed record BoundarySample(
        int PhysicalPageNumber,
        int SourceSequence,
        string Text,
        string? DominantFontName,
        double? MedianPointSize,
        double? FontRatio,
        int WordCount,
        int LetterCount,
        int NonWhitespaceCount,
        int AlphaNumericCount,
        double AlphaNumericRatio,
        int? PreviousBlockPage,
        string? PreviousBlockText,
        int? NextBlockPage,
        string? NextBlockText);

    private sealed record SegmentSample(
        int Ordinal,
        int FirstPhysicalPageNumber,
        int LastPhysicalPageNumber,
        string? HeadingText,
        int CharacterCount,
        int BlockCount,
        string TextSample);

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

    private sealed record EvaluationSegment(
        int Ordinal,
        int FirstPhysicalPageNumber,
        int LastPhysicalPageNumber,
        string? HeadingText,
        string Text,
        IReadOnlyList<BlockContext> Blocks);

    private sealed class EvaluationAccumulator(
        string headingText)
    {
        public string HeadingText { get; } =
            headingText;

        public int FirstPhysicalPageNumber { get; private set; }

        public int LastPhysicalPageNumber { get; private set; }

        public List<BlockContext> Blocks { get; } =
            [];

        public void Add(
            BlockContext context)
        {
            if (Blocks.Count == 0)
            {
                FirstPhysicalPageNumber =
                    context.PhysicalPageNumber;
            }

            LastPhysicalPageNumber =
                context.PhysicalPageNumber;

            Blocks.Add(
                context);
        }
    }

    private sealed record TextQualityMetrics(
        int LetterCount,
        int NonWhitespaceCount,
        int AlphaNumericCount,
        double AlphaNumericRatio);

    private sealed record FontSample(
        double PointSize,
        int Weight);
}
