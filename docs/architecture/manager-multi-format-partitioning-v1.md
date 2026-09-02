# Manager multi-format document partitioning v1

Status: accepted and implemented on 2026-09-02. MGR-BAT-02A through
MGR-BAT-02C3 are complete: PDF and EPUB now share the neutral partition
strategy while retaining their native coordinate systems from proposal through
approved execution.

The later structural-heading fallback is specified separately in
[Manager structural-heading partitioning V1](manager-structural-heading-partitioning-v1.md).

## Context

The Manager can currently replace one pending whole-document unit with ordered
physical-page ranges. Its preview, API and execution path deliberately use PDF
physical pages because that was the first qualified batch requirement.

That implementation must not become the universal document-partitioning model.
EPUB is a production format and explicitly has no physical pages. It provides a
native navigation document, legacy NCX data when present, an authoritative spine
order and stable content-unit identifiers. Future structured formats will expose
different native coordinates again.

MGR-BAT-02 therefore means automatic **document partitioning**, not automatic
PDF page splitting. PDF outline destinations are the first evidence adapter;
they are not the domain contract.

## Decision

Partitioning is separated into four responsibilities:

```text
format-native structure
        |
        v
partition evidence adapter
        |
        v
deterministic partition strategy
        |
        v
neutral partition proposal
        |
        v
human approval and atomic queue replacement
```

### Format adapters

Format projects remain responsible for understanding their native structure:

- PDF translates outline nodes and internal destinations into physical-page
  boundary evidence;
- EPUB translates `nav`/NCX targets and spine order into ordered content-unit
  boundary evidence;
- no Manager contract contains PdfPig bookmarks, EPUB resource paths or another
  provider-specific type.

The first EPUB increment accepts only navigation boundaries that resolve to
distinct ordered content units. Fragment-level splitting inside one XHTML
resource is deferred until there is qualified evidence that it is required.
Ambiguous or unresolved targets fail closed.

### Neutral axes and positions

Partition evidence declares exactly one coordinate axis:

- `PhysicalPages`: one-based physical pages with a complete source page count;
- `ContentUnits`: zero-based ordered units with stable unit identifiers.

A boundary position must belong to the declared axis. Strategies cannot mix
physical pages and content units in one proposal, and content units are never
presented as synthetic pages.

### Strategy

`IDocumentPartitionStrategy` is a synchronous, deterministic behavioral
strategy. It receives already acquired neutral evidence and either returns a
proposal or declines with `null`. It does not open source files, call format
libraries, render previews, mutate the queue or persist data.

The first strategy, `NativeNavigationPartitionStrategy`, consumes native
hierarchical navigation evidence. It:

1. selects the shallowest hierarchy level containing at least two distinct,
   monotonically ordered boundaries;
2. creates a leading untitled segment when navigation starts after the source;
3. creates contiguous, non-overlapping segments covering the complete source;
4. declines when hierarchy or destination order is insufficient or unsafe.

Later strategies may use reconciled structural headings or an explicitly
enabled mechanical-size fallback. If strategies are composed, the application
uses an ordered chain and accepts the first qualified proposal. Combining or
voting across strategies requires a separate evidence-backed decision.

### Proposal invariants

An automatic `DocumentPartitionProposal`:

- contains at least two segments;
- uses one coordinate axis;
- is ordered, contiguous and non-overlapping;
- covers the complete source so automation cannot silently discard content;
- carries a stable strategy identifier and categorical reliability;
- keeps titles optional because leading matter may have no publisher title.

Numeric pseudo-probabilities are not introduced. Reliability is categorical
until an evaluation corpus justifies calibrated scores.

### Human approval and execution

Automatic proposals never mutate the queue. The existing dialog remains the
human-approval boundary. Its shell becomes common, while navigation and preview
remain capability-specific:

- paged documents use thumbnails and physical-page ranges;
- structured reflowable documents use a hierarchy/list and content-unit ranges.

After approval, the Manager persists typed processing-unit scopes and replaces
the whole-document unit atomically. Physical-page and content-unit execution are
optional format capabilities selected through Core/Host boundaries. Manager
Core never references PDF or EPUB assemblies.

## Delivery sequence

### MGR-BAT-02A — neutral strategy foundation

- add neutral axes, positions, evidence, proposals and Strategy contracts;
- implement and test `NativeNavigationPartitionStrategy` against paged and
  ordered-content-unit examples;
- do not change queue persistence or UI behavior yet.

### MGR-BAT-02B — PDF outline adapter

- promote the already evaluated PdfPig outline observations behind a production
  format capability;
- map reliable internal destinations to physical-page boundary evidence;
- prefill the existing paged splitter without changing manual fallback.

### MGR-BAT-02C1 — EPUB navigation adapter

- expose resolved `nav`/NCX hierarchy and spine targets as content-unit evidence;
- project only unambiguous, distinct spine-unit boundaries;
- exercise the same neutral strategy on representative EPUB sources.

### MGR-BAT-02C2 — EPUB content-unit execution

- add a typed content-unit processing scope and format execution capability;
- persist and reload stable range boundary indexes and identifiers;
- propagate the typed scope through the Manager, Host and Engine;
- validate the approved boundaries against the current spine and extract only
  the requested units.

### MGR-BAT-02C3 — EPUB structured approval editor

- add the structured preview/editor variant;
- map an approved neutral proposal to typed content-unit scopes;
- atomically replace the pending whole-document unit with the approved ranges.

MGR-BAT-02 is complete only after both a qualified PDF and a qualified EPUB
exercise the same neutral strategy contract end to end.

MGR-BAT-02C2 acceptance evidence includes a real EPUBCheck-qualified execution
that retains only the approved spine range, Engine propagation tests, and a
PostgreSQL round trip through queue leasing and result publication.

MGR-BAT-02C3 acceptance evidence includes a real Habermas EPUB producing a
complete structured proposal, discriminated API/client contracts that preserve
stable content-unit boundary identifiers, and atomic PostgreSQL replacement
tests for both physical-page and content-unit ranges. The approval editor shows
PDF thumbnails only for paged documents and an ordered native-section list for
EPUB; it never synthesizes EPUB pages.

## Alternatives rejected

### One PDF-specific outline service in Manager

Rejected because it would embed format semantics in Manager Core and make EPUB
support a parallel workflow.

### One strategy class per file extension

Rejected because it merely hides a format switch. Formats adapt native facts;
strategies operate on neutral evidence and may be reused across formats.

### One universal page abstraction

Rejected because EPUB has no authoritative physical pages. Synthetic pagination
would be unstable across renderers and violate source provenance.

### One strategy owning inspection, preview and execution

Rejected as an oversized abstraction coupling read-only evidence acquisition,
pure planning, presentation and consequential queue mutation.

### LLM-generated boundaries

Rejected for this increment. Native navigation is cheaper, auditable and more
deterministic. Model assistance may only be evaluated later against a defined
corpus and never as an authorization boundary.

## Acceptance evidence

MGR-BAT-02A requires tests proving that the same strategy:

- builds complete page ranges from a PDF-like physical-page axis;
- builds complete structural ranges from an EPUB-like content-unit axis;
- preserves a leading segment not covered by native navigation;
- selects a usable hierarchy level deterministically;
- rejects mixed axes, out-of-range evidence and non-monotonic navigation;
- returns no proposal when fewer than two reliable boundaries exist.
