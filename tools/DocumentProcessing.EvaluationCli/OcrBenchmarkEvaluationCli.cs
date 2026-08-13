using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.EvaluationCli;

internal static class OcrBenchmarkEvaluationCli
{
    private const string ManifestSchemaVersion =
        "document-processing-ocr-benchmark-manifest-v1";

    private const string InputIndexSchemaVersion =
        "document-processing-ocr-benchmark-input-index-v1";

    private const string EngineResultSchemaVersion =
        "document-processing-ocr-engine-result-v1";

    private const string CorpusVerificationSchemaVersion =
        "document-processing-ocr-benchmark-corpus-verification-v1";

    private const string EvaluationReportSchemaVersion =
        "document-processing-ocr-benchmark-evaluation-v1";

    private const double DominantRasterImageAreaRatio =
        0.60;

    private const int MaximumTitleClusterSize =
        3;

    private const int MinimumContainmentCompactLength =
        8;

    private const double MinimumContainmentLengthRatio =
        0.20;

    private static readonly Regex TokenRegex =
        new(
            @"[\p{L}\p{Nd}]+",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    public static async Task<int> VerifyCorpusAsync(
        string[] args)
    {
        var options =
            CorpusVerificationOptions.Parse(
                args);

        var manifest =
            await ReadJsonAsync<OcrBenchmarkManifest>(
                options.ManifestPath);

        ValidateManifest(
            manifest);

        var sourcePath =
            Path.GetFullPath(
                options.SourcePath);

        var fileInfo =
            new FileInfo(
                sourcePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "Benchmark source PDF was not found.",
                sourcePath);
        }

        var sha256 =
            await ComputeSha256Async(
                sourcePath);

        if (!string.Equals(
                sha256,
                manifest.Source.Sha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Benchmark source SHA-256 differs from the corpus manifest.");
        }

        if (fileInfo.Length !=
            manifest.Source.ByteLength)
        {
            throw new InvalidDataException(
                "Benchmark source byte length differs from the corpus manifest.");
        }

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

        if (extracted.Pages.Count !=
            manifest.Source.TotalPages)
        {
            throw new InvalidDataException(
                "Benchmark source page count differs from the corpus manifest.");
        }

        var pageByNumber =
            extracted.Pages
                .ToDictionary(
                    page =>
                        page.PhysicalPageNumber);

        var observations =
            manifest.Pages
                .OrderBy(page =>
                    page.PageNumber)
                .Select(page =>
                {
                    if (!pageByNumber.TryGetValue(
                            page.PageNumber,
                            out var extractedPage))
                    {
                        throw new InvalidDataException(
                            $"Benchmark page {page.PageNumber} is missing from extraction.");
                    }

                    var observedState =
                        ClassifyNativeState(
                            extractedPage);

                    return new CorpusPageVerification(
                        page.PageNumber,
                        page.Group,
                        page.ExpectedNativeState,
                        observedState,
                        extractedPage.WordCount,
                        extractedPage.Blocks.Count,
                        extractedPage.LargestRasterImageAreaRatio,
                        page.ExpectedNativeState ==
                        observedState);
                })
                .ToArray();

        var report =
            new CorpusVerificationReport(
                CorpusVerificationSchemaVersion,
                DateTimeOffset.UtcNow,
                manifest.BenchmarkId,
                sha256,
                observations.Length,
                observations.Count(page =>
                    page.MatchesExpectedState),
                observations.Count(page =>
                    !page.MatchesExpectedState),
                observations);

        await WriteJsonAsync(
            options.ReportPath,
            report);

        Console.WriteLine(
            "RESULT: OCR BENCHMARK CORPUS VERIFIED");

        Console.WriteLine(
            $"Benchmark: {manifest.BenchmarkId}");

        Console.WriteLine(
            $"Pages: {report.PageCount}");

        Console.WriteLine(
            $"Expected native-state matches / mismatches: " +
            $"{report.MatchingPages} / " +
            $"{report.MismatchingPages}");

        foreach (var group in observations
                     .GroupBy(page =>
                         page.Group)
                     .OrderBy(group =>
                         group.Key,
                         StringComparer.Ordinal))
        {
            Console.WriteLine(
                $"  {group.Key}: " +
                $"{group.Count(page => page.MatchesExpectedState)}/" +
                $"{group.Count()} match");
        }

        if (report.MismatchingPages > 0)
        {
            foreach (var page in observations
                         .Where(page =>
                             !page.MatchesExpectedState))
            {
                Console.WriteLine(
                    $"  MISMATCH p{page.PageNumber}: " +
                    $"expected={page.ExpectedNativeState}, " +
                    $"observed={page.ObservedNativeState}, " +
                    $"words={page.WordCount}, blocks={page.BlockCount}, " +
                    $"largestRaster={page.LargestRasterImageAreaRatio:F3}");
            }

            throw new InvalidDataException(
                "OCR benchmark corpus no longer matches its native-text/raster expectations.");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(options.ReportPath)}");

        return 0;
    }

    public static async Task<int> EvaluateAsync(
        string[] args)
    {
        var options =
            EvaluationOptions.Parse(
                args);

        var manifest =
            await ReadJsonAsync<OcrBenchmarkManifest>(
                options.ManifestPath);

        var inputIndex =
            await ReadJsonAsync<OcrBenchmarkInputIndex>(
                options.InputIndexPath);

        var engineResult =
            await ReadJsonAsync<OcrEngineResult>(
                options.ResultPath);

        ValidateManifest(
            manifest);

        ValidateInputIndex(
            manifest,
            inputIndex);

        ValidateEngineResult(
            manifest,
            inputIndex,
            engineResult);

        var manifestPages =
            manifest.Pages
                .ToDictionary(
                    page =>
                        page.PageNumber);

        var inputPages =
            inputIndex.Pages
                .ToDictionary(
                    page =>
                        page.PageNumber);

        var resultPages =
            engineResult.Pages
                .ToDictionary(
                    page =>
                        page.PageNumber);

        var pageEvaluations =
            manifest.Pages
                .OrderBy(page =>
                    page.PageNumber)
                .Select(manifestPage =>
                    EvaluatePage(
                        manifestPage,
                        inputPages[
                            manifestPage.PageNumber],
                        resultPages[
                            manifestPage.PageNumber]))
                .ToArray();

        var rasterReference =
            pageEvaluations
                .Where(page =>
                    string.Equals(
                        page.Group,
                        "raster-reference",
                        StringComparison.Ordinal))
                .ToArray();

        var outlineTargets =
            pageEvaluations
                .Where(page =>
                    string.Equals(
                        page.Group,
                        "outline-target",
                        StringComparison.Ordinal))
                .ToArray();

        var bornDigitalControls =
            pageEvaluations
                .Where(page =>
                    string.Equals(
                        page.Group,
                        "born-digital-control",
                        StringComparison.Ordinal))
                .ToArray();

        var report =
            new OcrBenchmarkEvaluationReport(
                EvaluationReportSchemaVersion,
                DateTimeOffset.UtcNow,
                manifest.BenchmarkId,
                manifest.Source.Sha256,
                engineResult.Engine,
                engineResult.Performance,
                new OcrCoverageSummary(
                    pageEvaluations.Length,
                    pageEvaluations.Count(page =>
                        page.Status ==
                        OcrPageStatus.Completed),
                    pageEvaluations.Count(page =>
                        page.Status ==
                        OcrPageStatus.Failed),
                    pageEvaluations.Count(page =>
                        page.HasText),
                    pageEvaluations.Sum(page =>
                        page.RegionCount),
                    pageEvaluations.Sum(page =>
                        page.CharacterCount),
                    pageEvaluations.Sum(page =>
                        page.ElapsedMilliseconds)),
                new OcrRasterReferenceSummary(
                    rasterReference.Length,
                    rasterReference.Count(page =>
                        page.HasText),
                    rasterReference.Sum(page =>
                        page.CharacterCount),
                    manifest.HistoricalReferences
                        .EasyOcrRecoveredPages,
                    manifest.HistoricalReferences
                        .EasyOcrRecoveredCharacters),
                new OcrOutlineTitleSummary(
                    outlineTargets.Length,
                    outlineTargets.Count(page =>
                        page.TitleMatch is not null &&
                        IsPlausibleTitleMatch(
                            page.TitleMatch.Band)),
                    outlineTargets.Count(page =>
                        page.TitleMatch is not null &&
                        !IsPlausibleTitleMatch(
                            page.TitleMatch.Band)),
                    outlineTargets.Count(page =>
                        page.TitleMatch is null),
                    BuildTitleBandCounts(
                        outlineTargets)),
                new OcrBornDigitalControlSummary(
                    bornDigitalControls.Length,
                    bornDigitalControls.Count(page =>
                        page.HasText),
                    bornDigitalControls.Sum(page =>
                        page.CharacterCount)),
                pageEvaluations);

        await WriteJsonAsync(
            options.ReportPath,
            report);

        WriteEvaluationSummary(
            report,
            options.ReportPath);

        return 0;
    }

    private static OcrPageEvaluation EvaluatePage(
        OcrBenchmarkPage manifestPage,
        OcrBenchmarkInputPage inputPage,
        OcrEnginePageResult resultPage)
    {
        var orderedRegions =
            resultPage.Regions
                .OrderBy(region =>
                    region.Sequence)
                .ToArray();

        var characterCount =
            orderedRegions.Sum(region =>
                region.Text.Length);

        var hasText =
            orderedRegions.Any(region =>
                !string.IsNullOrWhiteSpace(
                    region.Text));

        OcrTitleMatch? titleMatch =
            null;

        if (!string.IsNullOrWhiteSpace(
                manifestPage.ExpectedTitle))
        {
            titleMatch =
                FindBestTitleMatch(
                    manifestPage.ExpectedTitle,
                    orderedRegions);
        }

        return new OcrPageEvaluation(
            manifestPage.PageNumber,
            manifestPage.Group,
            manifestPage.ExpectedNativeState,
            resultPage.Status,
            resultPage.ElapsedMilliseconds,
            resultPage.ImageWidth,
            resultPage.ImageHeight,
            string.Equals(
                resultPage.InputSha256,
                inputPage.Sha256,
                StringComparison.Ordinal),
            orderedRegions.Length,
            characterCount,
            hasText,
            manifestPage.ExpectedTitle,
            manifestPage.ExpectedTitleSource,
            titleMatch);
    }

    private static OcrTitleMatch? FindBestTitleMatch(
        string expectedTitle,
        IReadOnlyList<OcrRegion> regions)
    {
        var candidates =
            new List<OcrTitleMatch>();

        for (var start = 0;
             start < regions.Count;
             start++)
        {
            for (var clusterSize = 1;
                 clusterSize <=
                 MaximumTitleClusterSize &&
                 start + clusterSize <=
                 regions.Count;
                 clusterSize++)
            {
                var cluster =
                    regions
                        .Skip(
                            start)
                        .Take(
                            clusterSize)
                        .ToArray();

                var candidateText =
                    string.Join(
                        " ",
                        cluster.Select(region =>
                            region.Text));

                if (string.IsNullOrWhiteSpace(
                        candidateText))
                {
                    continue;
                }

                var metrics =
                    BuildLexicalMetrics(
                        expectedTitle,
                        candidateText);

                if (metrics.SharedTokenCount == 0 &&
                    metrics.Containment ==
                    TextContainmentRelation.None)
                {
                    continue;
                }

                candidates.Add(
                    new OcrTitleMatch(
                        ClassifyTitleBand(
                            metrics),
                        metrics.Containment,
                        start,
                        clusterSize,
                        candidateText,
                        metrics.SharedTokenCount,
                        metrics.ExpectedTokenCount,
                        metrics.CandidateTokenCount,
                        metrics.ExpectedTokenCoverage,
                        metrics.CandidateTokenCoverage));
            }
        }

        return candidates
            .OrderByDescending(candidate =>
                GetTitleBandRank(
                    candidate.Band))
            .ThenByDescending(candidate =>
                candidate.ExpectedTokenCoverage)
            .ThenByDescending(candidate =>
                candidate.CandidateTokenCoverage)
            .ThenByDescending(candidate =>
                candidate.SharedTokenCount)
            .ThenBy(candidate =>
                candidate.ClusterSize)
            .ThenBy(candidate =>
                candidate.FirstRegionSequence)
            .FirstOrDefault();
    }

    private static LexicalMetrics BuildLexicalMetrics(
        string expectedTitle,
        string candidateText)
    {
        var expectedTokens =
            Tokenize(
                expectedTitle);

        var candidateTokens =
            Tokenize(
                candidateText);

        var shared =
            expectedTokens
                .Intersect(
                    candidateTokens,
                    StringComparer.Ordinal)
                .Count();

        var expectedCoverage =
            expectedTokens.Count == 0
                ? 0
                : shared /
                  (double)expectedTokens.Count;

        var candidateCoverage =
            candidateTokens.Count == 0
                ? 0
                : shared /
                  (double)candidateTokens.Count;

        return new LexicalMetrics(
            shared,
            expectedTokens.Count,
            candidateTokens.Count,
            Math.Round(
                expectedCoverage,
                3),
            Math.Round(
                candidateCoverage,
                3),
            GetContainment(
                expectedTitle,
                candidateText));
    }

    private static TextContainmentRelation GetContainment(
        string expectedTitle,
        string candidateText)
    {
        var expected =
            CompactText(
                expectedTitle);

        var candidate =
            CompactText(
                candidateText);

        if (expected.Length == 0 ||
            candidate.Length == 0)
        {
            return TextContainmentRelation.None;
        }

        if (string.Equals(
                expected,
                candidate,
                StringComparison.Ordinal))
        {
            return TextContainmentRelation.Equal;
        }

        var shorterLength =
            Math.Min(
                expected.Length,
                candidate.Length);

        var longerLength =
            Math.Max(
                expected.Length,
                candidate.Length);

        var lengthRatio =
            shorterLength /
            (double)longerLength;

        if (shorterLength <
            MinimumContainmentCompactLength ||
            lengthRatio <
            MinimumContainmentLengthRatio)
        {
            return TextContainmentRelation.None;
        }

        if (candidate.Contains(
                expected,
                StringComparison.Ordinal))
        {
            return TextContainmentRelation.ExpectedWithinCandidate;
        }

        if (expected.Contains(
                candidate,
                StringComparison.Ordinal))
        {
            return TextContainmentRelation.CandidateWithinExpected;
        }

        return TextContainmentRelation.None;
    }

    private static TitleMatchBand ClassifyTitleBand(
        LexicalMetrics metrics)
    {
        if (metrics.Containment ==
            TextContainmentRelation.Equal)
        {
            return TitleMatchBand.ExactEquivalent;
        }

        if (metrics.Containment is
            TextContainmentRelation.ExpectedWithinCandidate or
            TextContainmentRelation.CandidateWithinExpected)
        {
            return TitleMatchBand.Containment;
        }

        if (metrics.SharedTokenCount >= 3 &&
            metrics.ExpectedTokenCoverage >= 0.70 &&
            metrics.CandidateTokenCoverage >= 0.50)
        {
            return TitleMatchBand.HighOverlap;
        }

        if (metrics.SharedTokenCount >= 2 &&
            metrics.ExpectedTokenCoverage >= 0.50)
        {
            return TitleMatchBand.ModerateOverlap;
        }

        if (metrics.SharedTokenCount > 0)
        {
            return TitleMatchBand.WeakOverlap;
        }

        return TitleMatchBand.None;
    }

    private static int GetTitleBandRank(
        TitleMatchBand band) =>
        band switch
        {
            TitleMatchBand.ExactEquivalent => 5,
            TitleMatchBand.Containment => 4,
            TitleMatchBand.HighOverlap => 3,
            TitleMatchBand.ModerateOverlap => 2,
            TitleMatchBand.WeakOverlap => 1,
            _ => 0
        };

    private static bool IsPlausibleTitleMatch(
        TitleMatchBand band) =>
        band is
            TitleMatchBand.ExactEquivalent or
            TitleMatchBand.Containment or
            TitleMatchBand.HighOverlap;

    private static IReadOnlyDictionary<TitleMatchBand, int>
        BuildTitleBandCounts(
            IReadOnlyList<OcrPageEvaluation> outlineTargets) =>
        Enum.GetValues<TitleMatchBand>()
            .ToDictionary(
                band =>
                    band,
                band =>
                    outlineTargets.Count(page =>
                        page.TitleMatch?.Band ==
                        band));

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

    private static string CompactText(
        string text) =>
        new(
            text
                .Where(
                    char.IsLetterOrDigit)
                .Select(
                    char.ToUpperInvariant)
                .ToArray());

    private static NativePageExpectation ClassifyNativeState(
        DocumentProcessing.Core.Extraction.DocumentExtractionPage page)
    {
        if (page.WordCount == 0 &&
            page.LargestRasterImageAreaRatio >=
            DominantRasterImageAreaRatio)
        {
            return NativePageExpectation.TextlessDominantRaster;
        }

        if (page.WordCount > 0 &&
            page.Blocks.Count > 0)
        {
            return NativePageExpectation.NativeText;
        }

        return NativePageExpectation.Other;
    }

    private static void ValidateManifest(
        OcrBenchmarkManifest manifest)
    {
        if (!string.Equals(
                manifest.SchemaVersion,
                ManifestSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported OCR benchmark manifest schema '{manifest.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(
                manifest.BenchmarkId))
        {
            throw new InvalidDataException(
                "OCR benchmark manifest benchmarkId is required.");
        }

        if (manifest.Source is null ||
            string.IsNullOrWhiteSpace(
                manifest.Source.Sha256) ||
            manifest.Source.ByteLength <= 0 ||
            manifest.Source.TotalPages <= 0)
        {
            throw new InvalidDataException(
                "OCR benchmark manifest source metadata is invalid.");
        }

        if (manifest.Rendering is null ||
            manifest.Rendering.Dpi <= 0 ||
            !string.Equals(
                manifest.Rendering.Format,
                "png",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "OCR benchmark manifest rendering settings are invalid.");
        }

        if (manifest.Pages is null ||
            manifest.Pages.Count == 0)
        {
            throw new InvalidDataException(
                "OCR benchmark manifest contains no pages.");
        }

        var duplicatePages =
            manifest.Pages
                .GroupBy(page =>
                    page.PageNumber)
                .Where(group =>
                    group.Count() > 1)
                .Select(group =>
                    group.Key)
                .ToArray();

        if (duplicatePages.Length > 0)
        {
            throw new InvalidDataException(
                $"OCR benchmark manifest contains duplicate pages: " +
                $"{string.Join(", ", duplicatePages)}.");
        }

        foreach (var page in manifest.Pages)
        {
            if (page.PageNumber < 1 ||
                page.PageNumber >
                manifest.Source.TotalPages)
            {
                throw new InvalidDataException(
                    $"OCR benchmark page {page.PageNumber} is out of range.");
            }

            if (string.IsNullOrWhiteSpace(
                    page.Group))
            {
                throw new InvalidDataException(
                    $"OCR benchmark page {page.PageNumber} has no group.");
            }

            if (string.Equals(
                    page.Group,
                    "outline-target",
                    StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(
                    page.ExpectedTitle))
            {
                throw new InvalidDataException(
                    $"Outline-target page {page.PageNumber} has no expected title.");
            }
        }
    }

    private static void ValidateInputIndex(
        OcrBenchmarkManifest manifest,
        OcrBenchmarkInputIndex index)
    {
        if (!string.Equals(
                index.SchemaVersion,
                InputIndexSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported OCR benchmark input-index schema '{index.SchemaVersion}'.");
        }

        if (!string.Equals(
                index.BenchmarkId,
                manifest.BenchmarkId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "OCR input index benchmarkId differs from the manifest.");
        }

        if (!string.Equals(
                index.SourceSha256,
                manifest.Source.Sha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "OCR input index source SHA-256 differs from the manifest.");
        }

        if (index.Rasterizer is null ||
            index.Rasterizer.Dpi !=
            manifest.Rendering.Dpi ||
            !string.Equals(
                index.Rasterizer.Format,
                manifest.Rendering.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "OCR input index rasterizer settings differ from the manifest.");
        }

        ValidatePageSet(
            "input index",
            manifest.Pages.Select(page =>
                page.PageNumber),
            index.Pages.Select(page =>
                page.PageNumber));

        foreach (var page in index.Pages)
        {
            if (page.Width <= 0 ||
                page.Height <= 0 ||
                page.ByteLength <= 0 ||
                string.IsNullOrWhiteSpace(
                    page.Sha256))
            {
                throw new InvalidDataException(
                    $"OCR input index page {page.PageNumber} is invalid.");
            }
        }
    }

    private static void ValidateEngineResult(
        OcrBenchmarkManifest manifest,
        OcrBenchmarkInputIndex inputIndex,
        OcrEngineResult result)
    {
        if (!string.Equals(
                result.SchemaVersion,
                EngineResultSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported OCR engine-result schema '{result.SchemaVersion}'.");
        }

        if (!string.Equals(
                result.BenchmarkId,
                manifest.BenchmarkId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "OCR engine result benchmarkId differs from the manifest.");
        }

        if (!string.Equals(
                result.SourceSha256,
                manifest.Source.Sha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "OCR engine result source SHA-256 differs from the manifest.");
        }

        if (result.Engine is null ||
            string.IsNullOrWhiteSpace(
                result.Engine.Id) ||
            string.IsNullOrWhiteSpace(
                result.Engine.Version) ||
            string.IsNullOrWhiteSpace(
                result.Engine.Model) ||
            string.IsNullOrWhiteSpace(
                result.Engine.Backend) ||
            string.IsNullOrWhiteSpace(
                result.Engine.Device))
        {
            throw new InvalidDataException(
                "OCR engine metadata is incomplete.");
        }

        ValidatePageSet(
            "engine result",
            manifest.Pages.Select(page =>
                page.PageNumber),
            result.Pages.Select(page =>
                page.PageNumber));

        var inputByPage =
            inputIndex.Pages
                .ToDictionary(page =>
                    page.PageNumber);

        foreach (var page in result.Pages)
        {
            var input =
                inputByPage[
                    page.PageNumber];

            if (!string.Equals(
                    page.InputSha256,
                    input.Sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"OCR result page {page.PageNumber} input SHA-256 differs from the rendered benchmark input.");
            }

            if (page.ImageWidth !=
                    input.Width ||
                page.ImageHeight !=
                    input.Height)
            {
                throw new InvalidDataException(
                    $"OCR result page {page.PageNumber} image dimensions differ from the rendered benchmark input.");
            }

            if (!double.IsFinite(
                    page.ElapsedMilliseconds) ||
                page.ElapsedMilliseconds < 0)
            {
                throw new InvalidDataException(
                    $"OCR result page {page.PageNumber} elapsed time is invalid.");
            }

            var duplicateSequences =
                page.Regions
                    .GroupBy(region =>
                        region.Sequence)
                    .Where(group =>
                        group.Count() > 1)
                    .Select(group =>
                        group.Key)
                    .ToArray();

            if (duplicateSequences.Length > 0)
            {
                throw new InvalidDataException(
                    $"OCR result page {page.PageNumber} contains duplicate region sequences.");
            }

            foreach (var region in page.Regions)
            {
                ValidateRegion(
                    page.PageNumber,
                    region);
            }
        }
    }

    private static void ValidateRegion(
        int pageNumber,
        OcrRegion region)
    {
        if (region.Sequence < 0)
        {
            throw new InvalidDataException(
                $"OCR result page {pageNumber} has a negative region sequence.");
        }

        if (region.Confidence is double confidence &&
            (!double.IsFinite(confidence) ||
             confidence < 0 ||
             confidence > 1))
        {
            throw new InvalidDataException(
                $"OCR result page {pageNumber} has invalid confidence.");
        }

        var bounds =
            region.Bounds;

        if (bounds is null ||
            !IsNormalizedCoordinate(bounds.Left) ||
            !IsNormalizedCoordinate(bounds.Top) ||
            !IsNormalizedCoordinate(bounds.Right) ||
            !IsNormalizedCoordinate(bounds.Bottom) ||
            bounds.Right < bounds.Left ||
            bounds.Bottom < bounds.Top)
        {
            throw new InvalidDataException(
                $"OCR result page {pageNumber} has invalid normalized bounds.");
        }
    }

    private static bool IsNormalizedCoordinate(
        double value) =>
        double.IsFinite(value) &&
        value >= 0 &&
        value <= 1;

    private static void ValidatePageSet(
        string label,
        IEnumerable<int> expectedPages,
        IEnumerable<int> actualPages)
    {
        var expected =
            expectedPages
                .OrderBy(page =>
                    page)
                .ToArray();

        var actual =
            actualPages
                .OrderBy(page =>
                    page)
                .ToArray();

        if (expected.SequenceEqual(
                actual))
        {
            return;
        }

        var missing =
            expected
                .Except(
                    actual)
                .ToArray();

        var unexpected =
            actual
                .Except(
                    expected)
                .ToArray();

        throw new InvalidDataException(
            $"OCR benchmark {label} page set mismatch. " +
            $"Missing=[{string.Join(",", missing)}], " +
            $"unexpected=[{string.Join(",", unexpected)}].");
    }

    private static async Task<T> ReadJsonAsync<T>(
        string path)
    {
        var fullPath =
            Path.GetFullPath(
                path);

        if (!File.Exists(
                fullPath))
        {
            throw new FileNotFoundException(
                "JSON input was not found.",
                fullPath);
        }

        await using var stream =
            File.OpenRead(
                fullPath);

        return await JsonSerializer.DeserializeAsync<T>(
                   stream,
                   JsonOptions) ??
               throw new InvalidDataException(
                   $"Could not deserialize '{fullPath}'.");
    }

    private static async Task WriteJsonAsync<T>(
        string path,
        T value)
    {
        var fullPath =
            Path.GetFullPath(
                path);

        var directory =
            Path.GetDirectoryName(
                fullPath);

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        var temporary =
            fullPath +
            ".tmp-" +
            Guid.NewGuid().ToString("N");

        try
        {
            await using var stream =
                File.Create(
                    temporary);

            await JsonSerializer.SerializeAsync(
                stream,
                value,
                JsonOptions);

            await stream.FlushAsync();

            File.Move(
                temporary,
                fullPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(
                    temporary))
            {
                File.Delete(
                    temporary);
            }
        }
    }

    private static async Task<string> ComputeSha256Async(
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

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

        options.Converters.Add(
            new JsonStringEnumConverter());

        return options;
    }

    private static void WriteEvaluationSummary(
        OcrBenchmarkEvaluationReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: OCR BENCHMARK EVALUATED");

        Console.WriteLine(
            $"Benchmark: {report.BenchmarkId}");

        Console.WriteLine(
            $"Engine: {report.Engine.Id} " +
            $"{report.Engine.Version} / " +
            $"{report.Engine.Model} / " +
            $"{report.Engine.Backend} / " +
            $"{report.Engine.Device}");

        Console.WriteLine(
            $"Coverage completed / failed / text pages: " +
            $"{report.Coverage.CompletedPages} / " +
            $"{report.Coverage.FailedPages} / " +
            $"{report.Coverage.PagesWithText}");

        Console.WriteLine(
            $"Regions / chars / elapsed ms: " +
            $"{report.Coverage.RegionCount} / " +
            $"{report.Coverage.CharacterCount} / " +
            $"{report.Coverage.TotalElapsedMilliseconds:F1}");

        Console.WriteLine(
            $"Raster reference recovered: " +
            $"{report.RasterReference.PagesWithText}/" +
            $"{report.RasterReference.PageCount}; " +
            $"chars={report.RasterReference.CharacterCount}; " +
            $"historical EasyOCR={report.RasterReference.HistoricalEasyOcrRecoveredPages}/" +
            $"{report.RasterReference.PageCount}, " +
            $"{report.RasterReference.HistoricalEasyOcrCharacterCount} chars");

        Console.WriteLine(
            $"Outline-title plausible / exploratory / none: " +
            $"{report.OutlineTitles.PlausibleMatches} / " +
            $"{report.OutlineTitles.ExploratoryMatches} / " +
            $"{report.OutlineTitles.NoCandidate}");

        Console.WriteLine(
            $"Born-digital controls with OCR text: " +
            $"{report.BornDigitalControls.PagesWithText}/" +
            $"{report.BornDigitalControls.PageCount}");

        Console.WriteLine(
            "Outline title bands:");

        foreach (var pair in report.OutlineTitles.BandCounts)
        {
            Console.WriteLine(
                $"  {pair.Key}: {pair.Value}");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private sealed record CorpusVerificationOptions(
        string ManifestPath,
        string SourcePath,
        string ReportPath)
    {
        public static CorpusVerificationOptions Parse(
            string[] args)
        {
            string? manifest = null;
            string? source = null;
            string? report = null;

            ParseCommon(
                args,
                (option, value) =>
                {
                    switch (option)
                    {
                        case "--manifest":
                            manifest = value;
                            break;
                        case "--source":
                            source = value;
                            break;
                        case "--report":
                            report = value;
                            break;
                        default:
                            throw new ArgumentException(
                                $"Unknown option '{option}'.");
                    }
                });

            return new CorpusVerificationOptions(
                RequiredPath(
                    "--manifest",
                    manifest),
                RequiredPath(
                    "--source",
                    source),
                RequiredPath(
                    "--report",
                    report));
        }
    }

    private sealed record EvaluationOptions(
        string ManifestPath,
        string InputIndexPath,
        string ResultPath,
        string ReportPath)
    {
        public static EvaluationOptions Parse(
            string[] args)
        {
            string? manifest = null;
            string? inputIndex = null;
            string? result = null;
            string? report = null;

            ParseCommon(
                args,
                (option, value) =>
                {
                    switch (option)
                    {
                        case "--manifest":
                            manifest = value;
                            break;
                        case "--input-index":
                            inputIndex = value;
                            break;
                        case "--result":
                            result = value;
                            break;
                        case "--report":
                            report = value;
                            break;
                        default:
                            throw new ArgumentException(
                                $"Unknown option '{option}'.");
                    }
                });

            return new EvaluationOptions(
                RequiredPath(
                    "--manifest",
                    manifest),
                RequiredPath(
                    "--input-index",
                    inputIndex),
                RequiredPath(
                    "--result",
                    result),
                RequiredPath(
                    "--report",
                    report));
        }
    }

    private static void ParseCommon(
        IReadOnlyList<string> args,
        Action<string, string> consume)
    {
        for (var index = 0;
             index < args.Count;
             index++)
        {
            var option =
                args[index];

            if (!option.StartsWith(
                    "--",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Unexpected argument '{option}'.");
            }

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

            consume(
                option,
                value);
        }
    }

    private static string RequiredPath(
        string option,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                $"{option} is required.");
        }

        return Path.GetFullPath(
            value);
    }

    private enum NativePageExpectation
    {
        TextlessDominantRaster,
        NativeText,
        Other
    }

    private enum OcrPageStatus
    {
        Completed,
        Failed
    }

    private enum TextContainmentRelation
    {
        None,
        Equal,
        ExpectedWithinCandidate,
        CandidateWithinExpected
    }

    private enum TitleMatchBand
    {
        None,
        WeakOverlap,
        ModerateOverlap,
        HighOverlap,
        Containment,
        ExactEquivalent
    }

    private sealed record OcrBenchmarkManifest(
        string SchemaVersion,
        string BenchmarkId,
        string Description,
        OcrBenchmarkSource Source,
        OcrBenchmarkRendering Rendering,
        OcrHistoricalReferences HistoricalReferences,
        IReadOnlyList<OcrBenchmarkPage> Pages);

    private sealed record OcrBenchmarkSource(
        string FileName,
        string Sha256,
        long ByteLength,
        int TotalPages);

    private sealed record OcrBenchmarkRendering(
        string Format,
        int Dpi,
        string ColorMode,
        string Preprocessing);

    private sealed record OcrHistoricalReferences(
        string RasterReferencePageRange,
        int EasyOcrRecoveredPages,
        int EasyOcrRecoveredCharacters,
        string Note);

    private sealed record OcrBenchmarkPage(
        int PageNumber,
        string Group,
        NativePageExpectation ExpectedNativeState,
        string? ExpectedTitle = null,
        string? ExpectedTitleSource = null);

    private sealed record OcrBenchmarkInputIndex(
        string SchemaVersion,
        string BenchmarkId,
        string SourceSha256,
        OcrBenchmarkRasterizer Rasterizer,
        IReadOnlyList<OcrBenchmarkInputPage> Pages);

    private sealed record OcrBenchmarkRasterizer(
        string Id,
        string Version,
        int Dpi,
        string Format,
        string ColorMode,
        string Preprocessing);

    private sealed record OcrBenchmarkInputPage(
        int PageNumber,
        string FileName,
        string Sha256,
        long ByteLength,
        int Width,
        int Height);

    private sealed record OcrEngineResult(
        string SchemaVersion,
        string BenchmarkId,
        string SourceSha256,
        OcrEngineIdentity Engine,
        OcrPerformanceObservation? Performance,
        IReadOnlyList<OcrEnginePageResult> Pages);

    private sealed record OcrEngineIdentity(
        string Id,
        string Version,
        string Model,
        string Backend,
        string Device,
        IReadOnlyDictionary<string, string>? Metadata);

    private sealed record OcrPerformanceObservation(
        double? StartupMilliseconds,
        long? ProcessPeakWorkingSetBytes,
        long? AcceleratorPeakMemoryBytes);

    private sealed record OcrEnginePageResult(
        int PageNumber,
        string InputSha256,
        OcrPageStatus Status,
        double ElapsedMilliseconds,
        int ImageWidth,
        int ImageHeight,
        IReadOnlyList<OcrRegion> Regions,
        IReadOnlyList<string>? Diagnostics);

    private sealed record OcrRegion(
        int Sequence,
        string Text,
        double? Confidence,
        OcrBounds Bounds);

    private sealed record OcrBounds(
        double Left,
        double Top,
        double Right,
        double Bottom);

    private sealed record CorpusVerificationReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string BenchmarkId,
        string SourceSha256,
        int PageCount,
        int MatchingPages,
        int MismatchingPages,
        IReadOnlyList<CorpusPageVerification> Pages);

    private sealed record CorpusPageVerification(
        int PageNumber,
        string Group,
        NativePageExpectation ExpectedNativeState,
        NativePageExpectation ObservedNativeState,
        int WordCount,
        int BlockCount,
        double LargestRasterImageAreaRatio,
        bool MatchesExpectedState);

    private sealed record OcrBenchmarkEvaluationReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string BenchmarkId,
        string SourceSha256,
        OcrEngineIdentity Engine,
        OcrPerformanceObservation? Performance,
        OcrCoverageSummary Coverage,
        OcrRasterReferenceSummary RasterReference,
        OcrOutlineTitleSummary OutlineTitles,
        OcrBornDigitalControlSummary BornDigitalControls,
        IReadOnlyList<OcrPageEvaluation> Pages);

    private sealed record OcrCoverageSummary(
        int ExpectedPages,
        int CompletedPages,
        int FailedPages,
        int PagesWithText,
        int RegionCount,
        int CharacterCount,
        double TotalElapsedMilliseconds);

    private sealed record OcrRasterReferenceSummary(
        int PageCount,
        int PagesWithText,
        int CharacterCount,
        int HistoricalEasyOcrRecoveredPages,
        int HistoricalEasyOcrCharacterCount);

    private sealed record OcrOutlineTitleSummary(
        int PageCount,
        int PlausibleMatches,
        int ExploratoryMatches,
        int NoCandidate,
        IReadOnlyDictionary<TitleMatchBand, int> BandCounts);

    private sealed record OcrBornDigitalControlSummary(
        int PageCount,
        int PagesWithText,
        int CharacterCount);

    private sealed record OcrPageEvaluation(
        int PageNumber,
        string Group,
        NativePageExpectation ExpectedNativeState,
        OcrPageStatus Status,
        double ElapsedMilliseconds,
        int ImageWidth,
        int ImageHeight,
        bool InputIntegrityMatches,
        int RegionCount,
        int CharacterCount,
        bool HasText,
        string? ExpectedTitle,
        string? ExpectedTitleSource,
        OcrTitleMatch? TitleMatch);

    private sealed record OcrTitleMatch(
        TitleMatchBand Band,
        TextContainmentRelation Containment,
        int FirstRegionSequence,
        int ClusterSize,
        string CandidateText,
        int SharedTokenCount,
        int ExpectedTokenCount,
        int CandidateTokenCount,
        double ExpectedTokenCoverage,
        double CandidateTokenCoverage);

    private sealed record LexicalMetrics(
        int SharedTokenCount,
        int ExpectedTokenCount,
        int CandidateTokenCount,
        double ExpectedTokenCoverage,
        double CandidateTokenCoverage,
        TextContainmentRelation Containment);
}
