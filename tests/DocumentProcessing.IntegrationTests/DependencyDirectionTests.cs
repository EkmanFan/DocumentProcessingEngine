namespace DocumentProcessing.IntegrationTests;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void Core_DoesNotReferenceEngineOrPdf()
    {
        var references = typeof(DocumentProcessing.Core.AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("DocumentProcessing.Engine", references);
        Assert.DoesNotContain("DocumentProcessing.Pdf", references);
    }

    [Fact]
    public void Engine_DoesNotReferencePdf()
    {
        var references = typeof(DocumentProcessing.Engine.AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("DocumentProcessing.Pdf", references);
    }

    [Fact]
    public void Pdf_DoesNotReferenceEngine()
    {
        var references = typeof(DocumentProcessing.Pdf.AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("DocumentProcessing.Engine", references);
    }
}
