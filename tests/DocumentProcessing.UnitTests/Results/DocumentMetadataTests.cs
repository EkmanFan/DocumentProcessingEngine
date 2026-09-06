using System.Xml.Linq;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Results;
using DocumentProcessing.Epub.Extraction;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace DocumentProcessing.UnitTests.Results;

/// <summary>
/// Portable document metadata: the contract, and the engine's conclusion of it
/// from native evidence.
/// </summary>
/// <remarks>
/// The reconciliation policy under test is deliberately minimal, and the tests
/// assert that minimality rather than a richer behaviour: a native value is
/// concluded as-is, nothing is inferred from structure, and a field with no
/// evidence stays absent.
/// </remarks>
public sealed class DocumentMetadataTests
{
    #region Methods Contract

    [Fact]
    public void Empty_metadata_carries_no_value()
    {
        Assert.True(
            DocumentMetadata.Empty.IsEmpty);
        Assert.Empty(
            DocumentMetadata.Empty.Contributors);
        Assert.Empty(
            DocumentMetadata.Empty.Dates);
    }

    [Fact]
    public void A_value_requires_content()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentMetadataValue(
                    " ",
                    DocumentMetadataOrigin.Native));
    }

    [Fact]
    public void A_value_retains_its_origin_and_source()
    {
        var value =
            new DocumentMetadataValue(
                "A Title",
                DocumentMetadataOrigin.Native,
                "pdf.info.title");

        Assert.Equal(
            DocumentMetadataOrigin.Native,
            value.Origin);
        Assert.Equal(
            "pdf.info.title",
            value.SourceHint);
    }

    [Fact]
    public void A_result_without_metadata_exposes_an_empty_container()
    {
        // A consumer branches on the values it needs, never on the container.
        Assert.True(
            BuildResult(
                    NativeDocumentMetadata.Empty)
                .DocumentMetadata.IsEmpty);
    }

    #endregion

    #region Methods Reconciliation

    [Fact]
    public void A_native_title_is_concluded_as_is()
    {
        // Characterization found no converter artefact across the qualified
        // corpus, so no quality filter is applied: filtering would reject
        // values the evidence shows are good.
        var metadata =
            DocumentMetadataReconciler.Reconcile(
                new NativeDocumentMetadata(
                    new NativeDocumentMetadataValue(
                        "The Case for the Resurrection of Jesus",
                        PdfNativeMetadataReader.TitleSourceHint)));

        Assert.Equal(
            "The Case for the Resurrection of Jesus",
            metadata.Title?.Value);
        Assert.Equal(
            DocumentMetadataOrigin.Native,
            metadata.Title?.Origin);
        Assert.Equal(
            PdfNativeMetadataReader.TitleSourceHint,
            metadata.Title?.SourceHint);
    }

    [Fact]
    public void No_native_title_leaves_the_title_absent()
    {
        // The filename is never promoted, however title-like it reads.
        Assert.Null(
            DocumentMetadataReconciler.Reconcile(
                    NativeDocumentMetadata.Empty)
                .Title);
    }

    [Fact]
    public void Subtitle_is_never_concluded_in_this_version()
    {
        Assert.Null(
            DocumentMetadataReconciler.Reconcile(
                    new NativeDocumentMetadata(
                        new NativeDocumentMetadataValue(
                            "A Title",
                            "epub.opf.dc:title")))
                .Subtitle);
    }

    [Fact]
    public void Dates_keep_the_kind_their_own_field_states()
    {
        var metadata =
            DocumentMetadataReconciler.Reconcile(
                new NativeDocumentMetadata(
                    dates:
                    [
                        new NativeDocumentMetadataValue(
                            "D:20260511075937+00'00'",
                            PdfNativeMetadataReader.CreationDateSourceHint),
                        new NativeDocumentMetadataValue(
                            "D:20260512075937+00'00'",
                            PdfNativeMetadataReader.ModificationDateSourceHint),
                        new NativeDocumentMetadataValue(
                            "2004-03-26",
                            EpubNativeMetadataReader.DateSourceHint)
                    ]));

        Assert.Equal(
            DocumentMetadataDateKind.Created,
            metadata.Dates[0].Kind);
        Assert.Equal(
            DocumentMetadataDateKind.Modified,
            metadata.Dates[1].Kind);

        // An EPUB dc:date does not say which event it denotes.
        Assert.Equal(
            DocumentMetadataDateKind.Unspecified,
            metadata.Dates[2].Kind);
    }

    [Fact]
    public void A_sentinel_date_is_carried_rather_than_repaired()
    {
        // Calibre-produced packages emit this placeholder. Parsing it would
        // turn it into a plausible instant and hide the evidence a consumer
        // needs in order to reject it.
        Assert.Equal(
            "0101-01-01T00:00:00+00:00",
            DocumentMetadataReconciler.Reconcile(
                    new NativeDocumentMetadata(
                        dates:
                        [
                            new NativeDocumentMetadataValue(
                                "0101-01-01T00:00:00+00:00",
                                EpubNativeMetadataReader.DateSourceHint)
                        ]))
                .Dates[0]
                .Value);
    }

    [Fact]
    public void Contributors_stay_statements_and_are_not_declared_people()
    {
        // A real package lists its producing toolchain beside the authors.
        var metadata =
            DocumentMetadataReconciler.Reconcile(
                new NativeDocumentMetadata(
                    contributors:
                    [
                        new NativeDocumentMetadataValue(
                            "Bart D. Ehrman",
                            EpubNativeMetadataReader.CreatorSourceHint),
                        new NativeDocumentMetadataValue(
                            "calibre (9.11.0) [https://calibre-ebook.com]",
                            EpubNativeMetadataReader.CreatorSourceHint)
                    ]));

        Assert.Equal(
            2,
            metadata.Contributors.Count);
        Assert.Equal(
            "calibre (9.11.0) [https://calibre-ebook.com]",
            metadata.Contributors[1].Statement);
        Assert.All(
            metadata.Contributors,
            contributor =>
                Assert.Equal(
                    DocumentMetadataOrigin.Native,
                    contributor.Origin));
    }

    #endregion

    #region Methods Pdf

    [Fact]
    public void Pdf_evidence_reaches_portable_metadata()
    {
        var builder =
            new PdfDocumentBuilder();

        builder.DocumentInformation.Title =
            "Logic and Philosophy";
        builder.DocumentInformation.Author =
            "William H. Brenner";
        builder.AddPage(
            595,
            842);

        using var document =
            UglyToad.PdfPig.PdfDocument.Open(
                builder.Build());

        var metadata =
            DocumentMetadataReconciler.Reconcile(
                PdfNativeMetadataReader.Read(
                    document.Information));

        Assert.Equal(
            "Logic and Philosophy",
            metadata.Title?.Value);
        Assert.Equal(
            "William H. Brenner",
            Assert.Single(
                    metadata.Contributors)
                .Statement);

        // The PDF information dictionary has no field for either.
        Assert.Null(
            metadata.Language);
        Assert.Null(
            metadata.Publisher);
    }

    #endregion

    #region Methods Epub

    [Fact]
    public void Epub_package_evidence_reaches_portable_metadata()
    {
        var metadata =
            DocumentMetadataReconciler.Reconcile(
                EpubNativeMetadataReader.Read(
                    XDocument.Parse(
                        """
                        <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
                          <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                            <dc:title>The Case for the Resurrection of Jesus</dc:title>
                            <dc:creator>Gary R. Habermas</dc:creator>
                            <dc:publisher>Kregel Publications</dc:publisher>
                            <dc:language>en-US</dc:language>
                            <dc:date>2004-03-26</dc:date>
                          </metadata>
                        </package>
                        """)));

        Assert.Equal(
            "The Case for the Resurrection of Jesus",
            metadata.Title?.Value);
        Assert.Equal(
            EpubNativeMetadataReader.TitleSourceHint,
            metadata.Title?.SourceHint);
        Assert.Equal(
            "en-US",
            metadata.Language?.Value);
        Assert.Equal(
            "Kregel Publications",
            metadata.Publisher?.Value);
        Assert.Single(
            metadata.Contributors);
        Assert.Single(
            metadata.Dates);
    }

    #endregion

    #region Methods Helpers

    private static DocumentProcessingResult BuildResult(
        NativeDocumentMetadata native) =>
        new(
            new Core.Provenance.DocumentSourceDescriptor(
                DocumentFormatId.Pdf,
                "9f2c4b8e1d7a306f5c9b2e8a4d1f7c30596b8e2a4d1f7c30596b8e2a4d1f7c30",
                byteLength:
                    1024,
                "a-file-that-reads-like-a-title.pdf"),
            new Core.Provenance.DocumentProcessingManifest(
                engineVersion:
                    "test-engine",
                nativeExtraction:
                    new Core.Provenance.ProcessingComponentIdentity(
                        "native",
                        "native-v1"),
                rasterization:
                    null,
                layoutAnalysis:
                    null,
                ocr:
                    [],
                reconciliation:
                    null,
                visualPreservationProfileIds:
                    [],
                assemblyProfileId:
                    "assembly-v1",
                normalizationProfileId:
                    "normalization-v1",
                segmentationProfileId:
                    "segmentation-v1"),
            [],
            [],
            structuralSegments:
                [],
            segmentProcessingEvidence:
                [],
            visualAssets:
                [],
            DocumentProcessingQualityObservations.Empty,
            sourceStructure:
                null,
            notes:
                null,
            DocumentMetadataReconciler.Reconcile(
                native));

    #endregion
}
