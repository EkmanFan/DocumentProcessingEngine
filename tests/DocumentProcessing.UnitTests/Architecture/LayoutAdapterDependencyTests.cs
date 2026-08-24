using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Layout.Adapters.PpStructureV3;

namespace DocumentProcessing.UnitTests.Architecture;

public sealed class LayoutAdapterDependencyTests
{
    [Fact]
    public void EngineAssembly_DoesNotReferenceLayoutAdapters()
    {
        var references =
            typeof(LayoutTextPolicy)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

        Assert.DoesNotContain(
            "DocumentProcessing.Layout.Adapters",
            references);
    }

    [Fact]
    public void LayoutAdaptersAssembly_DoesNotReferenceEngine()
    {
        var references =
            typeof(PpStructureV3LayoutAdapter)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

        Assert.DoesNotContain(
            "DocumentProcessing.Engine",
            references);
    }
}
