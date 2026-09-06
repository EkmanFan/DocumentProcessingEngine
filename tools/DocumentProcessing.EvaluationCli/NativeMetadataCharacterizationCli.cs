using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Epub.Extraction;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.EvaluationCli;

/// <summary>
/// Characterizes native descriptive metadata across a targeted corpus.
/// </summary>
/// <remarks>
/// Evaluation only: nothing is modified and no processing route runs. The
/// command opens each source, acquires native metadata and records what was
/// found verbatim.
///
/// It deliberately reports no semantic verdict. Whether a given
/// <c>/Title</c> is a usable document title is a judgement for review, and
/// encoding that judgement here would turn characterization evidence into an
/// unvalidated classifier.
/// </remarks>
internal static class NativeMetadataCharacterizationCli
{
    #region Variables and Constants

    private const string SchemaVersion =
        "document-native-metadata-characterization-v1";

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

    #region Methods

    public static Task<int> RunAsync(
        string[] args)
    {
        string? corpusRoot = null;
        string? reportPath = null;
        string? jsonPath = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--corpus":
                    corpusRoot =
                        RequireValue(
                            args,
                            ref index);
                    break;
                case "--report":
                    reportPath =
                        RequireValue(
                            args,
                            ref index);
                    break;
                case "--json":
                    jsonPath =
                        RequireValue(
                            args,
                            ref index);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown option '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(
                corpusRoot))
        {
            throw new ArgumentException(
                "The --corpus option is required.");
        }

        var observations =
            new List<SourceObservation>();

        observations.AddRange(
            Observe(
                Path.Combine(
                    corpusRoot,
                    "pdf",
                    "full"),
                "*.pdf",
                ObservePdf));

        observations.AddRange(
            Observe(
                Path.Combine(
                    corpusRoot,
                    "epub"),
                "*.epub",
                ObserveEpub));

        var report =
            new CharacterizationReport(
                SchemaVersion,
                DateTimeOffset.UtcNow,
                observations
                    .OrderBy(
                        observation =>
                            observation.Format,
                        StringComparer.Ordinal)
                    .ThenBy(
                        observation =>
                            observation.FileName,
                        StringComparer.Ordinal)
                    .ToArray());

        if (!string.IsNullOrWhiteSpace(
                jsonPath))
        {
            Write(
                jsonPath,
                JsonSerializer.Serialize(
                    report,
                    JsonOptions));
        }

        var markdown =
            RenderMarkdown(
                report);

        if (!string.IsNullOrWhiteSpace(
                reportPath))
        {
            Write(
                reportPath,
                markdown);
        }
        else
        {
            Console.WriteLine(
                markdown);
        }

        return Task.FromResult(
            0);
    }

    #endregion

    #region Methods Observation

    private static IEnumerable<SourceObservation> Observe(
        string directory,
        string pattern,
        Func<string, SourceObservation> observe)
    {
        if (!Directory.Exists(
                directory))
        {
            yield break;
        }

        foreach (var path in
                 Directory
                     .EnumerateFiles(
                         directory,
                         pattern)
                     .Order(
                         StringComparer.Ordinal))
        {
            yield return observe(
                path);
        }
    }

    private static SourceObservation ObservePdf(
        string path)
    {
        try
        {
            using var document =
                UglyToad.PdfPig.PdfDocument.Open(
                    path);

            return Describe(
                "pdf",
                path,
                PdfNativeMetadataReader.Read(
                    document.Information));
        }
        catch (Exception exception)
        {
            return Failed(
                "pdf",
                path,
                exception);
        }
    }

    private static SourceObservation ObserveEpub(
        string path)
    {
        try
        {
            using var archive =
                System.IO.Compression.ZipFile.OpenRead(
                    path);

            var packageEntry =
                archive.Entries
                    .FirstOrDefault(
                        entry =>
                            entry.FullName.EndsWith(
                                ".opf",
                                StringComparison.OrdinalIgnoreCase));

            if (packageEntry is null)
            {
                return Failed(
                    "epub",
                    path,
                    new InvalidDataException(
                        "No OPF package document was found."));
            }

            using var stream =
                packageEntry.Open();

            return Describe(
                "epub",
                path,
                EpubNativeMetadataReader.Read(
                    XDocument.Load(
                        stream)));
        }
        catch (Exception exception)
        {
            return Failed(
                "epub",
                path,
                exception);
        }
    }

    private static SourceObservation Describe(
        string format,
        string path,
        NativeDocumentMetadata metadata) =>
        new(
            format,
            Path.GetFileName(
                path),
            Acquired:
                true,
            Failure:
                null,
            Describe(
                metadata.Title),
            Describe(
                metadata.Description),
            metadata.Contributors
                .Select(
                    Describe)
                .ToArray()!,
            Describe(
                metadata.Publisher),
            Describe(
                metadata.Language),
            metadata.Dates
                .Select(
                    Describe)
                .ToArray()!,
            metadata.IsEmpty);

    private static SourceObservation Failed(
        string format,
        string path,
        Exception exception) =>
        new(
            format,
            Path.GetFileName(
                path),
            Acquired:
                false,
            exception.GetType()
                .Name,
            null,
            null,
            [],
            null,
            null,
            [],
            IsEmpty:
                true);

    private static ObservedValue? Describe(
        NativeDocumentMetadataValue? value) =>
        value is null
            ? null
            : new ObservedValue(
                value.Value,
                value.SourceHint);

    #endregion

    #region Methods Rendering

    private static string RenderMarkdown(
        CharacterizationReport report)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "# Native document metadata characterization v1");
        builder.AppendLine();
        builder.AppendLine(
            $"- Schema: `{report.SchemaVersion}`");
        builder.AppendLine(
            $"- Generated: {report.GeneratedAtUtc:u}");
        builder.AppendLine(
            $"- Sources: {report.Sources.Count}");
        builder.AppendLine();
        builder.AppendLine(
            "Acquisition evidence only. No semantic verdict is recorded: " +
            "whether a native title is usable is a review decision.");
        builder.AppendLine();

        foreach (var format in
                 report.Sources
                     .Select(
                         source =>
                             source.Format)
                     .Distinct(
                         StringComparer.Ordinal)
                     .Order(
                         StringComparer.Ordinal))
        {
            var sources =
                report.Sources
                    .Where(
                        source =>
                            string.Equals(
                                source.Format,
                                format,
                                StringComparison.Ordinal))
                    .ToArray();

            builder.AppendLine(
                $"## {format.ToUpperInvariant()}");
            builder.AppendLine();
            builder.AppendLine(
                $"- Sources: {sources.Length}");
            builder.AppendLine(
                "- With a native title: " +
                $"{sources.Count(source => source.Title is not null)}");
            builder.AppendLine(
                "- With no metadata at all: " +
                $"{sources.Count(source => source.IsEmpty)}");
            builder.AppendLine();
            builder.AppendLine(
                "| Source | Title | Contributors | Language | Publisher | Dates |");
            builder.AppendLine(
                "|---|---|---|---|---|---|");

            foreach (var source in sources)
            {
                builder.AppendLine(
                    $"| {Escape(source.FileName)} " +
                    $"| {Escape(source.Title?.Value) ?? "—"} " +
                    $"| {Escape(string.Join("; ", source.Contributors.Select(value => value.Value))) ?? "—"} " +
                    $"| {Escape(source.Language?.Value) ?? "—"} " +
                    $"| {Escape(source.Publisher?.Value) ?? "—"} " +
                    $"| {Escape(string.Join("; ", source.Dates.Select(value => value.Value))) ?? "—"} |");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string? Escape(
        string? value) =>
        string.IsNullOrWhiteSpace(
            value)
            ? null
            : value
                .Replace(
                    "|",
                    "\\|",
                    StringComparison.Ordinal)
                .ReplaceLineEndings(
                    " ");

    private static void Write(
        string path,
        string content)
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

        // Repository convention: LF and exactly one trailing newline, so a
        // generated artifact never fails the staged whitespace gate.
        File.WriteAllText(
            path,
            content
                .ReplaceLineEndings(
                    "\n")
                .TrimEnd('\n') +
            "\n");
    }

    private static string RequireValue(
        string[] args,
        ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException(
                $"Option '{args[index]}' requires a value.");
        }

        index++;
        return args[index];
    }

    #endregion

    #region Types

    private sealed record ObservedValue(
        string Value,
        string SourceHint);

    private sealed record SourceObservation(
        string Format,
        string FileName,
        bool Acquired,
        string? Failure,
        ObservedValue? Title,
        ObservedValue? Description,
        IReadOnlyList<ObservedValue> Contributors,
        ObservedValue? Publisher,
        ObservedValue? Language,
        IReadOnlyList<ObservedValue> Dates,
        bool IsEmpty);

    private sealed record CharacterizationReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        IReadOnlyList<SourceObservation> Sources);

    #endregion
}
