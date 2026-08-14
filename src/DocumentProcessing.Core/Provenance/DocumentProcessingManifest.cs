namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Compact deterministic processing manifest.
///
/// This is default custody information, not a chronological runtime log.
/// </summary>
public sealed record DocumentProcessingManifest
{
    public DocumentProcessingManifest(
        string engineVersion,
        ProcessingComponentIdentity nativeExtraction,
        ProcessingComponentIdentity? rasterization,
        ProcessingComponentIdentity? layoutAnalysis,
        IReadOnlyList<ProcessingComponentIdentity> ocr,
        ProcessingComponentIdentity? reconciliation,
        IReadOnlyList<string> visualPreservationProfileIds,
        string assemblyProfileId,
        string normalizationProfileId,
        string segmentationProfileId)
    {
        if (string.IsNullOrWhiteSpace(
                engineVersion))
        {
            throw new ArgumentException(
                "Engine version cannot be empty.",
                nameof(engineVersion));
        }

        NativeExtraction =
            nativeExtraction ??
            throw new ArgumentNullException(
                nameof(nativeExtraction));

        ArgumentNullException.ThrowIfNull(
            ocr);

        ArgumentNullException.ThrowIfNull(
            visualPreservationProfileIds);

        if (string.IsNullOrWhiteSpace(
                assemblyProfileId))
        {
            throw new ArgumentException(
                "Assembly profile ID cannot be empty.",
                nameof(assemblyProfileId));
        }

        if (string.IsNullOrWhiteSpace(
                normalizationProfileId))
        {
            throw new ArgumentException(
                "Normalization profile ID cannot be empty.",
                nameof(normalizationProfileId));
        }

        if (string.IsNullOrWhiteSpace(
                segmentationProfileId))
        {
            throw new ArgumentException(
                "Segmentation profile ID cannot be empty.",
                nameof(segmentationProfileId));
        }

        EngineVersion =
            engineVersion.Trim();

        Rasterization = rasterization;
        LayoutAnalysis = layoutAnalysis;

        Ocr =
            ocr
                .Distinct()
                .OrderBy(
                    identity =>
                        identity.BackendId,
                    StringComparer.Ordinal)
                .ThenBy(
                    identity =>
                        identity.ProfileId,
                    StringComparer.Ordinal)
                .ToArray();

        Reconciliation = reconciliation;

        VisualPreservationProfileIds =
            visualPreservationProfileIds
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .Select(
                    value =>
                        value.Trim())
                .Distinct(
                    StringComparer.Ordinal)
                .OrderBy(
                    value =>
                        value,
                    StringComparer.Ordinal)
                .ToArray();

        AssemblyProfileId =
            assemblyProfileId.Trim();

        NormalizationProfileId =
            normalizationProfileId.Trim();

        SegmentationProfileId =
            segmentationProfileId.Trim();
    }

    public string EngineVersion { get; }

    public ProcessingComponentIdentity NativeExtraction { get; }

    public ProcessingComponentIdentity? Rasterization { get; }

    public ProcessingComponentIdentity? LayoutAnalysis { get; }

    public IReadOnlyList<ProcessingComponentIdentity> Ocr { get; }

    public ProcessingComponentIdentity? Reconciliation { get; }

    public IReadOnlyList<string> VisualPreservationProfileIds { get; }

    public string AssemblyProfileId { get; }

    public string NormalizationProfileId { get; }

    public string SegmentationProfileId { get; }
}
