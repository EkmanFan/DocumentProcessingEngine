using System.Reflection;
using DocumentProcessing.Core.DocumentModel;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Results;

namespace DocumentProcessing.UnitTests.Results;

/// <summary>
/// Verifies the narrow format-neutral portable footnote model.
/// </summary>
public sealed class DocumentFootnoteTests
{
    #region Methods Tests

    [Fact]
    public void Reference_EmbedsAutonomousProvenance()
    {
        var provenance =
            new DocumentFootnoteProvenance(
                "element-17",
                new TestSourceLocation(
                    "marker"));

        var reference =
            new DocumentFootnoteReference(
                provenance);

        Assert.Same(
            provenance,
            reference.Provenance);

        Assert.Equal(
            "element-17",
            reference.Provenance.ElementId);
    }

    [Fact]
    public void Footnote_RetainsMultipleSourceLocations()
    {
        const string text =
            "Cross-boundary footnote text.";

        var footnote =
            new DocumentFootnote(
                footnoteId:
                    "footnote-000000",
                ordinal:
                    0,
                label:
                    "756",
                text,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    text),
                sourceLocations:
                    [
                        new TestSourceLocation(
                            "page-513"),
                        new TestSourceLocation(
                            "page-514")
                    ],
                references:
                    [
                        new DocumentFootnoteReference(
                            new DocumentFootnoteProvenance(
                                "element-17",
                                new TestSourceLocation(
                                    "marker-513")))
                    ]);

        Assert.Equal(
            2,
            footnote.SourceLocations.Count);

        Assert.Equal(
            "756",
            footnote.Label);
    }

    [Fact]
    public void Footnote_RejectsTextHashMismatch()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentFootnote(
                    "footnote-000000",
                    ordinal:
                        0,
                    label:
                        "1",
                    text:
                        "Exact text.",
                    textSha256:
                        new string(
                            '0',
                            64),
                    sourceLocations:
                        [new TestSourceLocation("source")],
                    references:
                        [
                            new DocumentFootnoteReference(
                                new DocumentFootnoteProvenance(
                                    "element-1",
                                    new TestSourceLocation(
                                        "marker")))
                        ]));
    }

    [Fact]
    public void PortableFootnoteTypes_DoNotDependOnIngestionResult()
    {
        var modelTypes =
            new[]
            {
                typeof(DocumentFootnote),
                typeof(DocumentFootnoteReference),
                typeof(DocumentFootnoteProvenance)
            };

        Assert.DoesNotContain(
            modelTypes,
            type =>
                type.GetProperties()
                    .Any(
                        property =>
                            property.PropertyType.Name ==
                            "DocumentIngestionResult"));
    }

    [Fact]
    public void Projector_FootnotesParameter_IsRequired()
    {
        var method =
            typeof(DocumentProcessingResultProjector)
                .GetMethod(
                    nameof(DocumentProcessingResultProjector.Project),
                    BindingFlags.Public |
                    BindingFlags.Static);

        Assert.NotNull(
            method);

        var footnotes =
            Assert.Single(
                method.GetParameters(),
                parameter =>
                    parameter.Name ==
                    "footnotes");

        Assert.False(
            footnotes.IsOptional);

        Assert.False(
            footnotes.HasDefaultValue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProcessingResult_RejectsFootnoteReferenceOnDifferentPageThanOwnerElement(
        bool retainPagedSourceStructure)
    {
        const string bodyText =
            "Body text with marker.";

        const string footnoteText =
            "Footnote payload.";

        var element =
            new DocumentElement(
                elementId:
                    "element-1",
                ordinal:
                    0,
                DocumentElementKind.Text,
                new PagedDocumentSourceLocation(
                    physicalPageNumber:
                        1),
                segmentId:
                    null,
                bodyText,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    bodyText));

        var evidence =
            new DocumentElementProcessingEvidence(
                elementId:
                    element.ElementId,
                DocumentTextSourceKind.Native,
                selectedSourceText:
                    bodyText,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    bodyText),
                nativeCandidateSequence:
                    0,
                layoutCandidateSequence:
                    null,
                ocrBackendId:
                    null,
                ocrProfileId:
                    null,
                reconciliationDecision:
                    null,
                textsEquivalent:
                    null,
                hasReconciliationDivergence:
                    false,
                selectedTextPreparation:
                    null,
                normalizationDehyphenation:
                    null,
                normalizationChangedText:
                    false,
                exclusionReason:
                    null,
                isResolved:
                    true,
                layoutKind:
                    null);

        var footnote =
            new DocumentFootnote(
                footnoteId:
                    "footnote-000000",
                ordinal:
                    0,
                label:
                    "1",
                footnoteText,
                ProvenanceTextHashing.ComputeUtf8Sha256(
                    footnoteText),
                sourceLocations:
                    [
                        new PagedDocumentSourceLocation(
                            physicalPageNumber:
                                2)
                    ],
                references:
                    [
                        new DocumentFootnoteReference(
                            new DocumentFootnoteProvenance(
                                element.ElementId,
                                new PagedDocumentSourceLocation(
                                    physicalPageNumber:
                                        2)))
                    ]);

        DocumentSourceStructure? sourceStructure =
            retainPagedSourceStructure
                ? new PagedDocumentSourceStructure(
                    [
                        new PagedDocumentPageDescriptor(
                            physicalPageNumber:
                                1,
                            new NormalizedRectangle(
                                0,
                                0,
                                1,
                                1)),
                        new PagedDocumentPageDescriptor(
                            physicalPageNumber:
                                2,
                            new NormalizedRectangle(
                                0,
                                0,
                                1,
                                1))
                    ])
                : null;

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentProcessingResult(
                        new DocumentSourceDescriptor(
                            DocumentFormatId.Pdf,
                            new string(
                                'a',
                                64),
                            byteLength:
                                100),
                        new DocumentProcessingManifest(
                            engineVersion:
                                "test-engine",
                            nativeExtraction:
                                new ProcessingComponentIdentity(
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
                        elements:
                            [element],
                        elementProcessingEvidence:
                            [evidence],
                        structuralSegments:
                            [],
                        segmentProcessingEvidence:
                            [],
                        visualAssets:
                            [],
                        qualityObservations:
                            DocumentProcessingQualityObservations.Empty,
                        sourceStructure:
                            sourceStructure,
                        footnotes:
                            [footnote]));

        Assert.Contains(
            "referenced document element is on physical page 1",
            exception.Message,
            StringComparison.Ordinal);
    }

    #endregion

    #region Test Types

    private sealed record TestSourceLocation(
        string Value)
        : DocumentSourceLocation;

    #endregion
}
