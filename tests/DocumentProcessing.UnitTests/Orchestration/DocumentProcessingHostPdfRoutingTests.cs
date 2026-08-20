using System.Reflection;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Pdf;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentProcessingHostPdfRoutingTests
{
    #region Methods Tests

    [Fact]
    public void PublicConstructor_AcceptsConfigurationOnly()
    {
        var constructors =
            typeof(global::DocumentProcessing.DocumentProcessingHost)
                .GetConstructors(
                    BindingFlags.Public |
                    BindingFlags.Instance);

        var constructor =
            Assert.Single(
                constructors);

        var parameter =
            Assert.Single(
                constructor.GetParameters());

        Assert.Equal(
            typeof(global::DocumentProcessing.DocumentProcessingHostOptions),
            parameter.ParameterType);

        Assert.DoesNotContain(
            constructor.GetParameters(),
            candidate =>
                typeof(IEnumerable<IDocumentFormatProcessor>)
                    .IsAssignableFrom(
                        candidate.ParameterType));
    }

    [Fact]
    public async Task ProcessDocumentAsync_UnsupportedSourceReturnsFunctionalFailure()
    {
        using var host =
            CreateHost();

        await using var stream =
            new MemoryStream(
                [1, 2, 3, 4],
                writable:
                    false);

        var outcome =
            await host
                .ProcessDocumentAsync(
                    new DocumentSource(
                        stream,
                        "unknown.pdf",
                        "application/pdf"));

        Assert.False(
            outcome.IsSuccess);

        Assert.Null(
            outcome.Result);

        Assert.False(
            string.IsNullOrWhiteSpace(
                outcome.ErrorMessage));

        Assert.Contains(
            "not supported",
            outcome.ErrorMessage!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispose_PreventsFurtherHostProcessing()
    {
        var host =
            CreateHost();

        host.Dispose();

        await using var stream =
            new MemoryStream(
                [1, 2, 3],
                writable:
                    false);

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () =>
                host.ProcessDocumentAsync(
                    new DocumentSource(
                        stream)));
    }

    #endregion

    #region Methods Fixtures

    private static global::DocumentProcessing.DocumentProcessingHost CreateHost() =>
        new(
            new global::DocumentProcessing.DocumentProcessingHostOptions(
                "test-engine-v1",
                new PpStructureV3Options(
                    new Uri(
                        "http://127.0.0.1:1/layout-parsing")),
                new PaddleOcrOptions(
                    new Uri(
                        "http://127.0.0.1:1/ocr"),
                    "test-ocr-profile"),
                new PdfDocumentProcessingOptions()));

    #endregion
}
