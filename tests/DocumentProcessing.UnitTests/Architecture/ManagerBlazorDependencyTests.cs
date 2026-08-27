namespace DocumentProcessing.UnitTests.Architecture;

public sealed class ManagerBlazorDependencyTests
{
    #region Tests

    [Fact]
    public void BlazorAdapter_HasNoCompileTimeManagerImplementationDependency()
    {
        var documentProcessingReferences =
            typeof(global::DocumentProcessing.Manager.Blazor.Program)
                .Assembly
                .GetReferencedAssemblies()
                .Select(
                    reference =>
                        reference.Name)
                .Where(
                    name =>
                        name is not null &&
                        name.StartsWith(
                            "DocumentProcessing",
                            StringComparison.Ordinal))
                .Select(
                    name =>
                        name!)
                .ToArray();

        Assert.Empty(
            documentProcessingReferences);
    }

    [Fact]
    public void Workshop_IsExposedAsAReusableComponent()
    {
        var workshopType =
            typeof(global::DocumentProcessing.Manager.Blazor.Components.Workshop.ManagerWorkshop);

        Assert.True(
            workshopType.IsPublic);

        Assert.True(
            typeof(global::DocumentProcessing.Manager.Blazor.DependencyInjection.ManagerWorkshopServiceCollectionExtensions)
                .IsPublic);
    }

    #endregion
}
