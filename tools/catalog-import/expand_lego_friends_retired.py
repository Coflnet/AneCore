#!/usr/bin/env python3
"""Rebuild the R48 Friends identities from reviewed official instruction/product headers."""
import argparse
import json
from pathlib import Path
import re
from urllib.parse import urlparse

import build_seed as seed
import normalize as nz
from expand_lego import CATEGORIES

PROOF = Path(__file__).parent / "proofs" / "lego-friends-retired-2026-10-29-r48.json"
OUTPUT = Path(seed.SEED_DIR) / "lego-friends-retired.json"


def products():
    rows = []
    for fact in json.loads(PROOF.read_text())["facts"]:
        number, name, metadata = fact["number"], fact["name"], fact["metadata"]
        url = urlparse(fact["source_url"])
        if not re.fullmatch(r"[1-9]\d{4}", number) or not name.strip() or metadata["h1"] != name:
            raise ValueError(f"Invalid reviewed identity: {fact}")
        if url.scheme != "https" or url.netloc != "www.lego.com" or not re.fullmatch(r"/en-[a-z]{2}/(?:service/building-?instructions/" + number + r"|product/[a-z0-9-]+-" + number + r")", url.path):
            raise ValueError(f"Invalid official identity URL: {fact}")
        if fact["evidence_type"] == "instruction_panel":
            valid = "/service/" in url.path and metadata.get("printed_number") == "#" + number and re.fullmatch(r"\d+(?:-\d+|\+) \| #" + number + r" \| \d+ pieces", metadata.get("panel", ""))
        elif fact["evidence_type"] == "official_product_page":
            valid = "/product/" in url.path and metadata.get("theme_and_printed_number") == "Friends#" + number
        else:
            valid = False
        if not valid:
            raise ValueError(f"Incomplete official header proof: {fact}")
        row = seed.make_product(f"lego-{number}", "LEGO", number, f"LEGO {number}", CATEGORIES,
                                "lego", {f"LEGO {number}", f"LEGO Set {number}", f"LEGO Friends {number}", f"LEGO {number} {name}"},
                                "unused", {"set_number": [number]})
        row["source"] = "seed:lego-official-friends-retired-2026-10-29-r48"
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
            raise SystemExit("Friends seed differs from reviewed official headers")
    else:
        seed.write_seed("lego-friends-retired", expected["products"])
    print(f"Verified {len(expected['products'])} distinct official LEGO Friends identities")
