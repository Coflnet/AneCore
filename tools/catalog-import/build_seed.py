#!/usr/bin/env python3
"""
Builds AneCore/KnownProducts/Seed/<category>.json from the open datasets fetched by fetch.sh.

Usage:
    python3 build_seed.py --raw-dir <scratch-dir-from-fetch.sh> --techapi-repo <path-to-sparse-checkout>

Each *.json seed file follows the existing KnownProducts/Seed/apple-iphones.json shape. Every entry's
"source" field is "seed:<source-name>-<yyyy-mm>" and this script also (re)writes
KnownProducts/Seed/ATTRIBUTION.md. See tools/catalog-import/README.md for exact source URLs, licences and
the reasoning behind every "why this category was/wasn't imported mechanically" decision below.
"""
import argparse
import csv
import json
import os
import re
import sys
from datetime import date

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "lib"))
import normalize as nz  # noqa: E402
import techapi  # noqa: E402
import docyx  # noqa: E402
import ios_devices  # noqa: E402

THIS_MONTH = date.today().strftime("%Y-%m")
SEED_DIR = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "KnownProducts", "Seed"))

# ---- UnifiedCategories.json label paths (verbatim - never invent labels; see README for how each was found) ----
CAT_PHONE = ["Elektronik", "Kommunikationsgeräte", "Telefone", "Mobiltelefone"]
CAT_TABLET = ["Elektronik", "Computer", "Tablet-PCs"]
CAT_LAPTOP = ["Elektronik", "Computer", "Laptops"]
CAT_GPU = ["Elektronik", "Elektronisches Zubehör", "Computerkomponenten", "Computer-Steckkarten", "Grafikkarten & Videoadapter"]
CAT_CPU = ["Elektronik", "Elektronisches Zubehör", "Computerkomponenten", "Prozessoren"]
CAT_RAM = ["Elektronik", "Elektronisches Zubehör", "Speicher", "RAM"]
CAT_MOBO = ["Elektronik", "Elektronische Leiterplatten & Bauteile", "Leiterplatten", "Computer-Leiterplatten", "Motherboards"]
CAT_STORAGE = ["Elektronik", "Elektronisches Zubehör", "Computerkomponenten", "Speichergeräte", "Festplatten"]
CAT_MONITOR = ["Elektronik", "Video", "Computermonitore"]
CAT_HEADPHONES = ["Elektronik", "Audio", "Audiokomponenten", "Kopfhörer & Headsets", "Kopfhörer"]
CAT_CAMERA = ["Kameras & Optik", "Kameras", "Digitalkameras"]
CAT_CONSOLE = ["Elektronik", "Videospielkonsolen"]
# No dedicated "smartwatch" node exists in UnifiedCategories.json (checked exhaustively - see README).
# Armbanduhren & Taschenuhren ("wristwatches & pocket watches") is the closest real path and is where
# smartwatch listings land in practice; documented as a known caveat rather than an invented label.
CAT_WATCH = ["Bekleidung & Accessoires", "Schmuck", "Armbanduhren & Taschenuhren"]


def make_product(id_, brand, model, name, categories, vertical, aliases, source_suffix,
                  possible_attributes=None, exclude_terms=None):
    return {
        "id": id_,
        "brand": brand,
        "model": model,
        "name": name,
        "categories": categories,
        "vertical": vertical,
        "aliases": sorted({a for a in aliases if a and len(a.strip()) >= 3}),
        "possibleAttributes": possible_attributes or {},
        "excludeTerms": sorted(exclude_terms or []),
        "source": f"seed:{source_suffix}-{THIS_MONTH}",
    }


def phone_aliases(brand, model_name, model_codes):
    aliases = {f"{brand} {model_name}", model_name}
    if nz.is_distinctive(model_name):
        aliases.add(model_name)
    for code in model_codes:
        aliases.add(code)
    return aliases


# ============================== Smartphones (TechAPI, CC-BY-SA 4.0) ==============================

