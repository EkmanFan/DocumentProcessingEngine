using System.Reflection;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class SharedProcessingCapabilityOwnershipTests
{
    #region Methods Tests

    [Fact]
    public void HostOptions_ExposeSharedProviderConfigurationOutsidePdfOptions()
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

        var pdfPropertyNames =
            typeof(PdfDocumentProcessingOptions)
                .GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance)
                .Select(property =>
                    property.Name)
                .ToArray();

        Assert.DoesNotContain(
            "LayoutEndpoint",
            pdfPropertyNames);

        Assert.DoesNotContain(
            "LayoutRequestTimeout",
            pdfPropertyNames);

        Assert.DoesNotContain(
            "OcrEndpoint",
            pdfPropertyNames);

        Assert.DoesNotContain(
            "OcrProfileId",
            pdfPropertyNames);

        Assert.DoesNotContain(
            "OcrRequestTimeout",
            pdfPropertyNames);
    }

    [Fact]
    public void Resolver_DoesNotOwnSharedHttpClientLifecycle()
    {
        var assembly =
            typeof(global::DocumentProcessing.DocumentProcessingHost)
                .Assembly;

        var resolverType =
            assembly.GetType(
                "DocumentProcessing.Formats.DocumentFormatProcessorResolver",
                throwOnError:
                    true)!;

        Assert.False(
            typeof(IDisposable)
                .IsAssignableFrom(
                    resolverType));

        Assert.DoesNotContain(
            resolverType.GetFields(
                BindingFlags.NonPublic |
                BindingFlags.Instance),
            field =>
                field.FieldType ==
                typeof(HttpClient));
    }

    [Fact]
    public void SharedComposition_OwnsTwoServiceHttpClients()
    {
        var assembly =
            typeof(global::DocumentProcessing.DocumentProcessingHost)
                .Assembly;

        var sharedType =
            assembly.GetType(
                "DocumentProcessing.Composition.SharedProcessingCapabilities",
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
                .Where(field =>
                    field.FieldType ==
                    typeof(HttpClient))
                .ToArray();

        Assert.Equal(
            2,
            httpClientFields.Length);
    }

    #endregion
}
