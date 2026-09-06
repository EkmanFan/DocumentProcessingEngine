# Test tooling — dynamic skip reported as failure — 2026-09-07

**Status:** latent — the symptom is currently masked, the defect is not fixed
**Type:** technical debt
**Scope:** test tooling only. No production code, no format adapter, no
portable contract.
**Explicitly not part of:** P0-01, P0-02, P1-01.

## Symptom

Any test that declares itself unrunnable through `SkipException.ForSkip` is
reported as a **failure** rather than a skip, and the raw sentinel leaks into the
message:

```text
DocumentProcessing.UnitTests.Epub.EpubDocumentFormatTests
  .Extractor_TargetedLocalCorporaConcludeExpectedNoteRelations [FAIL]
  $XunitDynamicSkip$Targeted EPUB control
  'Historical Theology_ An Introduction to Christian Doctrine - Gregg Allison.epub'
  is unavailable.
```

`$XunitDynamicSkip$` is an internal marker the runner is expected to translate
into a skip. It reaching the output verbatim is the tell.

## Consequence

`scripts/commit-document-processing.sh` runs the full suite and refuses to
commit on any failure. A developer whose machine lacks one optional corpus file
therefore **cannot commit anything at all**, however unrelated the change.

This blocked the P0-01 commit on 2026-09-06 and required an explicit, owner
approved exception to the normal workflow. That exception is recorded in
`df54602`.

The missing control was restored locally on 2026-09-07 and the suite is green
again: 1012 of 1012 unit tests, 54 of 54 integration tests. **That resolves the
symptom, not the defect.**

The corpus is excluded from version control by design, through
`.git/info/exclude` rather than `.gitignore`, so it is local to each machine and
absent from a fresh clone. The skip path therefore is not an edge case reached by
accident: it is the path every machine without the corpus takes, including CI and
any new developer. The next one to take it will be blocked exactly as this one
was.

## Suspected cause

`tests/DocumentProcessing.UnitTests/DocumentProcessing.UnitTests.csproj`
references:

```text
xunit                       2.9.3
xunit.runner.visualstudio   3.1.4
```

The major versions do not match. Dynamic skip is an xUnit v3 feature, and a v3
runner paired with a v2 framework has no v2 counterpart to translate the marker
into, so it surfaces as a failed assertion.

This is a reading of the manifest and the observed output, not a verified root
cause. Confirming it is the first task below.

## Tasks

1. Confirm the cause. Check the framework and runner versions across every test
   project, not only the unit tests, and establish whether the mismatch is the
   real mechanism.
2. Realign the pair. Either bring `xunit.runner.visualstudio` back to the
   framework's major version, or move the projects to xUnit v3 wholesale.
   Whichever is chosen, apply it uniformly: a partially migrated solution would
   reproduce this class of problem elsewhere.
3. Restore the expected behaviour of `SkipException.ForSkip`, and prove it with
   a test that skips deterministically without depending on a corpus file.
4. Re-run the full suite on a machine lacking the optional corpora and confirm
   the commit helper passes. Restoring a corpus file is not a validation of this
   fix: the absence case is the one that must be proven.

## Related question, deliberately left open

Whether a missing optional corpus control should skip at all, or whether the
qualified corpus should be a hard requirement of the suite, is a separate policy
decision. This task restores the behaviour the code already asks for; it does not
decide what that behaviour ought to be.

## Verification

Independently reproduced on bare `HEAD` (`bac8957`) with a clean working tree,
before any P0-01 change existed. The failure is unrelated to metadata
acquisition.
