using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Ports;

namespace DocumentProcessing.Manager.Persistence.Files;

/// <summary>
/// Content-addressed filesystem adapter preserving exact immutable source bytes.
/// </summary>
public sealed class FileSystemSourceArtifactCustodyStore
    : ISourceArtifactWriter,
      ISourceArtifactReader,
      ISourceArtifactPurger
{
    #region Variables and Constants

    private readonly FileSystemContentAddressedStore
        _store;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the content-addressed filesystem custody adapter.
    /// </summary>
    public FileSystemSourceArtifactCustodyStore(
        FileSystemSourceArtifactCustodyOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        _store =
            new FileSystemContentAddressedStore(
                options.RootDirectory,
                options.MaximumArtifactBytes);
    }

    #endregion

    #region Methods Write

    /// <inheritdoc />
    public async ValueTask<SourceArtifact> StoreAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stored =
                await _store.StoreAsync(
                        content,
                        cancellationToken)
                    .ConfigureAwait(false);

            return new SourceArtifact(
                stored.Digest,
                stored.ByteLength);
        }
        catch (ContentAddressedFileIntegrityException exception)
        {
            throw new SourceArtifactIntegrityException(
                exception.ExpectedDigest,
                exception.Message);
        }
    }

    #endregion

    #region Methods Read

    /// <inheritdoc />
    public ValueTask<bool> VerifyAsync(
        SourceArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            artifact);

        return _store.VerifyAsync(
            new ContentAddressedFile(
                artifact.Digest,
                artifact.ByteLength),
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Stream> OpenReadAsync(
        SourceArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            artifact);

        try
        {
            return await _store
                .OpenReadAsync(
                    new ContentAddressedFile(
                        artifact.Digest,
                        artifact.ByteLength),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ContentAddressedFileIntegrityException exception)
        {
            throw new SourceArtifactIntegrityException(
                exception.ExpectedDigest,
                exception.Message);
        }
    }

    #endregion

    #region Methods Delete

    /// <inheritdoc />
    public ValueTask DeleteAsync(
        Sha256Digest digest,
        CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(digest, cancellationToken);

    #endregion
}
