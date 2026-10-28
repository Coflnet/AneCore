#!/usr/bin/env python3
"""Rebuild the R41 LEGO supplement from reviewed manufacturer instruction panels."""
import argparse
import json
from pathlib import Path
import re

import build_seed as seed
import normalize as nz
from expand_lego import CATEGORIES

PROOF = Path(__file__).parent / "proofs" / "lego-city-technic-instructions-2026-10-29-r41.json"
OUTPUT = Path(seed.SEED_DIR) / "lego-city-technic-instructions.json"


def products():
    rows = []
    for fact in json.loads(PROOF.read_text())["facts"]:
        number = fact["number"]
        age = re.fullmatch(r"(\d+)(?:-\d+|\+) \| #" + number + r" \| \d+ pieces", fact["metadata"])
        if not age or not re.fullmatch(r"[1-9]\d{4}", number):
            raise ValueError(f"Invalid reviewed panel: {fact}")
        row = seed.make_product(f"lego-{number}", "LEGO", number, f"LEGO {number}", CATEGORIES,
                                "lego", {f"LEGO {number}", f"LEGO Set {number}", f"LEGO {number} {fact['name']}"},
                                "unused", {"set_number": [number], "min_age": [age[1]]})
        row["source"] = "seed:lego-official-city-technic-instructions-2026-10-29-r41"
        rows.append(row)
    existing = [p for path in sorted(OUTPUT.parent.glob("*.json")) if path != OUTPUT
                for p in json.loads(path.read_text())["products"]]
    ids, aliases = set(), {}
    for row in existing + rows:
        if row["id"] in ids:
            raise ValueError(f"Duplicate id: {row['id']}")
        ids.add(row["id"])
        for alias in row["aliases"]:
            key = tuple(nz.matcher_tokens(alias))
            if key in aliases and aliases[key] != row["id"]:
                raise ValueError(f"Alias collision: {alias}")
            aliases[key] = row["id"]
    return rows


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Require the seed to match the reviewed proof")
    args = parser.parse_args()
    expected = {"products": products()}
    if args.check:
        if json.loads(OUTPUT.read_text()) != expected:
            raise SystemExit("LEGO City/Technic seed differs from reviewed proof")
    else:
        seed.write_seed("lego-city-technic-instructions", expected["products"])
    print(f"Verified {len(expected['products'])} LEGO City/Technic/train identities")
