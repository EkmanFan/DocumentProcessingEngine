using System.Reflection;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class PageProcessingEvidenceContractTests
{
    [Theory]
    [InlineData(
        NativeTextStatus.Missing,
        TextAuthority.Missing)]
    [InlineData(
        NativeTextStatus.Healthy,
        TextAuthority.Trusted)]
    [InlineData(
        NativeTextStatus.Unverified,
        TextAuthority.NeedsVerification)]
    [InlineData(
        NativeTextStatus.Suspicious,
        TextAuthority.Corrupted)]
    public void TextAuthorityMapper_MapsExistingNativeStatusesWithoutSelectingRoute(
        NativeTextStatus source,
        TextAuthority expected)
    {
        var actual =
            TextAuthorityMapper
                .FromNativeTextStatus(
                    source);

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public void TextAuthorityMapper_RejectsUndefinedNativeStatus()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                TextAuthorityMapper
                    .FromNativeTextStatus(
                        (NativeTextStatus)999));
    }

    [Fact]
    public void PageEvidence_PreservesIndependentTextAndVisualAxes()
    {
        var evidence =
            new PageProcessingEvidence(
                physicalPageNumber:
                    79,
                textAuthority:
                    TextAuthority.NeedsVerification,
                visualElements:
                [
                    new VisualElementEvidence(
                        sourceVisualIndex:
                            0,
                        VisualEvidenceKind.CaptionedMeaningfulVisual)
                ]);

        Assert.Equal(
            79,
            evidence.PhysicalPageNumber);

        Assert.Equal(
            TextAuthority.NeedsVerification,
            evidence.TextAuthority);

        var visual =
            Assert.Single(
                evidence.VisualElements);

        Assert.Equal(
            0,
            visual.SourceVisualIndex);

        Assert.Equal(
            VisualEvidenceKind.CaptionedMeaningfulVisual,
            visual.Kind);
    }

    [Fact]
    public void PageEvidence_NoVisuals_UsesEmptyCollection()
    {
        var evidence =
            new PageProcessingEvidence(
                physicalPageNumber:
                    70,
                textAuthority:
                    TextAuthority.Trusted,
                visualElements:
                    []);

        Assert.Empty(
            evidence.VisualElements);
    }

    [Fact]
    public void PageEvidence_SnapshotsCallerOwnedVisualCollection()
    {
        var source =
            new List<VisualElementEvidence>
            {
                new(
                    sourceVisualIndex:
                        0,
                    VisualEvidenceKind.TinyOrNoise)
            };

        var evidence =
            new PageProcessingEvidence(
                physicalPageNumber:
                    3,
                textAuthority:
                    TextAuthority.Trusted,
                visualElements:
                    source);

        source.Add(
            new VisualElementEvidence(
                sourceVisualIndex:
                    1,
                VisualEvidenceKind.LargeIndependentVisual));

        Assert.Single(
            evidence.VisualElements);
    }

    [Fact]
    public void PageEvidence_RejectsInvalidPageNumber()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PageProcessingEvidence(
                    physicalPageNumber:
                        0,
                    textAuthority:
                        TextAuthority.Trusted,
                    visualElements:
                        []));
    }

    [Fact]
    public void PageEvidence_RejectsUndefinedTextAuthority()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PageProcessingEvidence(
                    physicalPageNumber:
                        1,
                    textAuthority:
                        (TextAuthority)999,
                    visualElements:
                        []));
    }

    [Fact]
    public void PageEvidence_RejectsNullVisualCollection()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new PageProcessingEvidence(
                    physicalPageNumber:
                        1,
                    textAuthority:
                        TextAuthority.Trusted,
                    visualElements:
                        null!));
    }

    [Fact]
    public void PageEvidence_RejectsDuplicateSourceVisualIndexes()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PageProcessingEvidence(
                    physicalPageNumber:
                        1,
                    textAuthority:
                        TextAuthority.Trusted,
                    visualElements:
                    [
                        new VisualElementEvidence(
                            sourceVisualIndex:
                                0,
                            VisualEvidenceKind.TinyOrNoise),
                        new VisualElementEvidence(
                            sourceVisualIndex:
                                0,
                            VisualEvidenceKind.LargeIndependentVisual)
                    ]));
    }

    [Fact]
    public void VisualElementEvidence_RejectsNegativeSourceIndex()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new VisualElementEvidence(
                    sourceVisualIndex:
                        -1,
                    VisualEvidenceKind.Unknown));
    }

    [Fact]
    public void VisualElementEvidence_RejectsUndefinedKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new VisualElementEvidence(
                    sourceVisualIndex:
                        0,
                    (VisualEvidenceKind)999));
    }

    [Fact]
    public void EvidenceContracts_DoNotContainRouteOrDisposition()
    {
        var pagePropertyTypes =
            typeof(PageProcessingEvidence)
                .GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance)
                .Select(
                    property =>
                        property.PropertyType)
                .ToArray();

        var visualPropertyTypes =
            typeof(VisualElementEvidence)
                .GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance)
                .Select(
                    property =>
                        property.PropertyType)
                .ToArray();

        Assert.DoesNotContain(
            typeof(PageProcessingRoute),
            pagePropertyTypes);

        Assert.DoesNotContain(
            typeof(VisualDisposition),
            pagePropertyTypes);

        Assert.DoesNotContain(
            typeof(PageProcessingRoute),
            visualPropertyTypes);

        Assert.DoesNotContain(
            typeof(VisualDisposition),
            visualPropertyTypes);
    }

    [Fact]
    public void VisualDisposition_RemainsSeparatePolicyVocabulary()
    {
        Assert.True(
            Enum.IsDefined(
                VisualDisposition.NoVisual));

        Assert.True(
            Enum.IsDefined(
                VisualDisposition.PresentationOnly));

        Assert.True(
            Enum.IsDefined(
                VisualDisposition.PreserveMeaningfulVisual));

        Assert.True(
            Enum.IsDefined(
                VisualDisposition.RequiresVisualAnalysis));
    }
}
