namespace DocumentProcessing.Epub.Validation;

internal interface IEpubCheckProcessRunner
{
    Task<EpubCheckProcessResult> RunAsync(
        EpubCheckProcessRequest request,
        CancellationToken cancellationToken = default);
}
