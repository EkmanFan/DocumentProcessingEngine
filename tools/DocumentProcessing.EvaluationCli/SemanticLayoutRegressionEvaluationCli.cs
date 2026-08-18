using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Visual;
using DocumentProcessing.Pdf;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.EvaluationCli;

internal static class SemanticLayoutRegressionEvaluationCli
{
    private const string GroundTruthSchemaVersion =
        "document-processing-semantic-regression-ground-truth-v1";

    private const string ReportSchemaVersion =
        "document-processing-semantic-layout-regression-v1";

    private static readonly JsonSerializerOptions ReadOptions =
        new()
        {
            PropertyNameCaseInsensitive =
                true
        };

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

        var groundTruth =
            await ReadJsonAsync<GroundTruthManifest>(
                options.GroundTruthPath);

        ValidateGroundTruth(
            groundTruth);

        var controls =
            groundTruth.Controls
                .Where(
                    IsLayoutMeaningfulVisualControl)
                .OrderBy(
                    control =>
                        control.Id,
                    StringComparer.Ordinal)
                .ToArray();

        if (controls.Length ==
            0)
        {
            throw new InvalidDataException(
                "Semantic ground truth contains no live layout meaningful-visual controls.");
        }

        using var httpClient =
            new HttpClient
            {
                Timeout =
                    Timeout.InfiniteTimeSpan
            };

        var analyzer =
            new PpStructureV3PageLayoutAnalyzer(
                new PpStructureV3ServingClient(
                    httpClient,
                    options.LayoutEndpoint,
                    requestTimeout:
                        TimeSpan.FromMinutes(
                            3)));

        var observations =
            new List<LayoutSemanticControlObservation>(
                controls.Length);

        foreach (var control in controls)
        {
            observations.Add(
                await EvaluateControlAsync(
                    control,
                    options.FixturesDirectory,
                    analyzer));
        }

        var semanticPassCount =
            observations.Count(
                observation =>
                    observation.SemanticPass);

        var semanticFailCount =
            observations.Count -
            semanticPassCount;

        var baselineMatchCount =
            observations.Count(
                observation =>
                    observation.BaselineMatches);

        var baselineMismatchCount =
            observations.Count -
            baselineMatchCount;

        var report =
            new LayoutSemanticRegressionReport(
                ReportSchemaVersion,
                DateTimeOffset.UtcNow,
                groundTruth.BaselineObserved,
                options.Mode.ToString(),
                observations.Count,
                semanticPassCount,
                semanticFailCount,
                baselineMatchCount,
                baselineMismatchCount,
                observations);

        await WriteJsonAsync(
            options.ReportPath,
            report);

        WriteSummary(
            report,
            options.ReportPath);

