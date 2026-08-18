using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.DualRun.Transport;

internal sealed class DocumentDualRunWorkerRequestJsonContract
{
    #region Properties

    [JsonPropertyName("schemaVersion"), JsonRequired]
    public string SchemaVersion { get; init; } =
        null!;

    [JsonPropertyName("jobId"), JsonRequired]
    public string JobId { get; init; } =
        null!;

    [JsonPropertyName("executionMode"), JsonRequired]
    public string ExecutionMode { get; init; } =
        null!;

    [JsonPropertyName("engineVersion"), JsonRequired]
    public string EngineVersion { get; init; } =
        null!;

    [JsonPropertyName("source"), JsonRequired]
    public DocumentDualRunWorkerSourceJsonContract Source { get; init; } =
        null!;

    [JsonPropertyName("authoritativePages"), JsonRequired]
    public DocumentDualRunAuthoritativePageBaselineJsonContract[]
        AuthoritativePages { get; init; } =
            [];

    #endregion

    #region Methods Mapping

    public static DocumentDualRunWorkerRequestJsonContract FromModel(
        DocumentDualRunWorkerRequest request) =>
        new()
        {
            SchemaVersion =
                DocumentDualRunTransportSchema.RequestV1,
            JobId =
                request.JobId.ToString(
                    "D"),
            ExecutionMode =
                DocumentDualRunTransportJsonMapping
                    .EnumName(
                        request.ExecutionMode),
            EngineVersion =
                request.EngineVersion,
            Source =
                DocumentDualRunWorkerSourceJsonContract
                    .FromModel(
                        request),
            AuthoritativePages =
                request
                    .AuthoritativePages
                    .Select(
                        DocumentDualRunAuthoritativePageBaselineJsonContract
                            .FromModel)
                    .ToArray()
        };

    public DocumentDualRunWorkerRequest ToModel() =>
        new(
            DocumentDualRunTransportJsonMapping
                .Guid(
                    JobId,
                    "jobId"),
            DocumentDualRunTransportJsonMapping
                .ParseEnum<DocumentDualRunExecutionMode>(
                    ExecutionMode,
                    "executionMode"),
            DocumentDualRunTransportJsonMapping
                .Required(
                    EngineVersion,
                    "engineVersion"),
            DocumentDualRunTransportJsonMapping
                .Required(
                    Source,
                    "source")
                .SourceSnapshotPath,
            DocumentDualRunTransportJsonMapping
                .Required(
                    Source,
                    "source")
                .SourceDocumentSha256,
            DocumentDualRunTransportJsonMapping
                .Required(
                    Source,
                    "source")
                .SourceByteLength,
            new DocumentFormatId(
                DocumentDualRunTransportJsonMapping
                    .Required(
                        Source,
                        "source")
                    .Format),
            DocumentDualRunTransportJsonMapping
                .RequiredArray(
                    AuthoritativePages,
                    "authoritativePages",
                    static page =>
                        page.ToModel()),
            Source.FileName,
            Source.DeclaredMediaType);

    #endregion
}

internal sealed class DocumentDualRunWorkerSourceJsonContract
{
    #region Properties

    [JsonPropertyName("snapshotPath"), JsonRequired]
    public string SourceSnapshotPath { get; init; } =
        null!;

    [JsonPropertyName("sha256"), JsonRequired]
    public string SourceDocumentSha256 { get; init; } =
        null!;

    [JsonPropertyName("byteLength"), JsonRequired]
    public long SourceByteLength { get; init; }

    [JsonPropertyName("format"), JsonRequired]
    public string Format { get; init; } =
        null!;

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    [JsonPropertyName("declaredMediaType")]
    public string? DeclaredMediaType { get; init; }

    #endregion

    #region Methods Mapping

    public static DocumentDualRunWorkerSourceJsonContract FromModel(
        DocumentDualRunWorkerRequest request) =>
        new()
        {
            SourceSnapshotPath =
                request.SourceSnapshotPath,
            SourceDocumentSha256 =
                request.SourceDocumentSha256,
            SourceByteLength =
                request.SourceByteLength,
            Format =
                request.Format.Value,
            FileName =
                request.FileName,
            DeclaredMediaType =
                request.DeclaredMediaType
        };

    #endregion
}

internal sealed class DocumentDualRunAuthoritativePageBaselineJsonContract
{
    #region Properties

    [JsonPropertyName("physicalPageNumber"), JsonRequired]
    public int PhysicalPageNumber { get; init; }

    [JsonPropertyName("nativeTextStatus"), JsonRequired]
    public string NativeTextStatus { get; init; } =
        null!;

