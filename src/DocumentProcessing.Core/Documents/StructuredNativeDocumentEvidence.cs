using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Native evidence for a structured source that has no authoritative physical
/// page model.
/// </summary>
public sealed record StructuredNativeDocumentEvidence
    : NativeDocumentEvidence
{
    #region Properties

    public DocumentSourceStructure SourceStructure { get; }

    public IReadOnlyList<StructuredNativeContentUnit> ContentUnits { get; }

    public IReadOnlyList<StructuredNativeVisual> Visuals { get; }

    /// <summary>
    /// Gets structured source locations identified as non-narrative note
    /// payload candidates, independently of relation resolution.
    /// </summary>
    public IReadOnlyList<DocumentSourceLocation> NotePayloadCandidateLocations
    {
        get;
    }

    public override ProcessingComponentIdentity NativeExtractionIdentity { get; }

    #endregion

    #region ctor

    public StructuredNativeDocumentEvidence(
        DocumentSourceStructure sourceStructure,
        IReadOnlyList<StructuredNativeContentUnit> contentUnits,
        ProcessingComponentIdentity nativeExtractionIdentity,
        IReadOnlyList<StructuredNativeVisual>? visuals = null)
        : this(
            sourceStructure,
            contentUnits,
            nativeExtractionIdentity,
            visuals,
            documentNotes:
                [])
    {
    }

    public StructuredNativeDocumentEvidence(
        DocumentSourceStructure sourceStructure,
        IReadOnlyList<StructuredNativeContentUnit> contentUnits,
        ProcessingComponentIdentity nativeExtractionIdentity,
        IReadOnlyList<StructuredNativeVisual>? visuals,
        IReadOnlyList<NativeDocumentNote> documentNotes,
        IReadOnlyList<DocumentSourceLocation>?
            notePayloadCandidateLocations = null)
        : base(
            documentNotes)
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

        var nativeVisuals =
            visuals?.ToArray() ??
            [];

        if (nativeVisuals.Any(
                visual =>
                    visual is null))
        {
            throw new ArgumentException(
                "Structured native evidence cannot contain null visuals.",
                nameof(visuals));
        }

        if (nativeVisuals
                .Select(
                    visual =>
                        visual.VisualId)
                .Distinct(
                    StringComparer.Ordinal)
                .Count() !=
            nativeVisuals.Length)
        {
            throw new ArgumentException(
                "Structured native evidence cannot contain duplicate visual IDs.",
                nameof(visuals));
        }

        Visuals =
            nativeVisuals;

        var candidateLocations =
            notePayloadCandidateLocations?.ToArray() ??
            [];

        if (candidateLocations.Any(
                location =>
                    location is null))
        {
            throw new ArgumentException(
                "Structured note-payload candidates cannot contain null locations.",
                nameof(notePayloadCandidateLocations));
        }

        NotePayloadCandidateLocations =
            Array.AsReadOnly(
                candidateLocations);

        NativeExtractionIdentity =
            nativeExtractionIdentity ??
            throw new ArgumentNullException(
                nameof(nativeExtractionIdentity));
    }

    #endregion
}
