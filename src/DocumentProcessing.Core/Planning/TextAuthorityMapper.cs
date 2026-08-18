using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.Planning;
/// <summary>
/// Stable semantic mapping from the existing native extraction assessment to
/// the policy-facing text-authority axis.
///
/// This mapper selects no processing route and performs no I/O.
/// </summary>
public static class TextAuthorityMapper
{
    public static TextAuthority FromNativeTextStatus(
        NativeTextStatus nativeTextStatus) =>
        nativeTextStatus switch
        {
            NativeTextStatus.Missing =>
                TextAuthority.Missing,

            NativeTextStatus.Healthy =>
                TextAuthority.Trusted,

            NativeTextStatus.Unverified =>
                TextAuthority.NeedsVerification,

            NativeTextStatus.Suspicious =>
                TextAuthority.Corrupted,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(nativeTextStatus),
                    nativeTextStatus,
                    "Native text status must be a defined value.")
        };
}
