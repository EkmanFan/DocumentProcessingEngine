using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DocumentProcessing.Manager.Host.Configuration;

namespace DocumentProcessing.Manager.Host.Hosting;

internal sealed class ResultAvailabilityNotificationHostedService(
    ResultAvailabilitySignal signal,
    ResultNotificationOptions options,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<ResultAvailabilityNotificationHostedService> logger)
    : BackgroundService
{
    internal const string SignatureHeader =
        "X-Manager-Notification-Signature";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (options.Observers.Count == 0)
        {
            return;
        }

        var observerChannels = options.Observers
            .Select(
                observer =>
                    new ObserverChannel(
                        observer,
                        Channel.CreateBounded<bool>(
                            new BoundedChannelOptions(1)
                            {
                                FullMode = BoundedChannelFullMode.DropWrite,
                                SingleReader = true,
                                SingleWriter = true
                            })))
            .ToArray();
        var observerWorkers = observerChannels
            .Select(channel => RunObserverAsync(channel, stoppingToken))
            .ToArray();

        signal.Notify();
        var reconciliation = ProduceReconciliationSignalsAsync(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await signal.WaitAsync(stoppingToken).ConfigureAwait(false);
                foreach (var observerChannel in observerChannels)
                {
                    observerChannel.Signals.Writer.TryWrite(true);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            try
            {
                await reconciliation.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }

            try
            {
                await Task.WhenAll(observerWorkers).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }

    private async Task ProduceReconciliationSignalsAsync(
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(options.ReconciliationInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken)
                   .ConfigureAwait(false))
        {
            signal.Notify();
        }
    }

    private async Task RunObserverAsync(
        ObserverChannel observerChannel,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await observerChannel.Signals.Reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            while (!await TryNotifyAsync(
                       observerChannel.Observer,
                       cancellationToken).ConfigureAwait(false))
            {
                await Task.Delay(options.RetryInterval, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> TryNotifyAsync(
        ResultNotificationObserver observer,
        CancellationToken cancellationToken)
    {
        var notification = new ResultAvailableNotification(
            Guid.NewGuid(),
            observer.ConsumerId,
            timeProvider.GetUtcNow());
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            notification,
            SerializerOptions);
        var signature = Convert.ToHexString(
                HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(observer.SharedSecret),
                    payload))
            .ToLowerInvariant();

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                observer.CallbackUrl);
            request.Headers.Add(SignatureHeader, $"sha256={signature}");
            request.Content = new ByteArrayContent(payload);
            request.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json");

            using var response = await httpClientFactory
                .CreateClient("ManagerResultNotifications")
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Notified result consumer {ConsumerId} that results may be available.",
                    observer.ConsumerId);
                return true;
            }

            logger.LogWarning(
                "Result notification for consumer {ConsumerId} returned HTTP {StatusCode}; it will be retried.",
                observer.ConsumerId,
                (int)response.StatusCode);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Result notification for consumer {ConsumerId} failed; it will be retried.",
                observer.ConsumerId);
        }
        catch (OperationCanceledException exception) when (
            !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Result notification for consumer {ConsumerId} timed out; it will be retried.",
                observer.ConsumerId);
        }

        return false;
    }

    internal sealed record ResultAvailableNotification(
        Guid NotificationId,
        string ConsumerId,
        DateTimeOffset OccurredAtUtc);

    private sealed record ObserverChannel(
        ResultNotificationObserver Observer,
        Channel<bool> Signals);
}
