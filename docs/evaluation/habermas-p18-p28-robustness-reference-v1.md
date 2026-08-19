# Habermas p18/p28 robustness reference V1

## Status

**PASS**

Baseline before this increment:

```text
ba3e141f68700192866022f96bad7e0382e9880e
```

## p18

The standalone `habermas-p0018.pdf` fixture now completes through the public
`DocumentProcessor.ProcessAsync(...)` path as `NativeOnly` with one live PP
layout call, zero OCR calls, zero Figure OCR, and exactly one preserved Figure.
Native text remains authoritative.

The global layout-only assessor is intentionally unchanged: the p18-like
horizontal Figure still evaluates to `Unknown` without source evidence. The
Healthy Native executor may resolve that evidence only when an already-resolved
source plan provides a one-to-one singleton visual mapping and the Figure is
spatially independent from semantic text-like observations.

## p28

The standalone `habermas-p0028.pdf` fixture now completes through the public
`DocumentProcessor.ProcessAsync(...)` path as `NativeOnly` with one preserved
diagram and zero OCR calls.

The native block `They claimed it.` retains `NativePdf` authority and source
block sequence `2`, and is placed after the preserved diagram by the narrow
geometry fallback because PP exposes no comparable layout-text owner for that
block.

`Unknown/footer` is **not** promoted to an OCR target or authoritative text.
Geometry is used only to derive an unambiguous whole-block visual band.
Overlap or geometry/layout-order conflict remains fail-closed.

## Existing controls

Habermas p40, p43, and p44 were re-executed through the same live-PP public
path and each retained one preserved visual with zero OCR and zero Figure OCR.

## Explicitly separate issue

PP currently labels the large paragraph below the p28 diagram as
`figure_title`. Caption-semantics hardening remains open and is intentionally
outside this increment.

## Performance reference

The frozen 67-fixture performance reference at `ba3e141` remains unchanged.
The new p18/p28 fixtures extend semantic correctness coverage; they do not
retroactively alter the 67-fixture performance baseline.
