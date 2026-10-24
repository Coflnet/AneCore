#!/usr/bin/env python3
"""Add exact camera models and GPU chipset families from pinned, MIT-licensed datasets."""
import argparse
import csv
import hashlib
import io
import json
from pathlib import Path

import build_seed as seed
import normalize as nz

GPU_SHA256 = "bb0d199e880d02d512f312132d68dbe70b4bf7079bbc18b850b1f40513874575"
CAMERA_SHA256 = "ba12ac7eb007e67a38b75749bc019ef909203e0dc69b7f414c43fe2b98e56845"
# CameraDatabase also contains Nokia smartphones. Restrict this pass to camera manufacturers whose
# product families were reviewed; do not classify every row in a camera dataset as a camera.
CAMERA_BRANDS = {"Canon", "Nikon", "Sony", "Fujifilm", "Olympus", "Panasonic", "Pentax", "Ricoh",
                 "Leica", "Sigma", "Kodak", "Casio", "Minolta", "Konica-Minolta", "Samsung"}
OUTPUTS = {"gpu-expanded", "cameras-expanded"}


def pinned_bytes(path, digest):
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != digest:
        raise ValueError(f"{path.name}: input differs from pinned dataset")
    return data


def expand(raw_dir):
    # Include the previous expansion as input, but never consume this script's own generated outputs.
    existing = [p for f in sorted(Path(seed.SEED_DIR).glob("*.json")) if f.stem not in OUTPUTS
                for p in json.loads(f.read_text())["products"]]
    gpu_rows = json.loads(pinned_bytes(raw_dir / "pcpp-video-card.json", GPU_SHA256))
    camera_rows = csv.DictReader(io.StringIO(pinned_bytes(raw_dir / "camera_data.csv", CAMERA_SHA256).decode()))
    candidates = {"gpu-expanded": [], "cameras-expanded": []}
    for chip in sorted({seed.clean_gpu_chipset(r["chipset"]) for r in gpu_rows}):
        brand = ("Intel" if chip.startswith("Arc ") else "AMD" if chip.startswith(("Radeon ", "Fire", "Vega "))
                 else "Nvidia")
        name = f"{brand} {chip.removeprefix('GeForce ')}"
        aliases = {name, chip}
        short = chip.removeprefix("GeForce ").removeprefix("Radeon ")
        if any(c.isalpha() for c in short):
            aliases.add(short)
        product = seed.make_product(nz.slugify(name), brand, chip, name, seed.CAT_GPU, "electronics",
                                    aliases, "docyx-pc-part-dataset")
        product["source"] = "seed:docyx-pc-part-dataset-2026-10"
        candidates["gpu-expanded"].append(product)
    for row in camera_rows:
        brand, model = row["Brand"].strip(), row["Model"].strip()
        if brand not in CAMERA_BRANDS or not any(c.isdigit() for c in model):
            continue  # Avoid umbrella names and models requiring separate no-number identity review.
        name = f"{brand} {model}"
        product = seed.make_product(nz.slugify(name), brand, model, name, seed.CAT_CAMERA, "electronics",
                                    {name}, "cameradatabase")
        product["source"] = "seed:cameradatabase-2026-10"
        candidates["cameras-expanded"].append(product)
    ids = {p["id"] for p in existing}
    aliases = {tuple(nz.matcher_tokens(a)) for p in existing for a in p["aliases"]}
    for output, rows in candidates.items():
        products = []
        for product in sorted(rows, key=lambda p: p["id"]):
            keys = {tuple(nz.matcher_tokens(a)) for a in product["aliases"]}
            if product["id"] in ids or keys & aliases:
                continue
            products.append(product)
            ids.add(product["id"])
            aliases.update(keys)
        excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in existing + products})
        for product in products:
            # A desktop GTX 780 must not claim a laptop's GTX 780M; explicit source M models retain
            # their own longer aliases. This mirrors the existing token-based variant exclusions.
            product["excludeTerms"] = sorted(excludes[product["id"]] | ({"m"} if output == "gpu-expanded" else set()))
        seed.write_seed(output, products)
        existing.extend(products)
        print(f"{output}: {len(products)} additions")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw-dir", type=Path, required=True)
    expand(parser.parse_args().raw_dir)
