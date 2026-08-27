using System.Security.Cryptography;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Persistence.Files;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class FileSystemSourceArtifactCustodyTests
{
    #region Tests

    [Fact]
    public async Task StoreAsync_PreservesExactBytesAndDeduplicatesByDigest()
    {
        var root =
            CreateTemporaryRoot();

        try
        {
            var store =
                CreateStore(
                    root);

            var content =
                new byte[]
                {
                    0,
                    1,
                    2,
                    3,
                    254,
                    255
                };

            await using var firstStream =
                new MemoryStream(
                    content,
                    writable:
                        false);

            var first =
                await store.StoreAsync(
                    firstStream);

            await using var retryStream =
                new MemoryStream(
                    content,
                    writable:
                        false);

            var retry =
                await store.StoreAsync(
                    retryStream);

            Assert.Equal(
                first,
                retry);

            Assert.Equal(
                Convert.ToHexString(
                        SHA256.HashData(
                            content))
                    .ToLowerInvariant(),
                first.Digest.Value);

            Assert.Equal(
                content.LongLength,
                first.ByteLength);

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
    public async Task OpenReadAsync_RejectsTamperedRetainedBytes()
    {
        var root =
            CreateTemporaryRoot();

        try
        {
            var store =
                CreateStore(
                    root);

            var original =
                "custody source"u8.ToArray();

            await using var source =
                new MemoryStream(
                    original,
                    writable:
                        false);

            var artifact =
                await store.StoreAsync(
                    source);

            var artifactPath =
                GetArtifactPath(
                    root,
                    artifact.Digest);

            MakeWritableForTamperTest(
                artifactPath);

            await File.WriteAllBytesAsync(
                artifactPath,
                "tampered bytes"u8.ToArray());

            Assert.False(
                await store.VerifyAsync(
                    artifact));

            var exception =
                await Assert.ThrowsAsync<SourceArtifactIntegrityException>(
                    () =>
                        store.OpenReadAsync(
                                artifact)
                            .AsTask());

            Assert.Equal(
                artifact.Digest,
                exception.ExpectedDigest);
        }
        finally
        {
            DeleteTemporaryRoot(
                root);
        }
    }

    [Fact]
    public async Task StoreAsync_RejectsEmptyAndOversizedSourcesWithoutRetainedArtifact()
    {
        var root =
            CreateTemporaryRoot();

        try
        {
            var store =
                new FileSystemSourceArtifactCustodyStore(
                    new FileSystemSourceArtifactCustodyOptions(
                        root,
                        maximumArtifactBytes:
                            3));

            await using var empty =
                new MemoryStream();

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    store.StoreAsync(
                            empty)
                        .AsTask());

            await using var oversized =
                new MemoryStream(
                    new byte[]
                    {
                        1,
                        2,
                        3,
                        4
                    },
                    writable:
                        false);

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    store.StoreAsync(
                            oversized)
                        .AsTask());

            Assert.Empty(
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

    #endregion

    #region Helpers

    private static FileSystemSourceArtifactCustodyStore CreateStore(
        string root) =>
        new(
            new FileSystemSourceArtifactCustodyOptions(
                root,
                maximumArtifactBytes:
                    1024 * 1024));

    private static string CreateTemporaryRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            $"dpengine-custody-{Guid.NewGuid():N}");

    private static string GetArtifactPath(
        string root,
        Sha256Digest digest) =>
        Path.Combine(
            root,
            "sha256",
            digest.Value[..2],
            digest.Value[2..4],
            digest.Value);

    private static void MakeWritableForTamperTest(
        string artifactPath)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(
                artifactPath,
                FileAttributes.Normal);

            return;
        }

        File.SetUnixFileMode(
            artifactPath,
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
