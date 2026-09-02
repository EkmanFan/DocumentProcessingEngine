using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Pdf.Notes;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Outline;

namespace DocumentProcessing.Pdf;

/// <summary>
/// PDF implementation of the neutral document-format acquisition contract and
/// the PDF-specific technical capabilities currently used by the Engine.
/// </summary>
/// <remarks>
/// This boundary recognizes PDF, acquires native evidence and exposes operations
/// that exist because the source is PDF. It does not decide whether native
/// evidence is sufficient, whether enrichment is required, or which processing
/// route is authoritative.
/// </remarks>
public sealed class PdfDocumentFormat
    : IPhysicalPageRangeDocumentFormat,
      IPhysicalPagePreviewDocumentFormat,
      INativeDocumentNavigationFormat,
      IStructuralHeadingDocumentFormat,
      IDocumentRasterizer,
      IVisualRasterObservationSource
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity
        NativeExtractionIdentity =
            new(
                "pdfpig",
                "pdfpig-native-v1");

    private readonly PdfFormatValidator _validator;
    private readonly PdfPigDocumentExtractor _extractor;
    private readonly PdfPigVisualRasterObservationSource
        _visualRasterObservationSource;
    private readonly PdftoppmDocumentRasterizer
        _documentRasterizer;

    #endregion

    #region Methods Preview

    /// <inheritdoc />
    public async ValueTask<int?> TryGetPhysicalPageCountAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!await _validator.ValidateAsync(source, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        long? originalPosition = source.Content.CanSeek ? source.Content.Position : null;

        try
        {
            if (source.Content.CanSeek)
            {
                source.Content.Position = 0;
            }

            using var document = UglyToad.PdfPig.PdfDocument.Open(source.Content);
            return document.NumberOfPages;
        }
        finally
        {
            if (originalPosition.HasValue)
            {
                source.Content.Position = originalPosition.Value;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask RenderPhysicalPagePreviewAsync(
        DocumentSource source,
        int physicalPageNumber,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await using var session =
            await new PdftoppmDocumentRasterizer(dpi: 96)
                .OpenAsync(source, DocumentFormatId.Pdf, cancellationToken)
                .ConfigureAwait(false);

        await session.RenderPageAsync(physicalPageNumber, destination, cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Methods Native Navigation

    /// <inheritdoc />
    public async ValueTask<NativeDocumentNavigationInspection?>
        TryInspectNativeNavigationAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        if (!await _validator
                .ValidateAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return null;
        }

        var originalPosition =
            source.Content.CanSeek
                ? source.Content.Position
                : (long?)null;

        try
        {
            if (source.Content.CanSeek)
            {
                source.Content.Position =
                    0;
            }

            using var document =
                UglyToad.PdfPig.PdfDocument.Open(
                    source.Content);

            var entries =
                new List<NativeDocumentNavigationEntry>();

            if (document.TryGetBookmarks(
                    out var bookmarks,
                    allowContainerNode:
                        true))
            {
                var sourceOrder =
                    0;

                foreach (var root in
                         bookmarks.Roots)
                {
                    AddNavigationEntry(
                        root,
                        document.NumberOfPages,
                        entries,
                        ref sourceOrder,
                        cancellationToken);
                }
            }

            return new NativeDocumentNavigationInspection(
                DocumentFormatId.Pdf,
                new DocumentStructureAxis.PhysicalPages(
                    document.NumberOfPages),
                entries);
        }
        finally
        {
            if (originalPosition.HasValue)
            {
                source.Content.Position =
                    originalPosition.Value;
            }
        }
    }

    private static void AddNavigationEntry(
        BookmarkNode node,
        int physicalPageCount,
        ICollection<NativeDocumentNavigationEntry> entries,
        ref int sourceOrder,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var currentSourceOrder =
            sourceOrder++;

        if (node is DocumentBookmarkNode documentNode &&
            !string.IsNullOrWhiteSpace(
                node.Title) &&
            documentNode.PageNumber >=
                1 &&
            documentNode.PageNumber <=
                physicalPageCount)
        {
            entries.Add(
                new NativeDocumentNavigationEntry(
                    node.Title,
                    node.Level,
                    currentSourceOrder,
                    new DocumentStructurePosition.PhysicalPage(
                        documentNode.PageNumber)));
        }

        foreach (var child in
                 node.Children)
        {
            AddNavigationEntry(
                child,
                physicalPageCount,
                entries,
                ref sourceOrder,
                cancellationToken);
        }
    }

    #endregion

    #region Methods Structural Headings

    /// <inheritdoc />
    public async ValueTask<StructuralHeadingInspection?>
        TryInspectStructuralHeadingsAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        if (!await _validator
                .ValidateAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return null;
        }

        var originalPosition =
            source.Content.CanSeek
                ? source.Content.Position
                : (long?)null;

        try
        {
            if (source.Content.CanSeek)
            {
                source.Content.Position =
                    0;
            }

            using var document =
                UglyToad.PdfPig.PdfDocument.Open(
                    source.Content);

            return PdfStructuralHeadingInspector.Inspect(
                document,
                cancellationToken);
        }
        finally
        {
            if (originalPosition.HasValue)
            {
                source.Content.Position =
                    originalPosition.Value;
            }
        }
    }

    #endregion

    #region Properties

    public DocumentFormatId Format =>
        DocumentFormatId.Pdf;

    #endregion

    #region ctor

    public PdfDocumentFormat()
    {
        _validator =
            new PdfFormatValidator();

        _extractor =
            new PdfPigDocumentExtractor();

        _visualRasterObservationSource =
            new PdfPigVisualRasterObservationSource();

        _documentRasterizer =
            new PdftoppmDocumentRasterizer(
                dpi:
                    300);
    }

    #endregion

    #region Methods Acquisition

    public async ValueTask<NativeEvidenceExtractionResult>
        TryExtractNativeEvidenceAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default) =>
        await AcquireNativeEvidenceAsync(
                source,
                physicalPageRange:
                    null,
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask<NativeEvidenceExtractionResult>
        TryExtractNativeEvidenceAsync(
            DocumentSource source,
            PhysicalPageRange physicalPageRange,
            CancellationToken cancellationToken = default) =>
        await AcquireNativeEvidenceAsync(
                source,
                physicalPageRange,
                cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<NativeEvidenceExtractionResult>
        AcquireNativeEvidenceAsync(
            DocumentSource source,
            PhysicalPageRange? physicalPageRange,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        var recognized =
            await _validator
                .ValidateAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!recognized)
        {
            return new NativeEvidenceExtractionResult
                .NotRecognized();
        }

        try
        {
            var extractionWithLinks =
                await _extractor
                    .ExtractWithRasterObservationsAndNativeLinksAsync(
                        source,
                        DocumentFormatId.Pdf,
                        _visualRasterObservationSource,
                        cancellationToken,
                        physicalPageRange)
                    .ConfigureAwait(false);

            var currentEvidence =
                extractionWithLinks
                    .ExtractionWithRasterObservations;

            var documentNotes =
                new PdfDocumentNoteAnalyzer()
                    .Analyze(
                        currentEvidence.Extraction,
                        extractionWithLinks.NativeNumericLinks,
                        cancellationToken);

            return new NativeEvidenceExtractionResult
                .Success(
                    new PagedNativeDocumentEvidence(
                        currentEvidence,
                        NativeExtractionIdentity,
                        documentNotes));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (PdfDocumentFormatException exception)
        {
            return Invalid(
                exception);
        }
        catch (InvalidDataException exception)
        {
            return Invalid(
                exception);
        }
    }

    #endregion

    #region Methods Rasterization Capability

    public bool CanRasterize(
        DocumentFormatId format) =>
        _documentRasterizer
            .CanRasterize(
                format);

    public ValueTask<IDocumentRasterizationSession> OpenAsync(
        DocumentSource source,
        DocumentFormatId format,
        CancellationToken cancellationToken = default) =>
        _documentRasterizer
            .OpenAsync(
                source,
                format,
                cancellationToken);

    #endregion

    #region Methods Native Visual Observation Capability

    public bool CanObserve(
        DocumentFormatId format) =>
        _visualRasterObservationSource
            .CanObserve(
                format);

    public ValueTask<IReadOnlyList<PageVisualRasterObservations>>
        ObserveAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            CancellationToken cancellationToken = default) =>
        _visualRasterObservationSource
            .ObserveAsync(
                source,
                format,
                extraction,
                cancellationToken);

    #endregion

    #region Methods Classification

    private static NativeEvidenceExtractionResult.Invalid Invalid(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        var detail =
            string.IsNullOrWhiteSpace(
                exception.Message)
                ? "The recognized PDF cannot be parsed as a valid document."
                : exception.Message.Trim();

        return new NativeEvidenceExtractionResult.Invalid(
            detail);
    }

    #endregion
}
