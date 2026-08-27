using DocumentProcessing.Manager.Persistence.Postgres;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class ManagerPersistenceDependencyTests
{
    #region Tests

    [Fact]
    public void PersistenceAssembly_ReferencesOnlyManagerProject()
    {
        var documentProcessingReferences =
            typeof(PostgresManagerSchema)
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

        Assert.Equal(
            ["DocumentProcessing.Manager"],
            documentProcessingReferences);
    }

    #endregion
}
