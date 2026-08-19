using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.UnitTests.Provenance;

/// <summary>
/// Verifies the format-neutral source descriptor used by the future result.
/// </summary>
public sealed class DocumentSourceDescriptorTests
{
    #region Variables and Constants

    private const string UppercaseSha =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    #endregion

    #region Methods Tests

    [Fact]
    public void Constructor_RetainsPortableSourceIdentity()
    {
        var source =
            new DocumentSourceDescriptor(
                new DocumentFormatId(
                    "epub"),
                UppercaseSha,
                byteLength:
                    1234,
                fileName:
                    " book.epub ",
                declaredMediaType:
                    " application/epub+zip ");

        Assert.Equal(
            "epub",
            source.Format.Value);

        Assert.Equal(
            UppercaseSha.ToLowerInvariant(),
            source.Sha256);

        Assert.Equal(
            1234,
            source.ByteLength);

        Assert.Equal(
            "book.epub",
            source.FileName);

        Assert.Equal(
            "application/epub+zip",
            source.DeclaredMediaType);
    }

    [Fact]
    public void Descriptor_HasNoPhysicalPageCount()
    {
        Assert.Null(
            typeof(DocumentSourceDescriptor)
                .GetProperty(
                    "PhysicalPageCount"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveByteLength(
        long byteLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentSourceDescriptor(
                    DocumentFormatId.Pdf,
                    UppercaseSha,
                    byteLength));
    }

    [Fact]
    public void Constructor_RejectsInvalidSha256()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentSourceDescriptor(
                    DocumentFormatId.Pdf,
                    "not-a-sha",
                    byteLength:
                        1));
    }

    #endregion
}