    [JsonPropertyName("authoritativeRoute"), JsonRequired]
    public string AuthoritativeRoute { get; init; } =
        null!;

    [JsonPropertyName("selectedTextSequenceSha256"), JsonRequired]
    public string SelectedTextSequenceSha256 { get; init; } =
        null!;

    [JsonPropertyName("textProjectionSha256"), JsonRequired]
    public string TextProjectionSha256 { get; init; } =
        null!;

    [JsonPropertyName("authoritativeTextElementCount"), JsonRequired]
    public int AuthoritativeTextElementCount { get; init; }

    [JsonPropertyName("authoritativeReconciliationEvidenceCount"), JsonRequired]
    public int AuthoritativeReconciliationEvidenceCount { get; init; }

    #endregion

    #region Methods Mapping

    public static DocumentDualRunAuthoritativePageBaselineJsonContract FromModel(
        DocumentDualRunAuthoritativePageBaseline page) =>
        new()
        {
            PhysicalPageNumber =
                page.PhysicalPageNumber,
            NativeTextStatus =
                DocumentDualRunTransportJsonMapping
                    .EnumName(
                        page.NativeTextStatus),
            AuthoritativeRoute =
                DocumentDualRunTransportJsonMapping
                    .EnumName(
                        page.AuthoritativeRoute),
            SelectedTextSequenceSha256 =
                page.SelectedTextSequenceSha256,
            TextProjectionSha256 =
                page.TextProjectionSha256,
            AuthoritativeTextElementCount =
                page.AuthoritativeTextElementCount,
            AuthoritativeReconciliationEvidenceCount =
                page.AuthoritativeReconciliationEvidenceCount
        };

    public DocumentDualRunAuthoritativePageBaseline ToModel() =>
        new(
            PhysicalPageNumber,
            DocumentDualRunTransportJsonMapping
                .ParseEnum<NativeTextStatus>(
                    NativeTextStatus,
                    "authoritativePages[].nativeTextStatus"),
            DocumentDualRunTransportJsonMapping
                .ParseEnum<PageProcessingRoute>(
                    AuthoritativeRoute,
                    "authoritativePages[].authoritativeRoute"),
            DocumentDualRunTransportJsonMapping
                .Required(
                    SelectedTextSequenceSha256,
                    "authoritativePages[].selectedTextSequenceSha256"),
            DocumentDualRunTransportJsonMapping
                .Required(
                    TextProjectionSha256,
                    "authoritativePages[].textProjectionSha256"),
            AuthoritativeTextElementCount,
            AuthoritativeReconciliationEvidenceCount);

    #endregion
}

internal sealed class DocumentDualRunWorkerResultJsonContract
{
    #region Properties

    [JsonPropertyName("schemaVersion"), JsonRequired]
    public string SchemaVersion { get; init; } =
        null!;

    [JsonPropertyName("jobId"), JsonRequired]
    public string JobId { get; init; } =
        null!;

    [JsonPropertyName("executionMode"), JsonRequired]
    public string ExecutionMode { get; init; } =
        null!;

    [JsonPropertyName("workerEngineVersion"), JsonRequired]
    public string WorkerEngineVersion { get; init; } =
        null!;

    [JsonPropertyName("sourceDocumentSha256"), JsonRequired]
    public string SourceDocumentSha256 { get; init; } =
        null!;

    [JsonPropertyName("status"), JsonRequired]
    public string Status { get; init; } =
        null!;

    [JsonPropertyName("pages"), JsonRequired]
    public DocumentDualRunWorkerPageResultJsonContract[] Pages { get; init; } =
        [];

    [JsonPropertyName("failure")]
    public DocumentDualRunWorkerFailureJsonContract? Failure { get; init; }

    #endregion

    #region Methods Mapping

    public static DocumentDualRunWorkerResultJsonContract FromModel(
        DocumentDualRunWorkerResult result) =>
        new()
        {
            SchemaVersion =
                DocumentDualRunTransportSchema.ResultV1,
            JobId =
                result.JobId.ToString(
                    "D"),
            ExecutionMode =
                DocumentDualRunTransportJsonMapping
                    .EnumName(
                        result.ExecutionMode),
            WorkerEngineVersion =
                result.WorkerEngineVersion,
            SourceDocumentSha256 =
                result.SourceDocumentSha256,
            Status =
                DocumentDualRunTransportJsonMapping
                    .EnumName(
                        result.Status),
            Pages =
                result
                    .Pages
                    .Select(
                        DocumentDualRunWorkerPageResultJsonContract
                            .FromModel)
                    .ToArray(),
            Failure =
                result.Failure is null
                    ? null
                    : DocumentDualRunWorkerFailureJsonContract
                        .FromModel(
                            result.Failure)
        };

