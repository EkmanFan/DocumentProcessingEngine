using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Submissions;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class SubmitDocumentServiceTests
{
    #region Tests

    [Fact]
    public async Task SubmitAsync_PreservesExactContentAndShelvesWholeDocumentByDefault()
    {
        var content =
            new byte[]
            {
                0,
                1,
                2,
                255
            };

        var artifact =
            new SourceArtifact(
                new Sha256Digest(
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                content.LongLength);

        var artifactWriter =
            new RecordingArtifactWriter(
                artifact);

        var submissionWriter =
            new RecordingSubmissionWriter();

        var submittedAtUtc =
            new DateTimeOffset(
                2026,
                8,
                27,
                10,
                30,
                0,
                TimeSpan.Zero);

        var submissionId =
            DocumentSubmissionId.New();

        await using var stream =
            new MemoryStream(
                content,
                writable:
                    false);

        var result =
            await new SubmitDocumentService(
                    artifactWriter,
                    submissionWriter,
                    new FixedTimeProvider(
                        submittedAtUtc))
                .SubmitAsync(
                    new SubmitDocumentCommand(
                        submissionId,
                        stream,
                        "/untrusted/path/book.pdf",
                        "application/pdf",
                        "manual import"));

        Assert.Equal(
            content,
            artifactWriter.StoredContent);

        var submission =
            Assert.IsType<DocumentSubmission>(
                submissionWriter.Submission);

        Assert.Equal(
            submissionId,
            submission.SubmissionId);

        Assert.Equal(
            artifact,
            submission.SourceArtifact);

        Assert.Equal(
            "book.pdf",
            submission.OriginalFileName);

        Assert.Equal(
            "application/pdf",
            submission.DeclaredMediaType);

        Assert.Equal(
            "manual import",
            submission.SourceOrigin);

        Assert.Equal(
            submittedAtUtc,
            submission.SubmittedAtUtc);

        var workItem =
            Assert.Single(
                    submissionWriter.ProcessingUnits)
                .WorkItem;

        Assert.Equal(
            ProcessingUnitDispatchState.Shelved,
            Assert.Single(
                    submissionWriter.ProcessingUnits)
                .DispatchState);

        Assert.Equal(
            submissionId,
            workItem.SubmissionId);

        Assert.IsType<ProcessingUnitScope.WholeDocument>(
            workItem.Scope);

        Assert.Equal(
            1,
            workItem.AttemptNumber);

        Assert.True(
            result.Created);

        Assert.Equal(
            workItem.UnitId,
            Assert.Single(
                result.ProcessingUnitIds));
    }

    [Fact]
    public async Task SubmitAsync_PreservesExplicitReadyDispatchIntent()
    {
        var artifact =
            new SourceArtifact(
                new Sha256Digest(
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                byteLength:
                    1);

        var submissionWriter =
            new RecordingSubmissionWriter();

        await using var content =
            new MemoryStream(
                [1],
                writable:
                    false);

        await new SubmitDocumentService(
                new RecordingArtifactWriter(
                    artifact),
                submissionWriter)
            .SubmitAsync(
                new SubmitDocumentCommand(
                    DocumentSubmissionId.New(),
                    content,
                    "book.pdf",
                    initialDispatchState:
                        ProcessingUnitDispatchState.Ready));

        Assert.Equal(
            ProcessingUnitDispatchState.Ready,
            Assert.Single(
                    submissionWriter.ProcessingUnits)
                .DispatchState);
    }

    [Fact]
    public async Task SubmitAsync_RegistersOrderedPageRangesAgainstOneSubmission()
    {
        var artifact =
            new SourceArtifact(
                new Sha256Digest(
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                byteLength:
                    1);

        var submissionWriter =
            new RecordingSubmissionWriter();

        await using var content =
            new MemoryStream(
                [1],
                writable:
                    false);

        await new SubmitDocumentService(
                new RecordingArtifactWriter(
                    artifact),
                submissionWriter)
            .SubmitAsync(
                new SubmitDocumentCommand(
                    DocumentSubmissionId.New(),
                    content,
                    "book.pdf",
                    scopes:
                    [
                        new ProcessingUnitScope.PageRange(1, 10, "Chapter one"),
                        new ProcessingUnitScope.PageRange(11, 20, "Chapter two")
                    ]));

        Assert.Collection(
            submissionWriter.ProcessingUnits,
            intake =>
            {
                var range =
                    Assert.IsType<ProcessingUnitScope.PageRange>(
                        intake.WorkItem.Scope);

                Assert.Equal(
                    "Chapter one",
                    range.Title);
            },
            intake =>
            {
                var range =
                    Assert.IsType<ProcessingUnitScope.PageRange>(
                        intake.WorkItem.Scope);

                Assert.Equal(
                    "Chapter two",
                    range.Title);
            });

        Assert.Single(
            submissionWriter.ProcessingUnits
                .Select(intake => intake.WorkItem.SubmissionId)
                .Distinct());
    }

    [Fact]
    public void Command_RejectsOverlappingPageRanges()
    {
        using var content =
            new MemoryStream(
                [1],
                writable:
                    false);

        Assert.Throws<ArgumentException>(
            () =>
                new SubmitDocumentCommand(
                    DocumentSubmissionId.New(),
                    content,
                    "book.pdf",
                    scopes:
                    [
                        new ProcessingUnitScope.PageRange(1, 10, "First"),
                        new ProcessingUnitScope.PageRange(10, 20, "Second")
                    ]));
    }

    [Fact]
    public void Command_AcceptsOrderedNonOverlappingContentUnitRanges()
    {
        using var content =
            new MemoryStream(
                [1],
                writable:
                    false);

        var ranges =
            new ProcessingUnitScope[]
            {
                new ProcessingUnitScope.ContentUnitRange(
                    0,
                    "OPS/front.xhtml",
                    1,
                    "OPS/chapter1.xhtml",
                    "Front and chapter one"),
                new ProcessingUnitScope.ContentUnitRange(
                    2,
                    "OPS/chapter2.xhtml",
                    3,
                    "OPS/chapter3.xhtml",
                    "Chapters two and three")
            };

        var command =
            new SubmitDocumentCommand(
                DocumentSubmissionId.New(),
                content,
                "book.epub",
                scopes:
                    ranges);

        Assert.Equal(
            ranges,
            command.Scopes);
    }

    [Fact]
    public void Command_RejectsOverlappingContentUnitRanges()
    {
        using var content =
            new MemoryStream(
                [1],
                writable:
                    false);

        Assert.Throws<ArgumentException>(
            () =>
                new SubmitDocumentCommand(
                    DocumentSubmissionId.New(),
                    content,
                    "book.epub",
                    scopes:
                    [
                        new ProcessingUnitScope.ContentUnitRange(
                            0,
                            "OPS/front.xhtml",
                            1,
                            "OPS/chapter1.xhtml",
                            "First"),
                        new ProcessingUnitScope.ContentUnitRange(
                            1,
                            "OPS/chapter1.xhtml",
                            2,
                            "OPS/chapter2.xhtml",
                            "Second")
                    ]));
    }

    [Fact]
    public void Contracts_RejectInvalidCustodyMetadata()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new Sha256Digest(
                    "not-a-digest"));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new SourceArtifact(
                    new Sha256Digest(
                        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                    byteLength:
                        0));

        Assert.Throws<ArgumentException>(
            () =>
                new DocumentSubmission(
                    DocumentSubmissionId.New(),
                    new SourceArtifact(
                        new Sha256Digest(
                            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                        byteLength:
                            1),
                    "../",
                    declaredMediaType:
                        null,
                    sourceOrigin:
                        null,
                    DateTimeOffset.UtcNow));
    }

    #endregion

    #region Test Doubles

    private sealed class RecordingArtifactWriter(
        SourceArtifact artifact)
        : ISourceArtifactWriter
    {
        public byte[] StoredContent
        {
            get;
            private set;
        } =
            [];

        public async ValueTask<SourceArtifact> StoreAsync(
            Stream content,
            CancellationToken cancellationToken = default)
        {
            await using var captured =
                new MemoryStream();

            await content.CopyToAsync(
                captured,
                cancellationToken);

            StoredContent =
                captured.ToArray();

            return artifact;
        }
    }

    private sealed class RecordingSubmissionWriter
        : IDocumentSubmissionWriter
    {
        public DocumentSubmission? Submission
        {
            get;
            private set;
        }

        public IReadOnlyList<ProcessingUnitIntake> ProcessingUnits
        {
            get;
            private set;
        } =
            [];

        public ValueTask<DocumentSubmissionRegistration>
            RegisterAsync(
            DocumentSubmission submission,
            IReadOnlyCollection<ProcessingUnitIntake> processingUnits,
            CancellationToken cancellationToken = default)
        {
            Submission =
                submission;

            ProcessingUnits =
                processingUnits.ToArray();

            return ValueTask.FromResult(
                new DocumentSubmissionRegistration(
                    submission,
                    ProcessingUnits.Select(
                        unit =>
                            unit.WorkItem.UnitId),
                    created:
                        true));
        }
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
    }

    #endregion
}
