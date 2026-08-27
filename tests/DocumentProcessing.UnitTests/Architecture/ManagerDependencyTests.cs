using DocumentProcessing.Manager.Control;

namespace DocumentProcessing.UnitTests.Architecture;

public sealed class ManagerDependencyTests
{
    #region Tests

    [Fact]
    public void ManagerAssembly_DoesNotReferenceProcessingOrInfrastructureProjects()
    {
        var references =
            typeof(ManagerStateMachine)
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
                .ToArray();

        Assert.Empty(
            references);
    }

    [Fact]
    public void ExistingProcessingFacade_DoesNotReferenceManager()
    {
        var references =
            typeof(global::DocumentProcessing.DocumentProcessingHost)
                .Assembly
                .GetReferencedAssemblies()
                .Select(
                    reference =>
                        reference.Name)
                .ToArray();

        Assert.DoesNotContain(
            "DocumentProcessing.Manager",
            references);
    }

    #endregion
}