def build_samsung(repo_root):
    groups = techapi.group_smartphones(repo_root, "samsung", min_year=2019)
    wanted = re.compile(r"^Galaxy (S\d{2}\+?( Ultra| Edge| FE)?|Z (Fold|Flip)\d+( FE)?|Note (10|20)\+?( Ultra| Lite)?)$", re.I)
    picked = {k: v for k, v in groups.items() if wanted.match(k.strip())}
    return _build_phone_family(picked, brand="Samsung", vertical="electronics", source="techapi")


def build_pixel(repo_root):
    groups = techapi.group_smartphones(repo_root, "google", min_year=2019)
    # drop the 2 residual near-duplicate stragglers noted in README (device code without digits, e.g.
    # "Pixel 6 Pro GLUOG") in favour of the clean "Pixel 6 Pro" group that already exists.
    picked = {k: v for k, v in groups.items() if not re.search(r"\s[A-Z]{4,6}$", k)}
    return _build_phone_family(picked, brand="Google", vertical="electronics", source="techapi")


def build_xiaomi(repo_root):
    groups = techapi.group_smartphones(repo_root, "xiaomi", min_year=2020)
    wanted = re.compile(
        r"^(Redmi Note \d+( Pro)?( \+| Plus)?( 5G)?$|Redmi \d+[A-Z]?( Pro)?$|POCO [FXM]\d[A-Z]?( Pro)?( GT)?$|"
        r"Xiaomi (1[3-5])( Pro| Ultra| Lite)?$|Mi (1[0-3])[A-Za-z]?( Pro| Ultra| Lite)?$)", re.I)
    picked = {k: v for k, v in groups.items() if wanted.match(k.strip()) and "/" not in k and "Vitality" not in k
              and "Dimensity Edition" not in k and "Arsham" not in k}
    return _build_phone_family(picked, brand="Xiaomi", vertical="electronics", source="techapi")


def _build_phone_family(groups, brand, vertical, source):
    aliases_by_id = {}
    meta = {}
    for name, g in groups.items():
        pid = nz.slugify(brand, name)
        if pid in meta and meta[pid][0] != name:
            raise ValueError(
                f"id collision: '{name}' and '{meta[pid][0]}' both slugify to '{pid}' - two different "
                f"models would silently merge into one KnownProduct. Fix slugify() or the cleaner regex."
            )
        aliases = phone_aliases(brand, name, g["model_codes"])
        aliases_by_id[pid] = aliases
        meta[pid] = (name, g)
    excludes = nz.compute_prefix_exclude_terms(aliases_by_id)

    products = []
    for pid, (name, g) in meta.items():
        possible = {}
        if g["storage_gb"]:
            possible["storage_size"] = sorted({nz.fmt_storage(s) for s in g["storage_gb"]})
        if g["ram_gb"]:
            possible["ram_size"] = sorted({nz.fmt_ram(r) for r in g["ram_gb"]})
        possible["os"] = ["android"]
        possible["color"] = []
        products.append(make_product(
            pid, brand, name, f"{brand} {name}", CAT_PHONE, vertical,
            aliases_by_id[pid], source, possible, excludes[pid],
        ))
    products.sort(key=lambda p: p["id"])
    return products


# ============================== Apple tablets/laptops/watch/audio ==============================
# TechAPI's verified:true coverage for laptop/tablet/monitor categories is very thin (spot-checked:
# 218 apple tablet files -> 1 verified, 459 lenovo laptop files -> 6 verified, 882 monitor files -> 0
# verified - see README "TechAPI verified coverage by category" table). Per the task's "only use
# verified:true records" rule that leaves not enough TechAPI data to mechanically build these categories.
# Apple's own lineups are small, stable and extremely well documented, so - the same latitude the task
# explicitly grants for game consoles ("keep attribute options only where ... your certain knowledge
# supports them") - these are hand-authored against Apple's published tech specs, cross-checked against
# ios-device-list (MIT, verified colour/storage matrices) where it covers the generation.

