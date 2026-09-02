using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Host.Hosting;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class NotifyingProcessingResultRegistryWriterTests
{
    [Fact]
    public async Task Register_NotifiesOnlyAfterDurableRegistration()
    {
        var result = CreateResult();
        var events = new List<string>();
        var writer = new NotifyingProcessingResultRegistryWriter(
            new RecordingWriter(events),
            new RecordingSignal(events));

        var registration = await writer.RegisterAsync(result);

        Assert.Same(result, registration.Result);
        Assert.Equal(["registered", "notified"], events);
    }

    [Fact]
    public async Task Register_DoesNotNotifyWhenDurableRegistrationFails()
    {
        var signal = new RecordingSignal([]);
        var writer = new NotifyingProcessingResultRegistryWriter(
            new FailingWriter(),
            signal);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await writer.RegisterAsync(CreateResult()));

        Assert.False(signal.Notified);
    }

    private static ProcessingResultRecord CreateResult() =>
        new(
            "result.json",
            ProcessingUnitId.New(),
            DocumentSubmissionId.New(),
            new ProcessingResultArtifact(
                new Sha256Digest(new string('a', 64)),
                1),
            "application/json",
            "test-v1",
            DateTimeOffset.UtcNow);

    private sealed class RecordingWriter(List<string> events)
        : IProcessingResultRegistryWriter
    {
        public ValueTask<ProcessingResultRegistration> RegisterAsync(
            ProcessingResultRecord result,
            CancellationToken cancellationToken = default)
        {
            events.Add("registered");
            return ValueTask.FromResult(
                new ProcessingResultRegistration(result, true));
        }
    }

    private sealed class FailingWriter : IProcessingResultRegistryWriter
    {
        public ValueTask<ProcessingResultRegistration> RegisterAsync(
            ProcessingResultRecord result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<ProcessingResultRegistration>(
                new InvalidOperationException("durable registration failed"));
    }

    private sealed class RecordingSignal(List<string> events)
        : IResultAvailabilitySignal
    {
        public bool Notified { get; private set; }

        public void Notify()
        {
            Notified = true;
            events.Add("notified");
        }
    }
}
