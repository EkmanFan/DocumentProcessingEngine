using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Results;
using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Persistence.Files;

/// <summary>
/// Content-addressed filesystem adapter for immutable processing-result bytes.
/// </summary>
public sealed class FileSystemProcessingResultArtifactStore
    : IProcessingResultArtifactWriter,
      IProcessingResultArtifactReader,
      IProcessingResultArtifactPurger
{
    #region Variables and Constants

    private readonly FileSystemContentAddressedStore
        _store;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the processing-result filesystem adapter.
    /// </summary>
    public FileSystemProcessingResultArtifactStore(
        FileSystemProcessingResultArtifactOptions options)
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
    public async ValueTask<ProcessingResultArtifact> StoreAsync(
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

            return new ProcessingResultArtifact(
                stored.Digest,
                stored.ByteLength);
        }
        catch (ContentAddressedFileIntegrityException exception)
        {
            throw new ProcessingResultIntegrityException(
                exception.ExpectedDigest,
                exception.Message);
        }
    }

    #endregion

    #region Methods Read

    /// <inheritdoc />
    public ValueTask<bool> VerifyAsync(
        ProcessingResultArtifact artifact,
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
        ProcessingResultArtifact artifact,
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
            throw new ProcessingResultIntegrityException(
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
