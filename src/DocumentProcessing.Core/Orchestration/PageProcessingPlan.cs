namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Side-effect-free processing intent returned by an
/// <see cref="IPageProcessingPolicy"/>.
///
/// V1 deliberately stores one atomic route instead of a bag of independent
/// booleans. Derived convenience properties expose the execution requirements
/// implied by that route without allowing contradictory combinations such as
/// OCR without layout/rasterization.
/// </summary>
public sealed record PageProcessingPlan
{
    public PageProcessingPlan(
        PageProcessingRoute route)
    {
        if (!Enum.IsDefined(
                typeof(PageProcessingRoute),
                route))
        {
            throw new ArgumentOutOfRangeException(
                nameof(route),
                route,
                "Page processing route must be a defined value.");
        }

        Route =
            route;
    }

    public PageProcessingRoute Route { get; }

    public bool UsesNativeTextOnly =>
        Route ==
        PageProcessingRoute.NativeOnly;

    public bool RequiresRasterization =>
        Route !=
        PageProcessingRoute.NativeOnly;

    public bool RequiresLayoutAnalysis =>
        Route !=
        PageProcessingRoute.NativeOnly;

    public bool RequiresTargetedOcr =>
        Route !=
        PageProcessingRoute.NativeOnly;

    public bool RequiresReconciliation =>
        Route ==
        PageProcessingRoute.LayoutWithTargetedOcrReconciliation;
}
