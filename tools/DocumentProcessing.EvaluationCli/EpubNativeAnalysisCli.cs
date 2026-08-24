using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Epub;
using DocumentProcessing.Epub.Locations;
using DocumentProcessing.Layout.Adapters.PpStructureV3;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;

namespace DocumentProcessing.EvaluationCli;

internal static class EpubNativeAnalysisCli
{
    #region Variables and Constants

    private const string SchemaVersion =
        "document-processing-native-epub-analysis-v1";

    private const string VisualSchemaVersion =
        "document-processing-epub-visual-analysis-v2";

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,
            WriteIndented =
                true,
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull
        };

    #endregion

    #region Methods Entry

    public static async Task<int> RunAsync(
        string[] args)
    {
        var options =
            Options.Parse(
                args);

        var sourcePath =
            Path.GetFullPath(
                options.SourcePath);

        var sourceFile =
            new FileInfo(
                sourcePath);

        if (!sourceFile.Exists)
        {
            throw new FileNotFoundException(
                "EPUB source was not found.",
                sourcePath);
        }

        await using var sourceStream =
            File.OpenRead(
                sourcePath);

        var visualWrites =
            new List<VisualWrite>();

        using var host =
            new global::DocumentProcessing.DocumentProcessingHost(
                new global::DocumentProcessing.DocumentProcessingHostOptions(
                    "epub-native-evaluation-v1",
                    new PpStructureV3Options(
                        new Uri(
                            "http://127.0.0.1:1/layout-parsing")),
                    new PaddleOcrOptions(
                        new Uri(
                            "http://127.0.0.1:1/ocr"),
                        "unused-epub-native-evaluation"),
                    userVisualAssetWriter:
                        (_, request, _) =>
                        {
                            var destination =
                                new MemoryStream();

                            visualWrites.Add(
                                new VisualWrite(
                                    request,
                                    destination));

                            return ValueTask.FromResult<Stream>(
                                destination);
                        },
                    epub:
                        new EpubDocumentFormatOptions(
                            new EpubCheckOptions(
                                options.EpubCheckDistributionPath,
                                timeout:
                                    TimeSpan.FromMinutes(
                                        2)))));

        var outcome =
            await host.ProcessDocumentAsync(
                new DocumentSource(
                    sourceStream,
                    sourceFile.Name,
                    "application/epub+zip"),
                new DocumentProcessingRequestOptions(
                    qualifyUnresolvedVisuals:
                        options.AnalyzeUnresolvedVisualsWithPaddle));

        if (!outcome.IsSuccess)
        {
            throw new InvalidDataException(
                outcome.ErrorMessage ??
                "EPUB processing failed without a consumer message.");
        }

        var result =
            outcome.Result ??
            throw new InvalidDataException(
                "Successful EPUB processing returned no result.");

        var structure =
            result.SourceStructure as
                EpubDocumentSourceStructure ??
            throw new InvalidDataException(
                "EPUB processing did not retain EPUB source structure.");

        var report =
            CreateReport(
                result,
                structure);

        var reportPath =
            Path.GetFullPath(
                options.ReportPath);

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                reportPath) ??
            Environment.CurrentDirectory);

        await File.WriteAllTextAsync(
            reportPath,
            JsonSerializer.Serialize(
                report,
                JsonOptions) +
            Environment.NewLine);

        if (options.VisualReportPath is not null)
        {
            var visualReportPath =
                Path.GetFullPath(
                    options.VisualReportPath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    visualReportPath) ??
                Environment.CurrentDirectory);

            await File.WriteAllTextAsync(
                visualReportPath,
                JsonSerializer.Serialize(
                    CreateVisualReport(
                        result,
                        visualWrites),
                    JsonOptions) +
                Environment.NewLine);

            Console.WriteLine(
                $"Visual report: {visualReportPath}");
        }

        Console.WriteLine(
            $"EPUB native analysis: {report.Processing.ElementCount} elements, " +
            $"{report.Processing.SegmentCount} segments, " +
            $"{report.Source.SpineItemCount} spine items.");

        Console.WriteLine(
            $"Report: {reportPath}");

        foreach (var visualWrite in
                 visualWrites)
        {
            await visualWrite.Destination.DisposeAsync();
        }

        return 0;
    }

    #endregion

    #region Methods Report

    private static Report CreateReport(
        DocumentProcessingResult result,
        EpubDocumentSourceStructure structure)
    {
        var authoritativeText =
            string.Join(
                "\n\n",
                result.Elements
                    .Where(
                        element =>
                            element.Text is not null)
                    .Select(
                        element =>
                            element.Text));

        return new Report(
            SchemaVersion,
            new SourceReport(
                result.Source.Sha256,
                result.Source.ByteLength,
                structure.PackagePath,
                structure.Title,
                structure.Identifier,
                structure.Language,
                structure.SpineItems.Count,
                structure.SpineItems.Count(
                    item =>
                        item.IsLinear)),
            new ProcessingReport(
                result.Elements.Count(
                    element =>
                        element.Kind !=
                        DocumentElementKind.Visual),
                result.Elements.Count(
                    element =>
                        element.Kind ==
                        DocumentElementKind.Text),
                result.Elements.Count(
                    element =>
                        element.Kind ==
                        DocumentElementKind.Heading),
                result.Elements.Count(
                    element =>
                        element.Kind ==
                        DocumentElementKind.Caption),
                result.StructuralSegments.Count,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    authoritativeText),
                result.Elements
                    .Where(
                        element =>
                            element.Kind !=
                            DocumentElementKind.Visual)
                    .All(
                    element =>
                        element.Location is
                            EpubDocumentSourceLocation),
                result.Elements.Any(
                    element =>
                        element.Location is
                            PagedDocumentSourceLocation),
                result.ProcessingManifest.NativeExtraction.BackendId,
                result.ProcessingManifest.NativeExtraction.ProfileId,
                result.ProcessingManifest.AssemblyProfileId,
                result.ProcessingManifest.NormalizationProfileId,
                result.ProcessingManifest.SegmentationProfileId));
    }

    private static VisualReport CreateVisualReport(
        DocumentProcessingResult result,
        IReadOnlyList<VisualWrite> visualWrites)
    {
        if (visualWrites.Count !=
            result.VisualAssets.Count)
        {
            throw new InvalidDataException(
                "EPUB visual writer calls do not match portable visual assets.");
        }

        var assets =
            visualWrites
                .Zip(
                    result.VisualAssets,
                    (write, asset) =>
                    {
                        var request =
                            write.Request as
                                UserSourceVisualAssetWriteRequest ??
                            throw new InvalidDataException(
                                "EPUB visual processing emitted a non-source visual request.");

                        return new VisualAssetReport(
                            request.VisualId,
                            request.SourceResourceId,
                            request.MediaType,
                            request.IsAuxiliary,
                            request.Qualification.ToString(),
                            asset.ContentLength,
                            asset.ContentSha256,
                            asset.PreservationProfileId,
                            asset.RasterDerivation is not null);
                    })
                .ToArray();

        return new VisualReport(
            VisualSchemaVersion,
            result.Source.Sha256,
            (result.SourceStructure as
                EpubDocumentSourceStructure)?
            .BodyMatterStartSpineIndex,
            assets.Length,
            assets.Count(
                asset =>
                    asset.IsAuxiliary),
            result.Elements.Count(
                element =>
                    element.Kind ==
                    DocumentElementKind.Visual),
            result.VisualAssets.Count,
            result.ProcessingManifest.VisualPreservationProfileIds,
            assets);
    }

    #endregion

    #region Types

    private sealed record Report(
        string SchemaVersion,
        SourceReport Source,
        ProcessingReport Processing);

    private sealed record SourceReport(
        string Sha256,
        long ByteLength,
        string PackagePath,
        string? Title,
        string? Identifier,
        string? Language,
        int SpineItemCount,
        int LinearSpineItemCount);

    private sealed record ProcessingReport(
        int ElementCount,
        int TextElementCount,
        int HeadingElementCount,
        int CaptionElementCount,
        int SegmentCount,
        string AuthoritativeTextSha256,
        bool AllElementLocationsAreEpub,
        bool HasPagedElementLocation,
        string NativeExtractionBackendId,
        string NativeExtractionProfileId,
        string AssemblyProfileId,
        string NormalizationProfileId,
        string SegmentationProfileId);

    private sealed record VisualReport(
        string SchemaVersion,
        string SourceSha256,
        int? BodyMatterStartSpineIndex,
        int SelectedVisualCount,
        int AuxiliaryVisualCount,
        int VisualElementCount,
        int VisualAssetCount,
        IReadOnlyList<string> PreservationProfileIds,
        IReadOnlyList<VisualAssetReport> Assets);

    private sealed record VisualAssetReport(
        string VisualId,
        string SourceResourceId,
        string MediaType,
        bool IsAuxiliary,
        string Qualification,
        long ContentLength,
        string ContentSha256,
        string PreservationProfileId,
        bool HasRasterDerivation);

    private sealed record VisualWrite(
        UserVisualAssetWriteRequest Request,
        MemoryStream Destination);

    private sealed record Options(
        string SourcePath,
        string EpubCheckDistributionPath,
        string ReportPath,
        string? VisualReportPath,
        bool AnalyzeUnresolvedVisualsWithPaddle)
    {
        public static Options Parse(
            IReadOnlyList<string> args)
        {
            string? source =
                null;

            string? epubCheckDistribution =
                null;

            string? report =
                null;

            string? visualReport =
                null;

            var analyzeUnresolvedVisualsWithPaddle =
                false;

            for (var index = 0;
                 index < args.Count;
                 index++)
            {
                switch (args[index])
                {
                    case "--source":
                        source =
                            ReadValue(
                                args,
                                ref index);
                        break;

                    case "--epubcheck-distribution":
                        epubCheckDistribution =
                            ReadValue(
                                args,
                                ref index);
                        break;

                    case "--report":
                        report =
                            ReadValue(
                                args,
                                ref index);
                        break;

                    case "--visual-report":
                        visualReport =
                            ReadValue(
                                args,
                                ref index);
                        break;

                    case "--analyze-unresolved-visuals-with-paddle":
                        analyzeUnresolvedVisualsWithPaddle =
                            true;
                        break;

                    default:
                        throw new ArgumentException(
                            $"Unknown analyze-epub option '{args[index]}'.");
                }
            }

            return new Options(
                Required(
                    source,
                    "--source"),
                Required(
                    epubCheckDistribution,
                    "--epubcheck-distribution"),
                Required(
                    report,
                    "--report"),
                visualReport,
                analyzeUnresolvedVisualsWithPaddle);
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            ref int index)
        {
            index++;

            if (index >=
                args.Count)
            {
                throw new ArgumentException(
                    "Missing option value.");
            }

            return args[index];
        }

        private static string Required(
            string? value,
            string option) =>
            string.IsNullOrWhiteSpace(
                value)
                ? throw new ArgumentException(
                    $"Required option '{option}' was not supplied.")
                : value;
    }

    #endregion
}
