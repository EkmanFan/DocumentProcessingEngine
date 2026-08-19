using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.UnitTests.Results;

/// <summary>
/// Verifies that the C2.1 document structure is genuinely format-neutral.
/// </summary>
public sealed class PortableDocumentStructureTests
{
    #region Methods Tests

    [Fact]
    public void Element_AllowsNonPagedSourceLocation()
    {
        const string text =
            "Portable EPUB-like text.";

        var location =
            new TestNonPagedLocation(
                "chapter-01.xhtml",
                "section-2");

        var element =
            new DocumentElement(
                elementId:
                    "element-1",
                ordinal:
                    0,
                DocumentElementKind.Text,
                location,
                segmentId:
                    "segment-1",
                text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text));

        Assert.Same(
            location,
            element.Location);

        Assert.Equal(
            "chapter-01.xhtml",
            location.ResourceId);
    }

    [Fact]
    public void Element_HasNoRequiredPhysicalPageProperty()
    {
        Assert.Null(
            typeof(DocumentElement)
                .GetProperty(
                    "PhysicalPageNumber"));

        Assert.Null(
            typeof(DocumentElement)
                .GetProperty(
                    "Bounds"));
    }

    [Fact]
    public void TextualElement_RejectsMissingAuthoritativeText()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentElement(
                    elementId:
                        "element-1",
                    ordinal:
                        0,
                    DocumentElementKind.Heading,
                    new TestNonPagedLocation(
                        "chapter.xhtml",
                        null),
                    segmentId:
                        null,
                    text:
                        null,
                    textSha256:
                        null));
    }

    [Fact]
    public void VisualElement_RejectsNarrativeText()
    {
        const string text =
            "Visuals do not own narrative text.";

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentElement(
                    elementId:
                        "visual-1",
                    ordinal:
                        0,
                    DocumentElementKind.Visual,
                    new TestNonPagedLocation(
                        "image-1.png",
                        null),
                    segmentId:
                        null,
                    text,
                    ProvenanceTextHashing.ComputeUtf8Sha256(
                        text)));
    }

    [Fact]
    public void StructuralSegment_HasNoPhysicalPageSpan()
    {
        Assert.Null(
            typeof(DocumentStructuralSegment)
                .GetProperty(
                    "FirstPhysicalPageNumber"));

        Assert.Null(
            typeof(DocumentStructuralSegment)
                .GetProperty(
                    "LastPhysicalPageNumber"));
    }

    [Fact]
    public void StructuralSegment_RetainsOrderedSourceElementMembership()
    {
        const string text =
            "A structural segment.";

        var segment =
            new DocumentStructuralSegment(
                segmentId:
                    "segment-1",
                ordinal:
                    0,
                text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text),
                headingText:
                    "Heading",
                sourceElementIds:
                    [
                        "element-1",
                        "element-2"
                    ]);

        Assert.Equal(
            [
                "element-1",
                "element-2"
            ],
            segment.SourceElementIds);
    }

    [Fact]
    public void StructuralSegment_RejectsDuplicateSourceElementMembership()
    {
        const string text =
            "A structural segment.";

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentStructuralSegment(
                    segmentId:
                        "segment-1",
                    ordinal:
                        0,
                    text,
                    ProvenanceTextHashing.ComputeUtf8Sha256(
                        text),
                    headingText:
                        null,
                    sourceElementIds:
                        [
                            "element-1",
                            "element-1"
                        ]));
    }

    #endregion

    #region Test Types

    private sealed record TestNonPagedLocation(
        string ResourceId,
        string? FragmentId)
        : DocumentSourceLocation;

    #endregion
}
