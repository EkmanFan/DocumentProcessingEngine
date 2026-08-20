using System.Reflection;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.UnitTests.Formats.Pdf;

public sealed class PdfDocumentFormatProcessorTests
{
    #region Methods Tests

    [Fact]
    public void AssemblyOwnership_IsPdfAndConstructorUsesPdfExecutionSeam()
    {
        var processorType =
            typeof(PdfDocumentFormatProcessor);

        Assert.Same(
            typeof(DocumentProcessing.Pdf.AssemblyMarker).Assembly,
            processorType.Assembly);

        var constructor =
            Assert.Single(
                processorType.GetConstructors(
                    BindingFlags.Public |
                    BindingFlags.Instance));

        var parameters =
            constructor.GetParameters();

        Assert.Equal(
            2,
            parameters.Length);

        Assert.Equal(
            typeof(PdfDocumentExecution),
            parameters[0].ParameterType);

        Assert.Equal(
            typeof(PdfPreservedVisualDestinationFactory),
            Nullable.GetUnderlyingType(
                parameters[1].ParameterType) ??
            parameters[1].ParameterType);

        Assert.DoesNotContain(
            parameters,
            parameter =>
                string.Equals(
                    parameter.ParameterType.Assembly.GetName().Name,
                    "DocumentProcessing.Engine",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_UsesPdfSignatureValidation()
    {
        var processor =
            new PdfDocumentFormatProcessor(
                static (_, _, _) =>
                    throw new InvalidOperationException(
                        "Execution must not run during validation."));

        await using var stream =
            new MemoryStream(
                "%PDF-1.7\nfixture"u8.ToArray(),
                writable:
                    false);

        var isValid =
            await processor.ValidateAsync(
                new DocumentSource(
                    stream,
                    "fixture.bin",
                    "application/octet-stream"));

        Assert.True(
            isValid);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ForwardsPdfExecutionAndVisualDestination()
    {
        var executeCalled =
            false;

        PdfPreservedVisualDestinationFactory destinationFactory =
            static (_, _, _) =>
                ValueTask.FromResult<Stream>(
                    new MemoryStream());

        Task<DocumentIngestionResult> ExecuteAsync(
            DocumentSource source,
            PdfPreservedVisualDestinationFactory?
                receivedDestinationFactory,
            CancellationToken cancellationToken)
        {
            executeCalled =
                true;

            Assert.NotNull(
                source);

            Assert.Same(
                destinationFactory,
                receivedDestinationFactory);

            cancellationToken.ThrowIfCancellationRequested();

            throw new ExpectedExecutionException();
        }

        var processor =
            new PdfDocumentFormatProcessor(
                ExecuteAsync,
                destinationFactory);

        await using var stream =
            new MemoryStream(
                "%PDF-1.7\nfixture"u8.ToArray(),
                writable:
                    false);

        await Assert.ThrowsAsync<ExpectedExecutionException>(
            () =>
                processor.ProcessDocumentAsync(
                    new DocumentSource(
                        stream)));

        Assert.True(
            executeCalled);
    }

    #endregion

    #region Test Types

    private sealed class ExpectedExecutionException
        : Exception;

    #endregion
}