    public DocumentDualRunWorkerResult ToModel() =>
        new(
            DocumentDualRunTransportJsonMapping
                .Guid(
                    JobId,
                    "jobId"),
            DocumentDualRunTransportJsonMapping
                .ParseEnum<DocumentDualRunExecutionMode>(
                    ExecutionMode,
                    "executionMode"),
            DocumentDualRunTransportJsonMapping
                .Required(
                    WorkerEngineVersion,
                    "workerEngineVersion"),
            DocumentDualRunTransportJsonMapping
                .Required(
                    SourceDocumentSha256,
                    "sourceDocumentSha256"),
            DocumentDualRunTransportJsonMapping
                .ParseEnum<DocumentDualRunWorkerResultStatus>(
                    Status,
                    "status"),
            DocumentDualRunTransportJsonMapping
                .RequiredArray(
                    Pages,
                    "pages",
                    static page =>
                        page.ToModel()),
            Failure
                ?.ToModel());

    #endregion
}

internal sealed class DocumentDualRunWorkerFailureJsonContract
{
    #region Properties

    [JsonPropertyName("stage"), JsonRequired]
    public string Stage { get; init; } =
        null!;

    [JsonPropertyName("exceptionType"), JsonRequired]
    public string ExceptionType { get; init; } =
        null!;

    [JsonPropertyName("message"), JsonRequired]
    public string Message { get; init; } =
        null!;

    [JsonPropertyName("physicalPageNumber")]
    public int? PhysicalPageNumber { get; init; }

    #endregion

    #region Methods Mapping

    public static DocumentDualRunWorkerFailureJsonContract FromModel(
        DocumentDualRunWorkerFailure failure) =>
        new()
        {
            Stage =
                DocumentDualRunTransportJsonMapping
                    .EnumName(
                        failure.Stage),
            ExceptionType =
                failure.ExceptionType,
            Message =
                failure.Message,
            PhysicalPageNumber =
                failure.PhysicalPageNumber
        };

    public DocumentDualRunWorkerFailure ToModel() =>
        new(
            DocumentDualRunTransportJsonMapping
                .ParseEnum<DocumentDualRunWorkerFailureStage>(
                    Stage,
                    "failure.stage"),
            DocumentDualRunTransportJsonMapping
                .Required(
                    ExceptionType,
                    "failure.exceptionType"),
            DocumentDualRunTransportJsonMapping
                .Required(
                    Message,
                    "failure.message"),
            PhysicalPageNumber);

    #endregion
}

internal sealed class DocumentDualRunWorkerPageResultJsonContract
{
    #region Properties

    [JsonPropertyName("physicalPageNumber"), JsonRequired]
    public int PhysicalPageNumber { get; init; }

    [JsonPropertyName("authoritativePlanningAgreement"), JsonRequired]
    public bool AuthoritativePlanningAgreement { get; init; }

    [JsonPropertyName("candidateTextMode"), JsonRequired]
    public string CandidateTextMode { get; init; } =
        null!;

    [JsonPropertyName("candidateRemovesAuthoritativeTextMl"), JsonRequired]
    public bool CandidateRemovesAuthoritativeTextMl { get; init; }

    [JsonPropertyName("candidateRequiresVisualAnalysis"), JsonRequired]
    public bool CandidateRequiresVisualAnalysis { get; init; }

    [JsonPropertyName("candidateRequiresMeaningfulVisualPreservation"), JsonRequired]
    public bool CandidateRequiresMeaningfulVisualPreservation { get; init; }

    [JsonPropertyName("candidateExecutionStatus")]
    public string? CandidateExecutionStatus { get; init; }

    [JsonPropertyName("selectedTextSequenceExact")]
    public bool? SelectedTextSequenceExact { get; init; }

    [JsonPropertyName("textProjectionExact")]
    public bool? TextProjectionExact { get; init; }

    [JsonPropertyName("authoritativeTextElementCount")]
    public int? AuthoritativeTextElementCount { get; init; }

    [JsonPropertyName("candidateTextElementCount")]
    public int? CandidateTextElementCount { get; init; }

    [JsonPropertyName("authoritativeReconciliationEvidenceCount")]
    public int? AuthoritativeReconciliationEvidenceCount { get; init; }

    [JsonPropertyName("candidateReconciliationEvidenceCount")]
    public int? CandidateReconciliationEvidenceCount { get; init; }

