using System.Net;

namespace DocumentProcessing.Manager.Blazor.ManagerApi;

internal sealed class ManagerSettingsRejectedException
    : HttpRequestException
{
    #region Properties

    public string? ManagerCode { get; }

    #endregion

    #region ctor

    public ManagerSettingsRejectedException(
        HttpStatusCode statusCode,
        string? managerCode,
        string message)
        : base(
            message,
            inner:
                null,
            statusCode)
    {
        ManagerCode =
            managerCode;
    }

    #endregion
}
