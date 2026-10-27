#!/usr/bin/env python3
"""Rebuild the exact turntable identities from the reviewed manufacturer proof manifest."""
import argparse
import json
from pathlib import Path

import build_seed as seed
import normalize as nz

PROOF = Path(__file__).parent / "proofs" / "turntables-manufacturer-2026-10-27.json"
OUTPUT = Path(seed.SEED_DIR) / "turntables.json"
CATEGORY = ["Elektronik", "Audio", "Audioplayer & -rekorder", "Plattenspieler"]


def products():
    rows = []
    proof = json.loads(PROOF.read_text())
    for group in proof["groups"]:
        for model in group["models"]:
            name = f"{group['brand']} {model}"
            row = seed.make_product(nz.slugify(name), group["brand"], model, name, CATEGORY,
                                    "electronics", {name}, "manufacturer-verified",
                                    possible_attributes={"device_type": ["turntable"]})
            row["source"] = "seed:manufacturer-verified-2026-10-27"
            rows.append(row)
    existing = [p for path in sorted(OUTPUT.parent.glob("*.json")) if path != OUTPUT
                for p in json.loads(path.read_text())["products"]]
    owners = {}
    for row in existing + rows:
        if row["id"] in owners:
            raise ValueError(f"Duplicate id: {row['id']}")
        owners[row["id"]] = row
    aliases = {}
    for row in existing + rows:
        for alias in row["aliases"]:
            key = tuple(nz.matcher_tokens(alias))
            if key in aliases and aliases[key] != row["id"]:
                raise ValueError(f"Alias collision: {alias}")
            aliases[key] = row["id"]
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in rows})
    for row in rows:
        row["excludeTerms"] = sorted(excludes[row["id"]] | set(proof["part_scope"]["terms"]))
    return sorted(rows, key=lambda p: p["id"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Require the committed seed to match the proof")
    args = parser.parse_args()
    expected = {"products": products()}
    if args.check:
        if json.loads(OUTPUT.read_text()) != expected:
            raise SystemExit("Turntable seed differs from reviewed proof")
    else:
        seed.write_seed("turntables", expected["products"])
    print(f"Verified {len(expected['products'])} turntable identities")
