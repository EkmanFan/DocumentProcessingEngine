using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;

namespace DocumentProcessing.UnitTests.Architecture;

public sealed class OcrAdapterDependencyTests
{
    [Fact]
    public void EngineAssembly_DoesNotReferenceOcrAdapters()
    {
        var references =
            typeof(TargetedOcrPlanner)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

        Assert.DoesNotContain(
            "DocumentProcessing.Ocr.Adapters",
            references);
    }

    [Fact]
    public void OcrAdaptersAssembly_DoesNotReferenceEngine()
    {
        var references =
            typeof(PaddleOcrRegionTextRecognizer)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

        Assert.DoesNotContain(
            "DocumentProcessing.Engine",
            references);
    }
}
