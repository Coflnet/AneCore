#!/usr/bin/env python3
"""Import exact LEGO identities from the owner-authorized CC BY-SA 3.0 2014 snapshot."""
import argparse
from collections import Counter
import csv
import gzip
import hashlib
import io
from pathlib import Path
import re
import subprocess
import tempfile

import build_seed as seed

ARCHIVE_SHA256 = "b83fb2628bae06e3af744ee5fed3dc050d843cd6e2101ac789f93482be4f91b0"
CURRENT_SHA256 = "877c7b7b0c86a86cd9d334c96c0932163d948244bc04f7da38dfe90cd2657fed"
CATEGORIES = ["Spielzeuge & Spiele", "Spielzeuge", "Bausteine & Bauspielzeug"]
# Small manually verified official-source supplement. No names or artwork are copied from bulk data.
OFFICIAL = {"10294": "Titanic", "42115": "Lamborghini Sián FKP 37",
            "42143": "Ferrari Daytona SP3", "75192": "Millennium Falcon"}


def verify(path, digest):
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != digest:
        raise ValueError(f"{path.name}: input differs from pinned dataset")
    return data


def expand(archive, current):
    verify(archive, ARCHIVE_SHA256)
    # The current CSV is used ONLY as a veto for re-used printed numbers; no metadata is imported.
    rows = csv.DictReader(io.StringIO(gzip.decompress(verify(current, CURRENT_SHA256)).decode()))
    current_counts = Counter(row["set_num"].split("-")[0] for row in rows)
    with tempfile.TemporaryDirectory() as scratch:
        subprocess.run(["7z", "x", "-y", f"-o{scratch}", str(archive.resolve())],
                       check=True, stdout=subprocess.DEVNULL)
        files = sorted(Path(scratch).glob("*.csv"))
        counts = Counter(path.stem.split("-")[0] for path in files)
        codes = {path.stem.split("-")[0] for path in files
                 if re.fullmatch(r"[1-9]\d{3,4}-1", path.stem)
                 and counts[path.stem.split("-")[0]] == 1
                 and current_counts[path.stem.split("-")[0]] == 1
                 and any(csv.DictReader(path.read_text().splitlines()))}
    products = []
    for code in sorted(codes | OFFICIAL.keys()):
        name = f"LEGO {code} {OFFICIAL[code]}" if code in OFFICIAL else f"LEGO Set {code}"
        product = seed.make_product(f"lego-{code}", "LEGO", code, name, CATEGORIES, "lego",
                                    {f"LEGO {code}", f"LEGO Set {code}"}, "unused",
                                    {"set_number": [code]})
        product["source"] = ("seed:lego-official-2026-10" if code in OFFICIAL
                             else "seed:rebrickable-joncraton-2014")
        products.append(product)
    seed.write_seed("lego-sets", products)
    print(f"lego-sets: {len(products)} additions ({len(codes)} historical, {len(OFFICIAL)} official)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--current-sets", type=Path, required=True)
    args = parser.parse_args()
    expand(args.archive, args.current_sets)
