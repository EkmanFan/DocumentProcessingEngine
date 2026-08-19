using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.UnitTests.Provenance;

/// <summary>
/// Verifies the C2.2 format-neutral processing-evidence contracts.
/// </summary>
public sealed class PortableProcessingEvidenceTests
{
    #region Methods Tests

    [Fact]
    public void TextSourceKind_DoesNotExposePdfSpecificOrigin()
    {
        var names =
            Enum.GetNames<DocumentTextSourceKind>();

        Assert.DoesNotContain(
            "NativePdf",
            names);

        Assert.Contains(
            "Native",
            names);
    }

    [Fact]
    public void ElementEvidence_RetainsNativeTextCustodyWithoutPageState()
    {
        const string selectedText =
            "Native source text.";

        var evidence =
            new DocumentElementProcessingEvidence(
                elementId:
                    "element-1",
                DocumentTextSourceKind.Native,
                selectedText,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    selectedText),
                nativeCandidateSequence:
                    12,
                layoutCandidateSequence:
                    null,
                ocrBackendId:
                    null,
                ocrProfileId:
                    null,
                reconciliationDecision:
                    TextReconciliationDecision.NativeOnly,
                textsEquivalent:
                    null,
                hasReconciliationDivergence:
                    false,
                selectedTextPreparation:
                    null,
                normalizationDehyphenation:
                    null,
                normalizationChangedText:
                    false,
                exclusionReason:
                    null,
                isResolved:
                    true,
                layoutKind:
                    null);

        Assert.Equal(
            DocumentTextSourceKind.Native,
            evidence.TextSource);

        Assert.Equal(
            12,
            evidence.NativeCandidateSequence);

        Assert.Null(
            typeof(DocumentElementProcessingEvidence)
                .GetProperty(
                    "PhysicalPageNumber"));

        Assert.Null(
            typeof(DocumentElementProcessingEvidence)
                .GetProperty(
                    "Bounds"));
    }

    [Fact]
    public void ElementEvidence_RetainsOcrBackendAndNormalizationCustody()
    {
        const string selectedText =
            "OCR source text.";

        var evidence =
            new DocumentElementProcessingEvidence(
                elementId:
                    "element-2",
                DocumentTextSourceKind.Ocr,
                selectedText,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    selectedText),
                nativeCandidateSequence:
                    null,
                layoutCandidateSequence:
                    4,
                ocrBackendId:
                    "paddle-ocr",
                ocrProfileId:
                    "ocr-profile-v1",
                reconciliationDecision:
                    TextReconciliationDecision.OcrOnly,
                textsEquivalent:
                    null,
                hasReconciliationDivergence:
                    false,
                selectedTextPreparation:
                    new TextDehyphenationProvenance(
                        softHyphenRemovalCount:
                            1,
                        boundaryJoinCount:
                            0),
                normalizationDehyphenation:
                    null,
                normalizationChangedText:
                    true,
                exclusionReason:
                    DocumentBlockExclusionReason.RepeatedFooter,
                isResolved:
                    true,
                layoutKind:
                    LayoutObservationKind.Text);

        Assert.Equal(
            "paddle-ocr",
            evidence.OcrBackendId);

        Assert.True(
            evidence.IsExcluded);

        Assert.True(
            evidence.NormalizationChangedText);
    }

    [Fact]
    public void ElementEvidence_RejectsTextWithoutTextSource()
    {
        const string selectedText =
            "Authoritative source text.";

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentElementProcessingEvidence(
                    elementId:
                        "element-1",
                    DocumentTextSourceKind.None,
                    selectedText,
                    ProvenanceTextHashing.ComputeUtf8Sha256(
                        selectedText),
                    nativeCandidateSequence:
                        null,
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
                        false,
                    exclusionReason:
                        null,
                    isResolved:
                        false,
                layoutKind:
                    null));
    }

    [Fact]
    public void SegmentEvidence_ReportsMixedNativeAndOcrSources()
    {
        var evidence =
            new DocumentSegmentProcessingEvidence(
                segmentId:
                    "segment-1",
                textSources:
                    [
                        DocumentTextSourceKind.Native,
                        DocumentTextSourceKind.Ocr
                    ],
                hasUnresolvedEvidence:
                    false);

        Assert.True(
            evidence.IsMixedTextSource);

        Assert.Equal(
            2,
            evidence.TextSources.Count);
    }

    [Fact]
    public void SegmentEvidence_DeduplicatesSameTextSource()
    {
        var evidence =
            new DocumentSegmentProcessingEvidence(
                segmentId:
                    "segment-1",
                textSources:
                    [
                        DocumentTextSourceKind.Native,
                        DocumentTextSourceKind.Native
                    ],
                hasUnresolvedEvidence:
                    false);

        Assert.False(
            evidence.IsMixedTextSource);

        Assert.Single(
            evidence.TextSources);
    }

    [Fact]
    public void SegmentEvidence_HasNoPhysicalPageSpan()
    {
        Assert.Null(
            typeof(DocumentSegmentProcessingEvidence)
                .GetProperty(
                    "FirstPhysicalPageNumber"));

        Assert.Null(
            typeof(DocumentSegmentProcessingEvidence)
                .GetProperty(
                    "LastPhysicalPageNumber"));
    }

    #endregion
}
