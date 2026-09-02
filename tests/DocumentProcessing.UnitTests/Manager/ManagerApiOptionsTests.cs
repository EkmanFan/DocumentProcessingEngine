using DocumentProcessing.Manager.Blazor.Configuration;
using Microsoft.Extensions.Configuration;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerApiOptionsTests
{
    #region Tests

    [Fact]
    public void Load_DisablesPermanentDeletionByDefault()
    {
        var options =
            ManagerApiOptions.Load(
                CreateConfiguration());

        Assert.False(
            options.AllowPermanentDeletion);
    }

    [Fact]
    public void Load_EnablesPermanentDeletionExplicitly()
    {
        var options =
            ManagerApiOptions.Load(
                CreateConfiguration(
                    allowPermanentDeletion:
                        true));

        Assert.True(
            options.AllowPermanentDeletion);
    }

    [Fact]
    public void Load_ExposesConfiguredReplayConsumer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ManagerApi:BaseAddress"] = "http://manager.local",
                    ["ManagerApi:ApiKey"] = new string('a', 32),
                    ["ManagerApi:ReplayConsumerId"] = "apologia-studio"
                })
            .Build();

        var options = ManagerApiOptions.Load(configuration);

        Assert.Equal("apologia-studio", options.ReplayConsumerId);
    }

    #endregion

    #region Methods

    private static IConfiguration CreateConfiguration(
        bool allowPermanentDeletion = false) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ManagerApi:BaseAddress"] =
                        "http://manager.local",
                    ["ManagerApi:ApiKey"] =
                        new string('a', 32),
                    ["ManagerApi:AllowPermanentDeletion"] =
                        allowPermanentDeletion.ToString()
                })
            .Build();

    #endregion
}
