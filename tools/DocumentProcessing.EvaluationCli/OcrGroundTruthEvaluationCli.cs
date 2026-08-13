using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocumentProcessing.EvaluationCli;

internal static class OcrGroundTruthEvaluationCli
{
    private const string GroundTruthSchemaVersion =
        "document-processing-ocr-ground-truth-v1";

    private const string EngineResultSchemaVersion =
        "document-processing-ocr-engine-result-v1";

    private const string ReportSchemaVersion =
        "document-processing-ocr-ground-truth-evaluation-v1";

    private static readonly Regex LineBreakHyphenRegex =
        new(
            @"(?<=\p{L})-\s*\n\s*(?=\p{L})",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex =
        new(
            @"\s+",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly Regex WordRegex =
        new(
            @"[\p{L}\p{Nd}]+(?:'[\p{L}\p{Nd}]+)*",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly JsonSerializerOptions ReadOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    private static readonly JsonSerializerOptions WriteOptions =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

    public static async Task<int> RunAsync(
        string[] args)
    {
        var options =
            EvaluationOptions.Parse(
                args);

        var groundTruth =
            await ReadJsonAsync<GroundTruthManifest>(
                options.GroundTruthPath);

        var engineResult =
            await ReadJsonAsync<OcrEngineResult>(
                options.ResultPath);

        Validate(
            groundTruth,
            engineResult);

        var pageByNumber =
            engineResult.Pages
                .ToDictionary(
                    page =>
                        page.PageNumber);

        var zoneEvaluations =
            groundTruth.Zones
                .Select(zone =>
                    EvaluateZone(
                        zone,
                        pageByNumber[
                            zone.PageNumber]))
                .ToArray();

        var totalReferenceCharacters =
            zoneEvaluations.Sum(zone =>
                zone.ReferenceCharacterCount);

        var totalCharacterEdits =
            zoneEvaluations.Sum(zone =>
                zone.CharacterEdits);

        var totalReferenceWords =
            zoneEvaluations.Sum(zone =>
                zone.ReferenceWordCount);

        var totalWordEdits =
            zoneEvaluations.Sum(zone =>
                zone.WordEdits);

        var report =
            new GroundTruthEvaluationReport(
                ReportSchemaVersion,
                DateTimeOffset.UtcNow,
                groundTruth.BenchmarkId,
                groundTruth.SourceSha256,
                groundTruth.Status,
                groundTruth.ReferenceMethod,
                groundTruth.NormalizationProfile,
                engineResult.Engine,
                zoneEvaluations.Length,
                totalReferenceCharacters,
                totalCharacterEdits,
                Divide(
                    totalCharacterEdits,
                    totalReferenceCharacters),
                totalReferenceWords,
                totalWordEdits,
                Divide(
                    totalWordEdits,
                    totalReferenceWords),
                zoneEvaluations);

        await WriteJsonAsync(
            options.ReportPath,
            report);

        WriteSummary(
            report,
            options.ReportPath);

        return 0;
    }

    private static OcrZoneEvaluation EvaluateZone(
        GroundTruthZone zone,
        OcrEnginePageResult page)
    {
        if (!string.Equals(
                page.Status,
                "Completed",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Ground-truth page {zone.PageNumber} did not complete OCR.");
        }

        var selected =
            page.Regions
                .Where(region =>
                    IsRegionCenterInside(
                        region.Bounds,
                        zone.Bounds))
                .OrderBy(region =>
                    region.Sequence)
                .ToArray();

        if (selected.Length == 0)
        {
            throw new InvalidDataException(
                $"Ground-truth zone '{zone.Id}' selected no OCR regions.");
        }

        var candidateRaw =
            string.Join(
                '\n',
                selected.Select(region =>
                    region.Text));

        var referenceNormalized =
            NormalizeForCer(
                zone.Text);

        var candidateNormalized =
            NormalizeForCer(
                candidateRaw);

        if (referenceNormalized.Length == 0)
        {
            throw new InvalidDataException(
                $"Ground-truth zone '{zone.Id}' has an empty normalized reference.");
        }

        var referenceRunes =
            referenceNormalized
                .EnumerateRunes()
                .Select(rune =>
                    rune.Value)
                .ToArray();

        var candidateRunes =
            candidateNormalized
                .EnumerateRunes()
                .Select(rune =>
                    rune.Value)
                .ToArray();

        var characterEdits =
            Levenshtein(
                referenceRunes,
                candidateRunes);

        var referenceWords =
            TokenizeWords(
                referenceNormalized);

        var candidateWords =
            TokenizeWords(
                candidateNormalized);

        if (referenceWords.Length == 0)
        {
            throw new InvalidDataException(
                $"Ground-truth zone '{zone.Id}' has no reference words.");
        }

        var wordEdits =
            Levenshtein(
                referenceWords,
                candidateWords);

        return new OcrZoneEvaluation(
            zone.Id,
            zone.PageNumber,
            zone.Description,
            zone.Bounds,
            selected.Length,
            referenceRunes.Length,
            candidateRunes.Length,
            characterEdits,
            Divide(
                characterEdits,
                referenceRunes.Length),
            referenceWords.Length,
            candidateWords.Length,
            wordEdits,
            Divide(
                wordEdits,
                referenceWords.Length),
            referenceNormalized,
            candidateNormalized);
    }

    private static bool IsRegionCenterInside(
        NormalizedBounds region,
        NormalizedBounds zone)
    {
        var centerX =
            (region.Left +
             region.Right) / 2.0;

        var centerY =
            (region.Top +
             region.Bottom) / 2.0;

        return centerX >= zone.Left &&
               centerX <= zone.Right &&
               centerY >= zone.Top &&
               centerY <= zone.Bottom;
    }

    private static string NormalizeForCer(
        string text)
    {
        var normalized =
            text
                .Normalize(
                    NormalizationForm.FormC)
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Replace(
                    '\r',
                    '\n');

        normalized =
            LineBreakHyphenRegex.Replace(
                normalized,
                string.Empty);

        var builder =
            new StringBuilder(
                normalized.Length);

        foreach (var character in normalized)
        {
            switch (character)
            {
                case '\u00AD':
                    break;

                case '\u00A0':
                    builder.Append(' ');
                    break;

                case '\u2018':
                case '\u2019':
                case '\u02BC':
                    builder.Append('\'');
                    break;

                case '\u201C':
                case '\u201D':
                    builder.Append('"');
                    break;

                case '\u2010':
                case '\u2011':
                case '\u2012':
                case '\u2013':
                case '\u2014':
                case '\u2015':
                    builder.Append('-');
                    break;

                default:
                    builder.Append(
                        char.ToLowerInvariant(
                            character));
                    break;
            }
        }

        return WhitespaceRegex
            .Replace(
                builder.ToString(),
                " ")
            .Trim();
    }

    private static string[] TokenizeWords(
        string normalizedText) =>
        WordRegex
            .Matches(
                normalizedText)
            .Select(match =>
                match.Value)
            .ToArray();

    private static int Levenshtein<T>(
        IReadOnlyList<T> reference,
        IReadOnlyList<T> candidate)
        where T : notnull
    {
        if (reference.Count == 0)
        {
            return candidate.Count;
        }

        if (candidate.Count == 0)
        {
            return reference.Count;
        }

        var previous =
            new int[
                candidate.Count + 1];

        var current =
            new int[
                candidate.Count + 1];

        for (var index = 0;
             index <= candidate.Count;
             index++)
        {
            previous[index] =
                index;
        }

        var comparer =
            EqualityComparer<T>.Default;

        for (var referenceIndex = 1;
             referenceIndex <= reference.Count;
             referenceIndex++)
        {
            current[0] =
                referenceIndex;

            for (var candidateIndex = 1;
                 candidateIndex <= candidate.Count;
                 candidateIndex++)
            {
                var substitutionCost =
                    comparer.Equals(
                        reference[
                            referenceIndex - 1],
                        candidate[
                            candidateIndex - 1])
                        ? 0
                        : 1;

                current[candidateIndex] =
                    Math.Min(
                        Math.Min(
                            previous[
                                candidateIndex] + 1,
                            current[
                                candidateIndex - 1] + 1),
                        previous[
                            candidateIndex - 1] +
                        substitutionCost);
            }

            (previous, current) =
                (current, previous);
        }

        return previous[
            candidate.Count];
    }

    private static double Divide(
        int numerator,
        int denominator) =>
        denominator == 0
            ? 0
            : Math.Round(
                numerator /
                (double)denominator,
                6);

    private static void Validate(
        GroundTruthManifest groundTruth,
        OcrEngineResult engineResult)
    {
        if (!string.Equals(
                groundTruth.SchemaVersion,
                GroundTruthSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported OCR ground-truth schema '{groundTruth.SchemaVersion}'.");
        }

        if (!string.Equals(
                engineResult.SchemaVersion,
                EngineResultSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported OCR engine-result schema '{engineResult.SchemaVersion}'.");
        }

        if (!string.Equals(
                groundTruth.BenchmarkId,
                engineResult.BenchmarkId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Ground truth and OCR result benchmarkId differ.");
        }

        if (!string.Equals(
                groundTruth.SourceSha256,
                engineResult.SourceSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Ground truth and OCR result source SHA-256 differ.");
        }

        if (groundTruth.Zones is null ||
            groundTruth.Zones.Count == 0)
        {
            throw new InvalidDataException(
                "OCR ground truth contains no zones.");
        }

        var duplicateIds =
            groundTruth.Zones
                .GroupBy(zone =>
                    zone.Id,
                    StringComparer.Ordinal)
                .Where(group =>
                    group.Count() > 1)
                .Select(group =>
                    group.Key)
                .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidDataException(
                $"OCR ground truth contains duplicate zone IDs: {string.Join(", ", duplicateIds)}.");
        }

        var pageNumbers =
            engineResult.Pages
                .Select(page =>
                    page.PageNumber)
                .ToHashSet();

        foreach (var zone in groundTruth.Zones)
        {
            if (string.IsNullOrWhiteSpace(
                    zone.Id) ||
                string.IsNullOrWhiteSpace(
                    zone.Text))
            {
                throw new InvalidDataException(
                    "OCR ground-truth zone ID and text are required.");
            }

            ValidateBounds(
                zone.Id,
                zone.Bounds);

            if (!pageNumbers.Contains(
                    zone.PageNumber))
            {
                throw new InvalidDataException(
                    $"OCR result does not contain ground-truth page {zone.PageNumber}.");
            }
        }

        foreach (var page in engineResult.Pages)
        {
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
                ValidateBounds(
                    $"page {page.PageNumber} region {region.Sequence}",
                    region.Bounds);
            }
        }
    }

    private static void ValidateBounds(
        string label,
        NormalizedBounds bounds)
    {
        if (!double.IsFinite(
                bounds.Left) ||
            !double.IsFinite(
                bounds.Top) ||
            !double.IsFinite(
                bounds.Right) ||
            !double.IsFinite(
                bounds.Bottom) ||
            bounds.Left < 0 ||
            bounds.Top < 0 ||
            bounds.Right > 1 ||
            bounds.Bottom > 1 ||
            bounds.Left > bounds.Right ||
            bounds.Top > bounds.Bottom)
        {
            throw new InvalidDataException(
                $"Invalid normalized bounds for {label}.");
        }
    }

    private static async Task<T> ReadJsonAsync<T>(
        string path)
    {
        await using var stream =
            File.OpenRead(
                Path.GetFullPath(
                    path));

        return await JsonSerializer
                   .DeserializeAsync<T>(
                       stream,
                       ReadOptions) ??
               throw new InvalidDataException(
                   $"Could not deserialize JSON: {path}");
    }

    private static async Task WriteJsonAsync(
        string path,
        GroundTruthEvaluationReport report)
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

        await using var stream =
            File.Create(
                fullPath);

        await JsonSerializer.SerializeAsync(
            stream,
            report,
            WriteOptions);
    }

    private static void WriteSummary(
        GroundTruthEvaluationReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: OCR GROUND TRUTH EVALUATED");

        Console.WriteLine(
            $"Benchmark: {report.BenchmarkId}");

        Console.WriteLine(
            $"Engine: {report.Engine.Id} {report.Engine.Version} / " +
            $"{report.Engine.Model} / {report.Engine.Backend} / {report.Engine.Device}");

        Console.WriteLine(
            $"Zones: {report.ZoneCount}");

        Console.WriteLine(
            $"CER: {report.CharacterErrorRate:P3} " +
            $"({report.CharacterEdits}/{report.ReferenceCharacterCount})");

        Console.WriteLine(
            $"WER: {report.WordErrorRate:P3} " +
            $"({report.WordEdits}/{report.ReferenceWordCount})");

        Console.WriteLine(
            "Per-zone CER / WER:");

        foreach (var zone in report.Zones)
        {
            Console.WriteLine(
                $"  {zone.Id} p{zone.PageNumber}: " +
                $"CER={zone.CharacterErrorRate:P3} " +
                $"WER={zone.WordErrorRate:P3} " +
                $"regions={zone.SelectedRegionCount}");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private sealed record EvaluationOptions(
        string GroundTruthPath,
        string ResultPath,
        string ReportPath)
    {
        public static EvaluationOptions Parse(
            string[] args)
        {
            string? groundTruth =
                null;

            string? result =
                null;

            string? report =
                null;

            for (var index = 0;
                 index < args.Length;
                 index++)
            {
                var option =
                    args[index];

                switch (option)
                {
                    case "--ground-truth":
                        groundTruth =
                            ReadValue(
                                args,
                                ref index,
                                option);
                        break;

                    case "--result":
                        result =
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

                    default:
                        throw new ArgumentException(
                            $"Unknown option '{option}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(
                    groundTruth) ||
                string.IsNullOrWhiteSpace(
                    result) ||
                string.IsNullOrWhiteSpace(
                    report))
            {
                throw new ArgumentException(
                    "--ground-truth, --result and --report are required.");
            }

            return new EvaluationOptions(
                Path.GetFullPath(
                    groundTruth),
                Path.GetFullPath(
                    result),
                Path.GetFullPath(
                    report));
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

    private sealed record GroundTruthManifest(
        string SchemaVersion,
        string BenchmarkId,
        string SourceSha256,
        string Status,
        string ReferenceMethod,
        string NormalizationProfile,
        IReadOnlyList<GroundTruthZone> Zones);

    private sealed record GroundTruthZone(
        string Id,
        int PageNumber,
        string Description,
        NormalizedBounds Bounds,
        string Text);

    private sealed record OcrEngineResult(
        string SchemaVersion,
        string BenchmarkId,
        string SourceSha256,
        OcrEngineMetadata Engine,
        IReadOnlyList<OcrEnginePageResult> Pages);

    private sealed record OcrEngineMetadata(
        string Id,
        string Version,
        string Model,
        string Backend,
        string Device);

    private sealed record OcrEnginePageResult(
        int PageNumber,
        string Status,
        IReadOnlyList<OcrRegion> Regions);

    private sealed record OcrRegion(
        int Sequence,
        string Text,
        NormalizedBounds Bounds);

    private sealed record NormalizedBounds(
        double Left,
        double Top,
        double Right,
        double Bottom);

    private sealed record GroundTruthEvaluationReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string BenchmarkId,
        string SourceSha256,
        string GroundTruthStatus,
        string ReferenceMethod,
        string NormalizationProfile,
        OcrEngineMetadata Engine,
        int ZoneCount,
        int ReferenceCharacterCount,
        int CharacterEdits,
        double CharacterErrorRate,
        int ReferenceWordCount,
        int WordEdits,
        double WordErrorRate,
        IReadOnlyList<OcrZoneEvaluation> Zones);

    private sealed record OcrZoneEvaluation(
        string Id,
        int PageNumber,
        string Description,
        NormalizedBounds Bounds,
        int SelectedRegionCount,
        int ReferenceCharacterCount,
        int CandidateCharacterCount,
        int CharacterEdits,
        double CharacterErrorRate,
        int ReferenceWordCount,
        int CandidateWordCount,
        int WordEdits,
        double WordErrorRate,
        string ReferenceNormalized,
        string CandidateNormalized);
}
