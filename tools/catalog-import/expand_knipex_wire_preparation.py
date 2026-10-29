#!/usr/bin/env python3
"""Rebuild the R45 manual wire-preparation articles from reviewed manufacturer facts, offline."""
import argparse
import json
from pathlib import Path

import build_seed as seed
import normalize as nz

PROOF = Path(__file__).parent / "proofs" / "knipex-wire-preparation-2026-10-29-r45.json"
OUTPUT = Path(seed.SEED_DIR) / "knipex-wire-preparation.json"


def products():
    proof = json.loads(PROOF.read_text())
    rows = []
    for group in proof["groups"]:
        for article, length in group["rows"]:
            compact = article.replace(" ", "")
            name = f"KNIPEX {group['name_kind']} {article}"
            aliases = {f"KNIPEX {article}", f"KNIPEX {compact}", name}
            aliases.update(proof["extra_aliases"].get(article, []))
            row = seed.make_product("knipex-" + compact.lower(), "KNIPEX", article, name,
                                    group["category"], "catalogued_goods", aliases, "unused",
                                    {"product_kind": ["hand_tool"], "tool_type": [group["tool_type"]],
                                     "article_number": [article], "length_mm": [str(length)]})
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
    excludes = nz.compute_prefix_exclude_terms({row["id"]: set(row["aliases"]) for row in existing + rows})
    for row in rows:
        row["excludeTerms"] = sorted(excludes[row["id"]] | set(proof["exclude_terms"])
                                     | set(proof["article_exclude_terms"].get(row["model"], [])))
    return sorted(rows, key=lambda row: row["id"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Require the selected seed to match the reviewed proof")
    parser.add_argument("--output", type=Path, default=OUTPUT, help="Use an external review candidate before integration")
    args = parser.parse_args()
    expected = {"products": products()}
    if args.check:
        if json.loads(args.output.read_text()) != expected:
            raise SystemExit("KNIPEX wire-preparation seed differs from reviewed proof")
    else:
        args.output.write_text(json.dumps(expected, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Verified {len(expected['products'])} KNIPEX manual wire-preparation article identities")
