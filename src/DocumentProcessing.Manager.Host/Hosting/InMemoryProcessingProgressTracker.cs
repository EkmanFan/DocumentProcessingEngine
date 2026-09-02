using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Host.Hosting;

/// <summary>
/// Process-local latest-value tracker for the strictly sequential Manager runtime.
/// </summary>
internal sealed class InMemoryProcessingProgressTracker
    : IProcessingProgressReporter,
      IProcessingProgressReader
{
    #region Variables and Constants

    private readonly object _gate =
        new();

    private ProcessingUnitId? _unitId;

    private ProcessingProgressSnapshot? _progress;

    #endregion

    #region Methods

    public void Report(
        ProcessingUnitId unitId,
        ProcessingProgressSnapshot progress)
    {
        ArgumentNullException.ThrowIfNull(
            progress);

        lock (_gate)
        {
            if (_unitId ==
                    unitId &&
                _progress is not null &&
                progress.Stage !=
                    ProcessingProgressStage.LoadingSource &&
                progress.CompletionPercentage <
                    _progress.CompletionPercentage)
            {
                return;
            }

            _unitId =
                unitId;

            _progress =
                progress;
        }
    }

    public ProcessingProgressSnapshot? TryGet(
        ProcessingUnitId unitId)
    {
        lock (_gate)
        {
            return _unitId ==
                    unitId
                ? _progress
                : null;
        }
    }

    #endregion
}
