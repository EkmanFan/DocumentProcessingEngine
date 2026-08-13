#!/usr/bin/env python3
import argparse, hashlib, json, os, platform, resource, sys, time, traceback
from pathlib import Path
import numpy as np
import torch, torchvision, doctr
from PIL import Image
from doctr.models import ocr_predictor

SCHEMA="document-processing-ocr-engine-result-v1"
MANIFEST_SCHEMA="document-processing-ocr-benchmark-manifest-v1"
INDEX_SCHEMA="document-processing-ocr-benchmark-input-index-v1"

def args():
    p=argparse.ArgumentParser()
    for name in ("manifest","input-index","input-dir","output"):
        p.add_argument(f"--{name}", required=True)
    return p.parse_args()

def load(path): return json.loads(Path(path).read_text(encoding="utf-8"))

def save(path,obj):
    path=Path(path); path.parent.mkdir(parents=True,exist_ok=True)
    tmp=path.with_name(path.name+f".tmp-{os.getpid()}")
    tmp.write_text(json.dumps(obj,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    tmp.replace(path)

def verify(manifest,index,input_dir):
    if manifest.get("schemaVersion")!=MANIFEST_SCHEMA: raise RuntimeError("Unsupported manifest schema")
    if index.get("schemaVersion")!=INDEX_SCHEMA: raise RuntimeError("Unsupported input-index schema")
    if index.get("benchmarkId")!=manifest.get("benchmarkId"): raise RuntimeError("benchmarkId mismatch")
    if index.get("sourceSha256")!=manifest["source"]["sha256"]: raise RuntimeError("source SHA mismatch")
    expected=sorted(p["pageNumber"] for p in manifest["pages"])
    actual=sorted(p["pageNumber"] for p in index["pages"])
    if expected!=actual: raise RuntimeError("page set mismatch")
    root=Path(input_dir)
    for page in index["pages"]:
        image=root/page["fileName"]
        if not image.is_file(): raise RuntimeError(f"Missing input image: {image}")
        if hashlib.sha256(image.read_bytes()).hexdigest()!=page["sha256"]:
            raise RuntimeError(f"Input SHA mismatch for p{page['pageNumber']}")

def bounds(geometry):
    pts=np.asarray(geometry,dtype=float)
    if pts.shape==(2,2):
        left,top=float(pts[0][0]),float(pts[0][1]); right,bottom=float(pts[1][0]),float(pts[1][1])
    elif pts.ndim==2 and pts.shape[1]==2:
        left,top=float(pts[:,0].min()),float(pts[:,1].min()); right,bottom=float(pts[:,0].max()),float(pts[:,1].max())
    else: raise RuntimeError(f"Unsupported docTR geometry shape: {pts.shape}")
    clamp=lambda v:min(max(v,0.0),1.0)
    return {"left":clamp(left),"top":clamp(top),"right":clamp(right),"bottom":clamp(bottom)}

def regions(page):
    out=[]
    for block in page.blocks:
        for line in block.lines:
            text=" ".join(str(w.value) for w in line.words if str(w.value).strip()).strip()
            if text:
                out.append({"sequence":len(out),"text":text,"confidence":None,"bounds":bounds(line.geometry)})
    return out

def main():
    a=args(); manifest=load(a.manifest); index=load(a.input_index); verify(manifest,index,a.input_dir)

    # Stable 1.0.x contract check: keep benchmark arguments aligned with the
    # tagged API rather than moving "latest" documentation.
    import inspect
    from doctr.models.builder import DocumentBuilder

    builder_params = set(inspect.signature(DocumentBuilder.__init__).parameters)
    required_builder_params = {
        "resolve_lines",
        "resolve_blocks",
        "paragraph_break",
        "export_as_straight_boxes",
    }

    if not required_builder_params.issubset(builder_params):
        raise RuntimeError(
            "Unexpected docTR DocumentBuilder signature: "
            + ", ".join(sorted(builder_params))
        )

    started=time.perf_counter()
    predictor=ocr_predictor(
        det_arch="fast_base", reco_arch="crnn_vgg16_bn", pretrained=True,
        assume_straight_pages=True, preserve_aspect_ratio=True, symmetric_pad=True,
        detect_orientation=False, straighten_pages=False, detect_language=False,
        resolve_lines=True, resolve_blocks=False,
    ).to(torch.device("cpu"))
    predictor.eval(); startup_ms=(time.perf_counter()-started)*1000.0
    pages=[]; root=Path(a.input_dir); trace_printed=False
    for spec in sorted(index["pages"],key=lambda x:x["pageNumber"]):
        n=spec["pageNumber"]; t=time.perf_counter(); diagnostics=[]
        try:
            with Image.open(root/spec["fileName"]) as image:
                rgb=image.convert("RGB")
                if rgb.size!=(spec["width"],spec["height"]): raise RuntimeError("image dimensions mismatch")
                array=np.asarray(rgb)
            with torch.inference_mode(): prediction=predictor([array])
            if len(prediction.pages)!=1: raise RuntimeError("expected one docTR page result")
            regs=regions(prediction.pages[0]); status="Completed"
        except Exception as exc:
            regs=[]; status="Failed"; diagnostics=[f"{type(exc).__name__}: {exc}"]
            print(f"p{n}: OCR ERROR: {diagnostics[0]}",file=sys.stderr,flush=True)
            if not trace_printed: traceback.print_exc(); trace_printed=True
        elapsed=(time.perf_counter()-t)*1000.0
        pages.append({"pageNumber":n,"inputSha256":spec["sha256"],"status":status,"elapsedMilliseconds":elapsed,
                      "imageWidth":spec["width"],"imageHeight":spec["height"],"regions":regs,"diagnostics":diagnostics})
        print(f"p{n}: {status}, regions={len(regs)}, elapsedMs={elapsed:.1f}",flush=True)
    rss=resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    peak=int(rss*1024) if sys.platform.startswith("linux") else int(rss)
    result={
      "schemaVersion":SCHEMA,"benchmarkId":manifest["benchmarkId"],"sourceSha256":manifest["source"]["sha256"],
      "engine":{"id":"doctr","version":getattr(doctr,"__version__","unknown"),"model":"fast_base+crnn_vgg16_bn",
        "backend":"pytorch","device":"cpu","metadata":{"torchVersion":getattr(torch,"__version__","unknown"),
        "torchvisionVersion":getattr(torchvision,"__version__","unknown"),"pythonVersion":platform.python_version(),
        "regionGranularity":"resolved-line","regionConfidence":"null-word-confidence-not-aggregated",
        "orientation":"disabled","languageDetection":"disabled","layoutDetection":"disabled","tableDetection":"disabled",
        "torchNumThreads":str(torch.get_num_threads())}},
      "performance":{"startupMilliseconds":startup_ms,"processPeakWorkingSetBytes":peak,"acceleratorPeakMemoryBytes":None},
      "pages":pages}
    save(a.output,result)
    completed=sum(p["status"]=="Completed" for p in pages); text_pages=sum(bool(p["regions"]) for p in pages)
    print("\nRESULT: DOCTR CPU BENCHMARK COMPLETE")
    print(f"docTR: {result['engine']['version']}")
    print(f"PyTorch: {result['engine']['metadata']['torchVersion']}")
    print(f"Completed / failed / text pages: {completed} / {len(pages)-completed} / {text_pages}")
    print(f"Startup ms: {startup_ms:.1f}")
    print(f"Output: {Path(a.output).resolve()}")
if __name__=="__main__": main()
