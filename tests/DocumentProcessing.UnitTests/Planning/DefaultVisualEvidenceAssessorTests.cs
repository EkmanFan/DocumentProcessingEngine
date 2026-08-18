using System.Reflection;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DefaultVisualEvidenceAssessorTests
{
    private readonly DefaultVisualEvidenceAssessor _sut =
        new();

    [Theory]
    [MemberData(
        nameof(FrozenDevelopmentVectors))]
    public void Assess_ReproducesFrozenDevelopmentEvidence(
        string caseName,
        VisualEvidenceObservation observation,
        VisualEvidenceKind expected)
    {
        Assert.False(
            string.IsNullOrWhiteSpace(
                caseName));

        var actual =
            _sut.Assess(
                observation);

        Assert.Equal(
            observation.SourceVisualIndex,
            actual.SourceVisualIndex);

        Assert.Equal(
            expected,
            actual.Kind);
    }

    [Theory]
    [MemberData(
        nameof(FrozenBlindHoldoutVectors))]
    public void Assess_ReproducesFrozenBlindHoldoutEvidence(
        string caseName,
        VisualEvidenceObservation observation,
        VisualEvidenceKind expected)
    {
        Assert.False(
            string.IsNullOrWhiteSpace(
                caseName));

        var actual =
            _sut.Assess(
                observation);

        Assert.Equal(
            expected,
            actual.Kind);
    }

    [Fact]
    public void Assess_StrongCaptionOverridesTextRichContainer()
    {
        var observation =
            Measured(
                foregroundPixelRatio:
                    0.032,
                pixelInteraction:
                    VisualPixelInteractionKind.ForegroundWordInteraction,
                nativeWordsTouchedRatio:
                    0.023,
                significantComponentCount:
                    1,
                effectiveVisualAreaRatio:
                    0.043,
                headingAssociation:
                    HeadingAssociationEvidenceKind.NoStrongAssociation,
                textContainment:
                    NativeTextContainmentEvidenceKind.TextRichContainer,
                captionAssociation:
                    CaptionAssociationEvidenceKind.StrongAssociation);

        Assert.Equal(
            VisualEvidenceKind.CaptionedMeaningfulVisual,
            _sut.Assess(
                observation).Kind);
    }

    [Fact]
    public void Assess_StrongSmallHeadingAssociationOverridesTinyNoise()
    {
        var observation =
            Measured(
                foregroundPixelRatio:
                    0.0015,
                pixelInteraction:
                    VisualPixelInteractionKind.NoForegroundWordIntersection,
                nativeWordsTouchedRatio:
                    0,
                significantComponentCount:
                    1,
                effectiveVisualAreaRatio:
                    0.0012,
                headingAssociation:
                    HeadingAssociationEvidenceKind.StrongAdjacentVisual,
                textContainment:
                    NativeTextContainmentEvidenceKind.NoContainedNativeText,
                captionAssociation:
                    CaptionAssociationEvidenceKind.NoStrongAssociation);

        Assert.Equal(
            VisualEvidenceKind.SmallHeadingAssociatedVisual,
            _sut.Assess(
                observation).Kind);
    }

    [Fact]
    public void Assess_UnknownObservationFailsClosed()
    {
        var observation =
            new VisualEvidenceObservation(
                sourceVisualIndex:
                    0,
                foregroundState:
                    VisualForegroundState.Unavailable,
                foregroundPixelRatio:
                    null,
                pixelInteraction:
                    VisualPixelInteractionKind.NotMeasured,
                nativeWordsTouchedRatio:
                    0,
                significantComponentCount:
                    null,
                effectiveVisualAreaRatio:
                    null,
                headingAssociation:
                    HeadingAssociationEvidenceKind.NotMeasured,
                textContainment:
                    NativeTextContainmentEvidenceKind.NotMeasured,
                captionAssociation:
                    CaptionAssociationEvidenceKind.NotMeasured);

        Assert.Equal(
            VisualEvidenceKind.Unknown,
            _sut.Assess(
                observation).Kind);
    }

    [Fact]
    public void Assess_DoesNotPromoteLargeVisualWithWordIntersection()
    {
        var observation =
            Measured(
                foregroundPixelRatio:
                    0.25,
                pixelInteraction:
                    VisualPixelInteractionKind.ForegroundWordInteraction,
                nativeWordsTouchedRatio:
                    0.10,
                significantComponentCount:
                    8,
                effectiveVisualAreaRatio:
                    0.30,
                headingAssociation:
                    HeadingAssociationEvidenceKind.NoStrongAssociation,
                textContainment:
                    NativeTextContainmentEvidenceKind.NoContainedNativeText,
                captionAssociation:
                    CaptionAssociationEvidenceKind.NoStrongAssociation);

        Assert.Equal(
            VisualEvidenceKind.Unknown,
            _sut.Assess(
                observation).Kind);
    }

    [Fact]
    public void Assess_ReturnsEvidenceOnly()
    {
        var method =
            typeof(DefaultVisualEvidenceAssessor)
                .GetMethod(
                    nameof(DefaultVisualEvidenceAssessor.Assess),
                    BindingFlags.Public |
                    BindingFlags.Instance);

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(VisualElementEvidence),
            method!.ReturnType);

        Assert.DoesNotContain(
            typeof(VisualDisposition),
            method.GetParameters()
                .Select(
                    parameter =>
                        parameter.ParameterType));

        Assert.DoesNotContain(
            typeof(PageProcessingRoute),
            method.GetParameters()
                .Select(
                    parameter =>
                        parameter.ParameterType));
    }

    [Fact]
    public void Observation_RejectsUndefinedEnums()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        (VisualForegroundState)999,
                    foregroundPixelRatio:
                        null,
                    pixelInteraction:
                        VisualPixelInteractionKind.NotMeasured,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        null,
                    effectiveVisualAreaRatio:
                        null,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NotMeasured,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NotMeasured,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NotMeasured));
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(1.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Observation_RejectsInvalidRatios(
        double ratio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        ratio,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        1,
                    effectiveVisualAreaRatio:
                        0.1,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NoStrongAssociation,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation));
    }

    [Fact]
    public void Observation_RejectsInconsistentUnavailableForeground()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Unavailable,
                    foregroundPixelRatio:
                        0.1,
                    pixelInteraction:
                        VisualPixelInteractionKind.NotMeasured,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        null,
                    effectiveVisualAreaRatio:
                        null,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NotMeasured,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NotMeasured,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NotMeasured));
    }

    [Fact]
    public void Observation_RejectsInconsistentBlankCanvasRatio()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.BlankCanvas,
                    foregroundPixelRatio:
                        0.001,
                    pixelInteraction:
                        VisualPixelInteractionKind.BlankCanvas,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        0,
                    effectiveVisualAreaRatio:
                        null,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NotMeasured,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NotMeasured,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NotMeasured));
    }

    public static TheoryData<
        string,
        VisualEvidenceObservation,
        VisualEvidenceKind> FrozenDevelopmentVectors =>
        new()
        {
            {
                "ehrman-p2-heading-backplate",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.025431835491241433,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        1,
                    significantComponentCount:
                        3,
                    effectiveVisualAreaRatio:
                        0.019571389442148458,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.HeadingDominatedContainedText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoAssociation),
                VisualEvidenceKind.HeadingBackplateOrPresentation
            },
            {
                "ehrman-p79-captioned-figure",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.031990860624523991,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.023323615160349854,
                    significantComponentCount:
                        1,
                    effectiveVisualAreaRatio:
                        0.042573676846221542,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NoStrongAssociation,
                    textContainment:
                        NativeTextContainmentEvidenceKind.TextRichContainer,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.StrongAssociation),
                VisualEvidenceKind.CaptionedMeaningfulVisual
            },
            {
                "ehrman-p185-text-container",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.18669362428128206,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.38461538461538464,
                    significantComponentCount:
                        6,
                    effectiveVisualAreaRatio:
                        0.37278031190380279,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.TextRichContainer,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.PossibleAssociation),
                VisualEvidenceKind.NativeTextContainerOrFrame
            },
            {
                "ehrman-p551-text-container",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.18745820433436533,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.34170854271356782,
                    significantComponentCount:
                        6,
                    effectiveVisualAreaRatio:
                        0.34180636098573752,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.TextRichContainer,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.NativeTextContainerOrFrame
            },
            {
                "ehrman-p331-heading-ornament",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0015377687649143659,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        1,
                    effectiveVisualAreaRatio:
                        0.0011289980940641195,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.StrongAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.SmallHeadingAssociatedVisual
            },
            {
                "ehrman-p543-blank-canvas",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.BlankCanvas,
                    foregroundPixelRatio:
                        0,
                    pixelInteraction:
                        VisualPixelInteractionKind.BlankCanvas,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        0,
                    effectiveVisualAreaRatio:
                        null,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NotMeasured,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NotMeasured,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NotMeasured),
                VisualEvidenceKind.BlankCanvas
            },
            {
                "ehrman-p114-caption-generalization",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0060262894243584848,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.0043988269794721412,
                    significantComponentCount:
                        2,
                    effectiveVisualAreaRatio:
                        0.31617618850917889,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.TextRichContainer,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.StrongAssociation),
                VisualEvidenceKind.CaptionedMeaningfulVisual
            },
            {
                "ehrman-p148-conservative-unknown",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0016423501696993983,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.0025348542458808617,
                    significantComponentCount:
                        3,
                    effectiveVisualAreaRatio:
                        0.023851767526771289,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.Unknown
            },
            {
                "ehrman-p233-missing-text-visual-unknown",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.33555519075244772,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoNativeWords,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        752,
                    effectiveVisualAreaRatio:
                        0.52566829790306802,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NoStrongAssociation,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoAssociation),
                VisualEvidenceKind.Unknown
            },
        };

    public static TheoryData<
        string,
        VisualEvidenceObservation,
        VisualEvidenceKind> FrozenBlindHoldoutVectors =>
        new()
        {
            {
                "H01-habermas-p74",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.4153601222214659,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        14,
                    effectiveVisualAreaRatio:
                        0.30353065729644541,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.LargeIndependentVisual
            }, // blind human disposition: PreserveMeaningfulVisual; diagram
            {
                "H02-habermas-p54",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.45874509086581478,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        26,
                    effectiveVisualAreaRatio:
                        0.30696606236812807,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.LargeIndependentVisual
            }, // blind human disposition: PreserveMeaningfulVisual; diagram
            {
                "H03-ehrman-p277",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0015118121709987067,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        1,
                    effectiveVisualAreaRatio:
                        0.001260805913034484,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.StrongAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.PossibleAssociation),
                VisualEvidenceKind.SmallHeadingAssociatedVisual
            }, // blind human disposition: PresentationOnly; icones
            {
                "H04-habermas-p65",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.37308711516158566,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        42,
                    effectiveVisualAreaRatio:
                        0.28734299197844004,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NoStrongAssociation,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.LargeIndependentVisual
            }, // blind human disposition: PreserveMeaningfulVisual; diagram
            {
                "H05-ehrman-p130",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0039380804953560375,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.0039215686274509803,
                    significantComponentCount:
                        5,
                    effectiveVisualAreaRatio:
                        0.10459923285193326,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.TextRichContainer,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.NativeTextContainerOrFrame
            }, // blind human disposition: PresentationOnly; icones
            {
                "H06-ehrman-p93",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0016191950464396285,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.0013280212483399733,
                    significantComponentCount:
                        1,
                    effectiveVisualAreaRatio:
                        0.0011883463140574674,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.TinyOrNoise
            }, // blind human disposition: PresentationOnly; icones
            {
                "H07-ehrman-p165",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0015610201544361045,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        1,
                    effectiveVisualAreaRatio:
                        0.0011355448788035675,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.StrongAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.SmallHeadingAssociatedVisual
            }, // blind human disposition: PresentationOnly; icones
            {
                "H08-ehrman-p372",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0036008504640240688,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.015645371577574969,
                    significantComponentCount:
                        4,
                    effectiveVisualAreaRatio:
                        0.36654029393542548,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.TextRichContainer,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoAssociation),
                VisualEvidenceKind.NativeTextContainerOrFrame
            }, // blind human disposition: PresentationOnly; icones
            {
                "H09-ehrman-p77",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0015506473724295506,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.0027434842249657062,
                    significantComponentCount:
                        1,
                    effectiveVisualAreaRatio:
                        0.0011728173448478472,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.TinyOrNoise
            }, // blind human disposition: PresentationOnly; icones
            {
                "H10-ehrman-p285",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        1.8510120408333258e-05,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.0011641443538998836,
                    significantComponentCount:
                        0,
                    effectiveVisualAreaRatio:
                        1.2342879117429938e-05,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NoStrongAssociation,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoAssociation),
                VisualEvidenceKind.TinyOrNoise
            }, // blind human disposition: PresentationOnly; icones
            {
                "H11-ehrman-p206",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0043596446015266429,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        5,
                    effectiveVisualAreaRatio:
                        0.12660743722135578,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.TextRichContainer,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.NativeTextContainerOrFrame
            }, // blind human disposition: PresentationOnly; icones
            {
                "H12-ehrman-p513",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0015116598333472159,
                    pixelInteraction:
                        VisualPixelInteractionKind.LowForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.0013404825737265416,
                    significantComponentCount:
                        1,
                    effectiveVisualAreaRatio:
                        0.0013371452377215903,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.StrongAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.SmallHeadingAssociatedVisual
            }, // blind human disposition: PresentationOnly; icones
            {
                "H13-ehrman-p112",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.BlankCanvas,
                    foregroundPixelRatio:
                        0,
                    pixelInteraction:
                        VisualPixelInteractionKind.BlankCanvas,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        0,
                    effectiveVisualAreaRatio:
                        null,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NotMeasured,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NotMeasured,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NotMeasured),
                VisualEvidenceKind.BlankCanvas
            }, // blind human disposition: PresentationOnly; icones
            {
                "H14-ehrman-p429",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        1.8379706353558158e-05,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.0011961722488038277,
                    significantComponentCount:
                        0,
                    effectiveVisualAreaRatio:
                        1.2271718413740769e-05,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NoStrongAssociation,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoAssociation),
                VisualEvidenceKind.TinyOrNoise
            }, // blind human disposition: PresentationOnly; icones
            {
                "H15-ehrman-p478",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.BlankCanvas,
                    foregroundPixelRatio:
                        0,
                    pixelInteraction:
                        VisualPixelInteractionKind.BlankCanvas,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        0,
                    effectiveVisualAreaRatio:
                        null,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NotMeasured,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NotMeasured,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NotMeasured),
                VisualEvidenceKind.BlankCanvas
            }, // blind human disposition: PresentationOnly; icones
            {
                "H16-ehrman-p367",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0040802948104899112,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        5,
                    effectiveVisualAreaRatio:
                        0.11699038221099238,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.TextRichContainer,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.PossibleAssociation),
                VisualEvidenceKind.NativeTextContainerOrFrame
            }, // blind human disposition: PresentationOnly; icones
            {
                "H17-ehrman-p287",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0038716912090182077,
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    nativeWordsTouchedRatio:
                        0.0034188034188034188,
                    significantComponentCount:
                        5,
                    effectiveVisualAreaRatio:
                        0.1225914397145292,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.PossibleAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.TextRichContainer,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.NativeTextContainerOrFrame
            }, // blind human disposition: PresentationOnly; icones
            {
                "H18-ehrman-p170",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.BlankCanvas,
                    foregroundPixelRatio:
                        0,
                    pixelInteraction:
                        VisualPixelInteractionKind.BlankCanvas,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        0,
                    effectiveVisualAreaRatio:
                        null,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NotMeasured,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NotMeasured,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NotMeasured),
                VisualEvidenceKind.BlankCanvas
            }, // blind human disposition: PresentationOnly; icones
            {
                "H19-habermas-p18",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.14870643337261091,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        16,
                    effectiveVisualAreaRatio:
                        0.060754715450855257,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.NoStrongAssociation,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.LargeIndependentVisual
            }, // blind human disposition: PreserveMeaningfulVisual; diagram
            {
                "H20-ehrman-p509",
                new VisualEvidenceObservation(
                    sourceVisualIndex:
                        0,
                    foregroundState:
                        VisualForegroundState.Measured,
                    foregroundPixelRatio:
                        0.0015949553751847157,
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        1,
                    effectiveVisualAreaRatio:
                        0.0011849163952732864,
                    headingAssociation:
                        HeadingAssociationEvidenceKind.StrongAdjacentVisual,
                    textContainment:
                        NativeTextContainmentEvidenceKind.NoContainedNativeText,
                    captionAssociation:
                        CaptionAssociationEvidenceKind.NoStrongAssociation),
                VisualEvidenceKind.SmallHeadingAssociatedVisual
            }, // blind human disposition: PresentationOnly; icones
        };

    private static VisualEvidenceObservation Measured(
        double foregroundPixelRatio,
        VisualPixelInteractionKind pixelInteraction,
        double nativeWordsTouchedRatio,
        int? significantComponentCount,
        double? effectiveVisualAreaRatio,
        HeadingAssociationEvidenceKind headingAssociation,
        NativeTextContainmentEvidenceKind textContainment,
        CaptionAssociationEvidenceKind captionAssociation) =>
        new(
            sourceVisualIndex:
                0,
            foregroundState:
                VisualForegroundState.Measured,
            foregroundPixelRatio:
                foregroundPixelRatio,
            pixelInteraction:
                pixelInteraction,
            nativeWordsTouchedRatio:
                nativeWordsTouchedRatio,
            significantComponentCount:
                significantComponentCount,
            effectiveVisualAreaRatio:
                effectiveVisualAreaRatio,
            headingAssociation:
                headingAssociation,
            textContainment:
                textContainment,
            captionAssociation:
                captionAssociation);
}
