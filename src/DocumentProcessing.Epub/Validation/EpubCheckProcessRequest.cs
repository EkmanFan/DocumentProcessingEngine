namespace DocumentProcessing.Epub.Validation;

internal sealed record EpubCheckProcessRequest(
    string JavaExecutablePath,
    string EpubCheckJarPath,
    string EpubPath,
    string ReportPath,
    TimeSpan Timeout);
