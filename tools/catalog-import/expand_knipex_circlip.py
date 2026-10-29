#!/usr/bin/env python3
"""Rebuild the R43 complete-tool supplement from reviewed manufacturer table cells."""
import argparse
import json
from pathlib import Path

import build_seed as seed
import normalize as nz

PROOF = Path(__file__).parent / "proofs" / "knipex-circlip-pliers-2026-10-29-r43.json"
OUTPUT = Path(seed.SEED_DIR) / "knipex-circlip-pliers.json"


def products():
    proof = json.loads(PROOF.read_text())
    rows = []
    for group in proof["groups"]:
        for article, length in group["rows"]:
            compact = article.replace(" ", "")
            name = f"KNIPEX Sicherungsringzange {article}"
            row = seed.make_product("knipex-" + compact.lower(), "KNIPEX", article, name,
                                    ["Heimwerkerbedarf", "Werkzeuge", "Zangen"], "catalogued_goods",
                                    {f"KNIPEX {article}", f"KNIPEX {compact}", name}, "unused",
                                    {"product_kind": ["hand_tool"], "tool_type": [group["tool_type"]],
                                     "article_number": [article], "length_mm": [str(length)]},
                                    proof["exclude_terms"])
            row["source"] = proof["source"]
            rows.append(row)
    existing = [row for path in sorted(OUTPUT.parent.glob("*.json")) if path != OUTPUT
                for row in json.loads(path.read_text())["products"]]
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
    return sorted(rows, key=lambda row: row["id"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Require the seed to match the reviewed proof")
    args = parser.parse_args()
    expected = {"products": products()}
    if args.check:
        if json.loads(OUTPUT.read_text()) != expected:
            raise SystemExit("KNIPEX circlip seed differs from reviewed proof")
    else:
        seed.write_seed("knipex-circlip-pliers", expected["products"])
    print(f"Verified {len(expected['products'])} KNIPEX circlip tool identities")
