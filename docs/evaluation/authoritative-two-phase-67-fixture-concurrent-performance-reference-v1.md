# Two-phase 67-fixture concurrent-residency baseline V1

## Scope

One synthetic 67-page PDF built from the canonical page fixtures:

- Habermas: 9
- Ehrman: 8
- De Decretis: 50

The public `DocumentProcessor.ProcessAsync(...)` is called once per run,
so every page requiring prepared layout completes Phase 1 and is written
to the Authoritative disk spool before Phase 2 starts.

PP-StructureV3 and PaddleOCR remain resident concurrently.

## Method

- 1 warmup run
- 3 measured runs
- monitor sampling: 200 ms
- fresh harness process per run
- no automatic acceptance threshold

## Runs

| Run | Total s | Layout calls | OCR calls | DPE peak MiB | PP max MiB | Paddle max MiB | Min available MiB | Tracked peak MiB | Spool MiB |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| warmup | 174.950 | 14 | 66 | 375.3 | 9443.8 | 2075.9 | 11295.0 | 10106.9 | 0.026 |
| run-1 | 178.223 | 14 | 66 | 386.2 | 9558.8 | 2134.7 | 10163.6 | 11678.1 | 0.026 |
| run-2 | 178.802 | 14 | 66 | 383.7 | 9603.1 | 2131.6 | 9927.2 | 11724.9 | 0.026 |
| run-3 | 176.491 | 14 | 66 | 350.9 | 9571.9 | 2184.0 | 9842.9 | 11936.8 | 0.026 |

## Measured aggregate

- Median total: 178.223 s
- Throughput: 0.38 pages/s
- Median full-page raster: 13.900 s
- Median PP service: 85.093 s
- Median targeted region render: 44.945 s
- Median PaddleOCR service: 29.849 s
- Median visual planning: 1.398 s
- Managed allocations median: 2786.5 MiB
- DPE peak working set max: 386.2 MiB
- PP sampled current max: 9603.1 MiB
- Paddle sampled current max: 2184.0 MiB
- Combined tracked residency max: 11936.8 MiB
- Minimum host MemAvailable: 9842.9 MiB
- Peak spool: 0.026 MiB

## Runtime two-phase guards

- Figure OCR = 0 on every run.
- full-page render count == layout call count on every run.
- peak spool file count == layout call count on every run.
- first targeted region render occurs only after the last PP layout call.

This is a characterization baseline, not a full-book functional run.
