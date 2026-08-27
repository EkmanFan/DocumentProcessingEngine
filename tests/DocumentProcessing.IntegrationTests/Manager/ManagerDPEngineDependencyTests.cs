using DocumentProcessing.Manager.DPEngine;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class ManagerDPEngineDependencyTests
{
    #region Tests

    [Fact]
    public void DPEngineAdapter_ReferencesManagerAndHostButNotPersistence()
    {
        var documentProcessingReferences =
            typeof(DocumentProcessingHostExecutor)
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

        Assert.Contains(
            "DocumentProcessing.Manager",
            documentProcessingReferences);

        Assert.Contains(
            "DocumentProcessing",
            documentProcessingReferences);

        Assert.DoesNotContain(
            "DocumentProcessing.Manager.Persistence",
            documentProcessingReferences);
    }

    #endregion
}
