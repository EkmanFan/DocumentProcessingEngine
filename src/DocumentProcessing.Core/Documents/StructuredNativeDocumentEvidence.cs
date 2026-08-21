using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Native evidence for a structured source that has no authoritative physical
/// page model.
/// </summary>
public sealed record StructuredNativeDocumentEvidence
{
    #region Properties

    public DocumentSourceStructure SourceStructure { get; }

    public IReadOnlyList<StructuredNativeContentUnit> ContentUnits { get; }

    public ProcessingComponentIdentity NativeExtractionIdentity { get; }

    #endregion

    #region ctor

    public StructuredNativeDocumentEvidence(
        DocumentSourceStructure sourceStructure,
        IReadOnlyList<StructuredNativeContentUnit> contentUnits,
        ProcessingComponentIdentity nativeExtractionIdentity)
    {
        SourceStructure =
            sourceStructure ??
            throw new ArgumentNullException(
                nameof(sourceStructure));

        ArgumentNullException.ThrowIfNull(
            contentUnits);

        if (contentUnits.Any(
                unit =>
                    unit is null))
        {
            throw new ArgumentException(
                "Structured native evidence cannot contain null content units.",
                nameof(contentUnits));
        }

        var units =
            contentUnits.ToArray();

        if (units
                .Select(
                    unit =>
                        unit.UnitId)
                .Distinct(
                    StringComparer.Ordinal)
                .Count() !=
            units.Length)
        {
            throw new ArgumentException(
                "Structured native evidence cannot contain duplicate content-unit IDs.",
                nameof(contentUnits));
        }

        ContentUnits =
            units;

        NativeExtractionIdentity =
            nativeExtractionIdentity ??
            throw new ArgumentNullException(
                nameof(nativeExtractionIdentity));
    }

    #endregion
}
