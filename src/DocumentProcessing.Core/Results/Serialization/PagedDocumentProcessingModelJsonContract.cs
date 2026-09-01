using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Quality;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Core.Results.Serialization;

/// <summary>
/// Explicit internal JSON V1 transport contract.
///
/// These DTOs are intentionally not public domain models. Every portable JSON
/// property name is fixed explicitly here so CLR names and convenience
/// properties cannot accidentally become the wire contract.
/// </summary>
internal sealed class PagedDocumentProcessingModelJsonContract
{
    #region Properties

    [JsonPropertyName("schemaVersion"), JsonRequired]
    public string SchemaVersion { get; init; } =
        null!;

    [JsonPropertyName("source"), JsonRequired]
    public DocumentSourceIdentityJsonContract Source { get; init; } =
        null!;

    [JsonPropertyName("processingManifest"), JsonRequired]
    public DocumentProcessingManifestJsonContract ProcessingManifest { get; init; } =
        null!;

    [JsonPropertyName("pages"), JsonRequired]
    public PagedDocumentProcessingPageJsonContract[] Pages { get; init; } =
        [];

    [JsonPropertyName("elements"), JsonRequired]
    public DocumentElementProvenanceJsonContract[] Elements { get; init; } =
        [];

    [JsonPropertyName("structuralSegments"), JsonRequired]
    public DocumentSegmentProvenanceJsonContract[] StructuralSegments { get; init; } =
        [];

    [JsonPropertyName("qualityObservations"), JsonRequired]
    public PagedDocumentProcessingQualityJsonContract QualityObservations { get; init; } =
        null!;

    #endregion


    #region Methods

    public static PagedDocumentProcessingModelJsonContract FromModel(
        PagedDocumentProcessingModel result) =>
        new()
        {
            SchemaVersion =
                PagedDocumentProcessingModel.SchemaVersionId,
            Source =
                DocumentSourceIdentityJsonContract
                    .FromModel(
                        result.Source),
            ProcessingManifest =
                DocumentProcessingManifestJsonContract
                    .FromModel(
                        result.ProcessingManifest),
            Pages =
                result.Pages
                    .Select(
                        PagedDocumentProcessingPageJsonContract
                            .FromModel)
                    .ToArray(),
            Elements =
                result.Elements
                    .Select(
                        DocumentElementProvenanceJsonContract
                            .FromModel)
                    .ToArray(),
            StructuralSegments =
                result.StructuralSegments
                    .Select(
                        DocumentSegmentProvenanceJsonContract
                            .FromModel)
                    .ToArray(),
            QualityObservations =
                PagedDocumentProcessingQualityJsonContract
                    .FromModel(
                        result.QualityObservations)
        };

    public PagedDocumentProcessingModel ToModel() =>
        new(
            JsonContractMapping
                .Require(
                    Source,
                    "source")
                .ToModel(),
            JsonContractMapping
                .Require(
                    ProcessingManifest,
                    "processingManifest")
                .ToModel(),
            JsonContractMapping
                .MapRequired(
                    Pages,
                    "pages",
                    static page =>
                        page.ToModel()),
            JsonContractMapping
                .MapRequired(
                    Elements,
                    "elements",
                    static element =>
                        element.ToModel()),
            JsonContractMapping
                .MapRequired(
                    StructuralSegments,
                    "structuralSegments",
                    static segment =>
                        segment.ToModel()),
            JsonContractMapping
                .Require(
                    QualityObservations,
                    "qualityObservations")
                .ToModel());

    #endregion
}

internal sealed class DocumentSourceIdentityJsonContract
{
    #region Properties

    [JsonPropertyName("format"), JsonRequired]
    public string Format { get; init; } =
        null!;

    [JsonPropertyName("sha256"), JsonRequired]
    public string Sha256 { get; init; } =
        null!;

    [JsonPropertyName("byteLength"), JsonRequired]
    public long ByteLength { get; init; }

