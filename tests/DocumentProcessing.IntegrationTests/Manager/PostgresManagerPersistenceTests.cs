using System.Security.Cryptography;
using System.Text.Json;
using DocumentProcessing.Layout.Adapters.PpStructureV3;
using DocumentProcessing.Manager.Control;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.DPEngine;
using DocumentProcessing.Manager.Persistence.Files;
using DocumentProcessing.Manager.Persistence.Postgres;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Results;
using DocumentProcessing.Manager.Runtime;
using DocumentProcessing.Manager.Submissions;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class PostgresManagerPersistenceTests
{
    #region Variables and Constants

    internal const string
        ConnectionStringEnvironmentVariable =
            "DOCUMENT_PROCESSING_MANAGER_POSTGRES_CONNECTION_STRING";

    #endregion

    #region Tests

    [PostgresFact]
    public async Task SchemaAndStateStore_AreIdempotentAndVersioned()
    {
        await using var context =
            await CreateContextAsync();

        await context.Schema.InitializeAsync();

        var initial =
            await context.StateStore.GetAsync();

        Assert.Equal(
            ManagerOperatingState.Stopped,
            initial.State);

        Assert.Equal(
            0,
            initial.Version);

        var changed =
            await context.StateStore.TrySetAsync(
                expectedVersion:
                    0,
                ManagerOperatingState.Running);

        Assert.NotNull(
            changed);

        Assert.Equal(
            1,
            changed.Version);

        var stale =
            await context.StateStore.TrySetAsync(
                expectedVersion:
                    0,
                ManagerOperatingState.Paused);

        Assert.Null(
            stale);

        await using var versionCommand =
            context.DataSource.CreateCommand(
                "SELECT MAX(version) FROM document_processing_manager.schema_versions;");

        Assert.Equal(
            3,
            Convert.ToInt32(
                await versionCommand.ExecuteScalarAsync()));
    }

    [PostgresFact]
    public async Task ProcessingResultRegistry_RegistersReadsAndReplaysIdempotently()
    {
        await using var context =
            await CreateContextAsync();

        var workItem =
            CreateWorkItem();

        await InsertPendingAsync(
            context.DataSource,
            workItem,
            queuePosition:
                1);

        var result =
            CreateProcessingResult(
                workItem,
                digestCharacter:
                    '1');

        var created =
            await context.ResultRegistry.RegisterAsync(
                result);

        var replay =
            await context.ResultRegistry.RegisterAsync(
                CreateProcessingResult(
                    workItem,
                    digestCharacter:
                        '1',
                    resultReference:
                        "manager-result:retry",
                    producedAtUtc:
                        DateTimeOffset.UnixEpoch.AddDays(
                            1)));

        Assert.True(
            created.Created);

        Assert.False(
            replay.Created);

        Assert.Equal(
            result,
            replay.Result);

        Assert.Equal(
            result,
            await context.ResultRegistry.GetByUnitAsync(
                workItem.UnitId));

        Assert.Equal(
            result,
            await context.ResultRegistry.GetByReferenceAsync(
                result.ResultReference));
    }

    [PostgresFact]
    public async Task ProcessingResultRegistry_RejectsConflictingReplayAtomically()
    {
        await using var context =
            await CreateContextAsync();

        var workItem =
            CreateWorkItem();

        await InsertPendingAsync(
            context.DataSource,
            workItem,
            queuePosition:
                1);

        await context.ResultRegistry.RegisterAsync(
            CreateProcessingResult(
                workItem,
                digestCharacter:
                    '2'));

        await Assert.ThrowsAsync<ProcessingResultConflictException>(
            () =>
                context.ResultRegistry
                    .RegisterAsync(
                        CreateProcessingResult(
                            workItem,
                            digestCharacter:
                                '3'))
                    .AsTask());

        await using var countCommand =
            context.DataSource.CreateCommand(
                "SELECT COUNT(*) FROM document_processing_manager.processing_result_artifacts;");

        Assert.Equal(
            1L,
            Convert.ToInt64(
                await countCommand.ExecuteScalarAsync()));
    }

    [PostgresFact]
    public async Task ProcessingResultRegistry_ConcurrentEquivalentRegistrationHasSingleCreation()
    {
        await using var context =
            await CreateContextAsync();

        var workItem =
            CreateWorkItem();

        await InsertPendingAsync(
            context.DataSource,
            workItem,
            queuePosition:
                1);

        var registrations =
            Enumerable.Range(
                    0,
                    8)
                .Select(
                    index =>
                        context.ResultRegistry
                            .RegisterAsync(
                                CreateProcessingResult(
                                    workItem,
                                    digestCharacter:
                                        '4',
                                    resultReference:
                                        $"manager-result:concurrent-{index}"))
                            .AsTask())
                .ToArray();

        var results =
            await Task.WhenAll(
                registrations);

        Assert.Single(
            results,
            result =>
                result.Created);

        Assert.Single(
            results
                .Select(
                    result =>
                        result.Result.ResultReference)
                .Distinct(
                    StringComparer.Ordinal));
    }

    [PostgresFact]
    public async Task ManagedExecution_ProcessesOnlyWhitelistedHabermasPage()
    {
        await ExecuteManagedPageAsync(
            "habermas-p0079.pdf");
    }

    [PostgresFact]
    public async Task ManagedExecution_ProcessesOnlyWhitelistedDeCretisPage()
    {
        await ExecuteManagedPageAsync(
            "decretis-p0512.pdf");
    }

    [PostgresFact]
    public async Task SubmitDocument_PreservesSourceAndRegistersQueueIdempotently()
    {
        await using var context =
            await CreateContextAsync();

        var custodyRoot =
            CreateTemporaryCustodyRoot();

        try
        {
            var custodyStore =
                new FileSystemSourceArtifactCustodyStore(
                    new FileSystemSourceArtifactCustodyOptions(
                        custodyRoot,
                        maximumArtifactBytes:
                            1024 * 1024));

            var submittedAtUtc =
                new DateTimeOffset(
                    2026,
                    8,
                    27,
                    12,
                    0,
                    0,
                    TimeSpan.Zero);

            var service =
                new SubmitDocumentService(
                    custodyStore,
                    context.SubmissionStore,
                    new FixedTimeProvider(
                        submittedAtUtc));

            var submissionId =
                DocumentSubmissionId.New();

            var sourceBytes =
                "exact pdf bytes\0\u00ff"u8.ToArray();

            await using var firstSource =
                new MemoryStream(
                    sourceBytes,
                    writable:
                        false);

            var first =
                await service.SubmitAsync(
                    new SubmitDocumentCommand(
                        submissionId,
                        firstSource,
                        "/imports/book.pdf",
                        "application/pdf",
                        "manual fixture"));

            await using var retrySource =
                new MemoryStream(
                    sourceBytes,
                    writable:
                        false);

            var retry =
                await service.SubmitAsync(
                    new SubmitDocumentCommand(
                        submissionId,
                        retrySource,
                        "book.pdf",
                        "application/pdf",
                        "manual fixture"));

            Assert.True(
                first.Created);

            Assert.False(
                retry.Created);

            Assert.Equal(
                first.ProcessingUnitIds,
                retry.ProcessingUnitIds);

            var persisted =
                await context.SubmissionStore.GetAsync(
                    submissionId);

            Assert.Equal(
                first.Submission,
                persisted);

            Assert.Equal(
                submittedAtUtc,
                persisted?.SubmittedAtUtc);

            Assert.True(
                await custodyStore.VerifyAsync(
                    first.Submission.SourceArtifact));

            await using var retained =
                await custodyStore.OpenReadAsync(
                    first.Submission.SourceArtifact);

            await using var copied =
                new MemoryStream();

            await retained.CopyToAsync(
                copied);

            Assert.Equal(
                sourceBytes,
                copied.ToArray());

            Assert.Equal(
                new SubmissionCounts(
                    Artifacts:
                        1,
                    Submissions:
                        1,
                    Events:
                        1,
                    Units:
                        1,
                    QueueVersion:
                        1),
                await ReadSubmissionCountsAsync(
                    context.DataSource));
        }
        finally
        {
            DeleteTemporaryCustodyRoot(
                custodyRoot);
        }
    }

    [PostgresFact]
    public async Task SubmissionStore_ConcurrentEquivalentRegistrationHasSingleCreation()
    {
        await using var context =
            await CreateContextAsync();

        var submission =
            CreateSubmission(
                DocumentSubmissionId.New(),
                digestCharacter:
                    'a');

        var registrations =
            Enumerable.Range(
                    0,
                    8)
                .Select(
                    _ =>
                        context.SubmissionStore
                            .RegisterAndEnqueueAsync(
                                submission,
                                [
                                    new ProcessingWorkItem(
                                        ProcessingUnitId.New(),
                                        submission.SubmissionId,
                                        new ProcessingUnitScope.WholeDocument(),
                                        attemptNumber:
                                            1)
                                ])
                            .AsTask())
                .ToArray();

        var results =
            await Task.WhenAll(
                registrations);

        Assert.Single(
            results,
            result =>
                result.Created);

        Assert.Single(
            results
                .Select(
                    result =>
                        Assert.Single(
                            result.ProcessingUnitIds))
                .Distinct());

        Assert.Equal(
            new SubmissionCounts(
                Artifacts:
                    1,
                Submissions:
                    1,
                Events:
                    1,
                Units:
                    1,
                QueueVersion:
                    1),
            await ReadSubmissionCountsAsync(
                context.DataSource));
    }

    [PostgresFact]
    public async Task SubmissionStore_IdempotentReplayPreservesInitialBatchOrder()
    {
        await using var context =
            await CreateContextAsync();

        var submission =
            CreateSubmission(
                DocumentSubmissionId.New(),
                digestCharacter:
                    '8');

        var scopes =
            new ProcessingUnitScope[]
            {
                new ProcessingUnitScope.PageRange(
                    startPhysicalPageNumber:
                        1,
                    endPhysicalPageNumber:
                        10,
                    title:
                        "Chapter one"),
                new ProcessingUnitScope.PageRange(
                    startPhysicalPageNumber:
                        11,
                    endPhysicalPageNumber:
                        20,
                    title:
                        "Chapter two"),
                new ProcessingUnitScope.PageRange(
                    startPhysicalPageNumber:
                        21,
                    endPhysicalPageNumber:
                        30,
                    title:
                        "Chapter three")
            };

        var initialUnits =
            scopes
                .Select(
                    scope =>
                        new ProcessingWorkItem(
                            ProcessingUnitId.New(),
                            submission.SubmissionId,
                            scope,
                            attemptNumber:
                                1))
                .ToArray();

        var created =
            await context.SubmissionStore.RegisterAndEnqueueAsync(
                submission,
                initialUnits);

        await context.QueueStore.ReorderPendingAsync(
            new ReorderProcessingQueueCommand(
                initialUnits
                    .Reverse()
                    .Select(
                        unit =>
                            unit.UnitId),
                expectedQueueVersion:
                    1));

        var replayUnits =
            scopes
                .Select(
                    scope =>
                        new ProcessingWorkItem(
                            ProcessingUnitId.New(),
                            submission.SubmissionId,
                            scope,
                            attemptNumber:
                                1))
                .ToArray();

        var replay =
            await context.SubmissionStore.RegisterAndEnqueueAsync(
                submission,
                replayUnits);

        Assert.False(
            replay.Created);

        Assert.Equal(
            created.ProcessingUnitIds,
            replay.ProcessingUnitIds);

        var conflictingPlan =
            new ProcessingWorkItem[]
            {
                replayUnits[0],
                replayUnits[1],
                new(
                    ProcessingUnitId.New(),
                    submission.SubmissionId,
                    new ProcessingUnitScope.PageRange(
                        startPhysicalPageNumber:
                            21,
                        endPhysicalPageNumber:
                            31,
                        title:
                            "Changed chapter three"),
                    attemptNumber:
                        1)
            };

        await Assert.ThrowsAsync<DocumentSubmissionConflictException>(
            () =>
                context.SubmissionStore
                    .RegisterAndEnqueueAsync(
                        submission,
                        conflictingPlan)
                    .AsTask());

        Assert.Equal(
            new SubmissionCounts(
                Artifacts:
                    1,
                Submissions:
                    1,
                Events:
                    1,
                Units:
                    3,
                QueueVersion:
                    2),
            await ReadSubmissionCountsAsync(
                context.DataSource));
    }

    [PostgresFact]
    public async Task SubmissionStore_RejectsIdempotencyConflict()
    {
        await using var context =
            await CreateContextAsync();

        var submissionId =
            DocumentSubmissionId.New();

        var original =
            CreateSubmission(
                submissionId,
                digestCharacter:
                    'b');

        var unit =
            new ProcessingWorkItem(
                ProcessingUnitId.New(),
                submissionId,
                new ProcessingUnitScope.WholeDocument(),
                attemptNumber:
                    1);

        await context.SubmissionStore.RegisterAndEnqueueAsync(
            original,
            [unit]);

        var conflicting =
            CreateSubmission(
                submissionId,
                digestCharacter:
                    'c');

        var exception =
            await Assert.ThrowsAsync<DocumentSubmissionConflictException>(
                () =>
                    context.SubmissionStore
                        .RegisterAndEnqueueAsync(
                            conflicting,
                            [
                                new ProcessingWorkItem(
                                    ProcessingUnitId.New(),
                                    submissionId,
                                    new ProcessingUnitScope.WholeDocument(),
                                    attemptNumber:
                                        1)
                            ])
                        .AsTask());

        Assert.Equal(
            submissionId,
            exception.SubmissionId);

        Assert.Equal(
            new SubmissionCounts(
                Artifacts:
                    1,
                Submissions:
                    1,
                Events:
                    1,
                Units:
                    1,
                QueueVersion:
                    1),
            await ReadSubmissionCountsAsync(
                context.DataSource));
    }

    [PostgresFact]
    public async Task SubmissionStore_RollsBackManifestWhenInitialEnqueueFails()
    {
        await using var context =
            await CreateContextAsync();

        var first =
            CreateSubmission(
                DocumentSubmissionId.New(),
                digestCharacter:
                    'd');

        var sharedUnitId =
            ProcessingUnitId.New();

        await context.SubmissionStore.RegisterAndEnqueueAsync(
            first,
            [
                new ProcessingWorkItem(
                    sharedUnitId,
                    first.SubmissionId,
                    new ProcessingUnitScope.WholeDocument(),
                    attemptNumber:
                        1)
            ]);

        var second =
            CreateSubmission(
                DocumentSubmissionId.New(),
                digestCharacter:
                    'e');

        var exception =
            await Assert.ThrowsAsync<PostgresException>(
                () =>
                    context.SubmissionStore
                        .RegisterAndEnqueueAsync(
                            second,
                            [
                                new ProcessingWorkItem(
                                    sharedUnitId,
                                    second.SubmissionId,
                                    new ProcessingUnitScope.WholeDocument(),
                                    attemptNumber:
                                        1)
                            ])
                        .AsTask());

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            exception.SqlState);

        Assert.Null(
            await context.SubmissionStore.GetAsync(
                second.SubmissionId));

        Assert.Equal(
            new SubmissionCounts(
                Artifacts:
                    1,
                Submissions:
                    1,
                Events:
                    1,
                Units:
                    1,
                QueueVersion:
                    1),
            await ReadSubmissionCountsAsync(
                context.DataSource));
    }

    [PostgresFact]
    public async Task ImmutableSubmissionRecords_RejectMutation()
    {
        await using var context =
            await CreateContextAsync();

        var submission =
            CreateSubmission(
                DocumentSubmissionId.New(),
                digestCharacter:
                    'f');

        await context.SubmissionStore.RegisterAndEnqueueAsync(
            submission,
            [
                new ProcessingWorkItem(
                    ProcessingUnitId.New(),
                    submission.SubmissionId,
                    new ProcessingUnitScope.WholeDocument(),
                    attemptNumber:
                        1)
            ]);

        var mutations =
            new[]
            {
                """
                UPDATE document_processing_manager.source_artifacts
                SET byte_length = byte_length
                WHERE sha256_digest =
                    (SELECT source_sha256_digest
                     FROM document_processing_manager.document_submissions
                     WHERE submission_id = @submission_id);
                """,
                """
                UPDATE document_processing_manager.document_submissions
                SET original_file_name = original_file_name
                WHERE submission_id = @submission_id;
                """,
                """
                UPDATE document_processing_manager.custody_events
                SET occurred_at_utc = occurred_at_utc
                WHERE submission_id = @submission_id;
                """,
                """
                UPDATE document_processing_manager.processing_units
                SET submission_unit_ordinal = submission_unit_ordinal + 1
                WHERE submission_id = @submission_id;
                """
            };

        foreach (var mutation in mutations)
        {
            await using var command =
                context.DataSource.CreateCommand(
                    mutation);

            command.Parameters.AddWithValue(
                "submission_id",
                NpgsqlDbType.Uuid,
                submission.SubmissionId.Value);

            var exception =
                await Assert.ThrowsAsync<PostgresException>(
                    () =>
                        command.ExecuteNonQueryAsync());

            Assert.Equal(
                PostgresErrorCodes.RaiseException,
                exception.SqlState);
        }
    }

    [PostgresFact]
    public async Task CustodySchema_RejectsEventForDifferentSource()
    {
        await using var context =
            await CreateContextAsync();

        var submission =
            CreateSubmission(
                DocumentSubmissionId.New(),
                digestCharacter:
                    '9');

        await context.SubmissionStore.RegisterAndEnqueueAsync(
            submission,
            [
                new ProcessingWorkItem(
                    ProcessingUnitId.New(),
                    submission.SubmissionId,
                    new ProcessingUnitScope.WholeDocument(),
                    attemptNumber:
                        1)
            ]);

        await using var command =
            context.DataSource.CreateCommand(
                """
                INSERT INTO document_processing_manager.custody_events
                (
                    submission_id,
                    event_kind,
                    source_sha256_digest,
                    occurred_at_utc
                )
                VALUES
                (
                    @submission_id,
                    0,
                    @wrong_source_sha256_digest,
                    @occurred_at_utc
                );
                """);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            submission.SubmissionId.Value);

        command.Parameters.AddWithValue(
            "wrong_source_sha256_digest",
            NpgsqlDbType.Text,
            new string(
                '0',
                count:
                    64));

        command.Parameters.AddWithValue(
            "occurred_at_utc",
            NpgsqlDbType.TimestampTz,
            DateTimeOffset.UnixEpoch);

        var exception =
            await Assert.ThrowsAsync<PostgresException>(
                () =>
                    command.ExecuteNonQueryAsync());

        Assert.Equal(
            PostgresErrorCodes.ForeignKeyViolation,
            exception.SqlState);
    }

    [PostgresFact]
    public async Task RuntimeLeaseStore_AllowsOnlyCurrentFencedOwner()
    {
        await using var context =
            await CreateContextAsync();

        var observedAtUtc =
            DateTimeOffset.UnixEpoch;

        var first =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-one",
                observedAtUtc,
                observedAtUtc.AddMinutes(
                    5));

        Assert.NotNull(
            first);

        Assert.InRange(
            first.ExpiresAtUtc,
            DateTimeOffset.UtcNow.AddMinutes(
                4),
            DateTimeOffset.UtcNow.AddMinutes(
                6));

        var competing =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-two",
                observedAtUtc,
                observedAtUtc.AddMinutes(
                    5));

        Assert.Null(
            competing);

        var stale =
            new ManagerRuntimeLease(
                Guid.NewGuid(),
                first.WorkerId,
                first.ExpiresAtUtc);

        Assert.False(
            await context.RuntimeLeaseStore.RenewAsync(
                stale,
                observedAtUtc,
                observedAtUtc.AddMinutes(
                    5)));

        Assert.False(
            await context.RuntimeLeaseStore.ReleaseAsync(
                stale,
                observedAtUtc));

        Assert.True(
            await context.RuntimeLeaseStore.RenewAsync(
                first,
                observedAtUtc,
                observedAtUtc.AddMinutes(
                    5)));

        Assert.True(
            await context.RuntimeLeaseStore.ReleaseAsync(
                first,
                observedAtUtc));
    }

    [PostgresFact]
    public async Task RuntimeLeaseStore_ConcurrentAcquisitionHasSingleWinner()
    {
        await using var context =
            await CreateContextAsync();

        var now =
            DateTimeOffset.UtcNow;

        var acquisitions =
            Enumerable.Range(
                    1,
                    12)
                .Select(
                    workerNumber =>
                        context.RuntimeLeaseStore
                            .TryAcquireAsync(
                                $"worker-{workerNumber}",
                                now,
                                now.AddMinutes(
                                    5))
                            .AsTask())
                .ToArray();

        var results =
            await Task.WhenAll(
                acquisitions);

        Assert.Single(
            results,
            lease =>
                lease is not null);
    }

    [PostgresFact]
    public async Task QueueStore_FencesStaleOwnerAndRecoversExpiredUnit()
    {
        await using var context =
            await CreateContextAsync();

        var workItem =
            CreateWorkItem();

        await InsertPendingAsync(
            context.DataSource,
            workItem,
            queuePosition:
                1);

        var now =
            DateTimeOffset.UtcNow;

        var firstRuntime =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-one",
                now,
                now.AddMinutes(
                    5));

        Assert.NotNull(
            firstRuntime);

        var claimed =
            await context.QueueStore.ClaimNextAsync(
                firstRuntime,
                firstRuntime.WorkerId,
                now,
                now.AddMilliseconds(
                    100));

        Assert.NotNull(
            claimed);

        Assert.True(
            await context.RuntimeLeaseStore.ReleaseAsync(
                firstRuntime,
                now));

        var secondRuntime =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-two",
                now,
                now.AddMinutes(
                    5));

        Assert.NotNull(
            secondRuntime);

        Assert.False(
            await context.QueueStore.CompleteAsync(
                claimed,
                new ProcessingExecutionOutcome.Success(
                    "stale-result"),
                DateTimeOffset.UtcNow));

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                150));

        Assert.Equal(
            1,
            await context.QueueStore.RecoverExpiredLeasesAsync(
                DateTimeOffset.UtcNow));

        var recovered =
            await context.QueueStore.ClaimNextAsync(
                secondRuntime,
                secondRuntime.WorkerId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(
                    1));

        Assert.NotNull(
            recovered);

        Assert.Equal(
            workItem.UnitId,
            recovered.WorkItem.UnitId);

        Assert.True(
            await context.QueueStore.CompleteAsync(
                recovered,
                new ProcessingExecutionOutcome.Success(
                    "durable-result"),
                DateTimeOffset.UtcNow));
    }

    [PostgresFact]
    public async Task QueueStore_RetriesAndReordersWithOptimisticConcurrency()
    {
        await using var context =
            await CreateContextAsync();

        var first =
            CreateWorkItem();

        var second =
            CreateWorkItem(
                new ProcessingUnitScope.PageRange(
                    startPhysicalPageNumber:
                        10,
                    endPhysicalPageNumber:
                        20,
                    title:
                        "Chapter two"));

        await InsertPendingAsync(
            context.DataSource,
            first,
            queuePosition:
                1);

        await InsertPendingAsync(
            context.DataSource,
            second,
            queuePosition:
                2);

        await context.QueueStore.ReorderPendingAsync(
            new ReorderProcessingQueueCommand(
                [second.UnitId, first.UnitId],
                expectedQueueVersion:
                    0));

        Assert.Equal(
            [second.UnitId, first.UnitId],
            await ReadPendingOrderAsync(
                context.DataSource));

        var conflict =
            await Assert.ThrowsAsync<ProcessingQueueConcurrencyException>(
                () =>
                    context.QueueStore
                        .ReorderPendingAsync(
                            new ReorderProcessingQueueCommand(
                                [first.UnitId, second.UnitId],
                                expectedQueueVersion:
                                    0))
                        .AsTask());

        Assert.Equal(
            1,
            conflict.ActualVersion);

        var now =
            DateTimeOffset.UtcNow;

        var runtime =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-one",
                now,
                now.AddMinutes(
                    5));

        Assert.NotNull(
            runtime);

        var claimed =
            await context.QueueStore.ClaimNextAsync(
                runtime,
                runtime.WorkerId,
                now,
                now.AddMinutes(
                    1));

        Assert.NotNull(
            claimed);

        Assert.Equal(
            second.UnitId,
            claimed.WorkItem.UnitId);

        Assert.IsType<ProcessingUnitScope.PageRange>(
            claimed.WorkItem.Scope);

        Assert.True(
            await context.QueueStore.RequeueAfterFailureAsync(
                claimed,
                new ProcessingFailure(
                    "temporary",
                    "Temporary failure."),
                DateTimeOffset.UtcNow));

        var next =
            await context.QueueStore.ClaimNextAsync(
                runtime,
                runtime.WorkerId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(
                    1));

        Assert.NotNull(
            next);

        Assert.Equal(
            first.UnitId,
            next.WorkItem.UnitId);

        Assert.True(
            await context.QueueStore.InterruptAndRequeueAsync(
                next,
                ProcessingInterruptionReason.ManagerStop,
                DateTimeOffset.UtcNow));

        var interrupted =
            await context.QueueStore.ClaimNextAsync(
                runtime,
                runtime.WorkerId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(
                    1));

        Assert.NotNull(
            interrupted);

        Assert.Equal(
            first.UnitId,
            interrupted.WorkItem.UnitId);

        Assert.Equal(
            1,
            interrupted.WorkItem.AttemptNumber);

        Assert.True(
            await context.QueueStore.FailAsync(
                interrupted,
                new ProcessingFailure(
                    "terminal",
                    "Terminal failure."),
                DateTimeOffset.UtcNow));

        var retried =
            await context.QueueStore.ClaimNextAsync(
                runtime,
                runtime.WorkerId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(
                    1));

        Assert.NotNull(
            retried);

        Assert.Equal(
            second.UnitId,
            retried.WorkItem.UnitId);

        Assert.Equal(
            2,
            retried.WorkItem.AttemptNumber);
    }

    [PostgresFact]
    public async Task QueueReader_ReturnsConsistentVersionedDisplayOrder()
    {
        await using var context =
            await CreateContextAsync();

        var submission =
            CreateSubmission(
                DocumentSubmissionId.New(),
                digestCharacter:
                    '7');

        var first =
            new ProcessingWorkItem(
                ProcessingUnitId.New(),
                submission.SubmissionId,
                new ProcessingUnitScope.PageRange(
                    startPhysicalPageNumber:
                        1,
                    endPhysicalPageNumber:
                        9,
                    title:
                        "Chapter one"),
                attemptNumber:
                    1);

        var second =
            new ProcessingWorkItem(
                ProcessingUnitId.New(),
                submission.SubmissionId,
                new ProcessingUnitScope.PageRange(
                    startPhysicalPageNumber:
                        10,
                    endPhysicalPageNumber:
                        20,
                    title:
                        "Chapter two"),
                attemptNumber:
                    1);

        await context.SubmissionStore.RegisterAndEnqueueAsync(
            submission,
            [first, second]);

        var initial =
            await context.QueueReader.GetSnapshotAsync();

        Assert.Equal(
            1,
            initial.Version);

        Assert.Equal(
            [first.UnitId, second.UnitId],
            initial.Items
                .Select(
                    item =>
                        item.WorkItem.UnitId));

        Assert.All(
            initial.Items,
            item =>
            {
                Assert.Equal(
                    ProcessingUnitStatus.Pending,
                    item.Status);

                Assert.Equal(
                    submission.OriginalFileName,
                    item.OriginalFileName);
            });

        await context.QueueStore.ReorderPendingAsync(
            new ReorderProcessingQueueCommand(
                [second.UnitId, first.UnitId],
                initial.Version));

        var reordered =
            await context.QueueReader.GetSnapshotAsync();

        Assert.Equal(
            2,
            reordered.Version);

        Assert.Equal(
            [second.UnitId, first.UnitId],
            reordered.Items
                .Select(
                    item =>
                        item.WorkItem.UnitId));

        var pageRange =
            Assert.IsType<ProcessingUnitScope.PageRange>(
                reordered.Items[0].WorkItem.Scope);

        Assert.Equal(
            "Chapter two",
            pageRange.Title);
    }

    [PostgresFact]
    public async Task QueueStore_RecoversExpiredUnitsInExpiryOrder()
    {
        await using var context =
            await CreateContextAsync();

        var earliestExpired =
            CreateWorkItem();

        var latestExpired =
            CreateWorkItem();

        var pending =
            CreateWorkItem();

        var now =
            DateTimeOffset.UtcNow;

        await InsertExpiredActiveAsync(
            context.DataSource,
            earliestExpired,
            now.AddMinutes(
                -2));

        await InsertExpiredActiveAsync(
            context.DataSource,
            latestExpired,
            now.AddMinutes(
                -1));

        await InsertPendingAsync(
            context.DataSource,
            pending,
            queuePosition:
                10);

        Assert.Equal(
            2,
            await context.QueueStore.RecoverExpiredLeasesAsync(
                now));

        Assert.Equal(
            [earliestExpired.UnitId, latestExpired.UnitId, pending.UnitId],
            await ReadPendingOrderAsync(
                context.DataSource));
    }

    [PostgresFact]
    public async Task Runtime_StopRequeuesActivePostgresUnitDurably()
    {
        await using var context =
            await CreateContextAsync();

        var workItem =
            CreateWorkItem();

        await InsertPendingAsync(
            context.DataSource,
            workItem,
            queuePosition:
                1);

        var entered =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    entered.TrySetResult();

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "Unreachable.");
                });

        var workerId =
            "postgres-runtime-worker";

        var dispatcher =
            new SequentialProcessingDispatcher(
                context.QueueStore,
                executor,
                new SequentialProcessingDispatcherOptions(
                    workerId,
                    leaseDuration:
                        TimeSpan.FromSeconds(
                            5),
                    leaseRenewalInterval:
                        TimeSpan.FromSeconds(
                            1)),
                new BoundedProcessingFailurePolicy(
                    maximumAttempts:
                        1));

        var runtime =
            new DocumentProcessingManagerRuntime(
                context.StateStore,
                context.RuntimeLeaseStore,
                dispatcher,
                new DocumentProcessingManagerRuntimeOptions(
                    workerId,
                    runtimeLeaseDuration:
                        TimeSpan.FromSeconds(
                            5),
                    runtimeLeaseRenewalInterval:
                        TimeSpan.FromSeconds(
                            1),
                    idlePollingInterval:
                        TimeSpan.FromMilliseconds(
                            20)));

        using var hostStopping =
            new CancellationTokenSource();

        var running =
            runtime.RunAsync(
                hostStopping.Token);

        await runtime.ExecuteAsync(
            new StartManagerCommand());

        await entered.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        var stopped =
            await runtime.ExecuteAsync(
                new StopManagerCommand());

        Assert.Equal(
            ManagerOperatingState.Stopped,
            stopped.Snapshot.State);

        Assert.Equal(
            [workItem.UnitId],
            await ReadPendingOrderAsync(
                context.DataSource));

        hostStopping.Cancel();

        await running.WaitAsync(
            TimeSpan.FromSeconds(
                5));
    }

    #endregion

    #region Helpers

    private static async Task<PostgresTestContext> CreateContextAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                ConnectionStringEnvironmentVariable) ??
            throw new InvalidOperationException(
                $"Missing {ConnectionStringEnvironmentVariable}.");

        var dataSource =
            NpgsqlDataSource.Create(
                connectionString);

        var context =
            new PostgresTestContext(
                dataSource);

        await context.Schema.InitializeAsync();

        await ResetAsync(
            dataSource);

        return context;
    }

    private static async Task ExecuteManagedPageAsync(
        string whitelistedFileName)
    {
        if (whitelistedFileName is not
            ("habermas-p0079.pdf" or "decretis-p0512.pdf"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(whitelistedFileName),
                whitelistedFileName,
                "Managed page tests accept only their explicit fixture whitelist.");
        }

        var fixturePath =
            Path.Combine(
                FindRepositoryRoot(),
                "tests",
                "document_corpus",
                "pdf",
                "pages",
                whitelistedFileName);

        if (!File.Exists(
                fixturePath))
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                $"Local qualified fixture '{whitelistedFileName}' is unavailable.");
        }

        await using var context =
            await CreateContextAsync();

        var custodyRoot =
            CreateTemporaryCustodyRoot();

        try
        {
            var sourceStore =
                new FileSystemSourceArtifactCustodyStore(
                    new FileSystemSourceArtifactCustodyOptions(
                        Path.Combine(
                            custodyRoot,
                            "sources"),
                        maximumArtifactBytes:
                            16 * 1024 * 1024));

            var resultStore =
                new FileSystemProcessingResultArtifactStore(
                    new FileSystemProcessingResultArtifactOptions(
                        Path.Combine(
                            custodyRoot,
                            "results"),
                        maximumArtifactBytes:
                            64 * 1024 * 1024));

            var submitter =
                new SubmitDocumentService(
                    sourceStore,
                    context.SubmissionStore);

            await using var source =
                new FileStream(
                    fixturePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize:
                        128 * 1024,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

            var submitted =
                await submitter.SubmitAsync(
                    new SubmitDocumentCommand(
                        DocumentSubmissionId.New(),
                        source,
                        whitelistedFileName,
                        "application/pdf",
                        "qualified local single-page fixture"));

            var unitId =
                Assert.Single(
                    submitted.ProcessingUnitIds);

            using var host =
                new global::DocumentProcessing.DocumentProcessingHost(
                    new global::DocumentProcessing.DocumentProcessingHostOptions(
                        "manager-integration-v1",
                        new PpStructureV3Options(
                            new Uri(
                                "http://127.0.0.1:1/layout-parsing")),
                        new PaddleOcrOptions(
                            new Uri(
                                "http://127.0.0.1:1/ocr"),
                            "manager-integration-ocr")));

            var executor =
                new DocumentProcessingHostExecutor(
                    host,
                    context.SubmissionStore,
                    sourceStore,
                    resultStore,
                    resultStore,
                    context.ResultRegistry,
                    context.ResultRegistry,
                    new PagedDocumentProcessingResultJsonEncoder());

            var workerId =
                $"managed-page-{Guid.NewGuid():N}";

            var now =
                DateTimeOffset.UtcNow;

            var runtimeLease =
                await context.RuntimeLeaseStore.TryAcquireAsync(
                    workerId,
                    now,
                    now.AddMinutes(
                        5));

            Assert.NotNull(
                runtimeLease);

            var dispatcher =
                new SequentialProcessingDispatcher(
                    context.QueueStore,
                    executor,
                    new SequentialProcessingDispatcherOptions(
                        workerId,
                        leaseDuration:
                            TimeSpan.FromMinutes(
                                2),
                        leaseRenewalInterval:
                            TimeSpan.FromSeconds(
                                30)),
                    new BoundedProcessingFailurePolicy(
                        maximumAttempts:
                            1));

            var dispatched =
                await dispatcher.DispatchNextAsync(
                    runtimeLease,
                    ProcessingInterruptionReason.HostShutdown);

            Assert.Equal(
                ProcessingDispatchStatus.Succeeded,
                dispatched.Status);

            Assert.Equal(
                unitId,
                dispatched.UnitId);

            var registered =
                await context.ResultRegistry.GetByUnitAsync(
                    unitId);

            Assert.NotNull(
                registered);

            Assert.True(
                await resultStore.VerifyAsync(
                    registered.Artifact));

            await using var retainedResult =
                await resultStore.OpenReadAsync(
                    registered.Artifact);

            using var json =
                await JsonDocument.ParseAsync(
                    retainedResult);

            var root =
                json.RootElement;

            Assert.Equal(
                "document-processing-result-v2",
                root.GetProperty(
                        "schemaVersion")
                    .GetString());

            Assert.Equal(
                submitted.Submission.SourceArtifact.Digest.Value,
                root.GetProperty(
                        "source")
                    .GetProperty(
                        "sha256")
                    .GetString());

            Assert.Equal(
                "paged",
                root.GetProperty(
                        "sourceStructure")
                    .GetProperty(
                        "kind")
                    .GetString());

            Assert.True(
                root.GetProperty(
                        "elements")
                    .GetArrayLength() >
                0);

            var replay =
                await executor.ExecuteAsync(
                    new ProcessingWorkItem(
                        unitId,
                        submitted.Submission.SubmissionId,
                        new ProcessingUnitScope.WholeDocument(),
                        attemptNumber:
                            2));

            Assert.Equal(
                registered.ResultReference,
                Assert.IsType<ProcessingExecutionOutcome.Success>(
                        replay)
                    .ResultReference);
        }
        finally
        {
            DeleteTemporaryCustodyRoot(
                custodyRoot);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "DocumentProcessingEngine.sln")))
            {
                return current.FullName;
            }

            current =
                current.Parent;
        }

        throw new InvalidOperationException(
            "DocumentProcessingEngine repository root could not be located.");
    }

    private static ProcessingWorkItem CreateWorkItem(
        ProcessingUnitScope? scope = null) =>
        new(
            ProcessingUnitId.New(),
            DocumentSubmissionId.New(),
            scope ??
            new ProcessingUnitScope.WholeDocument(),
            attemptNumber:
                1);

    private static async Task ResetAsync(
        NpgsqlDataSource dataSource)
    {
        await using var command =
            dataSource.CreateCommand(
                """
                TRUNCATE TABLE
                    document_processing_manager.processing_results,
                    document_processing_manager.processing_result_artifacts,
                    document_processing_manager.custody_events,
                    document_processing_manager.processing_units,
                    document_processing_manager.document_submissions,
                    document_processing_manager.source_artifacts
                RESTART IDENTITY;

                UPDATE document_processing_manager.queue_metadata
                SET version = 0
                WHERE singleton = TRUE;

                UPDATE document_processing_manager.runtime_lease
                SET token = NULL,
                    worker_id = NULL,
                    expires_at_utc = NULL
                WHERE singleton = TRUE;

                UPDATE document_processing_manager.manager_state
                SET operating_state = 0,
                    version = 0
                WHERE singleton = TRUE;
                """);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertPendingAsync(
        NpgsqlDataSource dataSource,
        ProcessingWorkItem workItem,
        long queuePosition)
    {
        await EnsureSubmissionFixtureAsync(
            dataSource,
            workItem.SubmissionId);

        var scopeKind =
            workItem.Scope is ProcessingUnitScope.WholeDocument
                ? (short)0
                : (short)1;

        var pageRange =
            workItem.Scope as ProcessingUnitScope.PageRange;

        await using var command =
            dataSource.CreateCommand(
                """
                INSERT INTO document_processing_manager.processing_units
                (
                    unit_id,
                    submission_id,
                    scope_kind,
                    start_physical_page_number,
                    end_physical_page_number,
                    scope_title,
                    attempt_number,
                    status,
                    queue_position
                )
                VALUES
                (
                    @unit_id,
                    @submission_id,
                    @scope_kind,
                    @start_page,
                    @end_page,
                    @scope_title,
                    @attempt_number,
                    0,
                    @queue_position
                );
                """);

        command.Parameters.AddWithValue(
            "unit_id",
            NpgsqlDbType.Uuid,
            workItem.UnitId.Value);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            workItem.SubmissionId.Value);

        command.Parameters.AddWithValue(
            "scope_kind",
            NpgsqlDbType.Smallint,
            scopeKind);

        command.Parameters.AddWithValue(
            "start_page",
            NpgsqlDbType.Integer,
            pageRange is null
                ? DBNull.Value
                : pageRange.StartPhysicalPageNumber);

        command.Parameters.AddWithValue(
            "end_page",
            NpgsqlDbType.Integer,
            pageRange is null
                ? DBNull.Value
                : pageRange.EndPhysicalPageNumber);

        command.Parameters.AddWithValue(
            "scope_title",
            NpgsqlDbType.Text,
            pageRange is null
                ? DBNull.Value
                : pageRange.Title);

        command.Parameters.AddWithValue(
            "attempt_number",
            NpgsqlDbType.Integer,
            workItem.AttemptNumber);

        command.Parameters.AddWithValue(
            "queue_position",
            NpgsqlDbType.Bigint,
            queuePosition);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertExpiredActiveAsync(
        NpgsqlDataSource dataSource,
        ProcessingWorkItem workItem,
        DateTimeOffset expiredAtUtc)
    {
        await EnsureSubmissionFixtureAsync(
            dataSource,
            workItem.SubmissionId);

        await using var command =
            dataSource.CreateCommand(
                """
                INSERT INTO document_processing_manager.processing_units
                (
                    unit_id,
                    submission_id,
                    scope_kind,
                    attempt_number,
                    status,
                    unit_lease_token,
                    runtime_lease_token,
                    worker_id,
                    unit_lease_expires_at_utc
                )
                VALUES
                (
                    @unit_id,
                    @submission_id,
                    0,
                    @attempt_number,
                    1,
                    @unit_lease_token,
                    @runtime_lease_token,
                    @worker_id,
                    @expired_at_utc
                );
                """);

        command.Parameters.AddWithValue(
            "unit_id",
            NpgsqlDbType.Uuid,
            workItem.UnitId.Value);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            workItem.SubmissionId.Value);

        command.Parameters.AddWithValue(
            "attempt_number",
            NpgsqlDbType.Integer,
            workItem.AttemptNumber);

        command.Parameters.AddWithValue(
            "unit_lease_token",
            NpgsqlDbType.Uuid,
            Guid.NewGuid());

        command.Parameters.AddWithValue(
            "runtime_lease_token",
            NpgsqlDbType.Uuid,
            Guid.NewGuid());

        command.Parameters.AddWithValue(
            "worker_id",
            NpgsqlDbType.Text,
            "crashed-worker");

        command.Parameters.AddWithValue(
            "expired_at_utc",
            NpgsqlDbType.TimestampTz,
            expiredAtUtc.ToUniversalTime());

        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureSubmissionFixtureAsync(
        NpgsqlDataSource dataSource,
        DocumentSubmissionId submissionId)
    {
        var sourceBytes =
            submissionId.Value.ToByteArray();

        var digest =
            Convert.ToHexString(
                    SHA256.HashData(
                        sourceBytes))
                .ToLowerInvariant();

        await using var command =
            dataSource.CreateCommand(
                """
                INSERT INTO document_processing_manager.source_artifacts
                    (sha256_digest, byte_length)
                VALUES
                    (@sha256_digest, @byte_length)
                ON CONFLICT (sha256_digest) DO NOTHING;

                INSERT INTO document_processing_manager.document_submissions
                (
                    submission_id,
                    source_sha256_digest,
                    original_file_name,
                    submitted_at_utc
                )
                VALUES
                (
                    @submission_id,
                    @sha256_digest,
                    'postgres-fixture.pdf',
                    @submitted_at_utc
                )
                ON CONFLICT (submission_id) DO NOTHING;
                """);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            submissionId.Value);

        command.Parameters.AddWithValue(
            "sha256_digest",
            NpgsqlDbType.Text,
            digest);

        command.Parameters.AddWithValue(
            "byte_length",
            NpgsqlDbType.Bigint,
            sourceBytes.LongLength);

        command.Parameters.AddWithValue(
            "submitted_at_utc",
            NpgsqlDbType.TimestampTz,
            DateTimeOffset.UnixEpoch);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<ProcessingUnitId>>
        ReadPendingOrderAsync(
        NpgsqlDataSource dataSource)
    {
        await using var command =
            dataSource.CreateCommand(
                """
                SELECT unit_id
                FROM document_processing_manager.processing_units
                WHERE status = 0
                ORDER BY queue_position, unit_id;
                """);

        await using var reader =
            await command.ExecuteReaderAsync();

        var result =
            new List<ProcessingUnitId>();

        while (await reader.ReadAsync())
        {
            result.Add(
                new ProcessingUnitId(
                    reader.GetGuid(
                        0)));
        }

        return result;
    }

    private static async Task<SubmissionCounts> ReadSubmissionCountsAsync(
        NpgsqlDataSource dataSource)
    {
        await using var command =
            dataSource.CreateCommand(
                """
                SELECT
                    (SELECT COUNT(*) FROM document_processing_manager.source_artifacts),
                    (SELECT COUNT(*) FROM document_processing_manager.document_submissions),
                    (SELECT COUNT(*) FROM document_processing_manager.custody_events),
                    (SELECT COUNT(*) FROM document_processing_manager.processing_units),
                    (SELECT version
                     FROM document_processing_manager.queue_metadata
                     WHERE singleton = TRUE);
                """);

        await using var reader =
            await command.ExecuteReaderAsync();

        Assert.True(
            await reader.ReadAsync());

        return new SubmissionCounts(
            Artifacts:
                reader.GetInt64(
                    0),
            Submissions:
                reader.GetInt64(
                    1),
            Events:
                reader.GetInt64(
                    2),
            Units:
                reader.GetInt64(
                    3),
            QueueVersion:
                reader.GetInt64(
                    4));
    }

    private static DocumentSubmission CreateSubmission(
        DocumentSubmissionId submissionId,
        char digestCharacter) =>
        new(
            submissionId,
            new SourceArtifact(
                new Sha256Digest(
                    new string(
                        digestCharacter,
                        count:
                            64)),
                byteLength:
                    128),
            "fixture.pdf",
            "application/pdf",
            "integration test",
            DateTimeOffset.UnixEpoch);

    private static ProcessingResultRecord CreateProcessingResult(
        ProcessingWorkItem workItem,
        char digestCharacter,
        string resultReference = "manager-result:test",
        DateTimeOffset? producedAtUtc = null) =>
        new(
            resultReference,
            workItem.UnitId,
            workItem.SubmissionId,
            new ProcessingResultArtifact(
                new Sha256Digest(
                    new string(
                        digestCharacter,
                        count:
                            64)),
                byteLength:
                    256),
            "application/vnd.document-processing-result+json",
            "document-processing-result-v2",
            producedAtUtc ??
            DateTimeOffset.UnixEpoch);

    private static string CreateTemporaryCustodyRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            $"dpengine-postgres-custody-{Guid.NewGuid():N}");

    private static void DeleteTemporaryCustodyRoot(
        string root)
    {
        if (!Directory.Exists(
                root))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var file in Directory.EnumerateFiles(
                         root,
                         "*",
                         SearchOption.AllDirectories))
            {
                File.SetAttributes(
                    file,
                    FileAttributes.Normal);
            }
        }

        Directory.Delete(
            root,
            recursive:
                true);
    }

    #endregion

    #region Internal Types

    private sealed class PostgresTestContext(
        NpgsqlDataSource dataSource)
        : IAsyncDisposable
    {
        public NpgsqlDataSource DataSource
        {
            get;
        } =
            dataSource;

        public PostgresManagerSchema Schema
        {
            get;
        } =
            new(
                dataSource);

        public PostgresManagerStateStore StateStore
        {
            get;
        } =
            new(
                dataSource);

        public PostgresManagerRuntimeLeaseStore RuntimeLeaseStore
        {
            get;
        } =
            new(
                dataSource);

        public PostgresProcessingQueueStore QueueStore
        {
            get;
        } =
            new(
                dataSource);

        public PostgresProcessingQueueReader QueueReader
        {
            get;
        } =
            new(
                dataSource);

        public PostgresDocumentSubmissionStore SubmissionStore
        {
            get;
        } =
            new(
                dataSource);

        public PostgresProcessingResultRegistry ResultRegistry
        {
            get;
        } =
            new(
                dataSource);

        public ValueTask DisposeAsync() =>
            DataSource.DisposeAsync();
    }

    private sealed class DelegateExecutor(
        Func<ProcessingWorkItem, CancellationToken, Task<ProcessingExecutionOutcome>>
            execute)
        : IDocumentProcessingExecutor
    {
        public ValueTask<ProcessingExecutionOutcome> ExecuteAsync(
            ProcessingWorkItem workItem,
            CancellationToken cancellationToken = default) =>
            new(
                execute(
                    workItem,
                    cancellationToken));
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
    }

    private readonly record struct SubmissionCounts(
        long Artifacts,
        long Submissions,
        long Events,
        long Units,
        long QueueVersion);

    #endregion
}
