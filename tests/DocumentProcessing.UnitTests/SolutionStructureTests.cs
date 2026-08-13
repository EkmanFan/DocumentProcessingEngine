namespace DocumentProcessing.UnitTests;

public sealed class SolutionStructureTests
{
    [Fact]
    public void ExpectedAssemblies_AreLoadable()
    {
        Assert.Equal(
            "DocumentProcessing.Core",
            typeof(DocumentProcessing.Core.AssemblyMarker).Assembly.GetName().Name);
        Assert.Equal(
            "DocumentProcessing.Engine",
            typeof(DocumentProcessing.Engine.AssemblyMarker).Assembly.GetName().Name);
        Assert.Equal(
            "DocumentProcessing.Pdf",
            typeof(DocumentProcessing.Pdf.AssemblyMarker).Assembly.GetName().Name);
    }
}
