using System.Reflection;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentProcessorFactoryTests
{
    #region Methods Tests

    [Fact]
    public void CreateHybrid_ExposesOnlyCoreAndFrameworkInputs()
    {
        var method =
            typeof(DocumentProcessorFactory)
                .GetMethod(
                    nameof(
                        DocumentProcessorFactory.CreateHybrid),
                    BindingFlags.Public |
                    BindingFlags.Static);

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(DocumentProcessor),
            method.ReturnType);

        var expectedParameterTypes =
            new[]
            {
                typeof(DocumentFormatId),
                typeof(IDocumentExtractor),
                typeof(IDocumentPreflightAnalyzer),
                typeof(IDocumentRasterizer),
                typeof(IVisualRasterObservationSource),
                typeof(IPageLayoutAnalyzer),
                typeof(IRegionTextRecognizer),
                typeof(string),
                typeof(ProcessingComponentIdentity),
                typeof(ProcessingComponentIdentity)
            };

        var actualParameterTypes =
            method
                .GetParameters()
                .Select(parameter =>
                    parameter.ParameterType)
                .ToArray();

        Assert.Equal(
            expectedParameterTypes,
            actualParameterTypes);

        Assert.DoesNotContain(
            method.GetParameters(),
            parameter =>
                string.Equals(
                    parameter.ParameterType
                        .Assembly
                        .GetName()
                        .Name,
                    "DocumentProcessing.Engine",
                    StringComparison.Ordinal));
    }

    #endregion
}
