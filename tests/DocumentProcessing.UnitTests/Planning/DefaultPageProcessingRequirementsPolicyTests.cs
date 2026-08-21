using System.Reflection;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.UnitTests.Planning;

public sealed class DefaultPageProcessingRequirementsPolicyTests
{
    private readonly DefaultPageProcessingRequirementsPolicy _sut =
        new();

    [Theory]
    [MemberData(
        nameof(TextAndVisualPolicyMatrix))]
    public void Decide_MapsTextAndVisualAxesIndependently(
        string caseName,
        TextAuthority textAuthority,
        VisualEvidenceKind visualEvidence,
        TextProcessingRequirement expectedTextRequirement,
        VisualDisposition expectedVisualDisposition)
    {
        Assert.False(
            string.IsNullOrWhiteSpace(
                caseName));

        var requirements =
            _sut.Decide(
                Evidence(
                    textAuthority,
                    visualEvidence));

        Assert.Equal(
            expectedTextRequirement,
            requirements.TextRequirement);

        var visual =
            Assert.Single(
                requirements.VisualElements);

        Assert.Equal(
            expectedVisualDisposition,
            visual.Disposition);
    }

    [Fact]
    public void Decide_NeedsVerificationWithNoVisuals_FailsClosed()
    {
        var requirements =
            _sut.Decide(
                new PageProcessingEvidence(
                    physicalPageNumber:
                        1,
                    textAuthority:
                        TextAuthority.NeedsVerification,
                    visualElements:
                        []));

        Assert.Equal(
            TextProcessingRequirement.VerifyNativeText,
            requirements.TextRequirement);

        Assert.Empty(
            requirements.VisualElements);
    }

