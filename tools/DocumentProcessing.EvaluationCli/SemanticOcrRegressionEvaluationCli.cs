using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Raster;
using DocumentProcessing.Engine.Visual;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.EvaluationCli;

internal static class SemanticOcrRegressionEvaluationCli
{
    private const string GroundTruthSchemaVersion =
        "document-processing-semantic-regression-ground-truth-v1";

    private const string ReportSchemaVersion =
        "document-processing-semantic-ocr-regression-v1";

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

        var expectation =
            await ReadExpectationAsync(
                options.GroundTruthPath,
                options.ControlId);

        var extraction =
            await ExtractAsync(
                options.FixturePath);

        if (extraction.Pages.Count !=
            1)
        {
            throw new InvalidDataException(
                $"Semantic OCR control '{expectation.Id}' requires a one-page fixture.");
        }

        var page =
            extraction.Pages[0];

        if (page.PhysicalPageNumber !=
            1)
        {
            throw new InvalidDataException(
                $"Semantic OCR control '{expectation.Id}' requires fixture PhysicalPageNumber=1.");
        }

        var decision =
            DocumentPageProcessingPlanner
                .CreateDefault()
                .Plan(
                    extraction)
                .Single();

        using var layoutHttpClient =
            new HttpClient
            {
                Timeout =
                    Timeout.InfiniteTimeSpan
            };

        IPageLayoutAnalyzer liveLayout =
            new PpStructureV3PageLayoutAnalyzer(
                new PpStructureV3ServingClient(
                    layoutHttpClient,
                    options.LayoutEndpoint,
                    requestTimeout:
                        TimeSpan.FromMinutes(
                            3)));

        var transitioningLayout =
            new TransitioningLayoutAnalyzer(
                liveLayout,
                options.LayoutCompleteMarker,
                options.OcrReadyMarker,
                TimeSpan.FromMinutes(
                    30));

        using var ocrHttpClient =
            new HttpClient
            {
                Timeout =
                    Timeout.InfiniteTimeSpan
            };

        IRegionTextRecognizer liveRecognizer =
            new PaddleOcrRegionTextRecognizer(
                new PaddleOcrServingClient(
                    ocrHttpClient,
                    options.OcrEndpoint,
                    options.OcrProfileId,
                    requestTimeout:
                        TimeSpan.FromMinutes(
                            3)));

        var countingRecognizer =
            new CountingRecognizer(
                liveRecognizer);

        var visualStore =
            new VisualDestinationStore();

        var visualPreserver =
            new VisualAssetPreserver();

        await using var source =
            File.OpenRead(
                options.FixturePath);

        await using var rasterSession =
            await new PdftoppmDocumentRasterizer(
                    dpi:
                        300)
                .OpenAsync(
                    new DocumentSource(
                        source,
                        Path.GetFileName(
                            options.FixturePath),
                        "application/pdf"),
                    DocumentFormatId.Pdf);

        var fixtureSha256 =
            await ComputeSha256Async(
                options.FixturePath);

        HybridDocumentPage hybridPage;

        if (decision.Assessment.NativeTextStatus ==
            DocumentProcessing.Core.Reconciliation.NativeTextStatus.Missing)
        {
            hybridPage =
                await new MissingNativeHybridPageExecutor(
                        transitioningLayout,
                        countingRecognizer,
                        visualPreserver)
                    .ExecuteAsync(
                        page,
                        decision,
                        rasterSession,
                        fixtureSha256,
                        visualStore.OpenAsync);
        }
        else
        {
            hybridPage =
                await new NativePresentHybridPageExecutor(
                        transitioningLayout,
                        countingRecognizer,
                        visualPreserver)
                    .ExecuteAsync(
                        page,
                        decision,
                        rasterSession,
                        fixtureSha256,
                        visualStore.OpenAsync);
        }

        var layout =
            transitioningLayout.LastResult ??
            throw new InvalidDataException(
                "Live PP-StructureV3 produced no captured layout result.");

        var observation =
            EvaluateResult(
                expectation,
                page,
                decision,
                layout,
                hybridPage,
                countingRecognizer,
                visualStore,
                fixtureSha256,
                options.OcrProfileId);

        await WriteJsonAsync(
            options.ReportPath,
            observation);

        WriteSummary(
            observation,
            options.ReportPath);

