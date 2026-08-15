namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Conservative status for target-centric native/layout pairing.
///
/// Multiple source blocks are not ambiguous by themselves. Ambiguity means
/// that the same native word evidence is claimed by more than one text target.
/// </summary>
public enum NativeLayoutTextPairingStatus
{
    NoNativeEvidence = 0,
    Comparable = 1,
    AmbiguousWordOwnership = 2
}