    [JsonPropertyName("physicalPageCount"), JsonRequired]
    public int PhysicalPageCount { get; init; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    [JsonPropertyName("declaredMediaType")]
    public string? DeclaredMediaType { get; init; }

    #endregion


    #region Methods

    public static DocumentSourceIdentityJsonContract FromModel(
        DocumentSourceIdentity source) =>
        new()
        {
            Format =
                source.Format.Value,
            Sha256 =
                source.Sha256,
            ByteLength =
                source.ByteLength,
            PhysicalPageCount =
                source.PhysicalPageCount,
            FileName =
                source.FileName,
            DeclaredMediaType =
                source.DeclaredMediaType
        };

    public DocumentSourceIdentity ToModel() =>
        new(
            new DocumentFormatId(
                JsonContractMapping
                    .RequireNonBlank(
                        Format,
                        "source.format")),
            JsonContractMapping
                .RequireNonBlank(
                    Sha256,
                    "source.sha256"),
            ByteLength,
            PhysicalPageCount,
            FileName,
            DeclaredMediaType);

    #endregion
}

internal sealed class DocumentProcessingManifestJsonContract
{
    #region Properties

    [JsonPropertyName("engineVersion"), JsonRequired]
    public string EngineVersion { get; init; } =
        null!;

    [JsonPropertyName("nativeExtraction"), JsonRequired]
    public ProcessingComponentIdentityJsonContract NativeExtraction { get; init; } =
        null!;

    [JsonPropertyName("rasterization")]
    public ProcessingComponentIdentityJsonContract? Rasterization { get; init; }

    [JsonPropertyName("layoutAnalysis")]
    public ProcessingComponentIdentityJsonContract? LayoutAnalysis { get; init; }

    [JsonPropertyName("ocr"), JsonRequired]
    public ProcessingComponentIdentityJsonContract[] Ocr { get; init; } =
        [];

    [JsonPropertyName("reconciliation")]
    public ProcessingComponentIdentityJsonContract? Reconciliation { get; init; }

    [JsonPropertyName("visualPreservationProfileIds"), JsonRequired]
    public string[] VisualPreservationProfileIds { get; init; } =
        [];

    [JsonPropertyName("assemblyProfileId"), JsonRequired]
    public string AssemblyProfileId { get; init; } =
        null!;

    [JsonPropertyName("normalizationProfileId"), JsonRequired]
    public string NormalizationProfileId { get; init; } =
        null!;

    [JsonPropertyName("segmentationProfileId"), JsonRequired]
    public string SegmentationProfileId { get; init; } =
        null!;

    #endregion


    #region Methods

    public static DocumentProcessingManifestJsonContract FromModel(
        DocumentProcessingManifest manifest) =>
        new()
        {
            EngineVersion =
                manifest.EngineVersion,
            NativeExtraction =
                ProcessingComponentIdentityJsonContract
                    .FromModel(
                        manifest.NativeExtraction),
            Rasterization =
                manifest.Rasterization is null
                    ? null
                    : ProcessingComponentIdentityJsonContract
                        .FromModel(
                            manifest.Rasterization),
            LayoutAnalysis =
                manifest.LayoutAnalysis is null
                    ? null
                    : ProcessingComponentIdentityJsonContract
                        .FromModel(
                            manifest.LayoutAnalysis),
            Ocr =
                manifest.Ocr
                    .Select(
                        ProcessingComponentIdentityJsonContract
                            .FromModel)
                    .ToArray(),
            Reconciliation =
                manifest.Reconciliation is null
                    ? null
                    : ProcessingComponentIdentityJsonContract
                        .FromModel(
                            manifest.Reconciliation),
            VisualPreservationProfileIds =
                manifest.VisualPreservationProfileIds
                    .ToArray(),
            AssemblyProfileId =
                manifest.AssemblyProfileId,
            NormalizationProfileId =
                manifest.NormalizationProfileId,
            SegmentationProfileId =
                manifest.SegmentationProfileId
        };

