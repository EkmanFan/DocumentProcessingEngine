using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using DocumentProcessing.Manager.Blazor.Components.Workshop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerBlazorLocalizationTests
{
    #region Variables and Constants

    private const string
        AnimationResourceName =
            "DocumentProcessing.Manager.Blazor.Resources.Components.Animation.LibrarianAnimation";

    private const string
        DocumentDropZoneResourceName =
            "DocumentProcessing.Manager.Blazor.Resources.Components.Workshop.ManagerDocumentDropZone";

    private const string
        StandaloneResourceName =
            "DocumentProcessing.Manager.Blazor.Resources.Localization.StandaloneUiResources";

    private const string
        WorkshopResourceName =
            "DocumentProcessing.Manager.Blazor.Resources.Components.Workshop.ManagerWorkshop";

    private static readonly Assembly
        BlazorAssembly =
            typeof(ManagerWorkshop)
                .Assembly;

    #endregion

    #region Tests

    [Theory]
    [InlineData(
        "en",
        "Document workshop",
        "Whole document")]
    [InlineData(
        "fr",
        "Atelier documentaire",
        "Document entier")]
    public void WorkshopResources_ExposeSupportedLanguages(
        string cultureName,
        string expectedTitle,
        string expectedScope)
    {
        var resourceManager =
            new ResourceManager(
                WorkshopResourceName,
                BlazorAssembly);

        var culture =
            CultureInfo.GetCultureInfo(
                cultureName);

        Assert.Equal(
            expectedTitle,
            resourceManager.GetString(
                "WorkshopTitle",
                culture));

        Assert.Equal(
            expectedScope,
            resourceManager.GetString(
                "WholeDocumentScope",
                culture));
    }

    [Theory]
    [InlineData(
        "en",
        "Download")]
    [InlineData(
        "fr",
        "Télécharger")]
    public void WorkshopResources_LocalizeResultDownload(
        string cultureName,
        string expectedDownloadLabel)
    {
        var resourceManager =
            new ResourceManager(
                WorkshopResourceName,
                BlazorAssembly);

        Assert.Equal(
            expectedDownloadLabel,
            resourceManager.GetString(
                "DownloadResult",
                CultureInfo.GetCultureInfo(
                    cultureName)));
    }

    [Theory]
    [InlineData(
        "en",
        "The browser reported no readable content for this file. If it is not empty, choose it by clicking the drop zone.")]
    [InlineData(
        "fr",
        "Le navigateur n’a fourni aucun contenu lisible pour ce fichier. S’il n’est pas vide, sélectionne-le en cliquant sur la zone.")]
    public void DocumentDropZoneResources_ExplainUnreadableBrowserDrop(
        string cultureName,
        string expectedMessage)
    {
        var resourceManager =
            new ResourceManager(
                DocumentDropZoneResourceName,
                BlazorAssembly);

        var culture =
            CultureInfo.GetCultureInfo(
                cultureName);

        Assert.Equal(
            expectedMessage,
            resourceManager.GetString(
                "UploadNoReadableContentError",
                culture));
    }

    [Fact]
    public void DocumentDropZone_DefaultsToShelvedSubmission()
    {
        var component =
            new ManagerDocumentDropZone();

        Assert.Equal(
            ManagerDocumentSubmissionBehavior.Shelve,
            component.SubmissionBehavior);
    }

    [Theory]
    [InlineData(
        "en-US",
        "Document workshop")]
    [InlineData(
        "fr-FR",
        "Atelier documentaire")]
    public void WorkshopLocalizer_FollowsAmbientUiCultureDespiteHostResourcePath(
        string cultureName,
        string expectedTitle)
    {
        var previousCulture =
            CultureInfo.CurrentCulture;

        var previousUiCulture =
            CultureInfo.CurrentUICulture;

        try
        {
            var culture =
                CultureInfo.GetCultureInfo(
                    cultureName);

            CultureInfo.CurrentCulture =
                culture;

            CultureInfo.CurrentUICulture =
                culture;

            var services =
                new ServiceCollection();

            services.AddLogging();

            services.AddLocalization(
                options =>
                    options.ResourcesPath =
                        "ApologiaResources");

            using var provider =
                services.BuildServiceProvider();

            var localizer =
                provider
                    .GetRequiredService<IStringLocalizer<ManagerWorkshop>>();

            Assert.Equal(
                expectedTitle,
                localizer["WorkshopTitle"]);
        }
        finally
        {
            CultureInfo.CurrentCulture =
                previousCulture;

            CultureInfo.CurrentUICulture =
                previousUiCulture;
        }
    }

    [Fact]
    public void LocalizationResources_HaveMatchingEnglishAndFrenchKeys()
    {
        AssertMatchingKeys(
            WorkshopResourceName);

        AssertMatchingKeys(
            AnimationResourceName);

        AssertMatchingKeys(
            DocumentDropZoneResourceName);

        AssertMatchingKeys(
            StandaloneResourceName);
    }

    [Fact]
    public void BlazorAdapter_DeclaresEnglishAsNeutralLanguage()
    {
        var attribute =
            BlazorAssembly
                .GetCustomAttribute<NeutralResourcesLanguageAttribute>();

        Assert.NotNull(
            attribute);

        Assert.Equal(
            "en",
            attribute.CultureName);
    }

    [Fact]
    public void BlazorAdapter_OwnsItsResourceLocation()
    {
        var attribute =
            BlazorAssembly
                .GetCustomAttribute<ResourceLocationAttribute>();

        Assert.NotNull(
            attribute);

        Assert.Equal(
            "Resources",
            attribute.ResourceLocation);
    }

    #endregion

    #region Methods

    private static void AssertMatchingKeys(
        string resourceName)
    {
        var resourceManager =
            new ResourceManager(
                resourceName,
                BlazorAssembly);

        var englishKeys =
            GetKeys(
                resourceManager,
                CultureInfo.InvariantCulture);

        var frenchKeys =
            GetKeys(
                resourceManager,
                CultureInfo.GetCultureInfo(
                    "fr"));

        Assert.NotEmpty(
            englishKeys);

        Assert.Equal(
            englishKeys,
            frenchKeys);
    }

    private static string[] GetKeys(
        ResourceManager resourceManager,
        CultureInfo culture)
    {
        var resourceSet =
            resourceManager.GetResourceSet(
                culture,
                createIfNotExists:
                    true,
                tryParents:
                    false) ??
            throw new InvalidOperationException(
                $"Missing resource set for '{culture.Name}'.");

        return resourceSet
            .Cast<DictionaryEntry>()
            .Select(
                entry =>
                    (string)entry.Key)
            .OrderBy(
                key =>
                    key,
                StringComparer.Ordinal)
            .ToArray();
    }

    #endregion
}
