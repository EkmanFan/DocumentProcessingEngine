using System.Text;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.Engine.Results;

/// <summary>
/// Engine-owned deterministic assembly of structured, non-paged native
/// evidence into the portable consumer result.
/// </summary>
internal static class StructuredNativeDocumentProjector
{
    #region Variables and Constants

    private const string AssemblyProfileId =
        "structured-native-assembly-v1";

    private const string NormalizationProfileId =
        "structured-native-whitespace-v1";

    private const string SegmentationProfileId =
        "structured-native-content-unit-v1";

    #endregion

    #region Methods Projection

    public static async Task<DocumentProcessingResult> ProjectAsync(
        PreparedDocumentSource prepared,
        IDocumentFormat documentFormat,
        StructuredNativeDocumentEvidence evidence,
        string engineVersion,
        UserVisualAssetWriter? userVisualAssetWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            prepared);

        ArgumentNullException.ThrowIfNull(
            evidence);

        ArgumentNullException.ThrowIfNull(
            documentFormat);

        cancellationToken.ThrowIfCancellationRequested();

        var format =
            documentFormat.Format;

        if (string.IsNullOrWhiteSpace(
                engineVersion))
        {
            throw new ArgumentException(
                "Engine version cannot be empty.",
                nameof(engineVersion));
        }

        var elements =
            new List<DocumentElement>();

        var elementEvidence =
            new List<DocumentElementProcessingEvidence>();

        var segments =
            new List<DocumentStructuralSegment>();

        var segmentEvidence =
            new List<DocumentSegmentProcessingEvidence>();

        var visualAssets =
            new List<DocumentVisualAsset>();

