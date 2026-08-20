using System.Reflection;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Pdf;

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
    }

    [Fact]
    public void HostOptions_ExposePortableLayoutVisualDestination()
    {
        var property =
            typeof(global::DocumentProcessing.DocumentProcessingHostOptions)
                .GetProperty(
                    nameof(
                        global::DocumentProcessing.DocumentProcessingHostOptions
                            .OpenPreservedLayoutVisualDestinationAsync));

        Assert.NotNull(
            property);

        Assert.Equal(
            typeof(PreservedLayoutVisualDestinationFactory),
            property.PropertyType);
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
    }

    #endregion
}
