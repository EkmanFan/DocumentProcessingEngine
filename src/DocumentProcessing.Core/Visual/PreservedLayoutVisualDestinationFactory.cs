using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Core.Visual;

/// <summary>
/// Opens the destination stream used to preserve one visual produced by the
/// layout-driven visual pipeline.
/// </summary>
/// <remarks>
/// The contract is intentionally format-neutral but intentionally scoped to a
/// <see cref="LayoutObservation"/>. It does not claim to model every possible
/// visual-custody mechanism of every future document format.
/// </remarks>
public delegate ValueTask<Stream>
    PreservedLayoutVisualDestinationFactory(
        DocumentSource source,
        LayoutObservation visual,
        CancellationToken cancellationToken);