        var visualPreservationProfileIds =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var unit in
                 evidence.ContentUnits)
        {
            var normalizedBlocks =
                unit.TextBlocks
                    .Select(
                        block =>
                            new
                            {
                                Block =
                                    block,
                                Text =
                                    NormalizeWhitespace(
                                        block.SourceText)
                            })
                    .Where(
                        item =>
                            item.Text.Length >
                            0)
                    .ToArray();

            if (normalizedBlocks.Length ==
                0)
            {
                continue;
            }

            var segmentId =
                $"segment-{segments.Count + 1:D6}";

            var sourceElementIds =
                new List<string>(
                    normalizedBlocks.Length);

            string? headingText =
                null;

            foreach (var item in
                     normalizedBlocks)
            {
                var elementId =
                    $"element-{elements.Count + 1:D6}";

                var finalTextHash =
                    ProvenanceTextHashing.ComputeUtf8Sha256(
                        item.Text);

                elements.Add(
                    new DocumentElement(
                        elementId,
                        elements.Count,
                        MapKind(
                            item.Block.Kind),
                        item.Block.Location,
                        segmentId,
                        item.Text,
                        finalTextHash));

                elementEvidence.Add(
                    new DocumentElementProcessingEvidence(
                        elementId,
                        DocumentTextSourceKind.Native,
                        item.Block.SourceText,
                        ProvenanceTextHashing.ComputeUtf8Sha256(
                            item.Block.SourceText),
                        nativeCandidateSequence:
                            elements.Count -
                            1,
                        layoutCandidateSequence:
                            null,
                        ocrBackendId:
                            null,
                        ocrProfileId:
                            null,
                        reconciliationDecision:
                            null,
                        textsEquivalent:
                            null,
                        hasReconciliationDivergence:
                            false,
                        selectedTextPreparation:
                            null,
                        normalizationDehyphenation:
                            null,
                        normalizationChangedText:
                            !string.Equals(
                                item.Block.SourceText,
                                item.Text,
                                StringComparison.Ordinal),
                        exclusionReason:
                            null,
                        isResolved:
                            true));

                sourceElementIds.Add(
                    elementId);

                if (headingText is null &&
                    item.Block.Kind ==
                    StructuredNativeTextBlockKind.Heading)
                {
                    headingText =
                        item.Text;
                }
            }

            var segmentText =
                string.Join(
                    "\n\n",
                    normalizedBlocks
                        .Select(
                            item =>
                                item.Text));

            segments.Add(
                new DocumentStructuralSegment(
                    segmentId,
                    segments.Count,
                    segmentText,
                    ProvenanceTextHashing.ComputeUtf8Sha256(
                        segmentText),
                    headingText,
                    sourceElementIds));

            segmentEvidence.Add(
                new DocumentSegmentProcessingEvidence(
                    segmentId,
                    [
                        DocumentTextSourceKind.Native
                    ],
                    hasUnresolvedEvidence:
                        false));
        }

        var selectedVisuals =
            evidence.Visuals
                .Where(
                    StructuredNativeVisualSelectionPolicy
                        .ShouldPreserve)
                .ToArray();

        if (selectedVisuals.Length >
            0)
        {
            if (userVisualAssetWriter is null)
            {
                throw new InvalidOperationException(
                    "Meaningful structured-document visual preservation requires the user's visual asset writer.");
            }

            if (documentFormat is not
                    IStructuredNativeVisualMaterializer materializer ||
                !materializer.CanMaterialize(
                    format))
            {
                throw new InvalidOperationException(
                    $"Document format '{format}' selected structured visuals without exposing their materialization capability.");
            }

            foreach (var visual in
                     selectedVisuals)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var destination =
                    await userVisualAssetWriter(
                            prepared.Source,
                            new UserSourceVisualAssetWriteRequest(
                                format,
                                visual),
                            cancellationToken)
                        .ConfigureAwait(false);

                if (destination is null)
                {
                    throw new InvalidOperationException(
                        "User visual asset writer returned null.");
                }

                var materialization =
                    await materializer.MaterializeAsync(
                            prepared.Source,
                            format,
                            visual,
                            destination,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (!string.Equals(
                        materialization.MediaType,
                        visual.MediaType,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Structured visual '{visual.VisualId}' materialized with a media type that differs from its selected native evidence.");
                }

                var elementId =
                    $"element-{elements.Count + 1:D6}";

                elements.Add(
                    new DocumentElement(
                        elementId,
                        elements.Count,
                        DocumentElementKind.Visual,
                        visual.Location,
                        segmentId:
                            null,
                        text:
                            null,
                        textSha256:
                            null));

                visualAssets.Add(
                    new DocumentVisualAsset(
                        $"{elementId}:preserved-visual",
                        elementId,
                        materialization.ProfileId,
                        materialization.MediaType,
                        materialization.ContentLength,
                        materialization.ContentSha256));

                visualPreservationProfileIds.Add(
                    materialization.ProfileId);
            }
        }

        var source =
            new DocumentSourceDescriptor(
                format,
                prepared.Sha256,
                prepared.ByteLength,
                prepared.Source.FileName,
                prepared.Source.DeclaredMediaType);

        var manifest =
            new DocumentProcessingManifest(
                engineVersion,
                evidence.NativeExtractionIdentity,
                rasterization:
                    null,
                layoutAnalysis:
                    null,
                ocr:
                    [],
                reconciliation:
                    null,
                visualPreservationProfileIds:
                    visualPreservationProfileIds.ToArray(),
                AssemblyProfileId,
                NormalizationProfileId,
                SegmentationProfileId);

        return new DocumentProcessingResult(
            source,
            manifest,
            elements,
            elementEvidence,
            segments,
            segmentEvidence,
            visualAssets,
            DocumentProcessingQualityObservations.Empty,
            evidence.SourceStructure);
    }

    #endregion

    #region Methods Mapping and Normalization

    private static DocumentElementKind MapKind(
        StructuredNativeTextBlockKind kind) =>
        kind switch
        {
            StructuredNativeTextBlockKind.Text =>
                DocumentElementKind.Text,
            StructuredNativeTextBlockKind.Heading =>
                DocumentElementKind.Heading,
            StructuredNativeTextBlockKind.Caption =>
                DocumentElementKind.Caption,
            _ =>
                throw new InvalidDataException(
                    $"Unsupported structured native text-block kind '{kind}'.")
        };

    private static string NormalizeWhitespace(
        string value)
    {
        var builder =
            new StringBuilder(
                value.Length);

        var pendingWhitespace =
            false;

        foreach (var character in
                 value)
        {
            if (char.IsWhiteSpace(
                    character))
            {
                pendingWhitespace =
                    builder.Length >
                    0;

                continue;
            }

            if (pendingWhitespace)
            {
                builder.Append(
                    ' ');

                pendingWhitespace =
                    false;
            }

            builder.Append(
                character);
        }

        return builder.ToString();
    }

    #endregion
}
