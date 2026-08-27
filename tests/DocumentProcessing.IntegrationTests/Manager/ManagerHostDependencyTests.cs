using System.Xml.Linq;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class ManagerHostDependencyTests
{
    #region Tests

    [Fact]
    public void Host_IsCompositionRootWithoutDirectEngineOrFormatDependencies()
    {
        var projectPath =
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "DocumentProcessing.Manager.Host",
                "DocumentProcessing.Manager.Host.csproj");

        var documentProcessingReferences =
            XDocument
                .Load(
                    projectPath)
                .Descendants(
                    "ProjectReference")
                .Select(
                    reference =>
                        Path.GetFileNameWithoutExtension(
                            reference.Attribute(
                                    "Include")
                                ?.Value.Replace(
                                    '\\',
                                    '/')))
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
            "DocumentProcessing.Manager.Persistence",
            documentProcessingReferences);

        Assert.Contains(
            "DocumentProcessing.Manager.DPEngine",
            documentProcessingReferences);

        Assert.Contains(
            "DocumentProcessing",
            documentProcessingReferences);

        Assert.Contains(
            "DocumentProcessing.Layout.Adapters",
            documentProcessingReferences);

        Assert.Contains(
            "DocumentProcessing.Ocr.Adapters",
            documentProcessingReferences);

        Assert.DoesNotContain(
            "DocumentProcessing.Engine",
            documentProcessingReferences);

        Assert.DoesNotContain(
            "DocumentProcessing.Pdf",
            documentProcessingReferences);

        Assert.DoesNotContain(
            "DocumentProcessing.Epub",
            documentProcessingReferences);
    }

    #endregion

    #region Methods

    private static string FindRepositoryRoot()
    {
        var current =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "DocumentProcessingEngine.sln")))
            {
                return current.FullName;
            }

            current =
                current.Parent;
        }

        throw new InvalidOperationException(
            "DocumentProcessingEngine repository root could not be located.");
    }

    #endregion
}
