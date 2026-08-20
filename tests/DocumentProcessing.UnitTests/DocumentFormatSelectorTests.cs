using Xunit;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests;

public sealed class DocumentFormatSelectorTests
{
    #region Variables and Constants

    private static readonly DocumentFormatId AlternateFormat =
        new(
            "selector-alternate");

    #endregion

    #region Methods Tests

    [Fact]
    public void ctor_RejectsEmptyRegistration()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentFormatSelector(
                    Array.Empty<IDocumentFormat>()));
    }

    [Fact]
    public void ctor_RejectsDuplicateFormatIdentifiers()
    {
        var formats =
            new IDocumentFormat[]
            {
                new StubDocumentFormat(
                    DocumentFormatId.Pdf,
                    _ =>
                        new NativeEvidenceExtractionResult
                            .NotRecognized()),
                new StubDocumentFormat(
                    DocumentFormatId.Pdf,
                    _ =>
                        new NativeEvidenceExtractionResult
                            .NotRecognized())
            };

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentFormatSelector(
                    formats));
    }

    [Fact]
    public async Task SelectAsync_NoRecognitionClaims_ReturnsNotRecognized()
    {
        var formats =
            new IDocumentFormat[]
            {
                new StubDocumentFormat(
                    DocumentFormatId.Pdf,
                    ReadPrefixThen(
                        new NativeEvidenceExtractionResult
                            .NotRecognized())),
                new StubDocumentFormat(
                    AlternateFormat,
                    ReadPrefixThen(
                        new NativeEvidenceExtractionResult
                            .NotRecognized()))
            };

        await using var prepared =
            await PreparedDocumentSource.CreateAsync(
                new DocumentSource(
                    new MemoryStream(
                        "selector fixture"u8.ToArray())),
                CancellationToken.None);

        var result =
            await new DocumentFormatSelector(
                    formats)
                .SelectAsync(
                    prepared);

        Assert.IsType<
            DocumentFormatSelectionResult.NotRecognized>(
            result);

        Assert.All(
            formats.Cast<StubDocumentFormat>(),
            format =>
                Assert.Equal(
                    new long[]
                    {
                        0
                    },
                    format.StartPositions));

        Assert.Equal(
            0,
            prepared.Source.Content.Position);
    }

    [Fact]
    public async Task SelectAsync_NotRecognizedThenSuccess_SelectsSingleRecognizedFormat()
    {
        var first =
            new StubDocumentFormat(
                DocumentFormatId.Pdf,
                ReadPrefixThen(
                    new NativeEvidenceExtractionResult
                        .NotRecognized()));

        var selectedEvidence =
            CreateEvidence(
                AlternateFormat);

        var second =
            new StubDocumentFormat(
                AlternateFormat,
                ReadPrefixThen(
                    new NativeEvidenceExtractionResult
                        .Success(
                            selectedEvidence)));

        await using var prepared =
            await PreparedDocumentSource.CreateAsync(
                new DocumentSource(
                    new MemoryStream(
                        "selector fixture"u8.ToArray())),
                CancellationToken.None);

        var result =
            await new DocumentFormatSelector(
                    new IDocumentFormat[]
                    {
                        first,
                        second
                    })
                .SelectAsync(
                    prepared);

        var success =
            Assert.IsType<
                DocumentFormatSelectionResult.Success>(
                result);

        Assert.Same(
            second,
            success.DocumentFormat);

        Assert.Same(
            selectedEvidence,
            success.Evidence);

        Assert.Equal(
            new long[]
            {
                0
            },
            first.StartPositions);

        Assert.Equal(
            new long[]
            {
                0
            },
            second.StartPositions);

        Assert.Equal(
            0,
            prepared.Source.Content.Position);
    }

    [Fact]
    public async Task SelectAsync_SingleInvalidClaim_ReturnsInvalid()
    {
        var invalidFormat =
            new StubDocumentFormat(
                DocumentFormatId.Pdf,
                ReadPrefixThen(
                    new NativeEvidenceExtractionResult
                        .Invalid(
                            "recognized but malformed")));

        var other =
            new StubDocumentFormat(
                AlternateFormat,
                ReadPrefixThen(
                    new NativeEvidenceExtractionResult
                        .NotRecognized()));

        await using var prepared =
            await PreparedDocumentSource.CreateAsync(
                new DocumentSource(
                    new MemoryStream(
                        "selector fixture"u8.ToArray())),
                CancellationToken.None);

        var result =
            await new DocumentFormatSelector(
                    new IDocumentFormat[]
                    {
                        invalidFormat,
                        other
                    })
                .SelectAsync(
                    prepared);

        var invalid =
            Assert.IsType<
                DocumentFormatSelectionResult.Invalid>(
                result);

        Assert.Same(
            invalidFormat,
            invalid.DocumentFormat);

        Assert.Equal(
            "recognized but malformed",
            invalid.Reason);

        Assert.Equal(
            0,
            prepared.Source.Content.Position);
    }

    [Fact]
    public async Task SelectAsync_TwoRecognitionClaims_ReturnsAmbiguousIndependentOfClaimKind()
    {
        var successFormat =
            new StubDocumentFormat(
                DocumentFormatId.Pdf,
                ReadPrefixThen(
                    new NativeEvidenceExtractionResult
                        .Success(
                            CreateEvidence(
                                DocumentFormatId.Pdf))));

        var invalidFormat =
            new StubDocumentFormat(
                AlternateFormat,
                ReadPrefixThen(
                    new NativeEvidenceExtractionResult
                        .Invalid(
                            "also recognized")));

        await using var prepared =
            await PreparedDocumentSource.CreateAsync(
                new DocumentSource(
                    new MemoryStream(
                        "selector fixture"u8.ToArray())),
                CancellationToken.None);

        var result =
            await new DocumentFormatSelector(
                    new IDocumentFormat[]
                    {
                        invalidFormat,
                        successFormat
                    })
                .SelectAsync(
                    prepared);

        var ambiguous =
            Assert.IsType<
                DocumentFormatSelectionResult.Ambiguous>(
                result);

        Assert.Equal(
            new[]
            {
                DocumentFormatId.Pdf,
                AlternateFormat
            }
                .OrderBy(
                    format =>
                        format.Value,
                    StringComparer.Ordinal),
            ambiguous.Formats);

        Assert.Equal(
            0,
            prepared.Source.Content.Position);
    }

    [Fact]
    public async Task SelectAsync_NonSeekableSource_IsPreparedOnceAndReplayedFromZero()
    {
        var bytes =
            "non-seekable selector fixture"u8.ToArray();

        await using var original =
            new NonSeekableReadStream(
                bytes);

        await using var prepared =
            await PreparedDocumentSource.CreateAsync(
                new DocumentSource(
                    original,
                    "fixture.bin",
                    "application/octet-stream"),
                CancellationToken.None);

        var first =
            new StubDocumentFormat(
                DocumentFormatId.Pdf,
                ReadPrefixThen(
                    new NativeEvidenceExtractionResult
                        .NotRecognized()));

        var secondEvidence =
            CreateEvidence(
                AlternateFormat);

        var second =
            new StubDocumentFormat(
                AlternateFormat,
                ReadPrefixThen(
                    new NativeEvidenceExtractionResult
                        .Success(
                            secondEvidence)));

        var result =
            await new DocumentFormatSelector(
                    new IDocumentFormat[]
                    {
                        first,
                        second
                    })
                .SelectAsync(
                    prepared);

        Assert.IsType<
            DocumentFormatSelectionResult.Success>(
            result);

        Assert.Equal(
            bytes.Length,
            original.BytesRead);

        Assert.Equal(
            new long[]
            {
                0
            },
            first.StartPositions);

        Assert.Equal(
            new long[]
            {
                0
            },
            second.StartPositions);

        Assert.Equal(
            0,
            prepared.Source.Content.Position);
    }

    [Fact]
    public async Task SelectAsync_PropagatesTechnicalFailureAndResetsPreparedSource()
    {
        var throwing =
            new StubDocumentFormat(
                DocumentFormatId.Pdf,
                source =>
                {
                    _ =
                        source.Content.ReadByte();

                    throw new IOException(
                        "technical failure");
                });

        await using var prepared =
            await PreparedDocumentSource.CreateAsync(
                new DocumentSource(
                    new MemoryStream(
                        "selector fixture"u8.ToArray())),
                CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(
            async () =>
                await new DocumentFormatSelector(
                        new[]
                        {
                            throwing
                        })
                    .SelectAsync(
                        prepared));

        Assert.Equal(
            0,
            prepared.Source.Content.Position);
    }

    #endregion

    #region Methods Fixtures

    private static Func<
        DocumentSource,
        NativeEvidenceExtractionResult>
        ReadPrefixThen(
            NativeEvidenceExtractionResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        return source =>
        {
            var buffer =
                new byte[3];

            _ =
                source.Content.Read(
                    buffer,
                    0,
                    buffer.Length);

            return result;
        };
    }

    private static NativeDocumentEvidence CreateEvidence(
        DocumentFormatId format)
    {
        var extraction =
            new DocumentExtractionResult(
                format);

        var current =
            new DocumentExtractionWithRasterObservationsResult(
                extraction,
                Array.Empty<PageVisualRasterObservations>(),
                rasterObservationFailure:
                    null);

        return new NativeDocumentEvidence(
            current);
    }

    #endregion

    #region Types

    private sealed class StubDocumentFormat
        : IDocumentFormat
    {
        #region Variables and Constants

        private readonly Func<
            DocumentSource,
            NativeEvidenceExtractionResult>
            _handler;

        private readonly List<long>
            _startPositions =
                new();

        #endregion

        #region ctor

        public StubDocumentFormat(
            DocumentFormatId format,
            Func<
                DocumentSource,
                NativeEvidenceExtractionResult>
                handler)
        {
            Format =
                format;

            _handler =
                handler ??
                throw new ArgumentNullException(
                    nameof(handler));
        }

        #endregion

        #region Properties

        public DocumentFormatId Format { get; }

        public IReadOnlyList<long> StartPositions =>
            _startPositions;

        #endregion

        #region Methods Acquisition

        public ValueTask<NativeEvidenceExtractionResult>
            TryExtractNativeEvidenceAsync(
                DocumentSource source,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            _startPositions.Add(
                source.Content.Position);

            return ValueTask.FromResult(
                _handler(
                    source));
        }

        #endregion
    }

    private sealed class NonSeekableReadStream
        : Stream
    {
        #region Variables and Constants

        private readonly MemoryStream
            _inner;

        #endregion

        #region ctor

        public NonSeekableReadStream(
            byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(
                bytes);

            _inner =
                new MemoryStream(
                    bytes,
                    writable:
                        false);
        }

        #endregion

        #region Properties

        public long BytesRead { get; private set; }

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
            int count)
        {
            var read =
                _inner.Read(
                    buffer,
                    offset,
                    count);

            BytesRead +=
                read;

            return read;
        }

        public override int Read(
            Span<byte> buffer)
        {
            var read =
                _inner.Read(
                    buffer);

            BytesRead +=
                read;

            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read =
                await _inner
                    .ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);

            BytesRead +=
                read;

            return read;
        }

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
