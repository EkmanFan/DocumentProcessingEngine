using System.Security.Cryptography;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Engine.DualRun.Isolation;

namespace DocumentProcessing.UnitTests.DualRun.Isolation;

public sealed class DocumentDualRunSourceSnapshotFactoryTests
{
    #region Methods Construction

    [Fact]
    public void Constructor_DoesNotCreateSpoolRoot()
    {
        using var scope =
            new TemporaryDirectoryScope(
                create:
                    false);

        var factory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        Assert.Equal(
            System.IO.Path.GetFullPath(
                scope.Path),
            factory.SpoolRootPath);

        Assert.False(
            Directory.Exists(
                scope.Path));
    }

    [Fact]
    public void Constructor_RelativeSpoolRoot_FailsClosed()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentDualRunSourceSnapshotFactory(
                    "relative-dual-run-spool"));
    }

    #endregion

    #region Methods Creation

    [Fact]
    public async Task CreateAsync_CopiesFullSourceFromZero_AndRestoresPosition()
    {
        using var scope =
            new TemporaryDirectoryScope(
                create:
                    false);

        var sourceBytes =
            "0123456789-dual-run-source"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        source.Position =
            5;

        var factory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        await using var snapshot =
            await factory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        Assert.Equal(
            5,
            source.Position);

        Assert.True(
            Directory.Exists(
                snapshot.JobDirectoryPath));

        Assert.True(
            File.Exists(
                snapshot.SourceSnapshotPath));

        Assert.Equal(
            DocumentDualRunTransportSchema
                .SourceSnapshotFileName,
            System.IO.Path.GetFileName(
                snapshot.SourceSnapshotPath));

        Assert.Equal(
            sourceBytes,
            await File.ReadAllBytesAsync(
                snapshot.SourceSnapshotPath));

        Assert.Equal(
            Sha256(
                sourceBytes),
            snapshot.SourceDocumentSha256);

        Assert.Equal(
            sourceBytes.Length,
            snapshot.SourceByteLength);
    }

    [Fact]
    public async Task CreateAsync_Dispose_RemovesEntireJobDirectory()
    {
        using var scope =
            new TemporaryDirectoryScope(
                create:
                    false);

        var sourceBytes =
            "dispose-source"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var factory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        var snapshot =
            await factory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        var jobDirectory =
            snapshot.JobDirectoryPath;

        Assert.True(
            Directory.Exists(
                jobDirectory));

        await snapshot
            .DisposeAsync();

        Assert.False(
            Directory.Exists(
                jobDirectory));

        await snapshot
            .DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_ShaMismatch_RemovesFailedJobDirectory()
    {
        using var scope =
            new TemporaryDirectoryScope(
                create:
                    false);

        var sourceBytes =
            "sha-mismatch"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var factory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await factory
                    .CreateAsync(
                        Guid.NewGuid(),
                        source,
                        new string(
                            '0',
                            64),
                        sourceBytes.Length));

        AssertNoJobDirectories(
            scope.Path);
    }

    [Fact]
    public async Task CreateAsync_LengthMismatch_RemovesFailedJobDirectory()
    {
        using var scope =
            new TemporaryDirectoryScope(
                create:
                    false);

        var sourceBytes =
            "length-mismatch"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var factory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await factory
                    .CreateAsync(
                        Guid.NewGuid(),
                        source,
                        Sha256(
                            sourceBytes),
                        sourceBytes.Length +
                        1));

        AssertNoJobDirectories(
            scope.Path);
    }

    [Fact]
    public async Task CreateAsync_PreCancelled_DoesNotCreateSpoolRoot()
    {
        using var scope =
            new TemporaryDirectoryScope(
                create:
                    false);

        var sourceBytes =
            "cancelled-source"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var factory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await factory
                    .CreateAsync(
                        Guid.NewGuid(),
                        source,
                        Sha256(
                            sourceBytes),
                        sourceBytes.Length,
                        cancellation.Token));

        Assert.False(
            Directory.Exists(
                scope.Path));
    }

    [Fact]
    public async Task CreateAsync_NonSeekableSource_CopiesCurrentReadableBytes()
    {
        using var scope =
            new TemporaryDirectoryScope(
                create:
                    false);

        var sourceBytes =
            "non-seekable-source"u8
                .ToArray();

        await using var source =
            new NonSeekableReadStream(
                sourceBytes);

        var factory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        await using var snapshot =
            await factory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        Assert.Equal(
            sourceBytes,
            await File.ReadAllBytesAsync(
                snapshot.SourceSnapshotPath));
    }

    [Fact]
    public async Task CreateAsync_OnUnix_RemovesGroupAndOtherAccess()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope =
            new TemporaryDirectoryScope(
                create:
                    false);

        var sourceBytes =
            "private-source"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var factory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        await using var snapshot =
            await factory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        const UnixFileMode groupOrOther =
            UnixFileMode.GroupRead |
            UnixFileMode.GroupWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherWrite |
            UnixFileMode.OtherExecute;

        var directoryMode =
            new DirectoryInfo(
                snapshot.JobDirectoryPath)
                .UnixFileMode;

        var sourceMode =
            new FileInfo(
                snapshot.SourceSnapshotPath)
                .UnixFileMode;

        Assert.Equal(
            0,
            (int)(
                directoryMode &
                groupOrOther));

        Assert.Equal(
            0,
            (int)(
                sourceMode &
                groupOrOther));
    }

    #endregion

    #region Methods Helpers

    private static string Sha256(
        byte[] source) =>
        Convert
            .ToHexString(
                SHA256.HashData(
                    source))
            .ToLowerInvariant();

    private static void AssertNoJobDirectories(
        string spoolRoot)
    {
        Assert.True(
            Directory.Exists(
                spoolRoot));

        Assert.Empty(
            Directory.EnumerateDirectories(
                spoolRoot));
    }

    #endregion

    #region Test Types

    private sealed class TemporaryDirectoryScope
        : IDisposable
    {
        #region ctor

        public TemporaryDirectoryScope(
            bool create)
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"dpe-dual-run-snapshot-test-{Guid.NewGuid():N}");

            if (create)
            {
                Directory.CreateDirectory(
                    Path);
            }
        }

        #endregion

        #region Properties

        public string Path { get; }

        #endregion

        #region Methods Lifecycle

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(
                        Path))
                {
                    Directory.Delete(
                        Path,
                        recursive:
                            true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        #endregion
    }

    private sealed class NonSeekableReadStream
        : Stream
    {
        #region Variables and Constants

        private readonly MemoryStream _inner;

        #endregion

        #region ctor

        public NonSeekableReadStream(
            byte[] content)
        {
            _inner =
                new MemoryStream(
                    content,
                    writable:
                        false);
        }

        #endregion

        #region Properties

        public override bool CanRead =>
            true;

        public override bool CanSeek =>
            false;

        public override bool CanWrite =>
            false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get =>
                throw new NotSupportedException();
            set =>
                throw new NotSupportedException();
        }

        #endregion

        #region Methods Stream

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            _inner.Read(
                buffer,
                offset,
                count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(
                buffer,
                cancellationToken);

        public override void Flush()
        {
        }

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(
            long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(
                disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner
                .DisposeAsync()
                .ConfigureAwait(false);

            GC.SuppressFinalize(
                this);
        }

        #endregion
    }

    #endregion
}
