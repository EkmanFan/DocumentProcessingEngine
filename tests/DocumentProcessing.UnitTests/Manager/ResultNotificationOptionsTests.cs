using DocumentProcessing.Manager.Host.Configuration;
using Microsoft.Extensions.Configuration;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ResultNotificationOptionsTests
{
    [Fact]
    public void Load_UsesSafeReconciliationDefaultsWithoutObservers()
    {
        var options = ResultNotificationOptions.Load(
            new ConfigurationBuilder().Build());

        Assert.Empty(options.Observers);
        Assert.Equal(TimeSpan.FromMinutes(5), options.ReconciliationInterval);
        Assert.Equal(TimeSpan.FromSeconds(10), options.RetryInterval);
    }

    [Fact]
    public void Load_ReadsLoopbackObserver()
    {
        var configuration = CreateConfiguration(
            "http://127.0.0.1:5090/internal/document-manager/result-available");

        var options = ResultNotificationOptions.Load(configuration);

        var observer = Assert.Single(options.Observers);
        Assert.Equal("apologia-studio", observer.ConsumerId);
        Assert.Equal("notification-secret-with-at-least-32-characters", observer.SharedSecret);
    }

    [Fact]
    public void Load_RejectsRemotePlainHttpObserver()
    {
        var configuration = CreateConfiguration(
            "http://apologia.example/internal/document-manager/result-available");

        Assert.Throws<InvalidOperationException>(
            () => ResultNotificationOptions.Load(configuration));
    }

    private static IConfiguration CreateConfiguration(string callbackUrl) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ManagerNotifications:Observers:0:ConsumerId"] =
                        "apologia-studio",
                    ["ManagerNotifications:Observers:0:CallbackUrl"] =
                        callbackUrl,
                    ["ManagerNotifications:Observers:0:SharedSecret"] =
                        "notification-secret-with-at-least-32-characters"
                })
            .Build();
}
