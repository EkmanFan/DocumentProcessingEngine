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

The corpus is excluded from version control **for copyright reasons**: the
controls are purchased books, and publishing them in a public repository would
expose them to free download. That is not negotiable and will not change. It is
enforced through `.git/info/exclude`, and no corpus file has ever been committed
in the repository's history — verified.

The skip path is therefore not an edge case reached by accident. It is the
**normal** path for every machine that does not hold a private 2.8 GB corpus:
CI, a fresh clone, any second developer. The next one to take it will be blocked
exactly as this one was.

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

## Why skipping is the only admissible behaviour

An earlier draft of this note left open whether the qualified corpus should
instead become a hard requirement of the suite. It should not, and the reason is
legal rather than technical: the controls cannot be distributed, so a suite that
requires them is a suite that cannot pass anywhere but on the machine holding the
purchased files.

Skipping is consequently a structural requirement of this repository, not a
convenience. The mechanism the code already reaches for is the right one; only
its translation by the runner is broken.

That raises one design question worth deciding alongside the fix: a skipped
control is silent, and a suite can be green while covering far less than it
appears to. Reporting the skipped-control count explicitly — as the integration
suite's 28 skips already do — keeps the difference visible between "everything
passed" and "everything runnable here passed".

## Verification

Independently reproduced on bare `HEAD` (`bac8957`) with a clean working tree,
before any P0-01 change existed. The failure is unrelated to metadata
acquisition.