        return observation.Pass
            ? 0
            : 1;
    }

    private static SemanticOcrControlReport EvaluateResult(
        ControlExpectation expectation,
        DocumentExtractionPage page,
        PageProcessingDecision decision,
        LayoutAnalysisResult layout,
        HybridDocumentPage hybridPage,
        CountingRecognizer countingRecognizer,
        VisualDestinationStore visualStore,
        string fixtureSha256,
        string ocrProfileId)
    {
        var mismatches =
            new List<string>();

        if (!string.IsNullOrWhiteSpace(
                expectation.ExpectedNativeStatus) &&
            !string.Equals(
                decision.Assessment.NativeTextStatus.ToString(),
                expectation.ExpectedNativeStatus,
                StringComparison.Ordinal))
        {
            mismatches.Add(
                $"Native status expected {expectation.ExpectedNativeStatus}, " +
                $"observed {decision.Assessment.NativeTextStatus}.");
        }

        if (!string.Equals(
                decision.Plan.Route.ToString(),
                expectation.ExpectedRoute,
                StringComparison.Ordinal))
        {
            mismatches.Add(
                $"Route expected {expectation.ExpectedRoute}, " +
                $"observed {decision.Plan.Route}.");
        }

        var figureOcrCount =
            countingRecognizer.Calls.Count(
                call =>
                    call.Kind ==
                    LayoutObservationKind.Figure);

        if (figureOcrCount !=
            expectation.ExpectedFigureOcrCount)
        {
            mismatches.Add(
                $"Figure OCR expected {expectation.ExpectedFigureOcrCount}, " +
                $"observed {figureOcrCount}.");
        }

        PreservedVisualReport? preservedVisual =
            null;

        ReadingOrderReport? readingOrder =
            null;

        ReconciliationReport? reconciliation =
            null;

        if (expectation.PreservedVisual is not null)
        {
            var visualElements =
                hybridPage.Elements
                    .Where(
                        element =>
                            element.Kind ==
                            HybridDocumentElementKind.Visual)
                    .ToArray();

            if (visualElements.Length !=
                1)
            {
                mismatches.Add(
                    $"Expected exactly one preserved visual, observed {visualElements.Length}.");
            }
            else
            {
                var visual =
                    visualElements[0];

                var preserved =
                    visual.PreservedVisual;

                if (preserved is null ||
                    visual.LayoutObservation is null)
                {
                    mismatches.Add(
                        "Preserved visual element lacks custody/layout provenance.");
                }
                else
                {
                    var bytes =
                        visualStore.GetBytes(
                            visual.LayoutObservation.PhysicalPageNumber,
                            visual.LayoutObservation.ObservationSequence);

                    var destinationSha256 =
                        Convert.ToHexString(
                                SHA256.HashData(
                                    bytes))
                            .ToLowerInvariant();

                    preservedVisual =
                        new PreservedVisualReport(
                            visual.LayoutObservation.ObservationSequence,
                            preserved.Crop.Width,
                            preserved.Crop.Height,
                            bytes.LongLength,
                            destinationSha256,
                            preserved.ContentLength,
                            preserved.ContentSha256);

                    if (preserved.Crop.Width !=
                        expectation.PreservedVisual.Width)
                    {
                        mismatches.Add(
                            $"Preserved visual width expected " +
                            $"{expectation.PreservedVisual.Width}, observed " +
                            $"{preserved.Crop.Width}.");
                    }

                    if (preserved.Crop.Height !=
                        expectation.PreservedVisual.Height)
                    {
                        mismatches.Add(
                            $"Preserved visual height expected " +
                            $"{expectation.PreservedVisual.Height}, observed " +
                            $"{preserved.Crop.Height}.");
                    }

                    if (bytes.LongLength !=
                        expectation.PreservedVisual.Bytes)
                    {
                        mismatches.Add(
                            $"Preserved visual bytes expected " +
                            $"{expectation.PreservedVisual.Bytes}, observed " +
                            $"{bytes.LongLength}.");
                    }

                    if (!string.Equals(
                            destinationSha256,
                            expectation.PreservedVisual.Sha256,
                            StringComparison.Ordinal))
                    {
                        mismatches.Add(
                            $"Preserved visual SHA expected " +
                            $"{expectation.PreservedVisual.Sha256}, observed " +
                            $"{destinationSha256}.");
                    }

                    if (preserved.ContentLength !=
                            bytes.LongLength ||
                        !string.Equals(
                            preserved.ContentSha256,
                            destinationSha256,
                            StringComparison.Ordinal))
                    {
                        mismatches.Add(
                            "Preserved visual provenance does not match destination bytes.");
                    }
                }
            }
        }

        if (expectation.ReadingOrderSentinels.Count >
            0)
        {
            readingOrder =
                EvaluateReadingOrder(
                    expectation.ReadingOrderSentinels,
                    hybridPage.Elements);

            if (!readingOrder.Pass)
            {
                mismatches.Add(
                    readingOrder.Failure ??
                    "Reading-order sentinel evaluation failed.");
            }
        }

        if (expectation.TargetSequence is not null)
        {
            var target =
                hybridPage.Elements
                    .SingleOrDefault(
                        element =>
                            element.LayoutObservation
                                ?.ObservationSequence ==
                            expectation.TargetSequence.Value);

            if (target is null)
            {
                mismatches.Add(
                    $"Expected reconciliation target seq=" +
                    $"{expectation.TargetSequence.Value} was not found.");
            }
            else
            {
                var result =
                    target.Reconciliation;

                if (result is null)
                {
                    mismatches.Add(
                        $"Target seq={expectation.TargetSequence.Value} " +
                        "has no reconciliation result.");
                }
                else
                {
                    var nativeBlockSourceSequence =
                        target.NativeBlock
                            ?.SourceSequence;

                    reconciliation =
                        new ReconciliationReport(
                            expectation.TargetSequence.Value,
                            target.Kind.ToString(),
                            result.Decision.ToString(),
                            target.TextOrigin.ToString(),
                            target.IsResolved,
                            result.HasDivergence,
                            nativeBlockSourceSequence);

                    CompareExpected(
                        "reconciliation decision",
                        expectation.ExpectedReconciliationDecision,
                        result.Decision.ToString(),
                        mismatches);

                    CompareExpected(
                        "selected origin",
                        expectation.ExpectedSelectedOrigin,
                        target.TextOrigin.ToString(),
                        mismatches);

                    if (expectation.ExpectedResolved is not null &&
                        target.IsResolved !=
                        expectation.ExpectedResolved.Value)
                    {
                        mismatches.Add(
                            $"Resolved expected " +
                            $"{expectation.ExpectedResolved.Value}, observed " +
                            $"{target.IsResolved}.");
                    }

                    if (expectation.ExpectedDivergence is not null &&
                        result.HasDivergence !=
                        expectation.ExpectedDivergence.Value)
                    {
                        mismatches.Add(
                            $"Divergence expected " +
                            $"{expectation.ExpectedDivergence.Value}, observed " +
                            $"{result.HasDivergence}.");
                    }

                    if (expectation.ExpectedNativeBlockSourceSequence is not null &&
                        nativeBlockSourceSequence !=
                        expectation.ExpectedNativeBlockSourceSequence)
                    {
                        mismatches.Add(
                            $"Native block source sequence expected " +
                            $"{expectation.ExpectedNativeBlockSourceSequence}, observed " +
                            $"{nativeBlockSourceSequence?.ToString() ?? "null"}.");
                    }
                }
            }
        }

        var figureSequences =
            layout.Observations
                .Where(
                    observation =>
                        observation.Kind ==
                        LayoutObservationKind.Figure)
                .Select(
                    observation =>
                        observation.ObservationSequence)
                .ToArray();

        return new SemanticOcrControlReport(
            ReportSchemaVersion,
            DateTimeOffset.UtcNow,
            expectation.Id,
            expectation.OriginalPhysicalPage,
            page.PhysicalPageNumber,
            fixtureSha256,
            ocrProfileId,
            page.WordCount,
            page.Blocks.Count,
            decision.Assessment.NativeTextStatus.ToString(),
            decision.Plan.Route.ToString(),
            layout.Observations.Count,
            figureSequences,
            countingRecognizer.Calls.Count,
            figureOcrCount,
            countingRecognizer.Calls,
            preservedVisual,
            readingOrder,
            reconciliation,
            mismatches.Count ==
                0,
            mismatches);
    }

    private static ReadingOrderReport EvaluateReadingOrder(
        IReadOnlyList<string> sentinels,
        IReadOnlyList<HybridDocumentElement> elements)
    {
        if (sentinels.Count !=
            2)
        {
            return new ReadingOrderReport(
                false,
                sentinels,
                [],
                "Current semantic OCR regression supports exactly two ordered sentinels.");
        }

        var matches =
            new List<ReadingOrderSentinelMatch>(
                sentinels.Count);

        foreach (var sentinel in
                 sentinels)
        {
            var normalizedSentinel =
                NormalizeComparableText(
                    sentinel);

            var match =
                elements
                    .Where(
                        element =>
                            element.Text is not null)
                    .Select(
                        element =>
                            new
                            {
                                Element =
                                    element,
                                Text =
                                    NormalizeComparableText(
                                        element.Text!)
                            })
                    .FirstOrDefault(
                        candidate =>
                            candidate.Text.Contains(
                                normalizedSentinel,
                                StringComparison.Ordinal));

            if (match is null)
            {
                return new ReadingOrderReport(
                    false,
                    sentinels,
                    matches,
                    $"Reading-order sentinel '{sentinel}' was not found in authoritative text.");
            }

            matches.Add(
                new ReadingOrderSentinelMatch(
                    sentinel,
                    match.Element.ReadingOrder,
                    match.Element.LayoutObservation
                        ?.ObservationSequence));
        }

        if (matches[0].ReadingOrder >=
            matches[1].ReadingOrder)
        {
            return new ReadingOrderReport(
                false,
                sentinels,
                matches,
                $"Reading order expected '{sentinels[0]}' before " +
                $"'{sentinels[1]}', observed orders " +
                $"{matches[0].ReadingOrder} and {matches[1].ReadingOrder}.");
        }

        return new ReadingOrderReport(
            true,
            sentinels,
            matches,
            null);
    }

    private static string NormalizeComparableText(
        string value) =>
        Regex.Replace(
                value
                    .ToLowerInvariant(),
                @"\s+",
                " ")
            .Trim();

    private static void CompareExpected(
        string label,
        string? expected,
        string observed,
        ICollection<string> mismatches)
    {
        if (expected is null)
        {
            return;
        }

        if (!string.Equals(
                expected,
                observed,
                StringComparison.Ordinal))
        {
            mismatches.Add(
                $"{label} expected {expected}, observed {observed}.");
        }
    }

    private static async Task<ControlExpectation>
        ReadExpectationAsync(
            string path,
            string controlId)
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

        JsonElement? selected =
            null;

        foreach (var control in
                 root.GetProperty(
                         "controls")
                     .EnumerateArray())
        {
            if (string.Equals(
                    control.GetProperty(
                            "id")
                        .GetString(),
                    controlId,
                    StringComparison.Ordinal))
            {
                selected =
                    control;
                break;
            }
        }

        if (selected is null)
        {
            throw new InvalidDataException(
                $"Semantic ground truth contains no control '{controlId}'.");
        }

        var controlElement =
            selected.Value;

        if (controlElement.GetProperty(
                "baselineClassification")
            .GetString() !=
            "PASS")
        {
            throw new InvalidDataException(
                $"Semantic OCR control '{controlId}' must be a known green Phase 15.1 control.");
        }

        if (controlId is not
            "ehrman-p233" and not
            "ehrman-p380" and not
            "ehrman-p405")
        {
            throw new InvalidDataException(
                $"Semantic OCR evaluator does not support control '{controlId}'.");
        }

        var expected =
            controlElement.GetProperty(
                "expected");

        var originalPhysicalPage =
            controlElement.GetProperty(
                    "originalPhysicalPage")
                .GetInt32();

        var expectedRoute =
            RequiredString(
                expected,
                "route");

        string? expectedNativeStatus =
            TryString(
                expected,
                "nativeStatus");

        var expectedFigureOcrCount =
            TryInt(
                expected,
                "figureOcrCount") ??
            0;

        ExactVisualExpectation? preservedVisual =
            null;

        if (expected.TryGetProperty(
                "preservedVisual",
                out var preservedElement))
        {
            preservedVisual =
                new ExactVisualExpectation(
                    preservedElement.GetProperty(
                            "width")
                        .GetInt32(),
                    preservedElement.GetProperty(
                            "height")
                        .GetInt32(),
                    preservedElement.GetProperty(
                            "bytes")
                        .GetInt64(),
                    NormalizeSha256(
                        controlId,
                        RequiredString(
                            preservedElement,
                            "sha256")));
        }

        var readingOrderSentinels =
            expected.TryGetProperty(
                    "readingOrderSentinels",
                    out var sentinelsElement)
                ? sentinelsElement
                    .EnumerateArray()
                    .Select(
                        item =>
                            item.GetString() ??
                            string.Empty)
                    .Where(
                        value =>
                            !string.IsNullOrWhiteSpace(
                                value))
                    .ToArray()
                : [];

        return new ControlExpectation(
            controlId,
            originalPhysicalPage,
            expectedNativeStatus,
            expectedRoute,
            expectedFigureOcrCount,
            preservedVisual,
            readingOrderSentinels,
            TryInt(
                expected,
                "targetSequence"),
            TryString(
                expected,
                "reconciliationDecision"),
            TryString(
                expected,
                "selectedOrigin"),
            TryBool(
                expected,
                "resolved"),
            TryBool(
                expected,
                "divergence"),
            TryInt(
                expected,
                "nativeBlockSourceSequence"));
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

    private static string? TryString(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(
                propertyName,
                out var property)
            ? property.GetString()
            : null;

    private static int? TryInt(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(
                propertyName,
                out var property)
            ? property.GetInt32()
            : null;

    private static bool? TryBool(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(
                propertyName,
                out var property)
            ? property.GetBoolean()
            : null;

    private static string NormalizeSha256(
        string controlId,
        string value)
    {
        var normalized =
            value
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
        SemanticOcrControlReport report)
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
        SemanticOcrControlReport report,
        string reportPath)
    {
        Console.WriteLine(
            $"RESULT: SEMANTIC OCR REGRESSION " +
            $"{(report.Pass ? "PASS" : "FAIL")}");

        Console.WriteLine(
            $"Control: {report.ControlId}");

        Console.WriteLine(
            $"Native: {report.NativeWordCount} words / " +
            $"{report.NativeTextStatus}");

        Console.WriteLine(
            $"Route: {report.Route}");

        Console.WriteLine(
            $"OCR calls: {report.OcrCallCount}; " +
            $"Figure OCR: {report.FigureOcrCount}");

        if (report.PreservedVisual is not null)
        {
            Console.WriteLine(
                $"Preserved visual: " +
                $"{report.PreservedVisual.Width}x" +
                $"{report.PreservedVisual.Height}; " +
                $"{report.PreservedVisual.Bytes} bytes; " +
                $"{report.PreservedVisual.Sha256}");
        }

        if (report.ReadingOrder is not null)
        {
            Console.WriteLine(
                $"Reading order: " +
                $"{(report.ReadingOrder.Pass ? "PASS" : "FAIL")}");
        }

        if (report.Reconciliation is not null)
        {
            Console.WriteLine(
                $"Reconciliation: " +
                $"{report.Reconciliation.Decision} / " +
                $"{report.Reconciliation.SelectedOrigin}; " +
                $"resolved={report.Reconciliation.Resolved}; " +
                $"divergence={report.Reconciliation.Divergence}; " +
                $"nativeBlock=" +
                $"{report.Reconciliation.NativeBlockSourceSequence?.ToString() ?? "null"}");
        }

        foreach (var mismatch in
                 report.Mismatches)
        {
            Console.WriteLine(
                $"  FAIL: {mismatch}");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private sealed class TransitioningLayoutAnalyzer
        : IPageLayoutAnalyzer
    {
        private readonly IPageLayoutAnalyzer _inner;
        private readonly string _layoutCompleteMarker;
        private readonly string _ocrReadyMarker;
        private readonly TimeSpan _timeout;

        public TransitioningLayoutAnalyzer(
            IPageLayoutAnalyzer inner,
            string layoutCompleteMarker,
            string ocrReadyMarker,
            TimeSpan timeout)
        {
            _inner =
                inner;

            _layoutCompleteMarker =
                layoutCompleteMarker;

            _ocrReadyMarker =
                ocrReadyMarker;

            _timeout =
                timeout;
        }

        public LayoutAnalysisResult? LastResult { get; private set; }

        public async ValueTask<LayoutAnalysisResult> AnalyzeAsync(
            Stream rasterPage,
            int physicalPageNumber,
            int pixelWidth,
            int pixelHeight,
            CancellationToken cancellationToken = default)
        {
            var result =
                await _inner
                    .AnalyzeAsync(
                        rasterPage,
                        physicalPageNumber,
                        pixelWidth,
                        pixelHeight,
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            LastResult =
                result;

            await File.WriteAllTextAsync(
                    _layoutCompleteMarker,
                    "layout-complete\n",
                    cancellationToken)
                .ConfigureAwait(
                    false);

            var deadline =
                DateTime.UtcNow +
                _timeout;

            while (!File.Exists(
                       _ocrReadyMarker))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                if (DateTime.UtcNow >
                    deadline)
                {
                    throw new TimeoutException(
                        "Timed out waiting for external PP-StructureV3 -> PaddleOCR handoff.");
                }

                await Task.Delay(
                        TimeSpan.FromMilliseconds(
                            200),
                        cancellationToken)
                    .ConfigureAwait(
                        false);
            }

            return result;
        }
    }

    private sealed class CountingRecognizer
        : IRegionTextRecognizer
    {
        private readonly IRegionTextRecognizer _inner;

        public CountingRecognizer(
            IRegionTextRecognizer inner)
        {
            _inner =
                inner;
        }

        public List<OcrCallReport> Calls { get; } =
            [];

        public async ValueTask<OcrRegionResult> RecognizeAsync(
            Stream rasterRegion,
            LayoutObservation sourceLayoutObservation,
            PixelRectangle crop,
            int pagePixelWidth,
            int pagePixelHeight,
            CancellationToken cancellationToken = default)
        {
            if (sourceLayoutObservation.Kind ==
                LayoutObservationKind.Figure)
            {
                throw new InvalidDataException(
                    "Critical safety failure: Figure entered OCR.");
            }

            var result =
                await _inner
                    .RecognizeAsync(
                        rasterRegion,
                        sourceLayoutObservation,
                        crop,
                        pagePixelWidth,
                        pagePixelHeight,
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            Calls.Add(
                new OcrCallReport(
                    sourceLayoutObservation.ObservationSequence,
                    sourceLayoutObservation.Kind));

            return result;
        }
    }

    private sealed class VisualDestinationStore
    {
        private readonly Dictionary<(int Page, int Sequence), MemoryStream>
            _streams =
                [];

        public ValueTask<Stream> OpenAsync(
            LayoutObservation observation,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var key =
                (
                    Page:
                        observation.PhysicalPageNumber,
                    Sequence:
                        observation.ObservationSequence
                );

            if (_streams.ContainsKey(
                    key))
            {
                throw new InvalidOperationException(
                    $"Duplicate visual destination for p{key.Page}/seq{key.Sequence}.");
            }

            var stream =
                new MemoryStream();

            _streams.Add(
                key,
                stream);

            return ValueTask.FromResult<Stream>(
                stream);
        }

        public byte[] GetBytes(
            int page,
            int sequence)
        {
            var key =
                (
                    Page:
                        page,
                    Sequence:
                        sequence
                );

            if (!_streams.TryGetValue(
                    key,
                    out var stream))
            {
                throw new InvalidDataException(
                    $"No visual destination exists for p{page}/seq{sequence}.");
            }

            return stream.ToArray();
        }
    }

    private sealed record EvaluationOptions(
        string ControlId,
        string GroundTruthPath,
        string FixturePath,
        Uri LayoutEndpoint,
        Uri OcrEndpoint,
        string OcrProfileId,
        string LayoutCompleteMarker,
        string OcrReadyMarker,
        string ReportPath)
    {
        public static EvaluationOptions Parse(
            string[] args)
        {
            var values =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

            for (var index =
                     0;
                 index <
                 args.Length;
                 index++)
            {
                var option =
                    args[index];

                if (!option.StartsWith(
                        "--",
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Unexpected positional argument '{option}'.");
                }

                if (index +
                        1 >=
                    args.Length)
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

                if (!values.TryAdd(
                        option,
                        value))
                {
                    throw new ArgumentException(
                        $"Duplicate option '{option}'.");
                }
            }

            var controlId =
                Required(
                    values,
                    "--control");

            var groundTruth =
                Path.GetFullPath(
                    Required(
                        values,
                        "--ground-truth"));

            var fixture =
                Path.GetFullPath(
                    Required(
                        values,
                        "--fixture"));

            var layoutEndpoint =
                ParseHttpUri(
                    Required(
                        values,
                        "--layout-endpoint"),
                    "--layout-endpoint");

            var ocrEndpoint =
                ParseHttpUri(
                    Required(
                        values,
                        "--ocr-endpoint"),
                    "--ocr-endpoint");

            var ocrProfile =
                Required(
                    values,
                    "--ocr-profile");

            var layoutComplete =
                Path.GetFullPath(
                    Required(
                        values,
                        "--layout-complete-marker"));

            var ocrReady =
                Path.GetFullPath(
                    Required(
                        values,
                        "--ocr-ready-marker"));

            var report =
                Path.GetFullPath(
                    Required(
                        values,
                        "--report"));

            if (!File.Exists(
                    groundTruth))
            {
                throw new FileNotFoundException(
                    "Semantic ground truth was not found.",
                    groundTruth);
            }

            if (!File.Exists(
                    fixture))
            {
                throw new FileNotFoundException(
                    "Semantic OCR fixture was not found.",
                    fixture);
            }

            EnsureParentDirectory(
                layoutComplete);

            EnsureParentDirectory(
                ocrReady);

            EnsureParentDirectory(
                report);

            DeleteIfExists(
                layoutComplete);

            DeleteIfExists(
                ocrReady);

            return new EvaluationOptions(
                controlId,
                groundTruth,
                fixture,
                layoutEndpoint,
                ocrEndpoint,
                ocrProfile,
                layoutComplete,
                ocrReady,
                report);
        }

        private static string Required(
            IReadOnlyDictionary<string, string> values,
            string option) =>
            values.TryGetValue(
                option,
                out var value)
                ? value
                : throw new ArgumentException(
                    $"Required option is missing: {option}.");

        private static Uri ParseHttpUri(
            string value,
            string option)
        {
            if (!Uri.TryCreate(
                    value,
                    UriKind.Absolute,
                    out var uri) ||
                (uri.Scheme !=
                     Uri.UriSchemeHttp &&
                 uri.Scheme !=
                     Uri.UriSchemeHttps))
            {
                throw new ArgumentException(
                    $"{option} must be an absolute HTTP or HTTPS URI.");
            }

            return uri;
        }

        private static void EnsureParentDirectory(
            string path)
        {
            var directory =
                Path.GetDirectoryName(
                    path);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }
        }

        private static void DeleteIfExists(
            string path)
        {
            if (File.Exists(
                    path))
            {
                File.Delete(
                    path);
            }
        }
    }

    private sealed record ControlExpectation(
        string Id,
        int OriginalPhysicalPage,
        string? ExpectedNativeStatus,
        string ExpectedRoute,
        int ExpectedFigureOcrCount,
        ExactVisualExpectation? PreservedVisual,
        IReadOnlyList<string> ReadingOrderSentinels,
        int? TargetSequence,
        string? ExpectedReconciliationDecision,
        string? ExpectedSelectedOrigin,
        bool? ExpectedResolved,
        bool? ExpectedDivergence,
        int? ExpectedNativeBlockSourceSequence);

    private sealed record ExactVisualExpectation(
        int Width,
        int Height,
        long Bytes,
        string Sha256);

    private sealed record SemanticOcrControlReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        string ControlId,
        int OriginalPhysicalPage,
        int FixturePhysicalPage,
        string FixtureSha256,
        string OcrProfileId,
        int NativeWordCount,
        int NativeBlockCount,
        string NativeTextStatus,
        string Route,
        int LayoutObservationCount,
        IReadOnlyList<int> FigureSequences,
        int OcrCallCount,
        int FigureOcrCount,
        IReadOnlyList<OcrCallReport> OcrCalls,
        PreservedVisualReport? PreservedVisual,
        ReadingOrderReport? ReadingOrder,
        ReconciliationReport? Reconciliation,
        bool Pass,
        IReadOnlyList<string> Mismatches);

    private sealed record OcrCallReport(
        int Sequence,
        LayoutObservationKind Kind);

    private sealed record PreservedVisualReport(
        int ObservationSequence,
        int Width,
        int Height,
        long Bytes,
        string Sha256,
        long ProvenanceBytes,
        string ProvenanceSha256);

    private sealed record ReadingOrderReport(
        bool Pass,
        IReadOnlyList<string> Sentinels,
        IReadOnlyList<ReadingOrderSentinelMatch> Matches,
        string? Failure);

    private sealed record ReadingOrderSentinelMatch(
        string Sentinel,
        int ReadingOrder,
        int? ObservationSequence);

    private sealed record ReconciliationReport(
        int TargetSequence,
        string Kind,
        string Decision,
        string SelectedOrigin,
        bool Resolved,
        bool Divergence,
        int? NativeBlockSourceSequence);
}