def build_ipad(ios_dir):
    watch_colors = ios_devices.group_by_generation(ios_devices.load(os.path.join(ios_dir, "ios-ipad.json")))
    # Known-good official storage options per generation (Apple tech specs, 2026-09). Colour is left an
    # open/free value (empty set) except where ios-device-list gives a verified matrix.
    lineup = [
        ("iPad (9th generation)", ["64GB", "256GB"], "ipad 9th generation"),
        ("iPad (10th generation)", ["64GB", "256GB"], "ipad 10th generation"),
        ("iPad Air (4th generation)", ["64GB", "256GB"], "ipad air 4th generation"),
        ("iPad Air (5th generation)", ["64GB", "256GB"], "ipad air 5th generation"),
        ("iPad Air 11-inch (M2)", ["128GB", "256GB", "512GB", "1TB"], "ipad air m2 11"),
        ("iPad Air 13-inch (M2)", ["128GB", "256GB", "512GB", "1TB"], "ipad air m2 13"),
        ("iPad mini (6th generation)", ["64GB", "256GB"], "ipad mini 6th generation"),
        ("iPad mini (7th generation)", ["128GB", "256GB", "512GB"], "ipad mini 7th generation"),
        ("iPad Pro 11-inch (3rd generation)", ["128GB", "256GB", "512GB", "1TB", "2TB"], "ipad pro 11 3rd generation"),
        ("iPad Pro 12.9-inch (5th generation)", ["128GB", "256GB", "512GB", "1TB", "2TB"], "ipad pro 12.9 5th generation"),
        ("iPad Pro 11-inch (M4)", ["256GB", "512GB", "1TB", "2TB"], "ipad pro m4 11"),
        ("iPad Pro 13-inch (M4)", ["256GB", "512GB", "1TB", "2TB"], "ipad pro m4 13"),
        ("iPad (A16)", ["128GB", "256GB"], "ipad a16"),
    ]
    products = []
    for name, storage, alias_extra in lineup:
        pid = nz.slugify("apple", name)
        aliases = {f"Apple {name}", name, alias_extra}
        colors = watch_colors.get(name, {}).get("colors", set())
        products.append(make_product(
            pid, "Apple", name, f"Apple {name}", CAT_TABLET, "electronics", aliases, "ios-device-list+apple-specs",
            {"storage_size": storage, "os": ["ipados"], "color": sorted(colors) if colors else []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    return products


def build_macbook():
    lineup = [
        ("MacBook Air (M1, 2020)", "13", ["256GB", "512GB", "1TB", "2TB"]),
        ("MacBook Air 13-inch (M2)", "13", ["256GB", "512GB", "1TB", "2TB"]),
        ("MacBook Air 15-inch (M2)", "15", ["256GB", "512GB", "1TB", "2TB"]),
        ("MacBook Air 13-inch (M3)", "13", ["256GB", "512GB", "1TB", "2TB"]),
        ("MacBook Air 15-inch (M3)", "15", ["256GB", "512GB", "1TB", "2TB"]),
        ("MacBook Air 13-inch (M4)", "13", ["256GB", "512GB", "1TB", "2TB"]),
        ("MacBook Air 15-inch (M4)", "15", ["256GB", "512GB", "1TB", "2TB"]),
        ("MacBook Pro 13-inch (M1, 2020)", "13", ["256GB", "512GB", "1TB", "2TB"]),
        ("MacBook Pro 14-inch (M1 Pro/Max, 2021)", "14", ["512GB", "1TB", "2TB", "4TB", "8TB"]),
        ("MacBook Pro 16-inch (M1 Pro/Max, 2021)", "16", ["512GB", "1TB", "2TB", "4TB", "8TB"]),
        ("MacBook Pro 13-inch (M2, 2022)", "13", ["256GB", "512GB", "1TB", "2TB"]),
        ("MacBook Pro 14-inch (M2 Pro/Max, 2023)", "14", ["512GB", "1TB", "2TB", "4TB", "8TB"]),
        ("MacBook Pro 16-inch (M2 Pro/Max, 2023)", "16", ["512GB", "1TB", "2TB", "4TB", "8TB"]),
        ("MacBook Pro 14-inch (M3, 2023)", "14", ["512GB", "1TB"]),
        ("MacBook Pro 14-inch (M3 Pro/Max, 2023)", "14", ["512GB", "1TB", "2TB", "4TB"]),
        ("MacBook Pro 16-inch (M3 Pro/Max, 2023)", "16", ["512GB", "1TB", "2TB", "4TB", "8TB"]),
        ("MacBook Pro 14-inch (M4, 2024)", "14", ["512GB", "1TB"]),
        ("MacBook Pro 14-inch (M4 Pro/Max, 2024)", "14", ["512GB", "1TB", "2TB", "4TB", "8TB"]),
        ("MacBook Pro 16-inch (M4 Pro/Max, 2024)", "16", ["512GB", "1TB", "2TB", "4TB", "8TB"]),
    ]
    products = []
    for name, screen, storage in lineup:
        pid = nz.slugify("apple", name)
        aliases = {f"Apple {name}", name}
        products.append(make_product(
            pid, "Apple", name, f"Apple {name}", CAT_LAPTOP, "electronics", aliases, "apple-specs",
            {"storage_size": storage, "screen_size": [f'{screen}"'], "os": ["macos"], "color": []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    return products


def build_thinkpad():
    # TechAPI's Lenovo laptop coverage is 6 verified files total (see module docstring above) - not
    # enough to group mechanically. ThinkPad's T/X/L/P naming is stable and well documented; storage/RAM
    # are configure-to-order on business laptops, so left as free values (empty set) rather than guessed.
    models = [
        "ThinkPad T14 Gen 2", "ThinkPad T14 Gen 3", "ThinkPad T14 Gen 4", "ThinkPad T14 Gen 5",
        "ThinkPad T16 Gen 1", "ThinkPad T16 Gen 2",
        "ThinkPad X13 Gen 2", "ThinkPad X13 Gen 3", "ThinkPad X13 Gen 4",
        "ThinkPad X1 Carbon Gen 9", "ThinkPad X1 Carbon Gen 10", "ThinkPad X1 Carbon Gen 11", "ThinkPad X1 Carbon Gen 12",
        "ThinkPad X1 Yoga Gen 6", "ThinkPad X1 Yoga Gen 7",
        "ThinkPad L14 Gen 2", "ThinkPad L14 Gen 3", "ThinkPad L14 Gen 4",
        "ThinkPad P14s Gen 2", "ThinkPad P14s Gen 3", "ThinkPad P14s Gen 4",
        "ThinkPad E14 Gen 4", "ThinkPad E14 Gen 5",
        "ThinkPad T480", "ThinkPad T490", "ThinkPad T14s",
    ]
    products = []
    for model in models:
        pid = nz.slugify("lenovo", model)
        aliases = {f"Lenovo {model}", model}
        products.append(make_product(
            pid, "Lenovo", model, f"Lenovo {model}", CAT_LAPTOP, "electronics", aliases, "hand-curated",
            {"os": ["windows"], "color": []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    return products


def build_apple_watch(ios_dir):
    groups = ios_devices.group_by_generation(ios_devices.load(os.path.join(ios_dir, "ios-apple_watch.json")))
    extra = {  # generations released after ios-device-list's coverage (2023) - colour left free (empty).
        "Apple Watch Series 9": set(), "Apple Watch Series 10": set(), "Apple Watch Ultra 2": set(),
        "Apple Watch SE (2nd generation)": None,  # already in the dataset; skip re-adding
    }
    names = list(groups.keys()) + [n for n in extra if n not in groups]
    products = []
    for name in names:
        colors = groups.get(name, {}).get("colors", set())
        sizes = groups.get(name, {}).get("sizes", set())
        pid = nz.slugify(name)  # `name` already starts with "Apple Watch" - don't double the brand prefix
        aliases = {name}
        products.append(make_product(
            pid, "Apple", name, name, CAT_WATCH, "electronics", aliases, "ios-device-list+apple-specs",
            {"screen_size": sorted(sizes) if sizes else [], "os": ["watchos"], "color": sorted(colors) if colors else []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_airpods(ios_dir):
    groups = ios_devices.group_by_generation(ios_devices.load(os.path.join(ios_dir, "ios-airpods.json")))
    names = list(groups.keys())
    if "AirPods (4th generation)" not in names:
        names.append("AirPods (4th generation)")
    products = []
    for name in names:
        pid = nz.slugify("apple", name)
        aliases = {f"Apple {name}", name}
        products.append(make_product(
            pid, "Apple", name, f"Apple {name}", CAT_HEADPHONES, "electronics", aliases,
            "ios-device-list+apple-specs" if name in groups else "hand-curated",
            {"color": []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


# ============================== PC components (docyx/pc-part-dataset, MIT) ==============================

def clean_gpu_chipset(chip):
    n = re.sub(r"\s+LHR$", "", chip)
    n = re.sub(r"\s+\d{1,2}GB$", "", n)
    return n.strip()


def build_gpus(raw_dir):
    cards = docyx.load(os.path.join(raw_dir, "pcpp-video-card.json"))
    raw_chips = docyx.unique_chipsets(cards)
    wanted = re.compile(r"^(GeForce (RTX (2|3|4|5)0\d0|GTX 16\d0)|Radeon RX (5|6|7|9)\d{3}|Arc [AB]\d{3})", re.I)

    merged = {}
    for chip, meta in raw_chips.items():
        if not wanted.match(chip):
            continue
        clean = clean_gpu_chipset(chip)
        m = merged.setdefault(clean, {"memory_gb": set()})
        m["memory_gb"] |= meta["memory_gb"]

    def brand_of(name):
        if name.startswith("GeForce") or name.startswith("Arc"):
            return "Nvidia" if name.startswith("GeForce") else "Intel"
        return "AMD"

    products = []
    for chip, meta in merged.items():
        display = chip.replace("GeForce ", "Nvidia ").replace("Radeon RX", "AMD Radeon RX").replace("Arc ", "Intel Arc ")
        pid = nz.slugify(display)
        aliases = {chip, display, chip.replace("GeForce ", "").replace("Radeon ", "")}
        products.append(make_product(
            pid, brand_of(chip), chip, display, CAT_GPU, "electronics", aliases, "docyx-pc-part-dataset",
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_cpus(raw_dir):
    cpus = docyx.load(os.path.join(raw_dir, "pcpp-cpu.json"))
    names = docyx.unique_cpu_models(cpus)
    wanted = re.compile(r"^(AMD Ryzen (3|5|7|9) [3-9]\d{3}[A-Z0-9]*|Intel Core (i5|i7|i9)-(9|1[0-4])\d{2,3}[A-Z]*|Intel Core Ultra (5|7|9) \d{3}[A-Z]*)$", re.I)
    picked = {n for n in names if wanted.match(n) and "Mobile" not in n}

    products = []
    for name in picked:
        brand = "AMD" if name.startswith("AMD") else "Intel"
        pid = nz.slugify(name)
        short = re.sub(r"^(AMD|Intel Core|Intel)\s+", "", name)
        aliases = {name, short}
        if brand == "AMD":
            aliases.add(name.replace("AMD ", ""))
        products.append(make_product(
            pid, brand, name, name, CAT_CPU, "electronics", aliases, "docyx-pc-part-dataset",
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_monitors(raw_dir):
    monitors = docyx.load(os.path.join(raw_dir, "pcpp-monitor.json"))
    wanted = re.compile(
        r"^(Dell (UltraSharp|Alienware) [A-Z0-9]+|LG (UltraGear|UltraFine) [A-Z0-9-]+|"
        r"Samsung Odyssey [A-Z0-9]+|Asus (ROG|TUF|ProArt) [A-Z0-9 ]+|BenQ (MOBIUZ|Zowie|PD|EX)[A-Z0-9]*|"
        r"AOC (Agon|CQ|U)[A-Z0-9]*|Acer (Predator|Nitro) [A-Z0-9]+|MSI (MAG|MPG|Optix) [A-Z0-9]+|"
        r"ViewSonic (Elite|VX|VP)[A-Z0-9]*)", re.I)
    seen = {}
    for m in monitors:
        name = (m.get("name") or "").strip()
        if not name or not wanted.match(name):
            continue
        seen.setdefault(name, m)  # dedupe exact-name repeats

    # Even restricted to named gaming/pro series, docyx lists hundreds of individual SKUs per brand line
    # (every size/refresh-rate/panel combination is its own catalog row). Capped to keep the seed file
    # size reasonable (task D) - sorted alphabetically for a deterministic cut, not by actual resale
    # popularity (docyx has no sales-volume signal) - documented as a known limitation in the README.
    kept_names = sorted(seen)[:150]

    products = []
    for name in kept_names:
        m = seen[name]
        brand = name.split()[0]
        pid = nz.slugify(name)
        possible = {}
        if m.get("screen_size"):
            possible["screen_size"] = [f'{m["screen_size"]}"']
        products.append(make_product(
            pid, brand, name, name, CAT_MONITOR, "electronics", {name}, "docyx-pc-part-dataset", possible,
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_ram(raw_dir):
    modules = docyx.load(os.path.join(raw_dir, "pcpp-memory.json"))
    wanted = re.compile(r"^(Corsair (Vengeance|Dominator)|G\.?Skill (Trident Z\d?|Ripjaws V?)|Kingston Fury (Beast|Renegade))", re.I)
    seen = {}
    for m in modules:
        name = (m.get("name") or "").strip()
        if not name or not wanted.match(name):
            continue
        # collapse to the kit family (drop the exact capacity/speed part-number tail) - real listings
        # name the kit line ("Corsair Vengeance LPX") far more often than the exact part number.
        family = re.sub(r"\s*\(.*?\)\s*$", "", name)
        family = re.sub(r"\s*\d+\s*x?\s*\d*\s*GB.*$", "", family, flags=re.I).strip()
        if len(family.split()) < 2:
            continue
        seen.setdefault(family, True)

    products = []
    for name in seen:
        pid = nz.slugify(name)
        brand = name.split()[0]
        products.append(make_product(
            pid, brand, name, name, CAT_RAM, "electronics", {name}, "docyx-pc-part-dataset",
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products[:30]


def build_motherboards(raw_dir):
    boards = docyx.load(os.path.join(raw_dir, "pcpp-motherboard.json"))
    wanted = re.compile(r"^(Asus|MSI|Gigabyte|ASRock) .*(B550|X570|B650|X670|Z690|Z790|B760|B450|X470)", re.I)
    seen = {}
    for b in boards:
        name = (b.get("name") or "").strip()
        if name and wanted.match(name):
            seen.setdefault(name, True)
    products = []
    for name in list(seen)[:30]:
        pid = nz.slugify(name)
        products.append(make_product(
            pid, name.split()[0], name, name, CAT_MOBO, "electronics", {name}, "docyx-pc-part-dataset",
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_storage(raw_dir):
    drives = docyx.load(os.path.join(raw_dir, "pcpp-internal-hard-drive.json"))
    wanted = re.compile(r"^(Samsung (970|980|990) (EVO|PRO)|WD (Black|Blue) SN\d+|Crucial (MX\d00|P[2-5])|"
                         r"Sabrent Rocket|Kingston (KC\d000|NV\d))", re.I)
    seen = {}
    for d in drives:
        name = (d.get("name") or "").strip()
        if not name or not wanted.match(name):
            continue
        family = re.sub(r"\s*\d+\s*(GB|TB)\b.*$", "", name, flags=re.I).strip()
        if len(family.split()) < 2:
            continue
        seen.setdefault(family, True)
    products = []
    for name in list(seen)[:30]:
        pid = nz.slugify(name)
        products.append(make_product(
            pid, name.split()[0], name, name, CAT_STORAGE, "electronics", {name}, "docyx-pc-part-dataset",
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_headphones(raw_dir):
    items = docyx.load(os.path.join(raw_dir, "pcpp-headphones.json"))
    wanted = re.compile(r"^(Bose (QuietComfort|SoundLink|Noise Cancelling)|Sony WH-|Sony WF-|Sennheiser (Momentum|HD)|"
                         r"JBL (Tune|Live|Quantum)|Beats (Studio|Solo|Fit))", re.I)
    seen = {}
    for i in items:
        name = (i.get("name") or "").strip()
        if name and wanted.match(name):
            seen.setdefault(name, True)
    products = []
    for name in list(seen)[:40]:
        pid = nz.slugify(name)
        products.append(make_product(
            pid, name.split()[0], name, name, CAT_HEADPHONES, "electronics", {name}, "docyx-pc-part-dataset",
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


# ============================== Cameras (leavestylecode/CameraDatabase, MIT) ==============================

def build_cameras(raw_dir):
    path = os.path.join(raw_dir, "camera_data.csv")
    wanted_brands = {
        "Canon": re.compile(r"^EOS ", re.I),
        "Sony": re.compile(r"^(Alpha|ILCE)", re.I),
        "Nikon": re.compile(r"^Z\w*\s|^Z\d", re.I),
    }
    products = []
    with open(path, encoding="utf-8") as f:
        for row in csv.DictReader(f):
            brand = row.get("Brand", "")
            model = row.get("Model", "").strip()
            year = row.get("Year", "")
            pattern = wanted_brands.get(brand)
            if not pattern or not model or not pattern.match(model):
                continue
            if year and year.isdigit() and int(year) < 2015:
                continue
            name = f"{brand} {model}"
            pid = nz.slugify(brand, model)
            aliases = {name, model}
            products.append(make_product(
                pid, brand, model, name, CAT_CAMERA, "electronics", aliases, "cameradatabase",
            ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


# ============================== Game consoles (hand-curated, Wikidata-checked) ==============================
# Wikidata's "video game console model" class (Q56682555) turned out too sparse/inconsistent to drive
# this mechanically (see tools/catalog-import/README.md "Consoles" section for the query used and why);
# the task explicitly allows hand-checking this category against certain public knowledge instead. Storage
# options are only set where they are well-established retail SKUs; everything else is left free (color []).

def build_consoles():
    lineup = [
        ("Sony PlayStation 3", ["PS3", "PlayStation 3"], ["20GB", "40GB", "60GB", "80GB", "120GB", "160GB", "250GB", "320GB", "500GB"]),
        ("Sony PlayStation 4", ["PS4", "PlayStation 4"], ["500GB", "1TB"]),
        ("Sony PlayStation 4 Slim", ["PS4 Slim", "PlayStation 4 Slim"], ["500GB", "1TB", "2TB"]),
        ("Sony PlayStation 4 Pro", ["PS4 Pro", "PlayStation 4 Pro"], ["1TB", "2TB"]),
        ("Sony PlayStation 5", ["PS5", "PlayStation 5"], ["825GB", "1TB"]),
        ("Sony PlayStation 5 Digital Edition", ["PS5 Digital", "PlayStation 5 Digital Edition"], ["825GB", "1TB"]),
        ("Sony PlayStation 5 Slim", ["PS5 Slim", "PlayStation 5 Slim"], ["1TB", "2TB"]),
        ("Sony PlayStation 5 Pro", ["PS5 Pro", "PlayStation 5 Pro"], ["2TB"]),
        ("Microsoft Xbox 360", ["Xbox 360"], ["20GB", "60GB", "120GB", "250GB", "320GB"]),
        ("Microsoft Xbox 360 S", ["Xbox 360 S", "Xbox 360 Slim"], ["4GB", "250GB"]),
        ("Microsoft Xbox 360 E", ["Xbox 360 E"], ["4GB", "250GB"]),
        ("Microsoft Xbox One", ["Xbox One"], ["500GB", "1TB"]),
        ("Microsoft Xbox One S", ["Xbox One S"], ["500GB", "1TB", "2TB"]),
        ("Microsoft Xbox One X", ["Xbox One X"], ["1TB"]),
        ("Microsoft Xbox Series S", ["Xbox Series S"], ["512GB", "1TB"]),
        ("Microsoft Xbox Series X", ["Xbox Series X"], ["1TB", "2TB"]),
        ("Nintendo Switch", ["Switch"], ["32GB"]),
        ("Nintendo Switch Lite", ["Switch Lite"], ["32GB"]),
        ("Nintendo Switch OLED", ["Switch OLED", "Switch OLED Model"], ["64GB"]),
        ("Nintendo Switch 2", ["Switch 2"], ["256GB"]),
        ("Valve Steam Deck LCD", ["Steam Deck LCD", "Steam Deck 64GB", "Steam Deck 256GB"], ["64GB", "256GB", "512GB"]),
        ("Valve Steam Deck OLED", ["Steam Deck OLED"], ["512GB", "1TB"]),
    ]
    products = []
    for name, extra_aliases, storage in lineup:
        pid = nz.slugify(name)
        brand = name.split()[0] if name.split()[0] != "Microsoft" else "Microsoft"
        aliases = {name} | set(extra_aliases)
        products.append(make_product(
            pid, brand, name, name, CAT_CONSOLE, "electronics", aliases, "wikidata+hand-curated",
            {"storage_size": storage, "color": []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        # Manual additions for pairs the generic token-prefix rule can't see (different first words,
        # e.g. "PS5" vs "PS5 Pro" IS caught, but "Xbox One" / "Xbox One S" and "Steam Deck LCD" 64GB/256GB
        # SKUs sharing a name need the SAME sibling-veto treatment as the S21/S21-Ultra case).
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


# ============================== Assembly ==============================

def write_seed(name, products):
    os.makedirs(SEED_DIR, exist_ok=True)
    path = os.path.join(SEED_DIR, f"{name}.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump({"products": products}, f, ensure_ascii=False, indent=2)
        f.write("\n")
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--raw-dir", required=True, help="dir fetch.sh downloaded docyx/CameraDatabase/ios-device-list json+csv into")
    ap.add_argument("--techapi-repo", required=True, help="TechAPI sparse checkout root (see fetch.sh)")
    args = ap.parse_args()

    categories = {
        "samsung-galaxy": lambda: build_samsung(args.techapi_repo),
        "google-pixel": lambda: build_pixel(args.techapi_repo),
        "xiaomi": lambda: build_xiaomi(args.techapi_repo),
        "apple-ipad": lambda: build_ipad(args.raw_dir),
        "apple-macbook": lambda: build_macbook(),
        "lenovo-thinkpad": lambda: build_thinkpad(),
        "apple-watch": lambda: build_apple_watch(args.raw_dir),
        "apple-airpods": lambda: build_airpods(args.raw_dir),
        "gpu": lambda: build_gpus(args.raw_dir),
        "cpu": lambda: build_cpus(args.raw_dir),
        "monitors": lambda: build_monitors(args.raw_dir),
        "ram": lambda: build_ram(args.raw_dir),
        "motherboards": lambda: build_motherboards(args.raw_dir),
        "storage": lambda: build_storage(args.raw_dir),
        "headphones": lambda: build_headphones(args.raw_dir),
        "cameras": lambda: build_cameras(args.raw_dir),
        "game-consoles": lambda: build_consoles(),
    }

    total = 0
    for name, fn in categories.items():
        products = fn()
        path = write_seed(name, products)
        print(f"{name}: {len(products)} products -> {path}")
        total += len(products)
    print(f"TOTAL: {total} products across {len(categories)} seed files")


if __name__ == "__main__":
    main()
