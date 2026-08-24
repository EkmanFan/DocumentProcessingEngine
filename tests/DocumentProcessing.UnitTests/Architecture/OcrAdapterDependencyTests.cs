using DocumentProcessing.Core.Ocr;
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
            typeof(PaddleOcrAdapter)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

        Assert.DoesNotContain(
            "DocumentProcessing.Engine",
            references);
    }

    [Fact]
    public void OcrPort_IsImplementedByAdapter_NotServingClient()
    {
        Assert.True(
            typeof(IRegionTextRecognizer)
                .IsAssignableFrom(
                    typeof(PaddleOcrAdapter)));

        Assert.False(
            typeof(IRegionTextRecognizer)
                .IsAssignableFrom(
                    typeof(PaddleOcrServingClient)));
    }
}
