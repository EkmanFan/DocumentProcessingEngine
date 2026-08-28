using System.Security.Cryptography;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Persistence.Files;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class FileSystemProcessingVisualAssetStoreTests
{
    #region Tests

    [Fact]
    public async Task ValidateRootAsync_RejectsMissingDirectory()
    {
        var root =
            CreateTemporaryRoot();

        var store =
            new FileSystemProcessingVisualAssetStore();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () =>
                store.ValidateRootAsync(
                        root)
                    .AsTask());
    }

    [Fact]
    public async Task CompleteAsync_PublishesVerifiedDirectoryAndReplaysIdempotently()
    {
        var root =
            CreateTemporaryRoot();

        Directory.CreateDirectory(
            root);

        try
        {
            var store =
                new FileSystemProcessingVisualAssetStore();

            var unitId =
                ProcessingUnitId.New();

            var first =
                new byte[]
                {
                    1,
                    2,
                    3,
                    4
                };

            var second =
                new byte[]
                {
                    8,
                    9,
                    10
                };

            var descriptors =
                new[]
                {
                    CreateDescriptor(
                        "page:1/figure:1",
                        "image/png",
                        first),
                    CreateDescriptor(
                        "page:2:source",
                        "image/jpeg",
                        second)
                };

            var firstDirectory =
                await WriteAndCompleteAsync(
                    store,
                    root,
                    unitId,
                    descriptors,
                    first,
                    second);

            Assert.True(
                Directory.Exists(
                    firstDirectory));

            Assert.Equal(
                3,
                Directory.EnumerateFiles(
                        firstDirectory)
                    .Count());

            Assert.Equal(
                first,
                await File.ReadAllBytesAsync(
                    Path.Combine(
                        firstDirectory,
                        "0001-page-1-figure-1.png")));

            Assert.Equal(
                second,
                await File.ReadAllBytesAsync(
                    Path.Combine(
                        firstDirectory,
                        "0002-page-2-source.jpg")));

            var replayDirectory =
                await WriteAndCompleteAsync(
                    store,
                    root,
                    unitId,
                    descriptors,
                    first,
                    second);

            Assert.Equal(
                firstDirectory,
                replayDirectory);
        }
        finally
        {
            DeleteTemporaryRoot(
                root);
        }
    }

    [Fact]
    public async Task CompleteAsync_RejectsConflictingCompletedBytes()
    {
        var root =
            CreateTemporaryRoot();

        Directory.CreateDirectory(
            root);

        try
        {
            var store =
                new FileSystemProcessingVisualAssetStore();

            var unitId =
                ProcessingUnitId.New();

            var bytes =
                new byte[]
                {
                    11,
                    12,
                    13
                };

            var descriptors =
                new[]
                {
                    CreateDescriptor(
                        "visual:1",
                        "image/png",
                        bytes)
                };

            var completedDirectory =
                await WriteAndCompleteAsync(
                    store,
                    root,
                    unitId,
                    descriptors,
                    bytes);

            var completedFile =
                Path.Combine(
                    completedDirectory,
                    "0001-visual-1.png");

            File.SetAttributes(
                completedFile,
                FileAttributes.Normal);

            await File.WriteAllBytesAsync(
                completedFile,
                new byte[]
                {
                    99,
                    98,
                    97
                });

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    WriteAndCompleteAsync(
                        store,
                        root,
                        unitId,
                        descriptors,
                        bytes));
        }
        finally
        {
            DeleteTemporaryRoot(
                root);
        }
    }

    [Fact]
    public async Task DisposeAsync_AfterValidationFailure_RemovesStagingBytes()
    {
        var root =
            CreateTemporaryRoot();

        Directory.CreateDirectory(
            root);

        try
        {
            var store =
                new FileSystemProcessingVisualAssetStore();

            await using (var session =
                         await store.BeginWriteAsync(
                             root,
                             ProcessingUnitId.New(),
                             "document.pdf"))
            {
                await using var destination =
                    await session.OpenWriteAsync(
                        "image/png");

                await destination.WriteAsync(
                    new byte[]
                    {
                        1,
                        2
                    });

                await Assert.ThrowsAsync<InvalidDataException>(
                    () =>
                        session.CompleteAsync(
                                [])
                            .AsTask());
            }

            var stagingRoot =
                Path.Combine(
                    root,
                    ".dpengine-staging");

            Assert.False(
                Directory.Exists(
                    stagingRoot) &&
                Directory.EnumerateFileSystemEntries(
                        stagingRoot)
                    .Any());
        }
        finally
        {
            DeleteTemporaryRoot(
                root);
        }
    }

    #endregion

    #region Methods

    private static async Task<string> WriteAndCompleteAsync(
        FileSystemProcessingVisualAssetStore store,
        string root,
        ProcessingUnitId unitId,
        IReadOnlyList<ProcessingVisualAssetDescriptor> descriptors,
        params byte[][] contents)
    {
        await using var session =
            await store.BeginWriteAsync(
                root,
                unitId,
                "A difficult/book.pdf".Replace(
                    '/',
                    '-'));

        for (var index = 0;
             index < contents.Length;
             index++)
        {
            await using var destination =
                await session.OpenWriteAsync(
                    descriptors[index].MediaType);

            await destination.WriteAsync(
                contents[index]);
        }

        return await session.CompleteAsync(
            descriptors);
    }

    private static ProcessingVisualAssetDescriptor CreateDescriptor(
        string assetId,
        string mediaType,
        byte[] content) =>
        new(
            assetId,
            mediaType,
            content.LongLength,
            new Sha256Digest(
                Convert.ToHexString(
                        SHA256.HashData(
                            content))
                    .ToLowerInvariant()));

    private static string CreateTemporaryRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            $"dpengine-manager-visuals-{Guid.NewGuid():N}");

    private static void DeleteTemporaryRoot(
        string root)
    {
        if (!Directory.Exists(
                root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(
                     root,
                     "*",
                     SearchOption.AllDirectories))
        {
            File.SetAttributes(
                file,
                FileAttributes.Normal);
        }

        Directory.Delete(
            root,
            recursive:
                true);
    }

    #endregion
}
