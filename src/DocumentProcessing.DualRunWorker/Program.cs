namespace DocumentProcessing.DualRunWorker;

internal static class Program
{
    #region Methods Entry Point

    public static Task<int> Main(
        string[] args) =>
        DocumentDualRunWorkerBootstrap
            .RunAsync(
                args);

    #endregion
}
