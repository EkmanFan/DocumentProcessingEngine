using DocumentProcessing.Manager.Persistence.Files;
using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class FileSystemProcessingResultArtifactTests
{
    #region Tests

    [Fact]
    public async Task StoreAsync_PreservesVerifiesAndDeduplicatesExactResultBytes()
    {
        var root =
            CreateTemporaryRoot();

        try
        {
            var store =
                CreateStore(
                    root);

            var content =
                "{\"schemaVersion\":\"document-processing-result-v3\"}"u8.ToArray();

            await using var firstSource =
                new MemoryStream(
                    content,
                    writable:
                        false);

            var first =
                await store.StoreAsync(
                    firstSource);

            await using var retrySource =
                new MemoryStream(
                    content,
                    writable:
                        false);

            Assert.Equal(
                first,
                await store.StoreAsync(
                    retrySource));

            Assert.True(
                await store.VerifyAsync(
                    first));

            await using var retained =
                await store.OpenReadAsync(
                    first);

            await using var copied =
                new MemoryStream();

            await retained.CopyToAsync(
                copied);

            Assert.Equal(
                content,
                copied.ToArray());

            Assert.Single(
                Directory.EnumerateFiles(
                    root,
                    "*",
                    SearchOption.AllDirectories));
        }
        finally
        {
            DeleteTemporaryRoot(
                root);
        }
    }

    [Fact]
    public async Task OpenReadAsync_RejectsSameLengthTampering()
    {
        var root =
            CreateTemporaryRoot();

        try
        {
            var store =
                CreateStore(
                    root);

            await using var source =
                new MemoryStream(
                    "original-result"u8.ToArray(),
                    writable:
                        false);

            var artifact =
                await store.StoreAsync(
                    source);

            var path =
                GetArtifactPath(
                    root,
                    artifact);

            MakeWritable(
                path);

            await File.WriteAllBytesAsync(
                path,
                "tampered-result"u8.ToArray());

            Assert.False(
                await store.VerifyAsync(
                    artifact));

            await Assert.ThrowsAsync<ProcessingResultIntegrityException>(
                () =>
                    store.OpenReadAsync(
                            artifact)
                        .AsTask());
        }
        finally
        {
            DeleteTemporaryRoot(
                root);
        }
    }

    #endregion

    #region Helpers

    private static FileSystemProcessingResultArtifactStore CreateStore(
        string root) =>
        new(
            new FileSystemProcessingResultArtifactOptions(
                root,
                maximumArtifactBytes:
                    1024 * 1024));

    private static string CreateTemporaryRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            $"dpengine-result-{Guid.NewGuid():N}");

    private static string GetArtifactPath(
        string root,
        ProcessingResultArtifact artifact) =>
        Path.Combine(
            root,
            "sha256",
            artifact.Digest.Value[..2],
            artifact.Digest.Value[2..4],
            artifact.Digest.Value);

    private static void MakeWritable(
        string path)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(
                path,
                FileAttributes.Normal);

            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite);
    }

    private static void DeleteTemporaryRoot(
        string root)
    {
        if (!Directory.Exists(
                root))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var file in Directory.EnumerateFiles(
                         root,
                         "*",
                         SearchOption.AllDirectories))
            {
                File.SetAttributes(
                    file,
                    FileAttributes.Normal);
            }
        }

        Directory.Delete(
            root,
            recursive:
                true);
    }

    #endregion
}
