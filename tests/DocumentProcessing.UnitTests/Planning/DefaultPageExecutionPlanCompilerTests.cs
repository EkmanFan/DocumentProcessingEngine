using System.Reflection;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.UnitTests.Planning;

public sealed class DefaultPageExecutionPlanCompilerTests
{
    private readonly DefaultPageExecutionPlanCompiler _sut =
        new();

    [Theory]
    [MemberData(
        nameof(TextAndVisualExecutionMatrix))]
    public void Compile_MapsRequirementsToIndependentExecutionModes(
        string caseName,
        TextProcessingRequirement textRequirement,
        VisualDisposition visualDisposition,
        TextExecutionMode expectedTextMode,
        VisualExecutionAction expectedVisualAction)
    {
        Assert.False(
            string.IsNullOrWhiteSpace(
                caseName));

        var plan =
            _sut.Compile(
                Requirements(
                    textRequirement,
                    visualDisposition));

        Assert.Equal(
            expectedTextMode,
            plan.TextMode);

        var visual =
            Assert.Single(
                plan.VisualElements);

        Assert.Equal(
            expectedVisualAction,
            visual.Action);
    }

    [Fact]
    public void Compile_UseNativeTextAndAnalyzeVisual_RequiresLayoutButNotOcr()
    {
        var plan =
            _sut.Compile(
                Requirements(
                    TextProcessingRequirement.UseNativeText,
                    VisualDisposition.RequiresVisualAnalysis));

        Assert.Equal(
            TextExecutionMode.NativeText,
            plan.TextMode);

        Assert.True(
            plan.RequiresRasterization);

        Assert.True(
            plan.RequiresLayoutAnalysis);

        Assert.False(
            plan.RequiresTargetedOcr);

        Assert.False(
            plan.RequiresNativeOcrReconciliation);

        Assert.True(
            plan.RequiresVisualAnalysis);
    }

    [Fact]
    public void Compile_UseNativeTextAndPreserveVisual_DoesNotInventLayoutOrOcr()
    {
        var plan =
            _sut.Compile(
                Requirements(
                    TextProcessingRequirement.UseNativeText,
                    VisualDisposition.PreserveMeaningfulVisual));

        Assert.Equal(
            TextExecutionMode.NativeText,
            plan.TextMode);

        Assert.False(
            plan.RequiresRasterization);

        Assert.False(
            plan.RequiresLayoutAnalysis);

        Assert.False(
            plan.RequiresTargetedOcr);

        Assert.False(
            plan.RequiresNativeOcrReconciliation);

        Assert.False(
            plan.RequiresVisualAnalysis);

        Assert.True(
            plan.RequiresMeaningfulVisualPreservation);
    }

    [Fact]
    public void Compile_UseNativeTextAndPresentationOnly_HasNoAdditionalSemanticWork()
    {
        var plan =
            _sut.Compile(
                Requirements(
                    TextProcessingRequirement.UseNativeText,
                    VisualDisposition.PresentationOnly));

        Assert.Equal(
            VisualExecutionAction.NoAdditionalSemanticProcessing,
            Assert.Single(
                plan.VisualElements).Action);

        Assert.False(
            plan.RequiresRasterization);

        Assert.False(
            plan.RequiresLayoutAnalysis);

        Assert.False(
            plan.RequiresTargetedOcr);

        Assert.False(
            plan.RequiresVisualAnalysis);

        Assert.False(
            plan.RequiresMeaningfulVisualPreservation);

        Assert.False(
            plan.HasAdditionalSemanticWork);
    }

    [Fact]
    public void Compile_Recovery_RequiresTargetedOcrWithoutNativeReconciliation()
    {
        var plan =
            _sut.Compile(
                Requirements(
                    TextProcessingRequirement.RecoverMissingNativeText,
                    VisualDisposition.PresentationOnly));

        Assert.Equal(
            TextExecutionMode.TargetedOcrRecovery,
            plan.TextMode);

        Assert.True(
            plan.RequiresRasterization);

        Assert.True(
            plan.RequiresLayoutAnalysis);

        Assert.True(
            plan.RequiresTargetedOcr);

        Assert.False(
            plan.RequiresNativeOcrReconciliation);
    }