        return options.Mode switch
        {
            EvaluationMode.Baseline =>
                baselineMismatchCount ==
                0
                    ? 0
                    : 1,

            EvaluationMode.AllPass =>
                semanticFailCount ==
                0
                    ? 0
                    : 1,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(options.Mode),
                    options.Mode,
                    "Unsupported semantic layout regression mode.")
        };
    }

    private static async Task<LayoutSemanticControlObservation>
        EvaluateControlAsync(
            GroundTruthControl control,
            string fixturesDirectory,
            IPageLayoutAnalyzer analyzer)
    {
        if (control.OriginalPhysicalPage is null ||
            control.OriginalPhysicalPage <=
            0)
        {
            throw new InvalidDataException(
                $"Semantic control '{control.Id}' has no valid originalPhysicalPage.");
        }

        var fixtureFileName =
            BuildFixtureFileName(
                control.Corpus,
                control.OriginalPhysicalPage.Value);

        var fixturePath =
            Path.Combine(
                fixturesDirectory,
                fixtureFileName);

        if (!File.Exists(
                fixturePath))
        {
            throw new FileNotFoundException(
                $"Semantic regression fixture was not found for '{control.Id}'.",
                fixturePath);
        }

        await VerifyStandaloneFixtureAsync(
            control.Id,
            fixturePath);

        var fixtureSha256 =
            await ComputeSha256Async(
                fixturePath);

        await using var source =
            File.OpenRead(
                fixturePath);

        await using var rasterSession =
            await new PdftoppmDocumentRasterizer(
                    dpi:
                        300)
                .OpenAsync(
                    new DocumentSource(
                        source,
                        fixtureFileName,
                        "application/pdf"),
                    DocumentFormatId.Pdf);

        await using var pageBytes =
            new MemoryStream();

        var pageRaster =
            await rasterSession
                .RenderPageAsync(
                    physicalPageNumber:
                        1,
                    destination:
                        pageBytes);

        pageBytes.Position =
            0;

        var layout =
            await analyzer
                .AnalyzeAsync(
                    pageBytes,
                    physicalPageNumber:
                        1,
                    pixelWidth:
                        pageRaster.OutputPixelWidth,
                    pixelHeight:
                        pageRaster.OutputPixelHeight);

        var evidence =
            new DefaultLayoutVisualEvidenceAssessor()
                .Assess(
                    layout)
                .ToArray();

        var preservingCandidates =
            evidence
                .Where(
                    item =>
                        item.Kind is
                            VisualEvidenceKind.CaptionedMeaningfulVisual or
                            VisualEvidenceKind.LargeIndependentVisual)
                .ToArray();

        var expectedExact =
            ResolveExactVisualExpectation(
                control);

        ExactVisualObservation? exactObserved =
            null;

        var preservationAuthorized =
            false;

        if (preservingCandidates.Length ==
            1)
        {
            await using var destination =
                new MemoryStream();

            var preserved =
                await new LayoutVisualRegionPreserver()
                    .PreserveAsync(
                        preservingCandidates[0],
                        rasterSession,
                        pageRaster,
                        fixtureSha256,
                        destination);

            var destinationBytes =
                destination.ToArray();

            var destinationSha256 =
                Convert.ToHexString(
                        SHA256.HashData(
                            destinationBytes))
                    .ToLowerInvariant();

            if (destinationBytes.LongLength !=
                    preserved.ContentLength ||
                !string.Equals(
                    destinationSha256,
                    preserved.ContentSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Semantic control '{control.Id}' visual custody metadata " +
                    "does not match the preserved destination bytes.");
            }

            exactObserved =
                new ExactVisualObservation(
                    preserved.Crop.Width,
                    preserved.Crop.Height,
                    preserved.ContentLength,
                    preserved.ContentSha256);

            preservationAuthorized =
                true;
        }

        bool? exactVisualPass =
            expectedExact is null
                ? null
                : exactObserved is not null &&
                  exactObserved.Width ==
                      expectedExact.Width &&
                  exactObserved.Height ==
                      expectedExact.Height &&
                  exactObserved.Bytes ==
                      expectedExact.Bytes &&
                  string.Equals(
                      exactObserved.Sha256,
                      expectedExact.Sha256,
                      StringComparison.Ordinal);

        var semanticPass =
            preservationAuthorized &&
            preservingCandidates.Length ==
                1 &&
            exactVisualPass is not
                false;

        var expectedBaselinePass =
            control.BaselineClassification switch
            {
                "PASS" =>
                    true,

                "FAIL" =>
                    false,

                _ =>
                    throw new InvalidDataException(
                        $"Layout semantic control '{control.Id}' has unsupported " +
                        $"baselineClassification '{control.BaselineClassification}'.")
            };

        return new LayoutSemanticControlObservation(
            control.Id,
            control.Corpus,
            control.OriginalPhysicalPage.Value,
            fixtureFileName,
            fixtureSha256,
            control.BaselineClassification,
            layout.Observations.Count,
            evidence.Length,
            preservingCandidates.Length,
            evidence
                .Select(
                    item =>
                        new VisualEvidenceObservation(
                            item.Observation.ObservationSequence,
                            item.Kind.ToString()))
                .ToArray(),
            preservationAuthorized,
            expectedExact,
            exactObserved,
            exactVisualPass,
            semanticPass,
            semanticPass ==
                expectedBaselinePass);
    }

    private static bool IsLayoutMeaningfulVisualControl(
        GroundTruthControl control)
    {
        var explicitPreserve =
            string.Equals(
                control.HumanGroundTruth?.RequiredAction,
                "PreserveMeaningfulVisual",
                StringComparison.Ordinal);

        var exactVisual =
            ResolveExactVisualExpectation(
                control) is not null;

        return explicitPreserve ||
               exactVisual;
    }

    private static ExactVisualExpectation?
        ResolveExactVisualExpectation(
            GroundTruthControl control)
    {
        if (control.ExpectedExactVisual is not null)
        {
            return new ExactVisualExpectation(
                control.ExpectedExactVisual.Width,
                control.ExpectedExactVisual.Height,
                control.ExpectedExactVisual.Bytes,
                NormalizeSha256(
                    control.Id,
                    control.ExpectedExactVisual.Sha256));
        }

        if (control.Expected?.PreservedVisual is not null)
        {
            return new ExactVisualExpectation(
                control.Expected.PreservedVisual.Width,
                control.Expected.PreservedVisual.Height,
                control.Expected.PreservedVisual.Bytes,
                NormalizeSha256(
                    control.Id,
                    control.Expected.PreservedVisual.Sha256));
        }

        return null;
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

                _ =>
                    throw new InvalidDataException(
                        $"Corpus '{corpus}' is not supported by the live " +
                        "layout semantic regression evaluator.")
            };

        return $"{prefix}-p{originalPhysicalPage:D4}.pdf";
    }

    private static async Task VerifyStandaloneFixtureAsync(
        string controlId,
        string fixturePath)
    {
        await using var source =
            File.OpenRead(
                fixturePath);

        var extracted =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    new DocumentSource(
                        source,
                        Path.GetFileName(
                            fixturePath),
                        "application/pdf"),
                    DocumentFormatId.Pdf);

        if (extracted.Pages.Count !=
            1 ||
            extracted.Pages[0].PhysicalPageNumber !=
            1)
        {
            throw new InvalidDataException(
                $"Semantic control '{controlId}' requires a standalone " +
                "one-page fixture with PhysicalPageNumber=1.");
        }
    }

    private static void ValidateGroundTruth(
        GroundTruthManifest groundTruth)
    {
        if (!string.Equals(
                groundTruth.SchemaVersion,
                GroundTruthSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported semantic ground-truth schema " +
                $"'{groundTruth.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(
                groundTruth.BaselineObserved))
        {
            throw new InvalidDataException(
                "Semantic ground truth has no baselineObserved value.");
        }

        if (groundTruth.Controls is null ||
            groundTruth.Controls.Count ==
            0)
        {
            throw new InvalidDataException(
                "Semantic ground truth contains no controls.");
        }

        var duplicateIds =
            groundTruth.Controls
                .GroupBy(
                    control =>
                        control.Id,
                    StringComparer.Ordinal)
                .Where(
                    group =>
                        group.Count() >
                        1)
                .Select(
                    group =>
                        group.Key)
                .ToArray();

        if (duplicateIds.Length >
            0)
        {
            throw new InvalidDataException(
                $"Semantic ground truth contains duplicate control IDs: " +
                $"{string.Join(", ", duplicateIds)}.");
        }
    }

    private static string NormalizeSha256(
        string controlId,
        string sha256)
    {
        var normalized =
            sha256
                .Trim()
                .ToLowerInvariant();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new InvalidDataException(
                $"Semantic control '{controlId}' has an invalid SHA-256.");
        }

        return normalized;
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
        LayoutSemanticRegressionReport report)
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
        LayoutSemanticRegressionReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: SEMANTIC LAYOUT REGRESSION EVALUATED");

        Console.WriteLine(
            $"Mode: {report.Mode}");

        Console.WriteLine(
            $"Controls: {report.ControlCount}");

        Console.WriteLine(
            $"Semantic PASS/FAIL: " +
            $"{report.SemanticPassCount}/" +
            $"{report.SemanticFailCount}");

        Console.WriteLine(
            $"Baseline match/mismatch: " +
            $"{report.BaselineMatchCount}/" +
            $"{report.BaselineMismatchCount}");

        foreach (var control in report.Controls)
        {
            Console.WriteLine(
                $"  {control.Id}: " +
                $"semantic={(control.SemanticPass ? "PASS" : "FAIL")} " +
                $"baseline={control.BaselineClassification} " +
                $"baselineMatch={control.BaselineMatches} " +
                $"figures={control.FigureEvidenceCount} " +
                $"preserveCandidates={control.PreservingCandidateCount}");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private sealed record EvaluationOptions(
        string GroundTruthPath,
        string FixturesDirectory,
        Uri LayoutEndpoint,
        string ReportPath,
        EvaluationMode Mode)
    {
        public static EvaluationOptions Parse(
            string[] args)
        {
            string? groundTruth =
                null;

            string? fixtures =
                null;

            string? layoutEndpoint =
                null;

            string? report =
                null;

            string? mode =
                null;

            for (var index = 0;
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

                    case "--layout-endpoint":
                        layoutEndpoint =
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

                    case "--mode":
                        mode =
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
                    layoutEndpoint) ||
                string.IsNullOrWhiteSpace(
                    report) ||
                string.IsNullOrWhiteSpace(
                    mode))
            {
                throw new ArgumentException(
                    "--ground-truth, --fixtures, --layout-endpoint, " +
                    "--report and --mode are required.");
            }

            if (!Uri.TryCreate(
                    layoutEndpoint,
                    UriKind.Absolute,
                    out var endpoint) ||
                (endpoint.Scheme !=
                     Uri.UriSchemeHttp &&
                 endpoint.Scheme !=
                     Uri.UriSchemeHttps))
            {
                throw new ArgumentException(
                    "--layout-endpoint must be an absolute HTTP or HTTPS URI.");
            }

            var parsedMode =
                mode switch
                {
                    "baseline" =>
                        EvaluationMode.Baseline,

                    "all-pass" =>
                        EvaluationMode.AllPass,

                    _ =>
                        throw new ArgumentException(
                            "--mode must be 'baseline' or 'all-pass'.")
                };

            var fixturesPath =
                Path.GetFullPath(
                    fixtures);

            if (!Directory.Exists(
                    fixturesPath))
            {
                throw new DirectoryNotFoundException(
                    $"Semantic fixture directory was not found: {fixturesPath}");
            }

            return new EvaluationOptions(
                Path.GetFullPath(
                    groundTruth),
                fixturesPath,
                endpoint,
                Path.GetFullPath(
                    report),
                parsedMode);
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

    private enum EvaluationMode
    {
        Baseline,
        AllPass
    }

    private sealed record GroundTruthManifest(
        string SchemaVersion,
        string BaselineObserved,
        IReadOnlyList<GroundTruthControl> Controls);

    private sealed record GroundTruthControl(
        string Id,
        string Corpus,
        int? OriginalPhysicalPage,
        HumanGroundTruth? HumanGroundTruth,
        GroundTruthExpected? Expected,
        ExactVisualExpectation? ExpectedExactVisual,
        string BaselineClassification);

    private sealed record HumanGroundTruth(
        string? SemanticVisual,
        string? RequiredAction);

    private sealed record GroundTruthExpected(
        ExactVisualExpectation? PreservedVisual);

    private sealed record ExactVisualExpectation(
        int Width,
        int Height,
        long Bytes,
        string Sha256);

    private sealed record LayoutSemanticRegressionReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string BaselineObserved,
        string Mode,
        int ControlCount,
        int SemanticPassCount,
        int SemanticFailCount,
        int BaselineMatchCount,
        int BaselineMismatchCount,
        IReadOnlyList<LayoutSemanticControlObservation> Controls);

    private sealed record LayoutSemanticControlObservation(
        string Id,
        string Corpus,
        int OriginalPhysicalPage,
        string FixtureFileName,
        string FixtureSha256,
        string BaselineClassification,
        int LayoutObservationCount,
        int FigureEvidenceCount,
        int PreservingCandidateCount,
        IReadOnlyList<VisualEvidenceObservation> VisualEvidence,
        bool PreservationAuthorized,
        ExactVisualExpectation? ExpectedExactVisual,
        ExactVisualObservation? ObservedExactVisual,
        bool? ExactVisualPass,
        bool SemanticPass,
        bool BaselineMatches);

    private sealed record VisualEvidenceObservation(
        int ObservationSequence,
        string Kind);

    private sealed record ExactVisualObservation(
        int Width,
        int Height,
        long Bytes,
        string Sha256);
}