    public DocumentProcessingManifest ToModel() =>
        new(
            JsonContractMapping
                .RequireNonBlank(
                    EngineVersion,
                    "processingManifest.engineVersion"),
            JsonContractMapping
                .Require(
                    NativeExtraction,
                    "processingManifest.nativeExtraction")
                .ToModel(),
            Rasterization
                ?.ToModel(),
            LayoutAnalysis
                ?.ToModel(),
            JsonContractMapping
                .MapRequired(
                    Ocr,
                    "processingManifest.ocr",
                    static identity =>
                        identity.ToModel()),
            Reconciliation
                ?.ToModel(),
            JsonContractMapping
                .RequireNonBlankItems(
                    VisualPreservationProfileIds,
                    "processingManifest.visualPreservationProfileIds"),
            JsonContractMapping
                .RequireNonBlank(
                    AssemblyProfileId,
                    "processingManifest.assemblyProfileId"),
            JsonContractMapping
                .RequireNonBlank(
                    NormalizationProfileId,
                    "processingManifest.normalizationProfileId"),
            JsonContractMapping
                .RequireNonBlank(
                    SegmentationProfileId,
                    "processingManifest.segmentationProfileId"));

    #endregion
}

internal sealed class ProcessingComponentIdentityJsonContract
{
    [JsonPropertyName("backendId"), JsonRequired]
    public string BackendId { get; init; } =
        null!;

    [JsonPropertyName("profileId"), JsonRequired]
    public string ProfileId { get; init; } =
        null!;

    public static ProcessingComponentIdentityJsonContract FromModel(
        ProcessingComponentIdentity identity) =>
        new()
        {
            BackendId =
                identity.BackendId,
            ProfileId =
                identity.ProfileId
        };

    public ProcessingComponentIdentity ToModel() =>
        new(
            JsonContractMapping
                .RequireNonBlank(
                    BackendId,
                    "processing component backendId"),
            JsonContractMapping
                .RequireNonBlank(
                    ProfileId,
                    "processing component profileId"));
}

internal sealed class PagedDocumentProcessingPageJsonContract
{
    [JsonPropertyName("physicalPageNumber"), JsonRequired]
    public int PhysicalPageNumber { get; init; }

    [JsonPropertyName("contentViewport"), JsonRequired]
    public NormalizedRectangleJsonContract ContentViewport { get; init; } =
        null!;

    [JsonPropertyName("orderedElementIds"), JsonRequired]
    public string[] OrderedElementIds { get; init; } =
        [];

    public static PagedDocumentProcessingPageJsonContract FromModel(
        PagedDocumentProcessingPage page) =>
        new()
        {
            PhysicalPageNumber =
                page.PhysicalPageNumber,
            ContentViewport =
                NormalizedRectangleJsonContract
                    .FromModel(
                        page.ContentViewport),
            OrderedElementIds =
                page.OrderedElementIds
                    .ToArray()
        };

    public PagedDocumentProcessingPage ToModel() =>
        new(
            PhysicalPageNumber,
            JsonContractMapping
                .Require(
                    ContentViewport,
                    "pages[].contentViewport")
                .ToModel(),
            JsonContractMapping
                .RequireNonBlankItems(
                    OrderedElementIds,
                    "pages[].orderedElementIds"));
}

internal sealed class DocumentElementProvenanceJsonContract
{
    #region Properties

    [JsonPropertyName("sourceDocumentSha256"), JsonRequired]
    public string SourceDocumentSha256 { get; init; } =
        null!;

    [JsonPropertyName("elementId"), JsonRequired]
    public string ElementId { get; init; } =
        null!;

    [JsonPropertyName("physicalPageNumber"), JsonRequired]
    public int PhysicalPageNumber { get; init; }

    [JsonPropertyName("readingOrder"), JsonRequired]
    public int ReadingOrder { get; init; }

    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } =
        null!;

    [JsonPropertyName("bounds"), JsonRequired]
    public NormalizedRectangleJsonContract Bounds { get; init; } =
        null!;

    [JsonPropertyName("segmentId")]
    public string? SegmentId { get; init; }

    [JsonPropertyName("selectedSourceText")]
    public string? SelectedSourceText { get; init; }

    [JsonPropertyName("selectedSourceTextSha256")]
    public string? SelectedSourceTextSha256 { get; init; }

    [JsonPropertyName("normalizedText")]
    public string? NormalizedText { get; init; }