    [JsonPropertyName("candidateVisualEvidence"), JsonRequired]
    public DocumentDualRunWorkerVisualEvidenceSummaryJsonContract[]
        CandidateVisualEvidence { get; init; } =
            [];

    #endregion

    #region Methods Mapping

    public static DocumentDualRunWorkerPageResultJsonContract FromModel(
        DocumentDualRunWorkerPageResult page) =>
        new()
        {
            PhysicalPageNumber =
                page.PhysicalPageNumber,
            AuthoritativePlanningAgreement =
                page.AuthoritativePlanningAgreement,
            CandidateTextMode =
                DocumentDualRunTransportJsonMapping
                    .EnumName(
                        page.CandidateTextMode),
            CandidateRemovesAuthoritativeTextMl =
                page.CandidateRemovesAuthoritativeTextMl,
            CandidateRequiresVisualAnalysis =
                page.CandidateRequiresVisualAnalysis,
            CandidateRequiresMeaningfulVisualPreservation =
                page.CandidateRequiresMeaningfulVisualPreservation,
            CandidateExecutionStatus =
                page.CandidateExecutionStatus.HasValue
                    ? DocumentDualRunTransportJsonMapping
                        .EnumName(
                            page.CandidateExecutionStatus.Value)
                    : null,
            SelectedTextSequenceExact =
                page.SelectedTextSequenceExact,
            TextProjectionExact =
                page.TextProjectionExact,
            AuthoritativeTextElementCount =
                page.AuthoritativeTextElementCount,
            CandidateTextElementCount =
                page.CandidateTextElementCount,
            AuthoritativeReconciliationEvidenceCount =
                page.AuthoritativeReconciliationEvidenceCount,
            CandidateReconciliationEvidenceCount =
                page.CandidateReconciliationEvidenceCount,
            CandidateVisualEvidence =
                page
                    .CandidateVisualEvidence
                    .Select(
                        DocumentDualRunWorkerVisualEvidenceSummaryJsonContract
                            .FromModel)
                    .ToArray()
        };

    public DocumentDualRunWorkerPageResult ToModel() =>
        new(
            PhysicalPageNumber,
            AuthoritativePlanningAgreement,
            DocumentDualRunTransportJsonMapping
                .ParseEnum<TextExecutionMode>(
                    CandidateTextMode,
                    "pages[].candidateTextMode"),
            CandidateRemovesAuthoritativeTextMl,
            CandidateRequiresVisualAnalysis,
            CandidateRequiresMeaningfulVisualPreservation,
            CandidateExecutionStatus is null
                ? null
                : DocumentDualRunTransportJsonMapping
                    .ParseEnum<DocumentDualRunCandidateTextPageStatus>(
                        CandidateExecutionStatus,
                        "pages[].candidateExecutionStatus"),
            SelectedTextSequenceExact,
            TextProjectionExact,
            AuthoritativeTextElementCount,
            CandidateTextElementCount,
            AuthoritativeReconciliationEvidenceCount,
            CandidateReconciliationEvidenceCount,
            DocumentDualRunTransportJsonMapping
                .RequiredArray(
                    CandidateVisualEvidence,
                    "pages[].candidateVisualEvidence",
                    static evidence =>
                        evidence.ToModel()));

    #endregion
}

internal sealed class DocumentDualRunWorkerVisualEvidenceSummaryJsonContract
{
    #region Properties

    [JsonPropertyName("observationSequence"), JsonRequired]
    public int ObservationSequence { get; init; }

    [JsonPropertyName("readingOrder")]
    public int? ReadingOrder { get; init; }

    [JsonPropertyName("bounds"), JsonRequired]
    public DocumentDualRunNormalizedRectangleJsonContract Bounds { get; init; } =
        null!;

    [JsonPropertyName("evidenceKind"), JsonRequired]
    public string EvidenceKind { get; init; } =
        null!;

    [JsonPropertyName("preservedProfileId")]
    public string? PreservedProfileId { get; init; }

    [JsonPropertyName("preservedMediaType")]
    public string? PreservedMediaType { get; init; }

    [JsonPropertyName("preservedContentLength")]
    public long? PreservedContentLength { get; init; }

    [JsonPropertyName("preservedContentSha256")]
    public string? PreservedContentSha256 { get; init; }

    #endregion

    #region Methods Mapping

