using System.Reflection;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.UnitTests.Planning;
public sealed class PageProcessingPolicyContractTests
{
    [Fact]
    public void Assessment_PreservesPageAndNativeStatus()
    {
        var assessment =
            new PageProcessingAssessment(
                physicalPageNumber:
                    7,
                NativeTextStatus.Suspicious);

        Assert.Equal(
            7,
            assessment.PhysicalPageNumber);

        Assert.Equal(
            NativeTextStatus.Suspicious,
            assessment.NativeTextStatus);
    }

    [Fact]
    public void Assessment_RejectsNonPositivePageNumber()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PageProcessingAssessment(
                    physicalPageNumber:
                        0,
                    NativeTextStatus.Healthy));
    }

    [Fact]
    public void Assessment_RejectsUndefinedNativeStatus()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PageProcessingAssessment(
                    physicalPageNumber:
                        1,
                    (NativeTextStatus)999));
    }

    [Theory]
    [InlineData(
        PageProcessingRoute.NativeOnly,
        true,
        false,
        false,
        false,
        false)]
    [InlineData(
        PageProcessingRoute.LayoutWithTargetedOcrRecovery,
        false,
        true,
        true,
        true,
        false)]
    [InlineData(
        PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
        false,
        true,
        true,
        true,
        true)]
    public void Plan_DerivesOneCoherentExecutionShapeFromAtomicRoute(
        PageProcessingRoute route,
        bool usesNativeTextOnly,
        bool requiresRasterization,
        bool requiresLayoutAnalysis,
        bool requiresTargetedOcr,
        bool requiresReconciliation)
    {
        var plan =
            new PageProcessingPlan(
                route);

        Assert.Equal(
            route,
            plan.Route);

        Assert.Equal(
            usesNativeTextOnly,
            plan.UsesNativeTextOnly);

        Assert.Equal(
            requiresRasterization,
            plan.RequiresRasterization);

        Assert.Equal(
            requiresLayoutAnalysis,
            plan.RequiresLayoutAnalysis);

        Assert.Equal(
            requiresTargetedOcr,
            plan.RequiresTargetedOcr);

        Assert.Equal(
            requiresReconciliation,
            plan.RequiresReconciliation);
    }

    [Fact]
    public void Plan_RejectsUndefinedRoute()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PageProcessingPlan(
                    (PageProcessingRoute)999));
    }

    [Fact]
    public void PolicyBoundary_AllowsDifferentPoliciesWithoutChangingConsumerContract()
    {
        var assessment =
            new PageProcessingAssessment(
                physicalPageNumber:
                    3,
                NativeTextStatus.Healthy);

        IPageProcessingPolicy conservative =
            new AlwaysReconcilePolicy();

        IPageProcessingPolicy nativePreferred =
            new AlwaysNativePolicy();

        var conservativePlan =
            Decide(
                conservative,
                assessment);

        var nativePlan =
            Decide(
                nativePreferred,
                assessment);

        Assert.Equal(
            PageProcessingRoute.LayoutWithTargetedOcrReconciliation,
            conservativePlan.Route);

        Assert.Equal(
            PageProcessingRoute.NativeOnly,
            nativePlan.Route);
    }

    [Fact]
    public void PublicPolicy_HasSinglePureDecisionBoundary()
    {
        var methods =
            typeof(IPageProcessingPolicy)
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Instance);

        var method =
            Assert.Single(
                methods);

        Assert.Equal(
            nameof(IPageProcessingPolicy.Decide),
            method.Name);

        Assert.Equal(
            typeof(PageProcessingPlan),
            method.ReturnType);

        var parameter =
            Assert.Single(
                method.GetParameters());

        Assert.Equal(
            typeof(PageProcessingAssessment),
            parameter.ParameterType);
    }

    private static PageProcessingPlan Decide(
        IPageProcessingPolicy policy,
        PageProcessingAssessment assessment) =>
        policy.Decide(
            assessment);

    private sealed class AlwaysNativePolicy
        : IPageProcessingPolicy
    {
        public PageProcessingPlan Decide(
            PageProcessingAssessment assessment)
        {
            ArgumentNullException.ThrowIfNull(
                assessment);

            return new PageProcessingPlan(
                PageProcessingRoute.NativeOnly);
        }
    }

    private sealed class AlwaysReconcilePolicy
        : IPageProcessingPolicy
    {
        public PageProcessingPlan Decide(
            PageProcessingAssessment assessment)
        {
            ArgumentNullException.ThrowIfNull(
                assessment);

            return new PageProcessingPlan(
                PageProcessingRoute.LayoutWithTargetedOcrReconciliation);
        }
    }
}