    [JsonPropertyName("normalizedTextSha256")]
    public string? NormalizedTextSha256 { get; init; }

    [JsonPropertyName("textOrigin"), JsonRequired]
    public string TextOrigin { get; init; } =
        null!;

    [JsonPropertyName("nativeBlockSourceSequence")]
    public int? NativeBlockSourceSequence { get; init; }

    [JsonPropertyName("layoutObservationSequence")]
    public int? LayoutObservationSequence { get; init; }

    [JsonPropertyName("layoutKind")]
    public string? LayoutKind { get; init; }

    [JsonPropertyName("ocrBackendId")]
    public string? OcrBackendId { get; init; }

    [JsonPropertyName("ocrProfileId")]
    public string? OcrProfileId { get; init; }

    [JsonPropertyName("reconciliationDecision")]
    public string? ReconciliationDecision { get; init; }

    [JsonPropertyName("textsEquivalent")]
    public bool? TextsEquivalent { get; init; }

    [JsonPropertyName("hasReconciliationDivergence"), JsonRequired]
    public bool HasReconciliationDivergence { get; init; }

    [JsonPropertyName("selectedTextPreparation")]
    public TextDehyphenationProvenanceJsonContract? SelectedTextPreparation { get; init; }

    [JsonPropertyName("normalizationDehyphenation")]
    public TextDehyphenationProvenanceJsonContract? NormalizationDehyphenation { get; init; }

    [JsonPropertyName("normalizationChangedText"), JsonRequired]
    public bool NormalizationChangedText { get; init; }

    [JsonPropertyName("exclusionReason")]
    public string? ExclusionReason { get; init; }

    [JsonPropertyName("isResolved"), JsonRequired]
    public bool IsResolved { get; init; }

    [JsonPropertyName("preservedVisual")]
    public PreservedVisualProvenanceJsonContract? PreservedVisual { get; init; }

    #endregion


    #region Methods

    public static DocumentElementProvenanceJsonContract FromModel(
        DocumentElementProvenance element) =>
        new()
        {
            SourceDocumentSha256 =
                element.SourceDocumentSha256,
            ElementId =
                element.ElementId,
            PhysicalPageNumber =
                element.PhysicalPageNumber,
            ReadingOrder =
                element.ReadingOrder,
            Kind =
                JsonContractMapping
                    .EnumName(
                        element.Kind),
            Bounds =
                NormalizedRectangleJsonContract
                    .FromModel(
                        element.Bounds),
            SegmentId =
                element.SegmentId,
            SelectedSourceText =
                element.SelectedSourceText,
            SelectedSourceTextSha256 =
                element.SelectedSourceTextSha256,
            NormalizedText =
                element.NormalizedText,
            NormalizedTextSha256 =
                element.NormalizedTextSha256,
            TextOrigin =
                JsonContractMapping
                    .EnumName(
                        element.TextOrigin),
            NativeBlockSourceSequence =
                element.NativeBlockSourceSequence,
            LayoutObservationSequence =
                element.LayoutObservationSequence,
            LayoutKind =
                element.LayoutKind.HasValue
                    ? JsonContractMapping
                        .EnumName(
                            element.LayoutKind.Value)
                    : null,
            OcrBackendId =
                element.OcrBackendId,
            OcrProfileId =
                element.OcrProfileId,
            ReconciliationDecision =
                element.ReconciliationDecision.HasValue
                    ? JsonContractMapping
                        .EnumName(
                            element.ReconciliationDecision.Value)
                    : null,
            TextsEquivalent =
                element.TextsEquivalent,
            HasReconciliationDivergence =
                element.HasReconciliationDivergence,
            SelectedTextPreparation =
                element.SelectedTextPreparation is null
                    ? null
                    : TextDehyphenationProvenanceJsonContract
                        .FromModel(
                            element.SelectedTextPreparation),
            NormalizationDehyphenation =
                element.NormalizationDehyphenation is null
                    ? null
                    : TextDehyphenationProvenanceJsonContract
                        .FromModel(
                            element.NormalizationDehyphenation),
            NormalizationChangedText =
                element.NormalizationChangedText,
            ExclusionReason =
                element.ExclusionReason.HasValue
                    ? JsonContractMapping
                        .EnumName(
                            element.ExclusionReason.Value)
                    : null,
            IsResolved =
                element.IsResolved,
            PreservedVisual =
                element.PreservedVisual is null
                    ? null
                    : PreservedVisualProvenanceJsonContract
                        .FromModel(
                            element.PreservedVisual)
        };

