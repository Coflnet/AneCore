#!/usr/bin/env python3
"""Import exact, source-backed model names beyond the original category caps."""
import argparse
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path

import build_seed as seed
import normalize as nz

REVISION = "c52a04ca9465c83997ed335f7767b09a2005dd26"
SOURCE = "seed:docyx-pc-part-dataset-2026-10"
# Pin the input bytes as well as the revision; never silently ingest changed upstream data.
INPUTS = {
    "monitor": ("57a6cdb4c1798e47160cb44596c7303e9817daecaa9ffe6a0f31072da75d8042", "monitors", seed.CAT_MONITOR,
                ("screen_size", "resolution", "refresh_rate")),
    "motherboard": ("7bf4ecb66756c40234ccc1f6e2e43ca477dea7ef3f6e5c92d9b432d5776c6a1b", "motherboards", seed.CAT_MOBO,
                    ("socket", "form_factor")),
    "cpu": ("1e3fd9ba6bf11fca4217675d67ed5edd0d60cc6cb2604418c443cf474fe58c1c", "cpu", seed.CAT_CPU,
            ("core_count", "core_clock", "boost_clock", "microarchitecture", "tdp", "graphics")),
    "internal-hard-drive": ("0634f9c8157d649cf4e6a67ffcaa69ae22695f7a0befb1c60fdfffe921845d3b", "storage", seed.CAT_STORAGE,
                            ("type", "form_factor", "interface")),
    "memory": ("2c70f628bebd31bb47ef5b46b39de52c41a417b17dfcc0c63991b2d27310de63", "ram", seed.CAT_RAM,
               ("speed", "modules", "cas_latency")),
    "headphones": ("84b601bafaeb48f981eda8caa00cc58e7812465c6b89044064ab0e78327c753c", "headphones", seed.CAT_HEADPHONES,
                   ("wireless", "microphone", "type", "enclosure_type")),
}
COMPOUND_BRANDS = ("Cooler Master", "Turtle Beach", "Cyber Acoustics", "Harman Kardon", "Altec Lansing",
                   "Mad Catz", "Final Audio", "Titan Army", "Western Digital", "Silicon Power", "SK Hynix",
                   "Oyen Digital", "Super Talent", "Neo Forza", "Fantom Drives", "Edge Tech")
# Reviewed spelling variants in the pinned source; never compact arbitrary model/SKU names.
CANONICAL_NAMES = {
    "Kingston SSD Now mS200": "Kingston SSDNow mS200",
    "AKG K271 MK II": "AKG K271MKII",
    "Audio-Technica ATHM50S": "Audio-Technica ATH-M50S",
    "Coby CVH32": "Coby CV-H32",
}


def expand(raw_dir):
    # Keep curated aliases/attributes and stable ids intact. Expanded files are reproducible outputs,
    # never input: deleting them and running again produces exactly the same catalogue.
    existing = [p for f in sorted(Path(seed.SEED_DIR).glob("*.json")) if not f.stem.endswith("-expanded")
                for p in json.loads(f.read_text())["products"]]
    ids = {p["id"] for p in existing}
    aliases = {tuple(nz.matcher_tokens(a)) for p in existing for a in p["aliases"]}
    for source_file, (digest, category, path, fields) in INPUTS.items():
        data = (raw_dir / f"pcpp-{source_file}.json").read_bytes()
        if hashlib.sha256(data).hexdigest() != digest:
            raise ValueError(f"{source_file}: input differs from pinned dataset {REVISION}")
        groups = defaultdict(list)
        for row in json.loads(data):
            name = (row.get("name") or "").strip()
            # Names without a model number are often umbrella families (LG UltraGear, HP OMEN),
            # which mix unrelated devices. Never manufacture variants from specs to fill those gaps.
            if (len(name.split()) < 2 or name.split()[0].casefold() == name.split()[1].casefold()
                    or not any(c.isdigit() for c in name.split(" ", 1)[1])):
                continue
            if source_file == "memory" and not any(c.isdigit() for c in re.sub(r"\s+\d+\s+GB$", "", name)):
                continue  # A capacity alone does not distinguish a RAM kit's generation/speed.
            groups[tuple(nz.matcher_tokens(CANONICAL_NAMES.get(name, name)))].append(row)
        products = []
        ambiguous = 0
        for key, rows in sorted(groups.items()):
            # Same marketing name may describe different sizes/panels, sockets or wireless variants.
            # A broad alias would merge those devices; omit the whole ambiguous name instead.
            if any(len({json.dumps(r.get(field), sort_keys=True) for r in rows}) != 1 for field in fields):
                ambiguous += 1
                continue
            if source_file == "headphones" and rows[0].get("microphone") is not False:
                continue  # Headsets need their own category/guard treatment.
            name = sorted(CANONICAL_NAMES.get(r["name"].strip(), r["name"].strip()) for r in rows)[0]
            pid = nz.slugify(name)
            if pid in ids or key in aliases:
                continue
            brand = next((b for b in COMPOUND_BRANDS if name.startswith(b + " ")), name.split()[0])
            possible = {}
            if source_file == "monitor" and rows[0].get("screen_size"):
                possible["screen_size"] = [f'{rows[0]["screen_size"]}"']
            if source_file == "internal-hard-drive":
                possible["storage_size"] = sorted({nz.fmt_storage(r["capacity"]) for r in rows if r.get("capacity")})
            if source_file == "memory" and rows[0].get("modules"):
                count, capacity = rows[0]["modules"]
                possible["ram_size"] = [nz.fmt_ram(count * capacity)]
            reviewed_aliases = {name} | {r["name"].strip() for r in rows if r["name"].strip() in CANONICAL_NAMES}
            product = seed.make_product(pid, brand, name, name, path, "electronics",
                                        reviewed_aliases, "docyx-pc-part-dataset", possible)
            product["source"] = SOURCE
            products.append(product)
            ids.add(pid)
            aliases.add(key)
        # Only exact source names are aliases; no brandless codes, inferred sizes or SKU cross products.
        excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in existing + products})
        for product in products:
            product["excludeTerms"] = sorted(excludes[product["id"]])
        seed.write_seed(category + "-expanded", sorted(products, key=lambda p: p["id"]))
        existing.extend(products)
        print(f"{category}: {len(products)} additions; {ambiguous} ambiguous source names omitted")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw-dir", type=Path, required=True)
    expand(parser.parse_args().raw_dir)
