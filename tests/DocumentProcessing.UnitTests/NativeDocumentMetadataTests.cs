using System.Xml.Linq;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Epub.Extraction;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace DocumentProcessing.UnitTests;

/// <summary>
/// Native descriptive-metadata acquisition for PDF and EPUB.
/// </summary>
/// <remarks>
/// Every PDF fixture is built in memory so the suite stays deterministic and
/// independent of the local corpus. The tests deliberately assert acquisition
/// and provenance, never the semantic quality of an arbitrary title: judging a
/// publisher's <c>/Title</c> is not a property of this slice.
/// </remarks>
public sealed class NativeDocumentMetadataTests
{
    #region Methods Pdf

    [Fact]
    public void Pdf_metadata_is_acquired_with_source_hints()
    {
        var bytes =
            BuildPdf(
                information =>
                {
                    information.Title =
                        "The Case for the Resurrection of Jesus";
                    information.Author =
                        "Gary R. Habermas";
                    information.Subject =
                        "Historical apologetics";
                });

        var metadata =
            ReadPdfMetadata(
                bytes);

        Assert.NotNull(
            metadata.Title);
        Assert.Equal(
            "The Case for the Resurrection of Jesus",
            metadata.Title!.Value);
        Assert.Equal(
            PdfNativeMetadataReader.TitleSourceHint,
            metadata.Title.SourceHint);

        var contributor =
            Assert.Single(
                metadata.Contributors);
        Assert.Equal(
            "Gary R. Habermas",
            contributor.Value);
        Assert.Equal(
            PdfNativeMetadataReader.AuthorSourceHint,
            contributor.SourceHint);

        Assert.NotNull(
            metadata.Description);
        Assert.Equal(
            PdfNativeMetadataReader.SubjectSourceHint,
            metadata.Description!.SourceHint);
    }

    [Fact]
    public void Pdf_without_metadata_yields_no_values()
    {
        var metadata =
            ReadPdfMetadata(
                BuildPdf(
                    _ =>
                    {
                    }));

        Assert.Null(
            metadata.Title);
        Assert.Empty(
            metadata.Contributors);
        Assert.True(
            metadata.IsEmpty);
    }

    [Fact]
    public void Pdf_title_of_poor_quality_is_acquired_verbatim()
    {
        // Converter artefacts are extremely common in real corpora. The adapter
        // must preserve them: cleaning here would destroy the evidence a later
        // reconciliation step needs in order to reject the value.
        var metadata =
            ReadPdfMetadata(
                BuildPdf(
                    information =>
                        information.Title =
                            "Microsoft Word - final3.docx"));

        Assert.Equal(
            "Microsoft Word - final3.docx",
            metadata.Title?.Value);
    }

    [Fact]
    public void Pdf_blank_metadata_is_treated_as_absent()
    {
        var metadata =
            ReadPdfMetadata(
                BuildPdf(
                    information =>
                    {
                        information.Title =
                            "   ";
                        information.Author =
                            string.Empty;
                    }));

        Assert.Null(
            metadata.Title);
        Assert.Empty(
            metadata.Contributors);
    }

    [Fact]
    public void Pdf_keywords_stand_in_for_an_absent_subject()
    {
        var metadata =
            ReadPdfMetadata(
                BuildPdf(
                    information =>
                        information.Keywords =
                            "apologetics; resurrection"));

        Assert.Equal(
            PdfNativeMetadataReader.KeywordsSourceHint,
            metadata.Description?.SourceHint);
    }

    [Fact]
    public void Pdf_reader_tolerates_absent_information()
    {
        Assert.True(
            PdfNativeMetadataReader.Read(
                    null)
                .IsEmpty);
    }

    #endregion

    #region Methods Epub

    [Fact]
    public void Epub_package_metadata_is_acquired_with_source_hints()
    {
        var metadata =
            EpubNativeMetadataReader.Read(
                BuildOpf());

        Assert.Equal(
            "Jesus and the Eyewitnesses",
            metadata.Title?.Value);
        Assert.Equal(
            EpubNativeMetadataReader.TitleSourceHint,
            metadata.Title?.SourceHint);
        Assert.Equal(
            "fr",
            metadata.Language?.Value);
        Assert.Equal(
            "Eerdmans",
            metadata.Publisher?.Value);
        Assert.Equal(
            2,
            metadata.Contributors.Count);
        Assert.Equal(
            EpubNativeMetadataReader.CreatorSourceHint,
            metadata.Contributors[0].SourceHint);
        Assert.Equal(
            EpubNativeMetadataReader.ContributorSourceHint,
            metadata.Contributors[1].SourceHint);
        Assert.Single(
            metadata.Dates);
    }

    #endregion

    #region Methods Contract

    [Fact]
    public void Evidence_without_metadata_exposes_an_empty_container()
    {
        // Existing adapters that supply no metadata must not force consumers to
        // branch on a null container.
        Assert.True(
            NativeDocumentMetadata.Empty.IsEmpty);
        Assert.Empty(
            NativeDocumentMetadata.Empty.Contributors);
    }

    [Fact]
    public void Metadata_value_rejects_an_empty_source_hint()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new NativeDocumentMetadataValue(
                    "value",
                    " "));
    }

    #endregion

    #region Methods Helpers

    private static NativeDocumentMetadata ReadPdfMetadata(
        byte[] bytes)
    {
        using var document =
            UglyToad.PdfPig.PdfDocument.Open(
                bytes);

        return PdfNativeMetadataReader.Read(
            document.Information);
    }

    private static byte[] BuildPdf(
        Action<PdfDocumentBuilder.DocumentInformationBuilder> configure)
    {
        var builder =
            new PdfDocumentBuilder();

        configure(
            builder.DocumentInformation);

        builder.AddPage(
            595,
            842);

        return builder.Build();
    }

    private static XDocument BuildOpf() =>
        XDocument.Parse(
            """
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>Jesus and the Eyewitnesses</dc:title>
                <dc:creator>Richard Bauckham</dc:creator>
                <dc:contributor>Traducteur</dc:contributor>
                <dc:publisher>Eerdmans</dc:publisher>
                <dc:language>fr</dc:language>
                <dc:description>Les Evangiles comme temoignage oculaire.</dc:description>
                <dc:date>2017-03-01</dc:date>
              </metadata>
            </package>
            """);

    #endregion
}
