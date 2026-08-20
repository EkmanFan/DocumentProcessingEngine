using System.Reflection;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentProcessorFactoryTests
{
    #region Methods Tests

    [Fact]
    public void PublicSurface_DoesNotExposeProcessingStrategyComposition()
    {
        var publicStaticMethods =
            typeof(DocumentProcessorFactory)
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly);

        Assert.Empty(
            publicStaticMethods);
    }

    #endregion
}