    public DocumentElementProvenance ToModel() =>
        new(
            JsonContractMapping
                .RequireNonBlank(
                    SourceDocumentSha256,
                    "elements[].sourceDocumentSha256"),
            JsonContractMapping
                .RequireNonBlank(
                    ElementId,
                    "elements[].elementId"),
            PhysicalPageNumber,
            ReadingOrder,
            JsonContractMapping
                .ParseEnum<HybridDocumentElementKind>(
                    Kind,
                    "elements[].kind"),
            JsonContractMapping
                .Require(
                    Bounds,
                    "elements[].bounds")
                .ToModel(),
            SegmentId,
            SelectedSourceText,
            SelectedSourceTextSha256,
            NormalizedText,
            NormalizedTextSha256,
            JsonContractMapping
                .ParseEnum<TextSelectionOrigin>(
                    TextOrigin,
                    "elements[].textOrigin"),
            NativeBlockSourceSequence,
            LayoutObservationSequence,
            LayoutKind is null
                ? null
                : JsonContractMapping
                    .ParseEnum<LayoutObservationKind>(
                        LayoutKind,
                        "elements[].layoutKind"),
            OcrBackendId,
            OcrProfileId,
            ReconciliationDecision is null
                ? null
                : JsonContractMapping
                    .ParseEnum<TextReconciliationDecision>(
                        ReconciliationDecision,
                        "elements[].reconciliationDecision"),
            TextsEquivalent,
            HasReconciliationDivergence,
            SelectedTextPreparation
                ?.ToModel(),
            NormalizationDehyphenation
                ?.ToModel(),
            NormalizationChangedText,
            ExclusionReason is null
                ? null
                : JsonContractMapping
                    .ParseEnum<DocumentBlockExclusionReason>(
                        ExclusionReason,
                        "elements[].exclusionReason"),
            IsResolved,
            PreservedVisual
                ?.ToModel());

    #endregion
}

internal sealed class DocumentSegmentProvenanceJsonContract
{
    #region Properties

    [JsonPropertyName("sourceDocumentSha256"), JsonRequired]
    public string SourceDocumentSha256 { get; init; } =
        null!;

    [JsonPropertyName("segmentId"), JsonRequired]
    public string SegmentId { get; init; } =
        null!;

    [JsonPropertyName("ordinal"), JsonRequired]
    public int Ordinal { get; init; }

    [JsonPropertyName("text"), JsonRequired]
    public string Text { get; init; } =
        null!;

    [JsonPropertyName("textSha256"), JsonRequired]
    public string TextSha256 { get; init; } =
        null!;

    [JsonPropertyName("headingText")]
    public string? HeadingText { get; init; }

    [JsonPropertyName("firstPhysicalPageNumber"), JsonRequired]
    public int FirstPhysicalPageNumber { get; init; }

    [JsonPropertyName("lastPhysicalPageNumber"), JsonRequired]
    public int LastPhysicalPageNumber { get; init; }

    [JsonPropertyName("sourceElementIds"), JsonRequired]
    public string[] SourceElementIds { get; init; } =
        [];

    [JsonPropertyName("textOrigins"), JsonRequired]
    public string[] TextOrigins { get; init; } =
        [];

    [JsonPropertyName("hasUnresolvedEvidence"), JsonRequired]
    public bool HasUnresolvedEvidence { get; init; }

    #endregion


    #region Methods