    public static DocumentDualRunWorkerVisualEvidenceSummaryJsonContract FromModel(
        DocumentDualRunWorkerVisualEvidenceSummary evidence) =>
        new()
        {
            ObservationSequence =
                evidence.ObservationSequence,
            ReadingOrder =
                evidence.ReadingOrder,
            Bounds =
                DocumentDualRunNormalizedRectangleJsonContract
                    .FromModel(
                        evidence.Bounds),
            EvidenceKind =
                DocumentDualRunTransportJsonMapping
                    .EnumName(
                        evidence.EvidenceKind),
            PreservedProfileId =
                evidence.PreservedProfileId,
            PreservedMediaType =
                evidence.PreservedMediaType,
            PreservedContentLength =
                evidence.PreservedContentLength,
            PreservedContentSha256 =
                evidence.PreservedContentSha256
        };

    public DocumentDualRunWorkerVisualEvidenceSummary ToModel() =>
        new(
            ObservationSequence,
            ReadingOrder,
            DocumentDualRunTransportJsonMapping
                .Required(
                    Bounds,
                    "pages[].candidateVisualEvidence[].bounds")
                .ToModel(),
            DocumentDualRunTransportJsonMapping
                .ParseEnum<VisualEvidenceKind>(
                    EvidenceKind,
                    "pages[].candidateVisualEvidence[].evidenceKind"),
            PreservedProfileId,
            PreservedMediaType,
            PreservedContentLength,
            PreservedContentSha256);

    #endregion
}

internal sealed class DocumentDualRunNormalizedRectangleJsonContract
{
    #region Properties

    [JsonPropertyName("left"), JsonRequired]
    public double Left { get; init; }

    [JsonPropertyName("top"), JsonRequired]
    public double Top { get; init; }

    [JsonPropertyName("right"), JsonRequired]
    public double Right { get; init; }

    [JsonPropertyName("bottom"), JsonRequired]
    public double Bottom { get; init; }

    #endregion

    #region Methods Mapping

    public static DocumentDualRunNormalizedRectangleJsonContract FromModel(
        NormalizedRectangle bounds) =>
        new()
        {
            Left =
                bounds.Left,
            Top =
                bounds.Top,
            Right =
                bounds.Right,
            Bottom =
                bounds.Bottom
        };

    public NormalizedRectangle ToModel() =>
        new(
            Left,
            Top,
            Right,
            Bottom);

    #endregion
}

internal static class DocumentDualRunTransportJsonMapping
{
    #region Methods Required Values

    public static string Required(
        string? value,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new JsonException(
                $"Required JSON property '{propertyName}' cannot be empty.");
        }

        return value.Trim();
    }

    public static T Required<T>(
        T? value,
        string propertyName)
        where T : class =>
        value ??
        throw new JsonException(
            $"Required JSON property '{propertyName}' cannot be null.");

    public static TModel[] RequiredArray<TContract, TModel>(
        TContract[]? value,
        string propertyName,
        Func<TContract, TModel> map)
        where TContract : class
    {
        if (value is null)
        {
            throw new JsonException(
                $"Required JSON array '{propertyName}' cannot be null.");
        }

        var result =
            new TModel[value.Length];

        for (var index = 0;
             index <
             value.Length;
             index++)
        {
            var item =
                value[index] ??
                throw new JsonException(
                    $"Required JSON array '{propertyName}' cannot contain null values.");

            result[index] =
                map(
                    item);
        }

        return result;
    }

    #endregion

    #region Methods Enum and Guid

    public static string EnumName<TEnum>(
        TEnum value)
        where TEnum : struct, Enum =>
        Enum.GetName(
            value) ??
        throw new InvalidOperationException(
            $"Undefined enum value '{value}' cannot cross the Dual Run transport boundary.");

    public static TEnum ParseEnum<TEnum>(
        string? value,
        string propertyName)
        where TEnum : struct, Enum
    {
        var normalized =
            Required(
                value,
                propertyName);

        if (!Enum.TryParse<TEnum>(
                normalized,
                ignoreCase:
                    false,
                out var parsed) ||
            !Enum.IsDefined(
                parsed) ||
            !string.Equals(
                Enum.GetName(
                    parsed),
                normalized,
                StringComparison.Ordinal))
        {
            throw new JsonException(
                $"JSON property '{propertyName}' contains unsupported " +
                $"{typeof(TEnum).Name} value '{normalized}'.");
        }

        return parsed;
    }

    public static Guid Guid(
        string? value,
        string propertyName)
    {
        var normalized =
            Required(
                value,
                propertyName);

        if (!System.Guid.TryParseExact(
                normalized,
                "D",
                out var parsed) ||
            parsed ==
                System.Guid.Empty)
        {
            throw new JsonException(
                $"JSON property '{propertyName}' must contain a non-empty GUID in D format.");
        }

        return parsed;
    }

    #endregion
}