    [Fact]
    public void Compile_Verification_RequiresSecondaryOcrAndReconciliation()
    {
        var plan =
            _sut.Compile(
                Requirements(
                    TextProcessingRequirement.VerifyNativeText,
                    VisualDisposition.PresentationOnly));

        Assert.Equal(
            TextExecutionMode.TargetedOcrVerification,
            plan.TextMode);

        Assert.True(
            plan.RequiresTargetedOcr);

        Assert.True(
            plan.RequiresNativeOcrReconciliation);
    }

    [Fact]
    public void Compile_CorruptionReconciliation_RequiresSecondaryOcrAndReconciliation()
    {
        var plan =
            _sut.Compile(
                Requirements(
                    TextProcessingRequirement.ReconcileCorruptedNativeText,
                    VisualDisposition.PresentationOnly));

        Assert.Equal(
            TextExecutionMode.TargetedOcrReconciliation,
            plan.TextMode);

        Assert.True(
            plan.RequiresTargetedOcr);

        Assert.True(
            plan.RequiresNativeOcrReconciliation);
    }

    [Fact]
    public void Compile_MultipleVisualActions_RemainIndependent()
    {
        var requirements =
            new PageProcessingRequirements(
                physicalPageNumber:
                    79,
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
                            1,
                        VisualDisposition.PreserveMeaningfulVisual),
                    new VisualElementDisposition(
                        sourceVisualIndex:
                            2,
                        VisualDisposition.RequiresVisualAnalysis)
                ]);

        var plan =
            _sut.Compile(
                requirements);

        Assert.Equal(
            TextExecutionMode.NativeText,
            plan.TextMode);

        Assert.Equal(
            3,
            plan.VisualElements.Count);

        Assert.Equal(
            VisualExecutionAction.NoAdditionalSemanticProcessing,
            plan.VisualElements[0].Action);

        Assert.Equal(
            VisualExecutionAction.PreserveMeaningfulVisual,
            plan.VisualElements[1].Action);

        Assert.Equal(
            VisualExecutionAction.AnalyzeVisual,
            plan.VisualElements[2].Action);

        Assert.True(
            plan.RequiresVisualAnalysis);

        Assert.True(
            plan.RequiresMeaningfulVisualPreservation);

        Assert.True(
            plan.RequiresRasterization);

        Assert.True(
            plan.RequiresLayoutAnalysis);

        Assert.False(
            plan.RequiresTargetedOcr);
    }

    [Fact]
    public void Compile_NoVisuals_UsesEmptyVisualExecutionCollection()
    {
        var plan =
            _sut.Compile(
                new PageProcessingRequirements(
                    physicalPageNumber:
                        70,
                    textRequirement:
                        TextProcessingRequirement.UseNativeText,
                    visualElements:
                        []));

        Assert.Empty(
            plan.VisualElements);

        Assert.False(
            plan.RequiresRasterization);

        Assert.False(
            plan.RequiresLayoutAnalysis);

        Assert.False(
            plan.RequiresTargetedOcr);

        Assert.False(
            plan.HasAdditionalSemanticWork);
    }

    [Fact]
    public void PageExecutionPlan_SnapshotsCallerOwnedVisualCollection()
    {
        var source =
            new List<VisualElementExecutionPlan>
            {
                new(
                    sourceVisualIndex:
                        0,
                    VisualExecutionAction.NoAdditionalSemanticProcessing)
            };

        var plan =
            new PageExecutionPlan(
                physicalPageNumber:
                    1,
                textMode:
                    TextExecutionMode.NativeText,
                visualElements:
                    source);

        source.Add(
            new VisualElementExecutionPlan(
                sourceVisualIndex:
                    1,
                VisualExecutionAction.PreserveMeaningfulVisual));

        Assert.Single(
            plan.VisualElements);
    }

    [Fact]
    public void PageExecutionPlan_RejectsDuplicateVisualIndexes()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PageExecutionPlan(
                    physicalPageNumber:
                        1,
                    textMode:
                        TextExecutionMode.NativeText,
                    visualElements:
                    [
                        new VisualElementExecutionPlan(
                            sourceVisualIndex:
                                0,
                            VisualExecutionAction.NoAdditionalSemanticProcessing),
                        new VisualElementExecutionPlan(
                            sourceVisualIndex:
                                0,
                            VisualExecutionAction.PreserveMeaningfulVisual)
                    ]));
    }

    [Fact]
    public void PageExecutionPlan_RejectsUndefinedTextMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PageExecutionPlan(
                    physicalPageNumber:
                        1,
                    textMode:
                        (TextExecutionMode)999,
                    visualElements:
                        []));
    }

    [Fact]
    public void VisualElementExecutionPlan_RejectsUndefinedAction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new VisualElementExecutionPlan(
                    sourceVisualIndex:
                        0,
                    action:
                        (VisualExecutionAction)999));
    }

    [Fact]
    public void IndependentExecutionContracts_DoNotContainLegacyRouteOrPlan()
    {
        var pagePropertyTypes =
            typeof(PageExecutionPlan)
                .GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance)
                .Select(
                    property =>
                        property.PropertyType)
                .ToArray();

        var visualPropertyTypes =
            typeof(VisualElementExecutionPlan)
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
            typeof(PageProcessingPlan),
            pagePropertyTypes);

        Assert.DoesNotContain(
            typeof(PageProcessingRoute),
            visualPropertyTypes);

        Assert.DoesNotContain(
            typeof(PageProcessingPlan),
            visualPropertyTypes);
    }

    [Fact]
    public void Compiler_ReturnsIndependentExecutionPlan_NotLegacyPlan()
    {
        var method =
            typeof(DefaultPageExecutionPlanCompiler)
                .GetMethod(
                    nameof(DefaultPageExecutionPlanCompiler.Compile),
                    BindingFlags.Public |
                    BindingFlags.Instance);

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(PageExecutionPlan),
            method!.ReturnType);

        var parameter =
            Assert.Single(
                method.GetParameters());

        Assert.Equal(
            typeof(PageProcessingRequirements),
            parameter.ParameterType);
    }

    public static TheoryData<
        string,
        TextProcessingRequirement,
        VisualDisposition,
        TextExecutionMode,
        VisualExecutionAction> TextAndVisualExecutionMatrix
    {
        get
        {
            var data =
                new TheoryData<
                    string,
                    TextProcessingRequirement,
                    VisualDisposition,
                    TextExecutionMode,
                    VisualExecutionAction>();

            var actualVisualDispositions =
                new[]
                {
                    VisualDisposition.PresentationOnly,
                    VisualDisposition.PreserveMeaningfulVisual,
                    VisualDisposition.PreserveUnqualifiedVisual,
                    VisualDisposition.RequiresVisualAnalysis
                };

            foreach (var textRequirement in
                     Enum.GetValues<TextProcessingRequirement>())
            {
                foreach (var visualDisposition in
                         actualVisualDispositions)
                {
                    data.Add(
                        $"{textRequirement}-{visualDisposition}",
                        textRequirement,
                        visualDisposition,
                        ExpectedTextMode(
                            textRequirement),
                        ExpectedVisualAction(
                            visualDisposition));
                }
            }

            return data;
        }
    }

    private static PageProcessingRequirements Requirements(
        TextProcessingRequirement textRequirement,
        VisualDisposition visualDisposition) =>
        new(
            physicalPageNumber:
                1,
            textRequirement:
                textRequirement,
            visualElements:
            [
                new VisualElementDisposition(
                    sourceVisualIndex:
                        0,
                    visualDisposition)
            ]);

    private static TextExecutionMode ExpectedTextMode(
        TextProcessingRequirement requirement) =>
        requirement switch
        {
            TextProcessingRequirement.UseNativeText =>
                TextExecutionMode.NativeText,

            TextProcessingRequirement.RecoverMissingNativeText =>
                TextExecutionMode.TargetedOcrRecovery,

            TextProcessingRequirement.VerifyNativeText =>
                TextExecutionMode.TargetedOcrVerification,

            TextProcessingRequirement.ReconcileCorruptedNativeText =>
                TextExecutionMode.TargetedOcrReconciliation,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(requirement),
                    requirement,
                    null)
        };

    private static VisualExecutionAction ExpectedVisualAction(
        VisualDisposition disposition) =>
        disposition switch
        {
            VisualDisposition.PresentationOnly =>
                VisualExecutionAction.NoAdditionalSemanticProcessing,

            VisualDisposition.PreserveMeaningfulVisual =>
                VisualExecutionAction.PreserveMeaningfulVisual,

            VisualDisposition.PreserveUnqualifiedVisual =>
                VisualExecutionAction.PreserveUnqualifiedVisual,

            VisualDisposition.RequiresVisualAnalysis =>
                VisualExecutionAction.AnalyzeVisual,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(disposition),
                    disposition,
                    null)
        };
}
