using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.UnitTests.DualRun.Transport;

public sealed class DocumentDualRunTextFingerprintTests
{
    #region Methods Fingerprints

    [Fact]
    public void Fingerprints_SameProjection_AreDeterministic()
    {
        var page =
            Page(
                Bounds(
                    0.10,
                    0.20,
                    0.80,
                    0.30),
                "Alpha");

        var text =
            page.AuthoritativeTextElements;

        Assert.Equal(
            DocumentDualRunTextFingerprint
                .SelectedTextSequenceSha256(
                    text),
            DocumentDualRunTextFingerprint
                .SelectedTextSequenceSha256(
                    text));

        Assert.Equal(
            DocumentDualRunTextFingerprint
                .TextProjectionSha256(
                    text),
            DocumentDualRunTextFingerprint
                .TextProjectionSha256(
                    text));
    }

    [Fact]
    public void Fingerprints_BoundsChange_ChangesProjectionOnly()
    {
        var first =
            Page(
                Bounds(
                    0.10,
                    0.20,
                    0.80,
                    0.30),
                "Alpha");

        var second =
            Page(
                Bounds(
                    0.11,
                    0.20,
                    0.80,
                    0.30),
                "Alpha");

        Assert.Equal(
            DocumentDualRunTextFingerprint
                .SelectedTextSequenceSha256(
                    first.AuthoritativeTextElements),
            DocumentDualRunTextFingerprint
                .SelectedTextSequenceSha256(
                    second.AuthoritativeTextElements));

        Assert.NotEqual(
            DocumentDualRunTextFingerprint
                .TextProjectionSha256(
                    first.AuthoritativeTextElements),
            DocumentDualRunTextFingerprint
                .TextProjectionSha256(
                    second.AuthoritativeTextElements));
    }

    [Fact]
    public void Fingerprints_SignedZeroBounds_MatchProjectionEquality()
    {
        var negativeZero =
            Page(
                Bounds(
                    -0.0,
                    0.20,
                    0.80,
                    0.30),
                "Alpha");

        var positiveZero =
            Page(
                Bounds(
                    0.0,
                    0.20,
                    0.80,
                    0.30),
                "Alpha");

        Assert.Equal(
            negativeZero
                .AuthoritativeTextElements[0]
                .Bounds,
            positiveZero
                .AuthoritativeTextElements[0]
                .Bounds);

        Assert.Equal(
            DocumentDualRunTextFingerprint
                .TextProjectionSha256(
                    negativeZero.AuthoritativeTextElements),
            DocumentDualRunTextFingerprint
                .TextProjectionSha256(
                    positiveZero.AuthoritativeTextElements));
    }

    [Fact]
    public void Fingerprints_TextChange_ChangesBoth()
    {
        var first =
            Page(
                Bounds(
                    0.10,
                    0.20,
                    0.80,
                    0.30),
                "Alpha");

        var second =
            Page(
                Bounds(
                    0.10,
                    0.20,
                    0.80,
                    0.30),
                "Beta");

        Assert.NotEqual(
            DocumentDualRunTextFingerprint
                .SelectedTextSequenceSha256(
                    first.AuthoritativeTextElements),
            DocumentDualRunTextFingerprint
                .SelectedTextSequenceSha256(
                    second.AuthoritativeTextElements));

        Assert.NotEqual(
            DocumentDualRunTextFingerprint
                .TextProjectionSha256(
                    first.AuthoritativeTextElements),
            DocumentDualRunTextFingerprint
                .TextProjectionSha256(
                    second.AuthoritativeTextElements));
    }

    [Fact]
    public void AuthoritativeBaseline_FromDecisionAndPage_IsCompactAndValidated()
    {
        var page =
            Page(
                Bounds(
                    0.10,
                    0.20,
                    0.80,
                    0.30),
                "Alpha");

        var baseline =
            DocumentDualRunAuthoritativePageBaseline
                .From(
                    new PageProcessingDecision(
                        new PageProcessingAssessment(
                            1,
                            NativeTextStatus.Healthy),
                        new PageProcessingPlan(
                            PageProcessingRoute.NativeOnly)),
                    page);

        Assert.Equal(
            1,
            baseline.PhysicalPageNumber);

        Assert.Equal(
            NativeTextStatus.Healthy,
            baseline.NativeTextStatus);

        Assert.Equal(
            PageProcessingRoute.NativeOnly,
            baseline.AuthoritativeRoute);

        Assert.Equal(
            64,
            baseline.SelectedTextSequenceSha256.Length);

        Assert.Equal(
            64,
            baseline.TextProjectionSha256.Length);

        Assert.Equal(
            1,
            baseline.AuthoritativeTextElementCount);

        Assert.Equal(
            0,
            baseline.AuthoritativeReconciliationEvidenceCount);
    }

    #endregion

    #region Methods Test Data

    private static HybridDocumentPage Page(
        NormalizedRectangle bounds,
        string text)
    {
        var block =
            new DocumentTextBlock(
                sourceSequence:
                    0,
                readingOrder:
                    0,
                text,
                bounds);

        return new HybridDocumentPage(
            1,
            [
                new HybridDocumentElement(
                    1,
                    readingOrder:
                        0,
                    HybridDocumentElementKind.Text,
                    bounds,
                    text,
                    TextSelectionOrigin.Native,
                    nativeBlock:
                        block)
            ]);
    }

    private static NormalizedRectangle Bounds(
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            left,
            top,
            right,
            bottom);

    #endregion
}
