using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Persistence.Files;

internal sealed class ContentAddressedFileIntegrityException(
    Sha256Digest expectedDigest,
    string message)
    : IOException(
        message)
{
    public Sha256Digest ExpectedDigest
    {
        get;
    } =
        expectedDigest;
}