    public static DocumentSegmentProvenanceJsonContract FromModel(
        DocumentSegmentProvenance segment) =>
        new()
        {
            SourceDocumentSha256 =
                segment.SourceDocumentSha256,
            SegmentId =
                segment.SegmentId,
            Ordinal =
                segment.Ordinal,
            Text =
                segment.Text,
            TextSha256 =
                segment.TextSha256,
            HeadingText =
                segment.HeadingText,
            FirstPhysicalPageNumber =
                segment.FirstPhysicalPageNumber,
            LastPhysicalPageNumber =
                segment.LastPhysicalPageNumber,
            SourceElementIds =
                segment.SourceElementIds
                    .ToArray(),
            TextOrigins =
                segment.TextOrigins
                    .Select(
                        JsonContractMapping
                            .EnumName)
                    .ToArray(),
            HasUnresolvedEvidence =
                segment.HasUnresolvedEvidence
        };

    public DocumentSegmentProvenance ToModel() =>
        new(
            JsonContractMapping
                .RequireNonBlank(
                    SourceDocumentSha256,
                    "structuralSegments[].sourceDocumentSha256"),
            JsonContractMapping
                .RequireNonBlank(
                    SegmentId,
                    "structuralSegments[].segmentId"),
            Ordinal,
            JsonContractMapping
                .Require(
                    Text,
                    "structuralSegments[].text"),
            JsonContractMapping
                .RequireNonBlank(
                    TextSha256,
                    "structuralSegments[].textSha256"),
            HeadingText,
            FirstPhysicalPageNumber,
            LastPhysicalPageNumber,
            JsonContractMapping
                .RequireNonBlankItems(
                    SourceElementIds,
                    "structuralSegments[].sourceElementIds"),
            JsonContractMapping
                .Require(
                    TextOrigins,
                    "structuralSegments[].textOrigins")
                .Select(
                    value =>
                        JsonContractMapping
                            .ParseEnum<TextSelectionOrigin>(
                                value,
                                "structuralSegments[].textOrigins[]"))
                .ToArray(),
            HasUnresolvedEvidence);

    #endregion
}

internal sealed class PagedDocumentProcessingQualityJsonContract
{
    [JsonPropertyName("ocrConfidenceObservations"), JsonRequired]
    public DocumentElementOcrQualityJsonContract[] OcrConfidenceObservations { get; init; } =
        [];

    public static PagedDocumentProcessingQualityJsonContract FromModel(
        PagedDocumentProcessingQualityObservations quality) =>
        new()
        {
            OcrConfidenceObservations =
                quality.OcrConfidenceObservations
                    .Select(
                        DocumentElementOcrQualityJsonContract
                            .FromModel)
                    .ToArray()
        };

    public PagedDocumentProcessingQualityObservations ToModel() =>
        new(
            JsonContractMapping
                .MapRequired(
                    OcrConfidenceObservations,
                    "qualityObservations.ocrConfidenceObservations",
                    static observation =>
                        observation.ToModel()));
}

internal sealed class DocumentElementOcrQualityJsonContract
{
    [JsonPropertyName("elementId"), JsonRequired]
    public string ElementId { get; init; } =
        null!;

    [JsonPropertyName("confidence"), JsonRequired]
    public OcrConfidenceSummaryJsonContract Confidence { get; init; } =
        null!;

    public static DocumentElementOcrQualityJsonContract FromModel(
        DocumentElementOcrQualityObservation observation) =>
        new()
        {
            ElementId =
                observation.ElementId,
            Confidence =
                OcrConfidenceSummaryJsonContract
                    .FromModel(
                        observation.Confidence)
        };

    public DocumentElementOcrQualityObservation ToModel() =>
        new(
            JsonContractMapping
                .RequireNonBlank(
                    ElementId,
                    "qualityObservations.ocrConfidenceObservations[].elementId"),
            JsonContractMapping
                .Require(
                    Confidence,
                    "qualityObservations.ocrConfidenceObservations[].confidence")
                .ToModel());
}

internal sealed class OcrConfidenceSummaryJsonContract
{
    [JsonPropertyName("observationCount"), JsonRequired]
    public int ObservationCount { get; init; }

    [JsonPropertyName("minimum"), JsonRequired]
    public double Minimum { get; init; }

    [JsonPropertyName("arithmeticMean"), JsonRequired]
    public double ArithmeticMean { get; init; }

