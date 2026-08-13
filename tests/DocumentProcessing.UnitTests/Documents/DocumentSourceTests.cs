using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.UnitTests.Documents;

public sealed class DocumentSourceTests
{
    [Fact]
    public void Constructor_PreservesReadableStream()
    {
        using var stream = new MemoryStream([1, 2, 3]);

        var source = new DocumentSource(
            stream,
            fileName: " sample.pdf ",
            declaredMediaType: " application/pdf ");

        Assert.Same(stream, source.Content);
        Assert.Equal("sample.pdf", source.FileName);
        Assert.Equal("application/pdf", source.DeclaredMediaType);
    }

    [Fact]
    public void Constructor_DoesNotDisposeCallerOwnedStream()
    {
        using var stream = new MemoryStream([1, 2, 3]);

        _ = new DocumentSource(stream);

        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Constructor_RejectsNullStream()
    {
        Assert.Throws<ArgumentNullException>(() => new DocumentSource(null!));
    }

    [Fact]
    public void Constructor_RejectsUnreadableStream()
    {
        using var stream = new UnreadableStream();

        Assert.Throws<ArgumentException>(() => new DocumentSource(stream));
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public override bool CanRead => false;
    }
}
