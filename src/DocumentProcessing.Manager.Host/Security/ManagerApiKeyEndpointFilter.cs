using System.Security.Cryptography;
using System.Text;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace DocumentProcessing.Manager.Host.Security;

internal sealed class ManagerApiKeyEndpointFilter(
    string expectedApiKey)
    : IEndpointFilter
{
    #region Variables and Constants

    public const string HeaderName =
        "X-Manager-Api-Key";

    private readonly byte[]
        _expectedApiKey =
            Encoding.UTF8.GetBytes(
                expectedApiKey);

    #endregion

    #region Methods

    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var supplied =
            context.HttpContext.Request.Headers[
                    HeaderName]
                .ToString();

        var suppliedBytes =
            Encoding.UTF8.GetBytes(
                supplied);

        if (suppliedBytes.Length !=
                _expectedApiKey.Length ||
            !CryptographicOperations.FixedTimeEquals(
                suppliedBytes,
                _expectedApiKey))
        {
            return ValueTask.FromResult<object?>(
                HttpResults.Unauthorized());
        }

        return next(
            context);
    }

    #endregion
}
