# EPUB-5 expanded-corpus validation V1

## Status

**EPUB-5 — Brenner and Septante corpus validation: complete**

EPUB-5 exercises the V1 EPUB path with two additional, structurally different
publications. Their exact results are frozen with the Calvin and Bauckham
results in `docs/evaluation/epub-multi-corpus-reference-v1.json`.

## Brenner

`Logic and Philosophy` is a compact textbook with many documentary images.
EPUBCheck 5.3.0 reports no error or warning.

```text
spine items                22
portable elements       2,580
ordinary text elements  2,530
heading elements           50
segments                   21
retained visuals          129
```

The front cover is excluded. Visual inspection confirms that the 129 retained
images are documentary material: logical formulas, truth tables, diagrams,
portraits and teaching illustrations. Every retained visual is qualified as
`Meaningful` from EPUB structure. The normal request and the optional
unresolved-visual request produce byte-identical reports without reaching
Paddle.

## La Septante

`La Septante Grec-Français` is a large bilingual publication. EPUBCheck 5.3.0
reports no error or warning.

```text
spine items                 1,181
portable elements          60,366
ordinary text elements     60,311
heading elements               55
segments                    1,179
retained visuals                0
```

The 55 headings correspond to the publication table of contents. Its page-list
contains thousands of physical-page references; none is incorrectly promoted
to a heading. The only packaged images are the front and back covers, and both
are excluded.

The official EPUBCheck JSON report is 1,202,273 bytes because it describes
1,181 spine items. The former one-MiB reader limit incorrectly returned the
consumer-safe “validation temporarily unavailable” result even though the EPUB
was conformant. The fixed report-size limit has been removed. The reader now
materializes only EPUBCheck validation messages and skips the potentially large
package inventory, with a focused regression test for conformant reports larger
than one MiB.

## Reproduction

```bash
./scripts/run-epub-multi-corpus-regression.sh
```

The four external corpus files remain local and ignored by Git. The script
compares every result with the frozen reference; it does not accept new output
automatically.

## Remaining V1 boundary

CSS background images, generic `object` resources and video poster frames
remain outside the acquisition boundary. No new extraction or visual-
qualification defect was observed in these two publications.
