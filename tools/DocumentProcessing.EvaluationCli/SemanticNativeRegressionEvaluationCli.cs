using System.Security.Cryptography;
using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Pdf;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.EvaluationCli;

internal static class SemanticNativeRegressionEvaluationCli
{
    private const string GroundTruthSchemaVersion =
        "document-processing-semantic-regression-ground-truth-v1";

    private const string ReportSchemaVersion =
        "document-processing-semantic-native-regression-v1";

    private static readonly JsonSerializerOptions WriteOptions =
        new()
        {
            WriteIndented =
                true,
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

    public static async Task<int> RunAsync(
        string[] args)
    {
        var options =
            EvaluationOptions.Parse(
                args);

        var expected =
            await ReadExpectationsAsync(
                options.GroundTruthPath);

        var manifestRows =
            await ReadManifestAsync(
                options.ManifestPath);

        var evaluation =
            await EvaluateAsync(
                expected,
                manifestRows,
                options.FixturesDirectory);

        var report =
            new SemanticNativeRegressionReport(
                ReportSchemaVersion,
                DateTimeOffset.UtcNow,
                expected.BaselineObserved,
                evaluation.Provenance.Pass &&
                evaluation.Native.Pass,
                evaluation.Provenance,
                evaluation.Native);

        await WriteJsonAsync(
            options.ReportPath,
            report);

        WriteSummary(
            report,
            options.ReportPath);

        return report.Pass
            ? 0
            : 1;
    }

    private static async Task<EvaluationResult> EvaluateAsync(
        GroundTruthExpectations expected,
        IReadOnlyList<ManifestRow> manifestRows,
        string fixturesDirectory)
    {
        var provenanceMismatches =
            new List<string>();

        var manifestFixtureNames =
            manifestRows
                .Select(
                    row =>
                        row.Fixture)
                .ToHashSet(
                    StringComparer.Ordinal);

        var actualFixtureNames =
            Directory
                .EnumerateFiles(
                    fixturesDirectory,
                    "*.pdf",
                    SearchOption.TopDirectoryOnly)
                .Select(
                    Path.GetFileName)
                .OfType<string>()
                .ToHashSet(
                    StringComparer.Ordinal);

        foreach (var missing in
                 manifestFixtureNames
                     .Except(
                         actualFixtureNames,
                         StringComparer.Ordinal)
                     .OrderBy(
                         value =>
                             value,
                         StringComparer.Ordinal))
        {
            provenanceMismatches.Add(
                $"Manifest fixture is missing from disk: {missing}");
        }

        foreach (var extra in
                 actualFixtureNames
                     .Except(
                         manifestFixtureNames,
                         StringComparer.Ordinal)
                     .OrderBy(
                         value =>
                             value,
                         StringComparer.Ordinal))
        {
            provenanceMismatches.Add(
                $"Fixture exists on disk but not in manifest: {extra}");
        }

        var duplicateFixtures =
            manifestRows
                .GroupBy(
                    row =>
                        row.Fixture,
                    StringComparer.Ordinal)
                .Where(
                    group =>
                        group.Count() >
                        1)
                .Select(
                    group =>
                        group.Key)
                .OrderBy(
                    value =>
                        value,
                    StringComparer.Ordinal)
                .ToArray();

        foreach (var duplicate in
                 duplicateFixtures)
        {
            provenanceMismatches.Add(
                $"Duplicate fixture manifest row: {duplicate}");
        }

        var observedCounts =
            manifestRows
                .GroupBy(
                    row =>
                        row.Corpus,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.Count(),
                    StringComparer.Ordinal);

        if (manifestRows.Count !=
            expected.Provenance.FixtureCount)
        {
            provenanceMismatches.Add(
                $"Expected {expected.Provenance.FixtureCount} fixture rows, " +
                $"observed {manifestRows.Count}.");
        }

        CompareCorpusCount(
            "Ehrman",
            expected.Provenance.EhrmanCount,
            observedCounts,
            provenanceMismatches);

        CompareCorpusCount(
            "Habermas",
            expected.Provenance.HabermasCount,
            observedCounts,
            provenanceMismatches);

        CompareCorpusCount(
            "DeDecretis",
            expected.Provenance.DeDecretisCount,
            observedCounts,
            provenanceMismatches);

        var fixtureObservations =
            new Dictionary<string, FixtureObservation>(
                StringComparer.Ordinal);

        var planner =
            DocumentPageProcessingPlanner
                .CreateDefault();

        foreach (var row in
                 manifestRows
                     .OrderBy(
                         row =>
                             row.Fixture,
                         StringComparer.Ordinal))
        {
            var rowMismatches =
                new List<string>();

            if (!expected.Provenance.SourceSha256ByCorpus
                    .TryGetValue(
                        row.Corpus,
                        out var expectedSourceSha))
            {
                rowMismatches.Add(
                    $"Unsupported corpus '{row.Corpus}'.");
            }
            else if (!string.Equals(
                         row.SourceSha256,
                         expectedSourceSha,
                         StringComparison.Ordinal))
            {
                rowMismatches.Add(
                    $"Source SHA mismatch for corpus {row.Corpus}.");
            }

            if (row.FixturePage !=
                expected.Provenance.StandaloneFixturePhysicalPage)
            {
                rowMismatches.Add(
                    $"Manifest fixture_page={row.FixturePage}; " +
                    $"expected {expected.Provenance.StandaloneFixturePhysicalPage}.");
            }

            var expectedFixtureName =
                BuildFixtureFileName(
                    row.Corpus,
                    row.SourcePhysicalPage);

            if (!string.Equals(
                    row.Fixture,
                    expectedFixtureName,
                    StringComparison.Ordinal))
            {
                rowMismatches.Add(
                    $"Fixture/source-page identity mismatch: " +
                    $"{row.Fixture} != {expectedFixtureName}.");
            }

            var fixturePath =
                Path.Combine(
                    fixturesDirectory,
                    row.Fixture);

            if (!File.Exists(
                    fixturePath))
            {
                foreach (var mismatch in
                         rowMismatches)
                {
                    provenanceMismatches.Add(
                        $"{row.Fixture}: {mismatch}");
                }

                provenanceMismatches.Add(
                    $"{row.Fixture}: fixture file is missing.");
                continue;
            }

            var actualLength =
                new FileInfo(
                    fixturePath)
                    .Length;

            if (actualLength !=
                row.Bytes)
            {
                rowMismatches.Add(
                    $"Fixture byte length mismatch: " +
                    $"manifest={row.Bytes}, actual={actualLength}.");
            }

            var actualSha256 =
                await ComputeSha256Async(
                    fixturePath);

            if (!string.Equals(
                    actualSha256,
                    row.FixtureSha256,
                    StringComparison.Ordinal))
            {
                rowMismatches.Add(
                    "Fixture SHA-256 does not match manifest.");
            }

            var extraction =
                await ExtractAsync(
                    fixturePath);

            if (extraction.Pages.Count !=
                1)
            {
                rowMismatches.Add(
                    $"Fixture extraction returned {extraction.Pages.Count} pages; expected 1.");

                foreach (var mismatch in
                         rowMismatches)
                {
                    provenanceMismatches.Add(
                        $"{row.Fixture}: {mismatch}");
                }

                continue;
            }

            var page =
                extraction.Pages[0];

            if (page.PhysicalPageNumber !=
                expected.Provenance.StandaloneFixturePhysicalPage)
            {
                rowMismatches.Add(
                    $"Fixture extraction PhysicalPageNumber=" +
                    $"{page.PhysicalPageNumber}; expected " +
                    $"{expected.Provenance.StandaloneFixturePhysicalPage}.");
            }

            var decision =
                planner
                    .Plan(
                        extraction)
                    .Single();

            var observation =
                new FixtureObservation(
                    row.Fixture,
                    row.Corpus,
                    row.SourcePhysicalPage,
                    page.PhysicalPageNumber,
                    actualSha256,
                    actualLength,
                    page.WordCount,
                    page.Blocks.Count,
                    decision.Assessment.NativeTextStatus.ToString(),
                    decision.Plan.Route.ToString(),
                    rowMismatches.Count ==
                        0,
                    rowMismatches);

            fixtureObservations.Add(
                row.Fixture,
                observation);

            foreach (var mismatch in
                     rowMismatches)
            {
                provenanceMismatches.Add(
                    $"{row.Fixture}: {mismatch}");
            }
        }

        var provenance =
            new ProvenanceEvaluation(
                provenanceMismatches.Count ==
                0,
                expected.Provenance.FixtureCount,
                manifestRows.Count,
                expected.Provenance.EhrmanCount,
                observedCounts.GetValueOrDefault(
                    "Ehrman"),
                expected.Provenance.HabermasCount,
                observedCounts.GetValueOrDefault(
                    "Habermas"),
                expected.Provenance.DeDecretisCount,
                observedCounts.GetValueOrDefault(
                    "DeDecretis"),
                provenanceMismatches);

        var native =
            EvaluateNativeControls(
                expected.Native,
                fixtureObservations);

        return new EvaluationResult(
            provenance,
            native);
    }

    private static NativeEvaluation EvaluateNativeControls(
        NativeExpectation expected,
        IReadOnlyDictionary<string, FixtureObservation> observations)
    {
        var mismatches =
            new List<string>();

        var habermas =
            new List<NativePageObservation>();

        foreach (var pageNumber in
                 expected.HabermasPages)
        {
            var fixtureName =
                BuildFixtureFileName(
                    "Habermas",
                    pageNumber);

            if (!observations.TryGetValue(
                    fixtureName,
                    out var observation))
            {
                mismatches.Add(
                    $"Native Habermas control is missing: {fixtureName}");
                continue;
            }

            var pagePass =
                string.Equals(
                    observation.NativeStatus,
                    expected.HabermasStatus,
                    StringComparison.Ordinal) &&
                string.Equals(
                    observation.Route,
                    expected.HabermasRoute,
                    StringComparison.Ordinal);

            if (!pagePass)
            {
                mismatches.Add(
                    $"{fixtureName}: expected " +
                    $"{expected.HabermasStatus}/{expected.HabermasRoute}, " +
                    $"observed {observation.NativeStatus}/{observation.Route}.");
            }

            habermas.Add(
                new NativePageObservation(
                    fixtureName,
                    pageNumber,
                    observation.WordCount,
                    observation.BlockCount,
                    observation.NativeStatus,
                    observation.Route,
                    pagePass));
        }

        var deDecretis =
            new List<NativePageObservation>();

        for (var pageNumber =
                 expected.DeDecretisFirstPage;
             pageNumber <=
             expected.DeDecretisLastPage;
             pageNumber++)
        {
            var fixtureName =
                BuildFixtureFileName(
                    "DeDecretis",
                    pageNumber);

            if (!observations.TryGetValue(
                    fixtureName,
                    out var observation))
            {
                mismatches.Add(
                    $"Native De Decretis control is missing: {fixtureName}");
                continue;
            }

            var pagePass =
                string.Equals(
                    observation.NativeStatus,
                    expected.DeDecretisStatus,
                    StringComparison.Ordinal) &&
                string.Equals(
                    observation.Route,
                    expected.DeDecretisRoute,
                    StringComparison.Ordinal);

            if (!pagePass)
            {
                mismatches.Add(
                    $"{fixtureName}: expected " +
                    $"{expected.DeDecretisStatus}/{expected.DeDecretisRoute}, " +
                    $"observed {observation.NativeStatus}/{observation.Route}.");
            }

            deDecretis.Add(
                new NativePageObservation(
                    fixtureName,
                    pageNumber,
                    observation.WordCount,
                    observation.BlockCount,
                    observation.NativeStatus,
                    observation.Route,
                    pagePass));
        }

        var deDecretisWords =
            deDecretis.Sum(
                observation =>
                    observation.WordCount);

        var deDecretisBlocks =
            deDecretis.Sum(
                observation =>
                    observation.BlockCount);

        if (deDecretis.Count !=
            expected.DeDecretisCount)
        {
            mismatches.Add(
                $"Expected {expected.DeDecretisCount} De Decretis native controls, " +
                $"observed {deDecretis.Count}.");
        }

        if (deDecretisWords !=
            expected.DeDecretisWords)
        {
            mismatches.Add(
                $"Expected {expected.DeDecretisWords} De Decretis words, " +
                $"observed {deDecretisWords}.");
        }

        if (deDecretisBlocks !=
            expected.DeDecretisBlocks)
        {
            mismatches.Add(
                $"Expected {expected.DeDecretisBlocks} De Decretis blocks, " +
                $"observed {deDecretisBlocks}.");
        }

        return new NativeEvaluation(
            mismatches.Count ==
            0,
            habermas,
            new DeDecretisNativeSummary(
                expected.DeDecretisFirstPage,
                expected.DeDecretisLastPage,
                expected.DeDecretisCount,
                deDecretis.Count,
                expected.DeDecretisWords,
                deDecretisWords,
                expected.DeDecretisBlocks,
                deDecretisBlocks,
                expected.DeDecretisStatus,
                expected.DeDecretisRoute,
                deDecretis),
            mismatches);
    }

    private static void CompareCorpusCount(
        string corpus,
        int expected,
        IReadOnlyDictionary<string, int> observedCounts,
        ICollection<string> mismatches)
    {
        var actual =
            observedCounts.GetValueOrDefault(
                corpus);

        if (actual !=
            expected)
        {
            mismatches.Add(
                $"Expected {expected} {corpus} fixtures, observed {actual}.");
        }
    }

    private static async Task<GroundTruthExpectations>
        ReadExpectationsAsync(
            string path)
    {
        await using var stream =
            File.OpenRead(
                Path.GetFullPath(
                    path));

        using var document =
            await JsonDocument.ParseAsync(
                stream);

        var root =
            document.RootElement;

        var schemaVersion =
            root.GetProperty(
                    "schemaVersion")
                .GetString();

        if (!string.Equals(
                schemaVersion,
                GroundTruthSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported semantic ground-truth schema '{schemaVersion}'.");
        }

        var baselineObserved =
            RequiredString(
                root,
                "baselineObserved");

        var controls =
            root.GetProperty(
                "controls");

        var nativeControl =
            FindControl(
                controls,
                "native-controls");

        var provenanceControl =
            FindControl(
                controls,
                "fixture-provenance");

        var nativeExpected =
            nativeControl.GetProperty(
                "expected");

        var provenanceExpected =
            provenanceControl.GetProperty(
                "expected");

        var habermasPages =
            nativeExpected
                .GetProperty(
                    "habermasPages")
                .EnumerateArray()
                .Select(
                    element =>
                        element.GetInt32())
                .ToArray();

        if (habermasPages.Length ==
            0)
        {
            throw new InvalidDataException(
                "Native semantic oracle has no Habermas pages.");
        }

        var deDecretisRange =
            ParsePageRange(
                RequiredString(
                    nativeExpected,
                    "deDecretisPages"));

        var sourceSha256ByCorpus =
            provenanceExpected
                .GetProperty(
                    "sourceSha256ByCorpus")
                .EnumerateObject()
                .ToDictionary(
                    property =>
                        property.Name,
                    property =>
                        NormalizeSha256(
                            property.Name,
                            property.Value.GetString()),
                    StringComparer.Ordinal);

        return new GroundTruthExpectations(
            baselineObserved,
            new NativeExpectation(
                habermasPages,
                RequiredString(
                    nativeExpected,
                    "habermasStatus"),
                RequiredString(
                    nativeExpected,
                    "habermasRoute"),
                deDecretisRange.FirstPage,
                deDecretisRange.LastPage,
                RequiredInt(
                    nativeExpected,
                    "deDecretisCount"),
                RequiredString(
                    nativeExpected,
                    "deDecretisStatus"),
                RequiredString(
                    nativeExpected,
                    "deDecretisRoute"),
                RequiredInt(
                    nativeExpected,
                    "deDecretisWords"),
                RequiredInt(
                    nativeExpected,
                    "deDecretisBlocks")),
            new ProvenanceExpectation(
                RequiredInt(
                    provenanceExpected,
                    "fixtureCount"),
                RequiredInt(
                    provenanceExpected,
                    "ehrmanCount"),
                RequiredInt(
                    provenanceExpected,
                    "habermasCount"),
                RequiredInt(
                    provenanceExpected,
                    "deDecretisCount"),
                RequiredInt(
                    provenanceExpected,
                    "standaloneFixturePhysicalPage"),
                sourceSha256ByCorpus));
    }

    private static JsonElement FindControl(
        JsonElement controls,
        string id)
    {
        foreach (var control in
                 controls.EnumerateArray())
        {
            if (string.Equals(
                    RequiredString(
                        control,
                        "id"),
                    id,
                    StringComparison.Ordinal))
            {
                return control;
            }
        }

        throw new InvalidDataException(
            $"Semantic ground truth contains no control '{id}'.");
    }

    private static (int FirstPage, int LastPage) ParsePageRange(
        string value)
    {
        var parts =
            value.Split(
                '-',
                2,
                StringSplitOptions.TrimEntries);

        if (parts.Length !=
                2 ||
            !int.TryParse(
                parts[0],
                out var firstPage) ||
            !int.TryParse(
                parts[1],
                out var lastPage) ||
            firstPage <=
                0 ||
            lastPage <
                firstPage)
        {
            throw new InvalidDataException(
                $"Invalid semantic page range '{value}'.");
        }

        return (
            firstPage,
            lastPage
        );
    }

    private static string RequiredString(
        JsonElement element,
        string propertyName)
    {
        var value =
            element
                .GetProperty(
                    propertyName)
                .GetString();

        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new InvalidDataException(
                $"Semantic ground truth property '{propertyName}' is missing or blank.");
        }

        return value;
    }

    private static int RequiredInt(
        JsonElement element,
        string propertyName) =>
        element
            .GetProperty(
                propertyName)
            .GetInt32();

    private static string NormalizeSha256(
        string label,
        string? value)
    {
        var normalized =
            value?
                .Trim()
                .ToLowerInvariant();

        if (normalized is null ||
            normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new InvalidDataException(
                $"Invalid source SHA-256 for '{label}'.");
        }

        return normalized;
    }

    private static async Task<IReadOnlyList<ManifestRow>>
        ReadManifestAsync(
            string path)
    {
        var lines =
            await File.ReadAllLinesAsync(
                Path.GetFullPath(
                    path));

        if (lines.Length ==
            0)
        {
            throw new InvalidDataException(
                "Fixture manifest is empty.");
        }

        var expectedHeader =
            string.Join(
                '\t',
                "fixture",
                "corpus",
                "source_file",
                "source_sha256",
                "source_physical_page",
                "fixture_page",
                "fixture_sha256",
                "bytes");

        if (!string.Equals(
                lines[0],
                expectedHeader,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Fixture manifest header does not match the semantic regression contract.");
        }

        var rows =
            new List<ManifestRow>(
                lines.Length -
                1);

        for (var lineNumber =
                 2;
             lineNumber <=
             lines.Length;
             lineNumber++)
        {
            var line =
                lines[lineNumber -
                      1];

            if (string.IsNullOrWhiteSpace(
                    line))
            {
                throw new InvalidDataException(
                    $"Fixture manifest contains a blank row at line {lineNumber}.");
            }

            var fields =
                line.Split(
                    '\t');

            if (fields.Length !=
                8)
            {
                throw new InvalidDataException(
                    $"Fixture manifest line {lineNumber} has {fields.Length} fields; expected 8.");
            }

            if (!int.TryParse(
                    fields[4],
                    out var sourcePhysicalPage) ||
                sourcePhysicalPage <=
                    0)
            {
                throw new InvalidDataException(
                    $"Fixture manifest line {lineNumber} has invalid source_physical_page.");
            }

            if (!int.TryParse(
                    fields[5],
                    out var fixturePage) ||
                fixturePage <=
                    0)
            {
                throw new InvalidDataException(
                    $"Fixture manifest line {lineNumber} has invalid fixture_page.");
            }

            if (!long.TryParse(
                    fields[7],
                    out var bytes) ||
                bytes <=
                    0)
            {
                throw new InvalidDataException(
                    $"Fixture manifest line {lineNumber} has invalid bytes.");
            }

            rows.Add(
                new ManifestRow(
                    fields[0],
                    fields[1],
                    fields[2],
                    NormalizeSha256(
                        $"manifest source line {lineNumber}",
                        fields[3]),
                    sourcePhysicalPage,
                    fixturePage,
                    NormalizeSha256(
                        $"manifest fixture line {lineNumber}",
                        fields[6]),
                    bytes));
        }

        return rows;
    }

    private static string BuildFixtureFileName(
        string corpus,
        int originalPhysicalPage)
    {
        var prefix =
            corpus switch
            {
                "Ehrman" =>
                    "ehrman",

                "Habermas" =>
                    "habermas",

                "DeDecretis" =>
                    "decretis",

                _ =>
                    throw new InvalidDataException(
                        $"Unsupported fixture corpus '{corpus}'.")
            };

        return $"{prefix}-p{originalPhysicalPage:D4}.pdf";
    }

    private static async Task<DocumentExtractionResult> ExtractAsync(
        string fixturePath)
    {
        await using var source =
            File.OpenRead(
                fixturePath);

        return await new PdfPigDocumentExtractor()
            .ExtractAsync(
                new DocumentSource(
                    source,
                    Path.GetFileName(
                        fixturePath),
                    "application/pdf"),
                DocumentFormatId.Pdf);
    }

    private static async Task<string> ComputeSha256Async(
        string path)
    {
        await using var stream =
            File.OpenRead(
                path);

        var hash =
            await SHA256.HashDataAsync(
                stream);

        return Convert.ToHexString(
                hash)
            .ToLowerInvariant();
    }

    private static async Task WriteJsonAsync(
        string path,
        SemanticNativeRegressionReport report)
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

        var temporaryPath =
            fullPath +
            ".tmp-" +
            Guid.NewGuid().ToString("N");

        try
        {
            await using var stream =
                File.Create(
                    temporaryPath);

            await JsonSerializer.SerializeAsync(
                stream,
                report,
                WriteOptions);

            await stream.FlushAsync();

            File.Move(
                temporaryPath,
                fullPath,
                overwrite:
                    true);
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
        SemanticNativeRegressionReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: SEMANTIC NATIVE REGRESSION EVALUATED");

        Console.WriteLine(
            $"Overall: {(report.Pass ? "PASS" : "FAIL")}");

        Console.WriteLine(
            $"Fixture provenance: " +
            $"{(report.Provenance.Pass ? "PASS" : "FAIL")} " +
            $"({report.Provenance.ObservedFixtureCount}/" +
            $"{report.Provenance.ExpectedFixtureCount})");

        Console.WriteLine(
            $"Habermas native controls: " +
            $"{report.Native.HabermasPages.Count(page => page.Pass)}/" +
            $"{report.Native.HabermasPages.Count}");

        Console.WriteLine(
            $"De Decretis native controls: " +
            $"{report.Native.DeDecretis.Pages.Count(page => page.Pass)}/" +
            $"{report.Native.DeDecretis.Pages.Count}");

        Console.WriteLine(
            $"De Decretis words: " +
            $"{report.Native.DeDecretis.ObservedWords}/" +
            $"{report.Native.DeDecretis.ExpectedWords}");

        Console.WriteLine(
            $"De Decretis blocks: " +
            $"{report.Native.DeDecretis.ObservedBlocks}/" +
            $"{report.Native.DeDecretis.ExpectedBlocks}");

        foreach (var mismatch in
                 report.Provenance.Mismatches)
        {
            Console.WriteLine(
                $"  PROVENANCE FAIL: {mismatch}");
        }

        foreach (var mismatch in
                 report.Native.Mismatches)
        {
            Console.WriteLine(
                $"  NATIVE FAIL: {mismatch}");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private sealed record EvaluationOptions(
        string GroundTruthPath,
        string FixturesDirectory,
        string ManifestPath,
        string ReportPath)
    {
        public static EvaluationOptions Parse(
            string[] args)
        {
            string? groundTruth =
                null;

            string? fixtures =
                null;

            string? manifest =
                null;

            string? report =
                null;

            for (var index =
                     0;
                 index <
                 args.Length;
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

                    case "--fixtures":
                        fixtures =
                            ReadValue(
                                args,
                                ref index,
                                option);
                        break;

                    case "--manifest":
                        manifest =
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
                    fixtures) ||
                string.IsNullOrWhiteSpace(
                    manifest) ||
                string.IsNullOrWhiteSpace(
                    report))
            {
                throw new ArgumentException(
                    "--ground-truth, --fixtures, --manifest and --report are required.");
            }

            var fixturesPath =
                Path.GetFullPath(
                    fixtures);

            if (!Directory.Exists(
                    fixturesPath))
            {
                throw new DirectoryNotFoundException(
                    $"Semantic fixture directory was not found: {fixturesPath}");
            }

            var manifestPath =
                Path.GetFullPath(
                    manifest);

            if (!File.Exists(
                    manifestPath))
            {
                throw new FileNotFoundException(
                    "Semantic fixture manifest was not found.",
                    manifestPath);
            }

            return new EvaluationOptions(
                Path.GetFullPath(
                    groundTruth),
                fixturesPath,
                manifestPath,
                Path.GetFullPath(
                    report));
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            ref int index,
            string option)
        {
            if (index +
                    1 >=
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

    private sealed record GroundTruthExpectations(
        string BaselineObserved,
        NativeExpectation Native,
        ProvenanceExpectation Provenance);

    private sealed record NativeExpectation(
        IReadOnlyList<int> HabermasPages,
        string HabermasStatus,
        string HabermasRoute,
        int DeDecretisFirstPage,
        int DeDecretisLastPage,
        int DeDecretisCount,
        string DeDecretisStatus,
        string DeDecretisRoute,
        int DeDecretisWords,
        int DeDecretisBlocks);

    private sealed record ProvenanceExpectation(
        int FixtureCount,
        int EhrmanCount,
        int HabermasCount,
        int DeDecretisCount,
        int StandaloneFixturePhysicalPage,
        IReadOnlyDictionary<string, string> SourceSha256ByCorpus);

    private sealed record ManifestRow(
        string Fixture,
        string Corpus,
        string SourceFile,
        string SourceSha256,
        int SourcePhysicalPage,
        int FixturePage,
        string FixtureSha256,
        long Bytes);

    private sealed record FixtureObservation(
        string Fixture,
        string Corpus,
        int OriginalPhysicalPage,
        int FixturePhysicalPage,
        string FixtureSha256,
        long FixtureBytes,
        int WordCount,
        int BlockCount,
        string NativeStatus,
        string Route,
        bool ProvenancePass,
        IReadOnlyList<string> ProvenanceMismatches);

    private sealed record EvaluationResult(
        ProvenanceEvaluation Provenance,
        NativeEvaluation Native);

    private sealed record SemanticNativeRegressionReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string BaselineObserved,
        bool Pass,
        ProvenanceEvaluation Provenance,
        NativeEvaluation Native);

    private sealed record ProvenanceEvaluation(
        bool Pass,
        int ExpectedFixtureCount,
        int ObservedFixtureCount,
        int ExpectedEhrmanCount,
        int ObservedEhrmanCount,
        int ExpectedHabermasCount,
        int ObservedHabermasCount,
        int ExpectedDeDecretisCount,
        int ObservedDeDecretisCount,
        IReadOnlyList<string> Mismatches);

    private sealed record NativeEvaluation(
        bool Pass,
        IReadOnlyList<NativePageObservation> HabermasPages,
        DeDecretisNativeSummary DeDecretis,
        IReadOnlyList<string> Mismatches);

    private sealed record NativePageObservation(
        string Fixture,
        int OriginalPhysicalPage,
        int WordCount,
        int BlockCount,
        string NativeStatus,
        string Route,
        bool Pass);

    private sealed record DeDecretisNativeSummary(
        int FirstOriginalPhysicalPage,
        int LastOriginalPhysicalPage,
        int ExpectedPageCount,
        int ObservedPageCount,
        int ExpectedWords,
        int ObservedWords,
        int ExpectedBlocks,
        int ObservedBlocks,
        string ExpectedStatus,
        string ExpectedRoute,
        IReadOnlyList<NativePageObservation> Pages);
}