    [Fact]
    public void Decide_NeedsVerificationWithOnlyKnownVisuals_UsesNativeText()
    {
        var requirements =
            _sut.Decide(
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
                            VisualEvidenceKind.CaptionedMeaningfulVisual),
                        new VisualElementEvidence(
                            sourceVisualIndex:
                                1,
                            VisualEvidenceKind.SmallHeadingAssociatedVisual)
                    ]));

        Assert.Equal(
            TextProcessingRequirement.UseNativeText,
            requirements.TextRequirement);

        Assert.True(
            requirements.HasMeaningfulVisuals);

        Assert.False(
            requirements.RequiresVisualAnalysis);
    }

    [Fact]
    public void Decide_NeedsVerificationWithOneUnknownVisual_FailsClosed()
    {
        var requirements =
            _sut.Decide(
                new PageProcessingEvidence(
                    physicalPageNumber:
                        148,
                    textAuthority:
                        TextAuthority.NeedsVerification,
                    visualElements:
                    [
                        new VisualElementEvidence(
                            sourceVisualIndex:
                                0,
                            VisualEvidenceKind.NativeTextContainerOrFrame),
                        new VisualElementEvidence(
                            sourceVisualIndex:
                                1,
                            VisualEvidenceKind.Unknown)
                    ]));

        Assert.Equal(
            TextProcessingRequirement.VerifyNativeText,
            requirements.TextRequirement);

        Assert.True(
            requirements.RequiresVisualAnalysis);
    }

    [Fact]
    public void Decide_TrustedTextWithUnknownVisual_DoesNotInventTextVerification()
    {
        var requirements =
            _sut.Decide(
                Evidence(
                    TextAuthority.Trusted,
                    VisualEvidenceKind.Unknown));

        Assert.Equal(
            TextProcessingRequirement.UseNativeText,
            requirements.TextRequirement);

        Assert.True(
            requirements.RequiresVisualAnalysis);
    }

    [Fact]
    public void Decide_MissingTextCannotBeOverriddenByMeaningfulVisual()
    {
        var requirements =
            _sut.Decide(
                Evidence(
                    TextAuthority.Missing,
                    VisualEvidenceKind.CaptionedMeaningfulVisual));

        Assert.Equal(
            TextProcessingRequirement.RecoverMissingNativeText,
            requirements.TextRequirement);

        Assert.True(
            requirements.HasMeaningfulVisuals);
    }

    [Fact]
    public void Decide_CorruptedTextCannotBeOverriddenByPresentationVisual()
    {
        var requirements =
            _sut.Decide(
                Evidence(
                    TextAuthority.Corrupted,
                    VisualEvidenceKind.BlankCanvas));

        Assert.Equal(
            TextProcessingRequirement.ReconcileCorruptedNativeText,
            requirements.TextRequirement);

        Assert.False(
            requirements.RequiresVisualAnalysis);
    }

    [Fact]
    public void Requirements_ExposeCoherentDerivedTextFlags()
    {
        var requirements =
            new PageProcessingRequirements(
                physicalPageNumber:
                    1,
                textRequirement:
                    TextProcessingRequirement.VerifyNativeText,
                visualElements:
                []);

        Assert.False(
            requirements.UsesNativeTextWithoutVerification);

        Assert.False(
            requirements.RequiresTextRecovery);

        Assert.True(
            requirements.RequiresTextVerification);

        Assert.False(
            requirements.RequiresTextReconciliation);
    }

    [Fact]
    public void Requirements_SnapshotCallerOwnedVisualCollection()
    {
        var source =
            new List<VisualElementDisposition>
            {
                new(
                    sourceVisualIndex:
                        0,
                    VisualDisposition.PresentationOnly)
            };

        var requirements =
            new PageProcessingRequirements(
                physicalPageNumber:
                    1,
                textRequirement:
                    TextProcessingRequirement.UseNativeText,
                visualElements:
                    source);

        source.Add(
            new VisualElementDisposition(
                sourceVisualIndex:
                    1,
                VisualDisposition.PreserveMeaningfulVisual));

        Assert.Single(
            requirements.VisualElements);
    }

    [Fact]
    public void Requirements_RejectDuplicateVisualIndexes()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PageProcessingRequirements(
                    physicalPageNumber:
                        1,
                    textRequirement:
                        TextProcessingRequirement.UseNativeText,
                    visualElements:
                    [
                        new VisualElementDisposition(
                            sourceVisualIndex:
                                0,
                            VisualDisposition.PresentationOnly),
                        new VisualElementDisposition(
                            sourceVisualIndex:
                                0,
                            VisualDisposition.PreserveMeaningfulVisual)
                    ]));
    }

    [Fact]
    public void VisualElementDisposition_RejectsNoVisualForExistingElement()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new VisualElementDisposition(
                    sourceVisualIndex:
                        0,
                    VisualDisposition.NoVisual));
    }

    [Fact]
    public void Requirements_RejectUndefinedTextRequirement()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PageProcessingRequirements(
                    physicalPageNumber:
                        1,
                    textRequirement:
                        (TextProcessingRequirement)999,
                    visualElements:
                        []));
    }

    [Fact]
    public void PublicPolicy_HasSinglePureRequirementsDecisionBoundary()
    {
        var methods =
            typeof(IPageProcessingRequirementsPolicy)
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Instance);

        var method =
            Assert.Single(
                methods);

        Assert.Equal(
            nameof(IPageProcessingRequirementsPolicy.Decide),
            method.Name);

        Assert.Equal(
            typeof(PageProcessingRequirements),
            method.ReturnType);

        var parameter =
            Assert.Single(
                method.GetParameters());

        Assert.Equal(
            typeof(PageProcessingEvidence),
            parameter.ParameterType);
    }

    [Fact]
    public void TwoAxisContracts_DoNotContainLegacyExecutionRoute()
    {
        var requirementPropertyTypes =
            typeof(PageProcessingRequirements)
                .GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance)
                .Select(
                    property =>
                        property.PropertyType)
                .ToArray();

        var visualPropertyTypes =
            typeof(VisualElementDisposition)
                .GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance)
                .Select(
                    property =>
                        property.PropertyType)
                .ToArray();

        Assert.DoesNotContain(
            typeof(PageProcessingRoute),
            requirementPropertyTypes);

        Assert.DoesNotContain(
            typeof(PageProcessingPlan),
            requirementPropertyTypes);

        Assert.DoesNotContain(
            typeof(PageProcessingRoute),
            visualPropertyTypes);

        Assert.DoesNotContain(
            typeof(PageProcessingPlan),
            visualPropertyTypes);
    }

    [Fact]
    public void DefaultPolicy_ReturnTypeRemainsRequirementsNotLegacyPlan()
    {
        var method =
            typeof(DefaultPageProcessingRequirementsPolicy)
                .GetMethod(
                    nameof(DefaultPageProcessingRequirementsPolicy.Decide),
                    BindingFlags.Public |
                    BindingFlags.Instance);

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(PageProcessingRequirements),
            method!.ReturnType);
    }

    public static TheoryData<
        string,
        TextAuthority,
        VisualEvidenceKind,
        TextProcessingRequirement,
        VisualDisposition> TextAndVisualPolicyMatrix
    {
        get
        {
            var data =
                new TheoryData<
                    string,
                    TextAuthority,
                    VisualEvidenceKind,
                    TextProcessingRequirement,
                    VisualDisposition>();

            foreach (var textAuthority in
                     Enum.GetValues<TextAuthority>())
            {
                foreach (var visualEvidence in
                         Enum.GetValues<VisualEvidenceKind>())
                {
                    var expectedVisualDisposition =
                        ExpectedVisualDisposition(
                            visualEvidence);

                    var expectedTextRequirement =
                        ExpectedTextRequirement(
                            textAuthority,
                            expectedVisualDisposition);

                    data.Add(
                        $"{textAuthority}-{visualEvidence}",
                        textAuthority,
                        visualEvidence,
                        expectedTextRequirement,
                        expectedVisualDisposition);
                }
            }

            return data;
        }
    }

    private static PageProcessingEvidence Evidence(
        TextAuthority textAuthority,
        VisualEvidenceKind visualEvidence) =>
        new(
            physicalPageNumber:
                1,
            textAuthority:
                textAuthority,
            visualElements:
            [
                new VisualElementEvidence(
                    sourceVisualIndex:
                        0,
                    visualEvidence)
            ]);

    private static VisualDisposition ExpectedVisualDisposition(
        VisualEvidenceKind visualEvidence) =>
        visualEvidence switch
        {
            VisualEvidenceKind.Unknown =>
                VisualDisposition.RequiresVisualAnalysis,

            VisualEvidenceKind.BlankCanvas or
            VisualEvidenceKind.TinyOrNoise or
            VisualEvidenceKind.SmallHeadingAssociatedVisual or
            VisualEvidenceKind.HeadingBackplateOrPresentation or
            VisualEvidenceKind.NativeTextContainerOrFrame or
            VisualEvidenceKind.PublicationPresentationVisual =>
                VisualDisposition.PresentationOnly,

            VisualEvidenceKind.CaptionedMeaningfulVisual or
            VisualEvidenceKind.LargeIndependentVisual or
            VisualEvidenceKind.SourceBackedMeaningfulVisual or
            VisualEvidenceKind.StructuredContentMeaningfulVisual =>
                VisualDisposition.PreserveMeaningfulVisual,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(visualEvidence),
                    visualEvidence,
                    null)
        };

    private static TextProcessingRequirement ExpectedTextRequirement(
        TextAuthority textAuthority,
        VisualDisposition visualDisposition) =>
        textAuthority switch
        {
            TextAuthority.Missing =>
                TextProcessingRequirement.RecoverMissingNativeText,

            TextAuthority.Corrupted =>
                TextProcessingRequirement.ReconcileCorruptedNativeText,

            TextAuthority.Trusted =>
                TextProcessingRequirement.UseNativeText,

            TextAuthority.NeedsVerification =>
                visualDisposition ==
                VisualDisposition.RequiresVisualAnalysis
                    ? TextProcessingRequirement.VerifyNativeText
                    : TextProcessingRequirement.UseNativeText,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(textAuthority),
                    textAuthority,
                    null)
        };
}