    [JsonPropertyName("maximum"), JsonRequired]
    public double Maximum { get; init; }

    public static OcrConfidenceSummaryJsonContract FromModel(
        OcrConfidenceSummary confidence) =>
        new()
        {
            ObservationCount =
                confidence.ObservationCount,
            Minimum =
                confidence.Minimum,
            ArithmeticMean =
                confidence.ArithmeticMean,
            Maximum =
                confidence.Maximum
        };

    public OcrConfidenceSummary ToModel() =>
        new(
            ObservationCount,
            Minimum,
            ArithmeticMean,
            Maximum);
}

internal sealed class TextDehyphenationProvenanceJsonContract
{
    [JsonPropertyName("softHyphenRemovalCount"), JsonRequired]
    public int SoftHyphenRemovalCount { get; init; }

    [JsonPropertyName("boundaryJoinCount"), JsonRequired]
    public int BoundaryJoinCount { get; init; }

    public static TextDehyphenationProvenanceJsonContract FromModel(
        TextDehyphenationProvenance provenance) =>
        new()
        {
            SoftHyphenRemovalCount =
                provenance.SoftHyphenRemovalCount,
            BoundaryJoinCount =
                provenance.BoundaryJoinCount
        };

    public TextDehyphenationProvenance ToModel() =>
        new(
            SoftHyphenRemovalCount,
            BoundaryJoinCount);
}

internal sealed class PreservedVisualProvenanceJsonContract
{
    #region Properties

    [JsonPropertyName("profileId"), JsonRequired]
    public string ProfileId { get; init; } =
        null!;

    [JsonPropertyName("mediaType"), JsonRequired]
    public string MediaType { get; init; } =
        null!;

    [JsonPropertyName("sourceRasterPixelWidth"), JsonRequired]
    public int SourceRasterPixelWidth { get; init; }

    [JsonPropertyName("sourceRasterPixelHeight"), JsonRequired]
    public int SourceRasterPixelHeight { get; init; }

    [JsonPropertyName("crop"), JsonRequired]
    public PixelRectangleJsonContract Crop { get; init; } =
        null!;

    [JsonPropertyName("contentLength"), JsonRequired]
    public long ContentLength { get; init; }

    [JsonPropertyName("contentSha256"), JsonRequired]
    public string ContentSha256 { get; init; } =
        null!;

    [JsonPropertyName("qualification")]
    public string? Qualification { get; init; }

    #endregion


    #region Methods

    public static PreservedVisualProvenanceJsonContract FromModel(
        PreservedVisualProvenance visual) =>
        new()
        {
            ProfileId =
                visual.ProfileId,
            MediaType =
                visual.MediaType,
            SourceRasterPixelWidth =
                visual.SourceRasterPixelWidth,
            SourceRasterPixelHeight =
                visual.SourceRasterPixelHeight,
            Crop =
                PixelRectangleJsonContract
                    .FromModel(
                        visual.Crop),
            ContentLength =
                visual.ContentLength,
            ContentSha256 =
                visual.ContentSha256,
            Qualification =
                JsonContractMapping
                    .EnumName(
                        visual.Qualification)
        };

    public PreservedVisualProvenance ToModel() =>
        new(
            JsonContractMapping
                .RequireNonBlank(
                    ProfileId,
                    "elements[].preservedVisual.profileId"),
            JsonContractMapping
                .RequireNonBlank(
                    MediaType,
                    "elements[].preservedVisual.mediaType"),
            SourceRasterPixelWidth,
            SourceRasterPixelHeight,
            JsonContractMapping
                .Require(
                    Crop,
                    "elements[].preservedVisual.crop")
                .ToModel(),
            ContentLength,
            JsonContractMapping
                .RequireNonBlank(
                    ContentSha256,
                    "elements[].preservedVisual.contentSha256"),
            Qualification is null
                ? DocumentVisualQualification.Meaningful
                : JsonContractMapping
                    .ParseEnum<DocumentVisualQualification>(
                        Qualification,
                        "elements[].preservedVisual.qualification"));

    #endregion
}

