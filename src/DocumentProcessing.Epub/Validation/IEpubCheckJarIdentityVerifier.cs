namespace DocumentProcessing.Epub.Validation;

internal interface IEpubCheckJarIdentityVerifier
{
    bool MatchesPinnedVersion(
        string jarPath);
}
