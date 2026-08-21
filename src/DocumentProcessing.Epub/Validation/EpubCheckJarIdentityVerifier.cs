using System.Security.Cryptography;

namespace DocumentProcessing.Epub.Validation;

internal sealed class EpubCheckJarIdentityVerifier
    : IEpubCheckJarIdentityVerifier
{
    #region Variables and Constants

    private const string ExpectedJarSha256 =
        "f7f96617c929371821609b88c8484d6dc9f24fe916499863c46094c5fb778a65";

    #endregion

    #region Methods Verification

    public bool MatchesPinnedVersion(
        string jarPath)
    {
        try
        {
            using var stream =
                File.OpenRead(
                    jarPath);

            var observed =
                Convert
                    .ToHexString(
                        SHA256.HashData(
                            stream))
                    .ToLowerInvariant();

            return string.Equals(
                observed,
                ExpectedJarSha256,
                StringComparison.Ordinal);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
            return false;
        }
    }

    #endregion
}