internal sealed class NormalizedRectangleJsonContract
{
    [JsonPropertyName("left"), JsonRequired]
    public double Left { get; init; }

    [JsonPropertyName("top"), JsonRequired]
    public double Top { get; init; }

    [JsonPropertyName("right"), JsonRequired]
    public double Right { get; init; }

    [JsonPropertyName("bottom"), JsonRequired]
    public double Bottom { get; init; }

    public static NormalizedRectangleJsonContract FromModel(
        NormalizedRectangle rectangle) =>
        new()
        {
            Left =
                rectangle.Left,
            Top =
                rectangle.Top,
            Right =
                rectangle.Right,
            Bottom =
                rectangle.Bottom
        };

    public NormalizedRectangle ToModel() =>
        new(
            Left,
            Top,
            Right,
            Bottom);
}

internal sealed class PixelRectangleJsonContract
{
    [JsonPropertyName("left"), JsonRequired]
    public int Left { get; init; }

    [JsonPropertyName("top"), JsonRequired]
    public int Top { get; init; }

    [JsonPropertyName("right"), JsonRequired]
    public int Right { get; init; }

    [JsonPropertyName("bottom"), JsonRequired]
    public int Bottom { get; init; }

    public static PixelRectangleJsonContract FromModel(
        PixelRectangle rectangle) =>
        new()
        {
            Left =
                rectangle.Left,
            Top =
                rectangle.Top,
            Right =
                rectangle.Right,
            Bottom =
                rectangle.Bottom
        };

    public PixelRectangle ToModel() =>
        new(
            Left,
            Top,
            Right,
            Bottom);
}

internal static class JsonContractMapping
{
    #region Methods

    public static T Require<T>(
        T? value,
        string path)
        where T : class =>
        value ??
        throw new JsonException(
            $"Required JSON value '{path}' cannot be null.");

    public static string Require(
        string? value,
        string path) =>
        value ??
        throw new JsonException(
            $"Required JSON string '{path}' cannot be null.");

    public static string RequireNonBlank(
        string? value,
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new JsonException(
                $"Required JSON string '{path}' cannot be empty.");
        }

        return value;
    }

    public static string[] RequireNonBlankItems(
        string[]? values,
        string path)
    {
        if (values is null)
        {
            throw new JsonException(
                $"Required JSON collection '{path}' cannot be null.");
        }

        for (var index = 0;
             index <
             values.Length;
             index++)
        {
            if (string.IsNullOrWhiteSpace(
                    values[index]))
            {
                throw new JsonException(
                    $"JSON collection '{path}' contains an empty value at index {index}.");
            }
        }

        return values;
    }

    public static TOutput[] MapRequired<TInput, TOutput>(
        TInput[]? values,
        string path,
        Func<TInput, TOutput> map)
        where TInput : class
    {
        if (values is null)
        {
            throw new JsonException(
                $"Required JSON collection '{path}' cannot be null.");
        }

        var result =
            new TOutput[
                values.Length];

        for (var index = 0;
             index <
             values.Length;
             index++)
        {
            var value =
                values[index] ??
                throw new JsonException(
                    $"JSON collection '{path}' contains null at index {index}.");

            result[index] =
                map(
                    value);
        }

        return result;
    }

    public static string EnumName<TEnum>(
        TEnum value)
        where TEnum : struct, Enum
    {
        var declaredName =
            Enum.GetName(
                value) ??
            throw new InvalidOperationException(
                $"Undefined {typeof(TEnum).Name} value '{value}' cannot be serialized.");

        return JsonNamingPolicy.CamelCase
            .ConvertName(
                declaredName);
    }

    public static TEnum ParseEnum<TEnum>(
        string? value,
        string path)
        where TEnum : struct, Enum
    {
        var actual =
            RequireNonBlank(
                value,
                path);

        foreach (var candidate in
                 Enum.GetValues<TEnum>())
        {
            if (string.Equals(
                    EnumName(
                        candidate),
                    actual,
                    StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        throw new JsonException(
            $"JSON value '{actual}' is not a supported {typeof(TEnum).Name} at '{path}'.");
    }

    #endregion
}
