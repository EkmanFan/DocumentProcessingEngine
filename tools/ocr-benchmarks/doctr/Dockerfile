FROM python:3.11-slim-bookworm

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgl1 libglib2.0-0 libgomp1 \
    && rm -rf /var/lib/apt/lists/*

RUN python -m pip install --no-cache-dir \
      --index-url https://download.pytorch.org/whl/cpu \
      "torch==2.8.0" "torchvision==0.23.0" \
    && python -m pip install --no-cache-dir "python-doctr==1.0.1"

WORKDIR /workspace
ENTRYPOINT ["python", "/workspace/tools/ocr-benchmarks/doctr/run_doctr_benchmark.py"]
