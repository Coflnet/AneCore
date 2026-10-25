#!/usr/bin/env python3
"""Add current LEGO identities from the pinned official product sitemap, not merchandising copy."""
import argparse
from collections import Counter, defaultdict
import csv
import gzip
import io
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

import build_seed as seed
from expand_lego import CATEGORIES, CURRENT_SHA256, verify

SITEMAP_SHA256 = "ca3c87d318c4c24763c6080f3412d856c5edfd90dc9f8699745d8e27f349038b"
OUTPUT = "lego-modern.json"


def expand(sitemap, current):
    root = ET.fromstring(verify(sitemap, SITEMAP_SHA256))
    counts = Counter(row["set_num"].split("-")[0] for row in csv.DictReader(
        io.StringIO(gzip.decompress(verify(current, CURRENT_SHA256)).decode())))
    urls = defaultdict(set)
    for item in root.findall("{http://www.sitemaps.org/schemas/sitemap/0.9}url"):
        url = item.findtext("{http://www.sitemaps.org/schemas/sitemap/0.9}loc", "")
        match = re.fullmatch(r"https://www\.lego\.com/en-us/product/[a-z0-9-]+-([1-9]\d{3,4})", url)
        if match:
            urls[match[1]].add(url)
    existing = {p["id"] for path in Path(seed.SEED_DIR).glob("*.json") if path.name != OUTPUT
                for p in json.loads(path.read_text())["products"]}
    products = []
    for code, official_urls in sorted(urls.items()):
        if len(official_urls) != 1 or counts[code] != 1 or f"lego-{code}" in existing:
            continue
        # Keep existing text-derived page identities; a URL slug is not an official display name.
        product = seed.make_product(f"lego-{code}", "LEGO", code, f"LEGO {code}", CATEGORIES, "lego",
                                    {f"LEGO {code}", f"LEGO Set {code}"}, "unused", {"set_number": [code]})
        product["source"] = "seed:lego-official-sitemap-2026-10"
        products.append(product)
    seed.write_seed("lego-modern", products)
    print(f"lego-modern: {len(products)} official identities; reused or unverified numbers omitted")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sitemap", type=Path, required=True)
    parser.add_argument("--current-sets", type=Path, required=True)
    args = parser.parse_args()
    expand(args.sitemap, args.current_sets)
