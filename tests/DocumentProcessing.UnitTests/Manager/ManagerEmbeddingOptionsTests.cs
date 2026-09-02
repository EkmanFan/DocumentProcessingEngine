using DocumentProcessing.Manager.Blazor.Configuration;
using Microsoft.Extensions.Configuration;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerEmbeddingOptionsTests
{
    #region Tests

    [Fact]
    public void Load_WithoutOrigins_DisablesEmbedding()
    {
        var options =
            ManagerEmbeddingOptions.Load(
                new ConfigurationBuilder()
                    .Build());

        Assert.False(
            options.IsEnabled);

        Assert.Empty(
            options.AllowedParentOrigins);
    }

    [Fact]
    public void Load_NormalizesAndDeduplicatesSafeOrigins()
    {
        var options =
            ManagerEmbeddingOptions.Load(
                CreateConfiguration(
                    "http://localhost:5090/",
                    "HTTP://LOCALHOST:5090",
                    "https://studio.apologia.example"));

        Assert.True(
            options.IsEnabled);

        Assert.Equal(
            2,
            options.AllowedParentOrigins.Count);

        Assert.Equal(
            "frame-ancestors 'self' http://localhost:5090 https://studio.apologia.example",
            options.FrameAncestorsPolicy);
    }

    [Theory]
    [InlineData("http://studio.apologia.example")]
    [InlineData("https://studio.apologia.example/workspace")]
    [InlineData("https://user@studio.apologia.example")]
    [InlineData("file:///tmp/studio.html")]
    public void Load_RejectsUnsafeOrNonOriginValues(
        string value)
    {
        Assert.Throws<InvalidOperationException>(
            () => ManagerEmbeddingOptions.Load(
                CreateConfiguration(
                    value)));
    }

    #endregion

    #region Methods

    private static IConfiguration CreateConfiguration(
        params string[] origins)
    {
        var values =
            origins
                .Select(
                    (origin, index) =>
                        new KeyValuePair<string, string?>(
                            $"ManagerEmbedding:AllowedParentOrigins:{index}",
                            origin));

        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                values)
            .Build();
    }

    #endregion
}
