using System.Reflection;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Pdf;
using DocumentProcessing.Epub;
using DocumentProcessing.Layout.Adapters.PpStructureV3;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;
using DocumentProcessing.ProviderLifecycle;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class SharedProcessingCapabilityOwnershipTests
{
    #region Methods Tests

    [Fact]
    public void HostOptions_ExposeSharedProviderConfiguration()
    {
        var hostOptionsType =
            typeof(global::DocumentProcessing.DocumentProcessingHostOptions);

        Assert.Equal(
            typeof(PpStructureV3Options),
            hostOptionsType
                .GetProperty(
                    nameof(
                        global::DocumentProcessing.DocumentProcessingHostOptions
                            .PpStructureV3))!
                .PropertyType);

        Assert.Equal(
            typeof(PaddleOcrOptions),
            hostOptionsType
                .GetProperty(
                    nameof(
                        global::DocumentProcessing.DocumentProcessingHostOptions
                            .PaddleOcr))!
                .PropertyType);

        Assert.Equal(
            typeof(ProcessingProviderLifecycleOptions),
            hostOptionsType
                .GetProperty(
                    nameof(
                        global::DocumentProcessing.DocumentProcessingHostOptions
                            .ProviderLifecycle))!
                .PropertyType);
    }

    [Fact]
    public void HostOptions_DefaultToManagedDockerAndAllowExternalStrategy()
    {
        var managed =
            CreateHostOptions();

        Assert.Equal(
            ProcessingProviderLifecycleMode.ManagedDocker,
            managed.ProviderLifecycle.Mode);

        var external =
            CreateHostOptions(
                ProcessingProviderLifecycleOptions.External);

        Assert.Equal(
            ProcessingProviderLifecycleMode.External,
            external.ProviderLifecycle.Mode);
    }

    [Fact]
    public void HostOptions_ExposeUserVisualAssetWriter()
    {
        var property =
            typeof(global::DocumentProcessing.DocumentProcessingHostOptions)
                .GetProperty(
                    nameof(
                        global::DocumentProcessing.DocumentProcessingHostOptions
                            .UserVisualAssetWriter));

        Assert.NotNull(
            property);

        Assert.Equal(
            typeof(UserVisualAssetWriter),
            property.PropertyType);

        Assert.Equal(
            typeof(UserVisualAssetWriteRequest),
            typeof(UserVisualAssetWriter)
                .GetMethod(
                    "Invoke")!
                .GetParameters()[1]
                .ParameterType);
    }

    [Fact]
    public void PdfAssembly_DoesNotDeclareOptionsOrVisualDestinationDelegate()
    {
        var pdfAssembly =
            typeof(PdfDocumentFormat)
                .Assembly;

        const string obsoletePdfOptionsType =
            "DocumentProcessing.Pdf.PdfDocumentProcessing" +
            "Options";

        const string obsoletePdfVisualDestinationType =
            "DocumentProcessing.Pdf.PdfPreservedVisualDestination" +
            "Factory";

        Assert.Null(
            pdfAssembly.GetType(
                obsoletePdfOptionsType));

        Assert.Null(
            pdfAssembly.GetType(
                obsoletePdfVisualDestinationType));
    }

    [Fact]
    public void DocumentFormats_PairRasterAndVisualObservationCapabilities()
    {
        var productionAssemblies =
            new[]
            {
                typeof(IDocumentFormat).Assembly,
                typeof(DocumentProcessingEngine).Assembly,
                typeof(PdfDocumentFormat).Assembly,
                typeof(EpubDocumentFormat).Assembly,
                typeof(global::DocumentProcessing.DocumentProcessingHost).Assembly
            };

        var documentFormatTypes =
            productionAssemblies
                .Distinct()
                .SelectMany(
                    assembly =>
                        assembly.GetTypes())
                .Where(
                    type =>
                        !type.IsAbstract &&
                        !type.IsInterface &&
                        typeof(IDocumentFormat)
                            .IsAssignableFrom(
                                type))
                .ToArray();

        Assert.NotEmpty(
            documentFormatTypes);

        foreach (var documentFormatType in documentFormatTypes)
        {
            Assert.Equal(
                typeof(IDocumentRasterizer)
                    .IsAssignableFrom(
                        documentFormatType),
                typeof(IVisualRasterObservationSource)
                    .IsAssignableFrom(
                        documentFormatType));
        }
    }

    [Fact]
    public void SharedCapabilities_OwnTwoServiceHttpClients()
    {
        var assembly =
            typeof(global::DocumentProcessing.DocumentProcessingHost)
                .Assembly;

        var sharedType =
            assembly.GetType(
                "DocumentProcessing.Shared.SharedProcessingCapabilities",
                throwOnError:
                    true)!;

        Assert.True(
            typeof(IDisposable)
                .IsAssignableFrom(
                    sharedType));

        var httpClientFields =
            sharedType
                .GetFields(
                    BindingFlags.NonPublic |
                    BindingFlags.Instance)
                .Where(
                    field =>
                        field.FieldType ==
                        typeof(HttpClient))
                .ToArray();

        Assert.Equal(
            2,
            httpClientFields.Length);

        Assert.Contains(
            sharedType.GetFields(
                BindingFlags.NonPublic |
                BindingFlags.Instance),
            field =>
                field.FieldType ==
                typeof(IProcessingProviderRuntime));
    }

    private static global::DocumentProcessing.DocumentProcessingHostOptions CreateHostOptions(
        ProcessingProviderLifecycleOptions? lifecycle = null) =>
        new(
            "test-engine-v1",
            new PpStructureV3Options(
                new Uri(
                    "http://127.0.0.1:8080/layout-parsing")),
            new PaddleOcrOptions(
                new Uri(
                    "http://127.0.0.1:8081/ocr"),
                "test-ocr-profile"),
            providerLifecycle:
                lifecycle);

    #endregion
}
