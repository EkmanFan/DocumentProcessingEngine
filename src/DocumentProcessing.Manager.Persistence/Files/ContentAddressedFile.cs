using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Persistence.Files;

internal readonly record struct ContentAddressedFile(
    Sha256Digest Digest,
    long ByteLength);
