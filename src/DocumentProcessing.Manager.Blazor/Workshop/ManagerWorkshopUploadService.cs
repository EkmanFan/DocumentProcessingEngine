using DocumentProcessing.Manager.Blazor.Configuration;
using DocumentProcessing.Manager.Blazor.Components.Workshop;
using DocumentProcessing.Manager.Blazor.ManagerApi;
using Microsoft.AspNetCore.Components.Forms;

namespace DocumentProcessing.Manager.Blazor.Workshop;

internal sealed class ManagerWorkshopUploadService
{
    #region Variables and Constants

    private const string
        EpubMediaType =
            "application/epub+zip";

    private const string
        PdfMediaType =
            "application/pdf";

    private const string
        SourceOrigin =
            "manager-blazor";

    private readonly IManagerSubmissionClient
        _submissionClient;

    private readonly ManagerApiOptions
        _options;

    #endregion

    #region ctor

    public ManagerWorkshopUploadService(
        IManagerSubmissionClient submissionClient,
        ManagerApiOptions options)
    {
        _submissionClient =
            submissionClient ??
            throw new ArgumentNullException(
                nameof(submissionClient));

        _options =
            options ??
            throw new ArgumentNullException(
                nameof(options));
    }

    #endregion

    #region Methods

    public async ValueTask<ManagerDocumentSubmissionResult> SubmitAsync(
        IBrowserFile file,
        ManagerDocumentSubmissionBehavior submissionBehavior,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ManagerPageRangeRequest>? pageRanges = null)
    {
        ArgumentNullException.ThrowIfNull(
            file);

        if (!Enum.IsDefined(
                submissionBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(submissionBehavior),
                submissionBehavior,
                "Unknown document submission behavior.");
        }

        if (file.Size <=
            0)
        {
            throw new ManagerWorkshopUploadValidationException(
                ManagerWorkshopUploadValidationFailure.NoReadableContent);
        }

        if (file.Size >
            _options.MaximumUploadBytes)
        {
            throw new ManagerWorkshopUploadValidationException(
                ManagerWorkshopUploadValidationFailure.TooLarge);
        }

        var mediaType =
            ResolveMediaType(
                file.Name);

        if (pageRanges is { Count: > 0 } &&
            !string.Equals(
                mediaType,
                PdfMediaType,
                StringComparison.Ordinal))
        {
            throw new ManagerWorkshopUploadValidationException(
                ManagerWorkshopUploadValidationFailure.PageRangesRequirePdf);
        }

        await using var content =
            file.OpenReadStream(
                _options.MaximumUploadBytes,
                cancellationToken);

        return await _submissionClient
            .SubmitDocumentAsync(
                new ManagerDocumentSubmissionRequest(
                    Guid.NewGuid(),
                    content,
                    file.Size,
                    file.Name,
                    mediaType,
                    SourceOrigin,
                    submissionBehavior,
                    pageRanges),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string ResolveMediaType(
        string fileName) =>
        Path.GetExtension(
                fileName)
            .ToLowerInvariant() switch
        {
            ".pdf" =>
                PdfMediaType,
            ".epub" =>
                EpubMediaType,
            _ =>
                throw new ManagerWorkshopUploadValidationException(
                    ManagerWorkshopUploadValidationFailure.UnsupportedFormat)
        };

    #endregion
}

internal sealed class ManagerWorkshopUploadValidationException(
    ManagerWorkshopUploadValidationFailure failure)
    : Exception(
        $"The selected source failed upload validation: {failure}.")
{
    public ManagerWorkshopUploadValidationFailure Failure { get; } =
        failure;
}

internal enum ManagerWorkshopUploadValidationFailure
{
    NoReadableContent,
    TooLarge,
    UnsupportedFormat,
    PageRangesRequirePdf
}
