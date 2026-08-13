#!/usr/bin/env python3

import argparse
import hashlib
import json
import math
import os
import platform
import resource
import sys
import time
from pathlib import Path


GROUND_TRUTH_SCHEMA = "document-processing-ocr-ground-truth-v1"
INPUT_INDEX_SCHEMA = "document-processing-ocr-benchmark-input-index-v1"
CROP_INDEX_SCHEMA = "document-processing-ocr-zone-crop-index-v1"
ENGINE_RESULT_SCHEMA = "document-processing-ocr-engine-result-v1"


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write_json_atomic(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + f".tmp-{os.getpid()}")
    temporary.write_text(
        json.dumps(value, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_args():
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    prepare = subparsers.add_parser("prepare")
    prepare.add_argument("--ground-truth", required=True)
    prepare.add_argument("--input-index", required=True)
    prepare.add_argument("--input-dir", required=True)
    prepare.add_argument("--crop-dir", required=True)
    prepare.add_argument("--crop-index", required=True)

    run = subparsers.add_parser("run")
    run.add_argument("--engine", choices=("paddleocr", "doctr"), required=True)
    run.add_argument("--ground-truth", required=True)
    run.add_argument("--crop-index", required=True)
    run.add_argument("--crop-dir", required=True)
    run.add_argument("--output", required=True)

    return parser.parse_args()


def validate_bounds(bounds, label):
    for name in ("left", "top", "right", "bottom"):
        value = bounds.get(name)
        if not isinstance(value, (int, float)) or not math.isfinite(value):
            raise RuntimeError(f"Invalid {name} for {label}.")

    if not (
        0.0 <= bounds["left"] < bounds["right"] <= 1.0
        and 0.0 <= bounds["top"] < bounds["bottom"] <= 1.0
    ):
        raise RuntimeError(f"Invalid normalized bounds for {label}.")


def prepare_crops(args):
    from PIL import Image

    ground_truth = read_json(args.ground_truth)
    input_index = read_json(args.input_index)

    if ground_truth.get("schemaVersion") != GROUND_TRUTH_SCHEMA:
        raise RuntimeError("Unsupported OCR ground-truth schema.")

    if input_index.get("schemaVersion") != INPUT_INDEX_SCHEMA:
        raise RuntimeError("Unsupported OCR input-index schema.")

    if ground_truth.get("benchmarkId") != input_index.get("benchmarkId"):
        raise RuntimeError("Ground truth and input index benchmarkId differ.")

    if ground_truth.get("sourceSha256") != input_index.get("sourceSha256"):
        raise RuntimeError("Ground truth and input index source SHA differ.")

    pages = {
        page["pageNumber"]: page
        for page in input_index["pages"]
    }

    crop_dir = Path(args.crop_dir)
    crop_dir.mkdir(parents=True, exist_ok=True)

    crops = []

    for ordinal, zone in enumerate(ground_truth["zones"]):
        zone_id = zone["id"]
        page_number = zone["pageNumber"]
        bounds = zone["bounds"]

        validate_bounds(bounds, zone_id)

        page_spec = pages.get(page_number)
        if page_spec is None:
            raise RuntimeError(
                f"Ground-truth page {page_number} is missing from OCR input index."
            )

        source_path = Path(args.input_dir) / page_spec["fileName"]

        if not source_path.is_file():
            raise RuntimeError(f"Rendered page image is missing: {source_path}")

        if sha256_file(source_path) != page_spec["sha256"]:
            raise RuntimeError(
                f"Rendered page SHA mismatch for page {page_number}."
            )

        with Image.open(source_path) as image:
            rgb = image.convert("RGB")
            width, height = rgb.size

            if width != page_spec["width"] or height != page_spec["height"]:
                raise RuntimeError(
                    f"Rendered page dimensions mismatch for page {page_number}."
                )

            left = max(0, min(width - 1, math.floor(bounds["left"] * width)))
            top = max(0, min(height - 1, math.floor(bounds["top"] * height)))
            right = max(left + 1, min(width, math.ceil(bounds["right"] * width)))
            bottom = max(top + 1, min(height, math.ceil(bounds["bottom"] * height)))

            crop = rgb.crop((left, top, right, bottom))
            file_name = f"{ordinal:02d}-{zone_id}.png"
            crop_path = crop_dir / file_name
            crop.save(crop_path, format="PNG")

        crop_sha = sha256_file(crop_path)

        crops.append(
            {
                "ordinal": ordinal,
                "zoneId": zone_id,
                "pageNumber": page_number,
                "description": zone["description"],
                "sourcePageFileName": page_spec["fileName"],
                "sourcePageSha256": page_spec["sha256"],
                "sourcePageWidth": page_spec["width"],
                "sourcePageHeight": page_spec["height"],
                "normalizedBounds": bounds,
                "pixelBounds": {
                    "left": left,
                    "top": top,
                    "right": right,
                    "bottom": bottom,
                },
                "fileName": file_name,
                "width": right - left,
                "height": bottom - top,
                "sha256": crop_sha,
            }
        )

        print(
            f"{zone_id}: p{page_number} "
            f"{right-left}x{bottom-top} "
            f"sha256={crop_sha[:16]}...",
            flush=True,
        )

    index = {
        "schemaVersion": CROP_INDEX_SCHEMA,
        "benchmarkId": ground_truth["benchmarkId"],
        "sourceSha256": ground_truth["sourceSha256"],
        "cropMethod": (
            "exact-normalized-ground-truth-bounds;"
            "floor-left-top;ceil-right-bottom;no-padding;no-rescale"
        ),
        "crops": crops,
    }

    write_json_atomic(args.crop_index, index)

    print()
    print("RESULT: OCR ZONE CROPS PREPARED")
    print(f"Zones: {len(crops)}")
    print(f"Crop index: {Path(args.crop_index).resolve()}")


def unwrap_paddle_result_json(value):
    if isinstance(value, dict):
        if "rec_texts" in value and "rec_boxes" in value:
            return value

        if "res" in value:
            found = unwrap_paddle_result_json(value["res"])
            if found is not None:
                return found

        for nested in value.values():
            found = unwrap_paddle_result_json(nested)
            if found is not None:
                return found

    return None


def build_paddle_engine():
    import paddle
    import paddleocr
    from paddleocr import PaddleOCR

    started = time.perf_counter()

    ocr = PaddleOCR(
        text_detection_model_name="PP-OCRv6_medium_det",
        text_recognition_model_name="PP-OCRv6_medium_rec",
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
        device="cpu",
    )

    startup_ms = (time.perf_counter() - started) * 1000.0

    def recognize(image_path):
        prediction = list(ocr.predict(str(image_path)))

        if len(prediction) != 1:
            raise RuntimeError(
                f"Expected one PaddleOCR result, received {len(prediction)}."
            )

        native = unwrap_paddle_result_json(prediction[0].json)

        if native is None:
            raise RuntimeError(
                "Could not locate PaddleOCR rec_texts in result."
            )

        texts = [
            str(text).strip()
            for text in native.get("rec_texts", [])
            if str(text).strip()
        ]

        return "\n".join(texts)

    metadata = {
        "id": "paddleocr-zone-crop",
        "version": getattr(paddleocr, "__version__", "unknown"),
        "model": "PP-OCRv6_medium_det+PP-OCRv6_medium_rec",
        "backend": "paddle_static",
        "device": "cpu",
        "metadata": {
            "paddleVersion": getattr(paddle, "__version__", "unknown"),
            "evaluationMode": "isolated-zone-crop",
            "cropRescale": "none",
        },
    }

    return recognize, metadata, startup_ms


def build_doctr_engine():
    import numpy as np
    import torch
    import doctr
    from PIL import Image
    from doctr.models import ocr_predictor

    started = time.perf_counter()

    predictor = ocr_predictor(
        det_arch="fast_base",
        reco_arch="crnn_vgg16_bn",
        pretrained=True,
        assume_straight_pages=True,
        preserve_aspect_ratio=True,
        symmetric_pad=True,
        detect_orientation=False,
        straighten_pages=False,
        detect_language=False,
        resolve_lines=True,
        resolve_blocks=False,
    ).to(torch.device("cpu"))

    predictor.eval()

    startup_ms = (time.perf_counter() - started) * 1000.0

    def recognize(image_path):
        with Image.open(image_path) as image:
            array = np.asarray(image.convert("RGB"))

        with torch.inference_mode():
            prediction = predictor([array])

        if len(prediction.pages) != 1:
            raise RuntimeError(
                f"Expected one docTR page result, received {len(prediction.pages)}."
            )

        lines = []

        for block in prediction.pages[0].blocks:
            for line in block.lines:
                text = " ".join(
                    str(word.value)
                    for word in line.words
                    if str(word.value).strip()
                ).strip()

                if text:
                    lines.append(text)

        return "\n".join(lines)

    metadata = {
        "id": "doctr-zone-crop",
        "version": getattr(doctr, "__version__", "unknown"),
        "model": "fast_base+crnn_vgg16_bn",
        "backend": "pytorch",
        "device": "cpu",
        "metadata": {
            "torchVersion": getattr(torch, "__version__", "unknown"),
            "evaluationMode": "isolated-zone-crop",
            "cropRescale": "none",
            "resolveLinesWithinCrop": "true",
        },
    }

    return recognize, metadata, startup_ms


def verify_crop_index(ground_truth, crop_index, crop_dir):
    if ground_truth.get("schemaVersion") != GROUND_TRUTH_SCHEMA:
        raise RuntimeError("Unsupported OCR ground-truth schema.")

    if crop_index.get("schemaVersion") != CROP_INDEX_SCHEMA:
        raise RuntimeError("Unsupported OCR zone-crop index schema.")

    if ground_truth.get("benchmarkId") != crop_index.get("benchmarkId"):
        raise RuntimeError("Ground truth and crop index benchmarkId differ.")

    if ground_truth.get("sourceSha256") != crop_index.get("sourceSha256"):
        raise RuntimeError("Ground truth and crop index source SHA differ.")

    zones = ground_truth["zones"]
    crops = crop_index["crops"]

    if len(zones) != len(crops):
        raise RuntimeError("Ground-truth zone count and crop count differ.")

    crop_root = Path(crop_dir)

    for zone, crop in zip(zones, crops):
        if zone["id"] != crop["zoneId"]:
            raise RuntimeError("Ground-truth and crop order differ.")

        if zone["pageNumber"] != crop["pageNumber"]:
            raise RuntimeError(
                f"Page mismatch for zone {zone['id']}."
            )

        if zone["bounds"] != crop["normalizedBounds"]:
            raise RuntimeError(
                f"Normalized bounds mismatch for zone {zone['id']}."
            )

        crop_path = crop_root / crop["fileName"]

        if not crop_path.is_file():
            raise RuntimeError(f"Crop image missing: {crop_path}")

        if sha256_file(crop_path) != crop["sha256"]:
            raise RuntimeError(
                f"Crop SHA mismatch for zone {zone['id']}."
            )


def run_engine(args):
    ground_truth = read_json(args.ground_truth)
    crop_index = read_json(args.crop_index)

    verify_crop_index(
        ground_truth,
        crop_index,
        args.crop_dir,
    )

    if args.engine == "paddleocr":
        recognize, engine, startup_ms = build_paddle_engine()
    elif args.engine == "doctr":
        recognize, engine, startup_ms = build_doctr_engine()
    else:
        raise RuntimeError(f"Unsupported engine: {args.engine}")

    crop_root = Path(args.crop_dir)
    pages = {}
    total_elapsed_ms = 0.0

    for crop in crop_index["crops"]:
        started = time.perf_counter()
        text = recognize(crop_root / crop["fileName"])
        elapsed_ms = (time.perf_counter() - started) * 1000.0
        total_elapsed_ms += elapsed_ms

        page_number = crop["pageNumber"]

        if page_number not in pages:
            pages[page_number] = {
                "pageNumber": page_number,
                "inputSha256": crop["sourcePageSha256"],
                "status": "Completed",
                "elapsedMilliseconds": 0.0,
                "imageWidth": crop["sourcePageWidth"],
                "imageHeight": crop["sourcePageHeight"],
                "regions": [],
                "diagnostics": [],
            }

        page = pages[page_number]
        sequence = len(page["regions"])

        page["elapsedMilliseconds"] += elapsed_ms
        page["regions"].append(
            {
                "sequence": sequence,
                "text": text,
                "confidence": None,
                "bounds": crop["normalizedBounds"],
            }
        )

        print(
            f"{engine['id']} {crop['zoneId']}: "
            f"chars={len(text)} elapsedMs={elapsed_ms:.1f}",
            flush=True,
        )

    peak_rss = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss

    if sys.platform.startswith("linux"):
        peak_working_set_bytes = int(peak_rss * 1024)
    else:
        peak_working_set_bytes = int(peak_rss)

    result = {
        "schemaVersion": ENGINE_RESULT_SCHEMA,
        "benchmarkId": ground_truth["benchmarkId"],
        "sourceSha256": ground_truth["sourceSha256"],
        "engine": engine,
        "performance": {
            "startupMilliseconds": startup_ms,
            "cropOcrElapsedMilliseconds": total_elapsed_ms,
            "processPeakWorkingSetBytes": peak_working_set_bytes,
        },
        "pages": [
            pages[number]
            for number in sorted(pages)
        ],
    }

    write_json_atomic(args.output, result)

    print()
    print("RESULT: OCR ZONE-CROP ENGINE RUN COMPLETE")
    print(f"Engine: {engine['id']} {engine['version']}")
    print(f"Zones: {len(crop_index['crops'])}")
    print(f"Startup ms: {startup_ms:.1f}")
    print(f"Crop OCR elapsed ms: {total_elapsed_ms:.1f}")
    print(f"Output: {Path(args.output).resolve()}")


def main():
    args = parse_args()

    if args.command == "prepare":
        prepare_crops(args)
    elif args.command == "run":
        run_engine(args)
    else:
        raise RuntimeError(f"Unsupported command: {args.command}")


if __name__ == "__main__":
    main()
