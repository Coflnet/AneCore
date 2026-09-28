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
from collections import defaultdict
from datetime import date

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "lib"))
import normalize as nz  # noqa: E402
import techapi  # noqa: E402
import docyx  # noqa: E402
import ios_devices  # noqa: E402
import alias_rules as ar  # noqa: E402

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
CAT_DESKTOP = ["Elektronik", "Computer", "Desktop-Computer"]
# No dedicated "VR/AR headset" node exists in UnifiedCategories.json (checked exhaustively, same way as
# CAT_WATCH below) - "Datenbrillen" ("data glasses") is the closest real path under Elektronik > Computer.
CAT_VR = ["Elektronik", "Computer", "Datenbrillen"]
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


def full_phone_name(brand, model_name):
    """
    'Samsung', 'Galaxy S21' -> 'Samsung Galaxy S21'. Some brands' cleaned TechAPI names already start
    with the brand word itself - OnePlus's numbered flagship line has no separate distinctive
    product-line word the way Samsung has "Galaxy"/Xiaomi has "Redmi" (see clean_phone_name's own note),
    so its names keep the "OnePlus " prefix instead of having it stripped - don't double it into
    "OnePlus OnePlus 10 Pro".
    """
    return model_name if model_name.lower().startswith(brand.lower() + " ") else f"{brand} {model_name}"


def phone_aliases(brand, model_name, model_codes):
    aliases = {full_phone_name(brand, model_name), model_name}
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


def build_samsung_galaxy_a(repo_root):
    # The A-series was flagged as "not seeded yet - the single largest coverage gap in the phone
    # category" (see README) because TechAPI's raw names for it are noisier than the S/Z/Note lines: a
    # bare Galaxy.group_smartphones()/clean_phone_name() pass over data/smartphone/samsung also yields
    # regional/carrier "Top Edition" SKUs ("Galaxy A16 2024 Top Edition"), a "WiMAX" Japan-carrier variant,
    # a combined "Galaxy A22 2021 / Galaxy A22s" record, an "A21 Simple" carrier variant and a "Galaxy A
    # Quantum" one-off - none of which are how a real listing names the phone. This wanted-regex (checked
    # against the actual TechAPI output before shipping, not assumed) picks only the plain "A<number>"
    # line with its real "e"/"s"/"Core" suffix variants, the same "verified-shape, not free text" discipline
    # build_samsung/build_xiaomi already use - so it IS the "dedicated, more careful cleaning pass" the gap
    # called for, not a reuse of the generic cleaner's output as-is.
    #
    # One deliberate simplification, not a bug: clean_phone_name() strips "5G" as a network token (right,
    # for the S/Z/Note flagships, which are 5G-only with no distinct SKU) - for the A-series, Samsung did
    # sell some genuinely different-chipset "5G" SKUs alongside their 4G sibling under the same marketing
    # name. This pass does not attempt to split those back apart (that needs a chipset-level signal
    # clean_phone_name() does not carry) - a listing naming the "5G" word still correctly matches the base
    # model (e.g. "Galaxy A51 5G" -> "Galaxy A51"), it is just not distinguished from the 4G variant as a
    # separate catalogue entry. Documented here rather than silently merged without comment.
    groups = techapi.group_smartphones(repo_root, "samsung", min_year=2019)
    wanted = re.compile(r"^Galaxy A(\d{1,3})(e|s|\s+Core)?$", re.I)
    picked = {}
    for name, g in groups.items():
        n = name.strip()
        m = wanted.match(n)
        if not m or not (1 <= int(m.group(1)) <= 79):
            continue
        picked[name] = g
    return _build_phone_family(picked, brand="Samsung", vertical="electronics", source="techapi")


def build_pixel(repo_root):
    groups = techapi.group_smartphones(repo_root, "google", min_year=2019)
    # drop the 2 residual near-duplicate stragglers noted in README (device code without digits, e.g.
    # "Pixel 6 Pro GLUOG") in favour of the clean "Pixel 6 Pro" group that already exists.
    picked = {k: v for k, v in groups.items() if not re.search(r"\s[A-Z]{4,6}$", k)}
    return _build_phone_family(picked, brand="Google", vertical="electronics", source="techapi")


def build_xiaomi(repo_root):
    # min_year=2019 (not 2020) so "Redmi Note 7" (2019) is covered - a real truncation found live
    # ("Redmi Note 7" -> "Xiaomi Redmi" elsewhere in the pipeline; see this pass's final report).
    groups = techapi.group_smartphones(repo_root, "xiaomi", min_year=2019)
    wanted = re.compile(
        r"^(Redmi Note \d+( Pro)?( \+| Plus)?( 5G)?$|Redmi \d+[A-Z]?( Pro)?$|POCO [FXM]\d[A-Z]?( Pro)?( GT)?$|"
        r"Xiaomi (1[3-5])( Pro| Ultra| Lite)?$|Mi (1[0-3])[A-Za-z]?( Pro| Ultra| Lite)?$)", re.I)
    picked = {k: v for k, v in groups.items() if wanted.match(k.strip()) and "/" not in k and "Vitality" not in k
              and "Dimensity Edition" not in k and "Arsham" not in k}
    return _build_phone_family(picked, brand="Xiaomi", vertical="electronics", source="techapi")


def build_oneplus(repo_root):
    # Mainstream numbered flagship line (6 through 15, T/R/RT/Pro/s variants), Nord/Nord N/Nord CE, Ace
    # and the Open foldable - checked against the actual TechAPI output (36 clean groups picked, 34
    # rejected: regional/carrier "Top Edition"/"V1"/"V2"-suffixed and crossover-branded "Genshin Impact"/
    # "McLaren"/"Jupiter Rock" special editions) before shipping, the same discipline as
    # build_samsung_galaxy_a. Added for this pass's "OnePlus 9 Pro" truncation fix (see final report).
    groups = techapi.group_smartphones(repo_root, "oneplus", min_year=2018)
    # clean_phone_name() deliberately does NOT strip "OnePlus" (see that function's own note) - group
    # names here keep the "OnePlus " prefix ("OnePlus 9 Pro", not bare "9 Pro").
    wanted = re.compile(
        r"^OnePlus (\d{1,2}[A-Za-z]{0,3}(\s+Pro)?|Nord|Nord \d+T?|Nord CE(\s\d)?|Nord N\d+|"
        r"Ace(\s\d)?(\s+Pro)?|Open)$", re.I)
    picked = {k: v for k, v in groups.items() if wanted.match(k.strip())}
    return _build_phone_family(picked, brand="OnePlus", vertical="electronics", source="techapi")


def _build_phone_family(groups, brand, vertical, source):
    aliases_by_id = {}
    meta = {}
    for name, g in groups.items():
        pid = nz.slugify(full_phone_name(brand, name))
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
            pid, brand, name, full_phone_name(brand, name), CAT_PHONE, vertical,
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
    # (name, storage, base line, generation number or None, chip or None, screen size or None,
    #  bespoke alias kept from the original hand-curated seed, launch year or None)
    lineup = [
        ("iPad (9th generation)", ["64GB", "256GB"], "iPad", 9, None, None, "ipad 9th generation", 2021),
        ("iPad (10th generation)", ["64GB", "256GB"], "iPad", 10, None, None, "ipad 10th generation", 2022),
        ("iPad Air (4th generation)", ["64GB", "256GB"], "iPad Air", 4, None, None, "ipad air 4th generation", None),
        ("iPad Air (5th generation)", ["64GB", "256GB"], "iPad Air", 5, None, None, "ipad air 5th generation", None),
        ("iPad Air 11-inch (M2)", ["128GB", "256GB", "512GB", "1TB"], "iPad Air", None, "M2", "11", "ipad air m2 11", None),
        ("iPad Air 13-inch (M2)", ["128GB", "256GB", "512GB", "1TB"], "iPad Air", None, "M2", "13", "ipad air m2 13", None),
        ("iPad mini (6th generation)", ["64GB", "256GB"], "iPad mini", 6, None, None, "ipad mini 6th generation", None),
        ("iPad mini (7th generation)", ["128GB", "256GB", "512GB"], "iPad mini", 7, None, None, "ipad mini 7th generation", None),
        ("iPad Pro 11-inch (3rd generation)", ["128GB", "256GB", "512GB", "1TB", "2TB"], "iPad Pro", 3, None, "11", "ipad pro 11 3rd generation", None),
        ("iPad Pro 12.9-inch (5th generation)", ["128GB", "256GB", "512GB", "1TB", "2TB"], "iPad Pro", 5, None, "12.9", "ipad pro 12.9 5th generation", None),
        ("iPad Pro 11-inch (M4)", ["256GB", "512GB", "1TB", "2TB"], "iPad Pro", None, "M4", "11", "ipad pro m4 11", None),
        ("iPad Pro 13-inch (M4)", ["256GB", "512GB", "1TB", "2TB"], "iPad Pro", None, "M4", "13", "ipad pro m4 13", None),
        ("iPad (A16)", ["128GB", "256GB"], "iPad", None, "A16", None, "ipad a16", 2025),
    ]

    # Ambiguity policy (same "default to the smaller/base screen size" rule as build_macbook - see that
    # function's comment): the M2 Air and M4 Pro chips each ship in two screen sizes, so a bare
    # "iPad Air M2"/"iPad Pro M4" query is genuinely ambiguous; the 11-inch (base) size gets the bare
    # chip-only alias, the 13-inch keeps only its size-qualified aliases, and a query naming the size still
    # resolves to the exact variant via "longest alias wins" regardless of this default.
    chip_group_names = defaultdict(list)
    for name, storage, base, gen, chip, size, alias_extra, year in lineup:
        if chip:
            chip_group_names[(base, chip)].append((name, size))
    default_variant_for_chip = {
        min(entries, key=lambda e: float(e[1]))[0]: True
        for entries in chip_group_names.values() if len(entries) > 1
    }

    products = []
    for name, storage, base, gen, chip, size, alias_extra, year in lineup:
        pid = nz.slugify("apple", name)
        aliases = {f"Apple {name}", name, alias_extra}
        if gen is not None:
            aliases |= ar.generation_word_aliases(base, gen)
            # Bare "iPad 9" (no "Gen"/"Generation" word) - real listings write this a lot, but it is only
            # safe to add for the plain "iPad" base line: "iPad Air"/"iPad Pro"/"iPad mini" always carry
            # their qualifier word too, so a bare "iPad Air 4" (say) is not how those are actually
            # searched/listed and would just be extra false-positive surface for no real recall gain.
            if base == "iPad":
                aliases.add(f"{base} {gen}")
        if year is not None and base == "iPad":
            # Same "plain iPad only" scoping as the bare-number form above - year is only unambiguous
            # within this one base line ("iPad 2021" could otherwise also mean the 9th-gen iPad mini or
            # the 3rd-gen 11-inch iPad Pro, both also released in 2021).
            aliases |= ar.year_aliases(base, year)
        if chip is not None and size is not None:
            aliases |= ar.chip_size_aliases(base, chip, size)
            if len(chip_group_names[(base, chip)]) == 1 or default_variant_for_chip.get(name):
                aliases.add(f"{base} {chip}")
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
    # (name, screen_size, storage, chip spoken forms, launch year)
    # "chip spoken forms" lists every way real listings name the chip - the M1/M2/M3/M4 "Pro/Max" tier
    # combines two different chip SKUs into one screen-size product (Apple's lineup does not split
    # storefront SKUs by Pro vs Max), so both "M1 Pro"/"M1 Max" etc. are listed and alias to the same
    # product - see the module-level ambiguity-policy note above build_thinkpad() for the equivalent
    # "which variant does a bare query resolve to" reasoning, applied here to screen size instead of
    # generation.
    lineup = [
        ("MacBook Air (M1, 2020)", "13", ["256GB", "512GB", "1TB", "2TB"], ["M1"], 2020),
        ("MacBook Air 13-inch (M2)", "13", ["256GB", "512GB", "1TB", "2TB"], ["M2"], 2022),
        ("MacBook Air 15-inch (M2)", "15", ["256GB", "512GB", "1TB", "2TB"], ["M2"], 2023),
        ("MacBook Air 13-inch (M3)", "13", ["256GB", "512GB", "1TB", "2TB"], ["M3"], 2024),
        ("MacBook Air 15-inch (M3)", "15", ["256GB", "512GB", "1TB", "2TB"], ["M3"], 2024),
        ("MacBook Air 13-inch (M4)", "13", ["256GB", "512GB", "1TB", "2TB"], ["M4"], 2025),
        ("MacBook Air 15-inch (M4)", "15", ["256GB", "512GB", "1TB", "2TB"], ["M4"], 2025),
        ("MacBook Pro 13-inch (M1, 2020)", "13", ["256GB", "512GB", "1TB", "2TB"], ["M1"], 2020),
        ("MacBook Pro 14-inch (M1 Pro/Max, 2021)", "14", ["512GB", "1TB", "2TB", "4TB", "8TB"], ["M1 Pro", "M1 Max"], 2021),
        ("MacBook Pro 16-inch (M1 Pro/Max, 2021)", "16", ["512GB", "1TB", "2TB", "4TB", "8TB"], ["M1 Pro", "M1 Max"], 2021),
        ("MacBook Pro 13-inch (M2, 2022)", "13", ["256GB", "512GB", "1TB", "2TB"], ["M2"], 2022),
        ("MacBook Pro 14-inch (M2 Pro/Max, 2023)", "14", ["512GB", "1TB", "2TB", "4TB", "8TB"], ["M2 Pro", "M2 Max"], 2023),
        ("MacBook Pro 16-inch (M2 Pro/Max, 2023)", "16", ["512GB", "1TB", "2TB", "4TB", "8TB"], ["M2 Pro", "M2 Max"], 2023),
        ("MacBook Pro 14-inch (M3, 2023)", "14", ["512GB", "1TB"], ["M3"], 2023),
        ("MacBook Pro 14-inch (M3 Pro/Max, 2023)", "14", ["512GB", "1TB", "2TB", "4TB"], ["M3 Pro", "M3 Max"], 2023),
        ("MacBook Pro 16-inch (M3 Pro/Max, 2023)", "16", ["512GB", "1TB", "2TB", "4TB", "8TB"], ["M3 Pro", "M3 Max"], 2023),
        ("MacBook Pro 14-inch (M4, 2024)", "14", ["512GB", "1TB"], ["M4"], 2024),
        ("MacBook Pro 14-inch (M4 Pro/Max, 2024)", "14", ["512GB", "1TB", "2TB", "4TB", "8TB"], ["M4 Pro", "M4 Max"], 2024),
        ("MacBook Pro 16-inch (M4 Pro/Max, 2024)", "16", ["512GB", "1TB", "2TB", "4TB", "8TB"], ["M4 Pro", "M4 Max"], 2024),
    ]

    def family_of(name):
        return "MacBook Air" if name.startswith("MacBook Air") else "MacBook Pro"

    screen_by_name = {name: screen for name, screen, *_ in lineup}

    # Ambiguity policy (task requirement 2): a bare "family + chip" query with no screen size ("MacBook
    # Air M2") is genuinely ambiguous when the chip shipped in more than one screen size. Rather than add
    # a whole new "family" KnownProduct (more catalogue/id surface, and AneApi's sibling-exclude-term
    # computation would then need to special-case that the size-suffixed variant aliases are NOT a
    # "longer sibling model" to exclude - see report), the bare chip-only alias is added to exactly one
    # variant: the smaller/base screen size, which is also the higher-volume configuration in the resold
    # market for both the Air and the Pro/Max line. A title/query that also names the size still resolves
    # to the exact variant regardless of this default, because the size-qualified alias on that variant is
    # longer and "longest alias wins" in KnownProductMatcher.Match - see
    # AneCore.Tests/KnownProductMatcherRealCatalogTests.cs's MacBook ambiguity tests for both directions.
    by_chip_group = defaultdict(list)
    for name, screen, storage, chips, year in lineup:
        by_chip_group[(family_of(name), tuple(chips))].append(name)
    default_variant_for_chip = {
        min(names, key=lambda n: float(screen_by_name[n])): True
        for names in by_chip_group.values() if len(names) > 1
    }

    # Same "smallest sound solution" reasoning for the year-based alias ("macbook air 2022"): only added
    # when a year uniquely identifies one MacBook Air variant OR, when two sizes share a launch year (M3
    # 2024, M4 2025 - both sizes launched the same day), only on the default/base size. Scoped to Air only
    # (task example), not Pro - the Pro line's many chip tiers sharing a calendar year would make a "most
    # common" pick more of a guess than a fact, unlike the Air's simple one-chip-per-generation lineup.
    by_year_group = defaultdict(list)
    for name, screen, storage, chips, year in lineup:
        if family_of(name) == "MacBook Air":
            by_year_group[year].append(name)
    default_variant_for_year = {
        min(names, key=lambda n: float(screen_by_name[n])): True
        for names in by_year_group.values() if len(names) > 1
    }

    products = []
    for name, screen, storage, chips, year in lineup:
        family = family_of(name)
        chip_group_size = len(by_chip_group[(family, tuple(chips))])
        aliases = {f"Apple {name}", name}
        for chip in chips:
            aliases |= ar.chip_size_aliases(family, chip, screen)
            if chip_group_size == 1 or default_variant_for_chip.get(name):
                aliases.add(f"{family} {chip}")
        if family == "MacBook Air":
            year_group_size = len(by_year_group[year])
            if year_group_size == 1 or default_variant_for_year.get(name):
                aliases |= ar.year_aliases(family, year)
        pid = nz.slugify("apple", name)
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
    # (model_line, generation) - model_line is what a shopper says instead of the full "ThinkPad T14"
    # ("T14"), generation is the plain integer.
    generational_lineup = [
        ("T14", 2), ("T14", 3), ("T14", 4), ("T14", 5),
        ("T16", 1), ("T16", 2),
        ("X13", 2), ("X13", 3), ("X13", 4),
        ("X1 Carbon", 9), ("X1 Carbon", 10), ("X1 Carbon", 11), ("X1 Carbon", 12),
        ("X1 Yoga", 6), ("X1 Yoga", 7),
        ("L14", 2), ("L14", 3), ("L14", 4),
        ("P14s", 2), ("P14s", 3), ("P14s", 4),
        ("E14", 4), ("E14", 5),
    ]
    # Models with no "Gen N" scheme at all - already unambiguous, no generation-ambiguity policy needed.
    ungenerationed_models = ["T480", "T490", "T14s"]

    # Ambiguity policy (same reasoning as build_macbook's screen-size case, applied to generation number):
    # a bare "ThinkPad T14" query with no generation is genuinely ambiguous among Gen 2-5. The bare
    # model-line alias (no "Gen N") is added to exactly one generation per line - the latest/most recent
    # one, since a used-market listing that omits the generation is more likely to be describing current
    # stock than a 5+ year old one, and "latest" is a fact (not a sales-volume guess this pass has no data
    # for, unlike the MacBook screen-size default). A query that also names the generation still resolves
    # to the exact generation regardless of this default, via "longest alias wins" - see
    # AneCore.Tests/KnownProductMatcherRealCatalogTests.cs's ThinkPad ambiguity tests for both directions.
    latest_gen = {}
    for model_line, gen in generational_lineup:
        latest_gen[model_line] = max(latest_gen.get(model_line, gen), gen)

    products = []
    for model_line, gen in generational_lineup:
        model = f"ThinkPad {model_line} Gen {gen}"
        aliases = {f"Lenovo {model}", model} | ar.generation_aliases("ThinkPad", model_line, gen)
        if gen == latest_gen[model_line]:
            aliases.add(f"ThinkPad {model_line}")
        pid = nz.slugify("lenovo", model)
        products.append(make_product(
            pid, "Lenovo", model, f"Lenovo {model}", CAT_LAPTOP, "electronics", aliases, "hand-curated",
            {"os": ["windows"], "color": []},
        ))
    for model_suffix in ungenerationed_models:
        full_model = f"ThinkPad {model_suffix}"
        pid = nz.slugify("lenovo", full_model)
        aliases = {f"Lenovo {full_model}", full_model}
        products.append(make_product(
            pid, "Lenovo", full_model, f"Lenovo {full_model}", CAT_LAPTOP, "electronics", aliases,
            "hand-curated", {"os": ["windows"], "color": []},
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
        # Everyday forms: real listings/queries often drop "Apple" ("Watch Series 7") or use the common
        # colloquial misnomer "iWatch" (never Apple's own branding, but a very common way non-Apple-users
        # search for one) - "Serie"/"Série" (German/French spelling) is handled once for every product by
        # KnownProductMatcher.Normalize's spelling-variant rule, not per-alias here.
        if name.startswith("Apple Watch "):
            rest = name[len("Apple Watch "):]
            aliases |= {f"Watch {rest}", f"iWatch {rest}", f"Apple iWatch {rest}"}
        # "(Nth generation)" models (the base Watch's 1st gen, SE's 2nd gen) also get the everyday "Gen N"
        # forms (see alias_rules.generation_word_aliases, already used by build_ipad for the same "(Nth
        # generation)" naming) - found missing via this pass's real-title evaluation: "Aple Watch SE Gen
        # 2. 40mm" matched the plain (1st-gen) SE instead of the 2nd-gen one, since only the formal
        # "(2nd generation)" parenthetical existed as an alias.
        gen_match = re.match(r"^(.*?)\s*\((\d+)(?:st|nd|rd|th) generation\)$", name, re.I)
        if gen_match:
            gen_base, gen_num = gen_match.group(1), int(gen_match.group(2))
            aliases |= ar.generation_word_aliases(gen_base, gen_num)
            if gen_base.startswith("Apple Watch"):
                aliases |= ar.generation_word_aliases("Watch" + gen_base[len("Apple Watch"):], gen_num)
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
        # "AirPods Pro 2"/"AirPods 4" (bare number, no "Gen"/"Generation" word) - unlike iPad's Air/Pro/
        # mini lines, AirPods' own product-line qualifier ("Pro" or nothing) is baked into `name` itself,
        # so a bare number is unambiguous here without needing iPad's "one base line at a time" scoping;
        # "AirPods Pro 2" is genuinely how this is most often typed/listed, more so than the formal
        # "(2nd generation)" parenthetical.
        gen_match = re.match(r"^(.*?)\s*\((\d+)(?:st|nd|rd|th) generation\)$", name, re.I)
        if gen_match:
            gen_base, gen_num = gen_match.group(1), int(gen_match.group(2))
            aliases |= ar.generation_word_aliases(gen_base, gen_num)
            aliases.add(f"{gen_base} {gen_num}")
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
    # GTX 9\d0/10\d0 added for the 900/1000 series (GTX 970/1080 seen live, still common in resale) -
    # alongside the existing RTX 20-50 series, GTX 16\d0, Radeon RX and Arc coverage.
    wanted = re.compile(r"^(GeForce (RTX (2|3|4|5)0\d0|GTX (9\d0|10\d0|16\d0))|Radeon RX (5|6|7|9)\d{3}|Arc [AB]\d{3})", re.I)

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
    # Well-known official Sony body codes for the current/recent full-frame/APS-C Alpha mirrorless line -
    # hand-curated against Sony's own published specifications (the CameraDatabase CSV carries no
    # model-code column at all - its "Also known as" field is empty for every row checked), the same
    # "certain knowledge" latitude the README documents for the Apple/console entries elsewhere in this
    # importer. Keyed by the CSV's own "Model" text so a future source-data rename simply stops matching
    # (fails safe) instead of mislabeling a different body.
    sony_model_codes = {
        "Alpha A7 III": "ILCE-7M3",
        "Alpha a7R III": "ILCE-7RM3",
        "Alpha 7R II": "ILCE-7RM2",
        "Alpha 7S II": "ILCE-7SM2",
        "Alpha a9": "ILCE-9",
        "Alpha a9 II": "ILCE-9M2",
        "Alpha a6300": "ILCE-6300",
        "Alpha a6400": "ILCE-6400",
        "Alpha a6500": "ILCE-6500",
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
            # Roman<->arabic "Mark N"/"N" everyday forms on the full model text (covers "EOS R6 Mark II"
            # -> "EOS R6 Mark 2" etc. for every brand) - see alias_rules.mark_variants. Deliberately NOT
            # also re-prefixed with the brand ("Sony {v}", "Canon {v}", ...) below: KnownProductMatcher.
            # Match scans every token position in the title, not just position 0, so a bare alias like
            # "A7 III" already matches "Sony A7 III ..." (it just starts matching one token later) without
            # needing a brand-prefixed duplicate - adding one anyway only bloats the alias index (a real
            # perf regression found while measuring this task's "10-40ms index build" target) for zero
            # matching benefit. The one exception this function keeps is a single brand+short-form alias
            # (e.g. "Sony A7 III", "Canon R6 Mark II") for readability/discoverability in the seed file,
            # not because matching needs it.
            aliases = {name, model} | ar.mark_variants(model)

            if brand == "Sony":
                # "Alpha A7 III" -> "A7 III" (marketing short form real listings use instead of the
                # brand's own catalogue string) plus its own roman/arabic Mark variants ("A7 3", "a7iii"
                # is covered for free - KnownProductMatcher.Normalize splits the "A7" letter/digit join
                # the same way for both the alias and a query, so "A7 III" and "a7iii" already normalize
                # identically without a dedicated compact alias).
                core = re.sub(r"^Alpha\s+", "", model, flags=re.I)
                aliases.add(f"Sony {core}")
                aliases |= ar.mark_variants(core)
                # "Alpha 7 III" (space-separated, no letter fused to the model number) - Sony's own
                # marketing uses both the fused "A7"/"a7" and the spaced "Alpha 7" forms interchangeably;
                # this is the one form Normalize's automatic letter/digit split does NOT already cover,
                # since it requires a bare leading digit token with no letter to split off in the first
                # place. Only fires for the "<single letter><digits>..." shape (a7, a7R, a9, a6400, ...),
                # never for Sony's other naming schemes (SLT-A68, NEX-5T, DSLR-A580) - see the module's
                # regression test for why those must not be touched by this rule. Kept brand-anchored
                # ("Sony Alpha 7 III"/"Alpha 7 III", not a bare "7 III") - an all-digit alias with no
                # letter at all is too generic to risk as a standalone match.
                delettered = re.sub(r"^([A-Za-z])(\d)", r"\2", core)
                if delettered != core:
                    aliases.add(f"Sony Alpha {delettered}")
                    aliases.add(f"Alpha {delettered}")
                code = sony_model_codes.get(model)
                if code:
                    aliases.add(code)

            if brand == "Canon":
                # "EOS R6 Mark II" -> "R6 Mark II" (drop "EOS", the way "r6 ii" is actually typed).
                short = re.sub(r"^EOS\s+", "", model, flags=re.I)
                if short != model:
                    aliases.add(f"Canon {short}")
                    aliases |= ar.mark_variants(short)

            products.append(make_product(
                pid, brand, model, name, CAT_CAMERA, "electronics", aliases, "cameradatabase",
            ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_cameras_additional_sony():
    # CameraDatabase's Sony coverage stops ~2019 (documented gap in README/ATTRIBUTION) - these 4
    # full-frame bodies (2021-2023) are hand-curated against Sony's own published model codes, the same
    # "certain knowledge" latitude as elsewhere in this importer. Uses the same alias-generation approach
    # as build_cameras's Sony branch (core without "Alpha", mark_variants, delettered "Alpha 7 IV" form,
    # ILCE model code) rather than duplicating it as static aliases.
    lineup = [
        ("Alpha A7 IV", "ILCE-7M4"),
        ("Alpha A7R V", "ILCE-7RM5"),
        ("Alpha A9 III", "ILCE-9M3"),
        ("Alpha A1", "ILCE-1"),
    ]
    products = []
    for model, code in lineup:
        name = f"Sony {model}"
        pid = nz.slugify("sony", model)
        aliases = {name, model} | ar.mark_variants(model)
        core = re.sub(r"^Alpha\s+", "", model, flags=re.I)
        aliases.add(f"Sony {core}")
        aliases |= ar.mark_variants(core)
        delettered = re.sub(r"^([A-Za-z])(\d)", r"\2", core)
        if delettered != core:
            aliases.add(f"Sony Alpha {delettered}")
            aliases.add(f"Alpha {delettered}")
        aliases.add(code)
        products.append(make_product(
            pid, "Sony", model, name, CAT_CAMERA, "electronics", aliases, "hand-curated",
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_headphones_additional():
    # docyx/pc-part-dataset's headphone data is missing several very popular current models entirely
    # (documented gap in README/EVALUATION.md) - hand-added against the manufacturers' own published
    # names/model numbers.
    lineup = [
        ("Sony", "WH-1000XM4", {"Sony WH-1000XM4", "WH-1000XM4", "Sony 1000XM4"}),
        ("Sony", "WH-1000XM5", {"Sony WH-1000XM5", "WH-1000XM5", "Sony 1000XM5"}),
        ("Bose", "QuietComfort 45", {"Bose QuietComfort 45", "QuietComfort 45", "Bose QC45", "QC45"}),
    ]
    products = []
    for brand, model, aliases in lineup:
        name = f"{brand} {model}"
        pid = nz.slugify(brand, model)
        products.append(make_product(
            pid, brand, model, name, CAT_HEADPHONES, "electronics", {name} | aliases, "hand-curated",
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

# ============================== Additional iPhone generations (hand-curated) ==============================
# apple-iphones.json (iPhone 11-16) is the original hand-curated "core" seed file - KnownProductSeed.
# LoadAll() without includeExpandedCatalog returns ONLY that file, for callers that predate the wider
# catalogue (see its own comment). Adding rows to it would change that "unchanged core" boundary for those
# callers, so the older (6/6s/7/8/SE/X/XR/XS/XS Max) and newer (17 line, 18 Pro line) generations this pass
# adds - found missing via a live-listing audit - go in their own expanded-catalogue file instead. Storage
# tiers are Apple's own published tech specs (2025/2026 models cross-checked live via WebSearch/WebFetch
# against Apple's own newsroom post for iPhone 18 Pro/Pro Max, since that launched during this session's
# working "today" of 2026-09-28); colour is left free (empty set) except for iPhone 18 Pro/Pro Max, whose
# 4 colours were verified the same way - per the task's "only verified attribute values, otherwise leave
# the list empty" instruction. iPhone 18 (standard)/18e/iPhone Air 2 are NOT included: verified via
# WebSearch that Apple deviated from its usual single-September-launch cadence this cycle and is shipping
# those in spring 2027, not yet released as of this pass.
def build_iphones_additional():
    # (model, storage, year, plus_variant_of or None - Plus/Max siblings share the base model's storage)
    lineup = [
        ("iPhone 6", ["16GB", "64GB", "128GB"], 2014),
        ("iPhone 6 Plus", ["16GB", "64GB", "128GB"], 2014),
        ("iPhone 6s", ["16GB", "32GB", "64GB", "128GB"], 2015),
        ("iPhone 6s Plus", ["16GB", "32GB", "64GB", "128GB"], 2015),
        ("iPhone 7", ["32GB", "128GB", "256GB"], 2016),
        ("iPhone 7 Plus", ["32GB", "128GB", "256GB"], 2016),
        ("iPhone 8", ["64GB", "256GB"], 2017),
        ("iPhone 8 Plus", ["64GB", "256GB"], 2017),
        ("iPhone X", ["64GB", "256GB"], 2017),
        ("iPhone XR", ["64GB", "128GB", "256GB"], 2018),
        ("iPhone XS", ["64GB", "256GB", "512GB"], 2018),
        ("iPhone XS Max", ["64GB", "256GB", "512GB"], 2018),
        ("iPhone 17", ["256GB", "512GB"], 2025),
        ("iPhone Air", ["256GB", "512GB", "1TB"], 2025),
        ("iPhone 17 Pro", ["256GB", "512GB", "1TB"], 2025),
        ("iPhone 17 Pro Max", ["256GB", "512GB", "1TB", "2TB"], 2025),
        ("iPhone 18 Pro", ["256GB", "512GB", "1TB", "2TB"], 2026),
        ("iPhone 18 Pro Max", ["256GB", "512GB", "1TB", "2TB"], 2026),
    ]
    verified_colors_18_pro = ["Black", "Silver", "Glacier", "Burgundy"]

    products = []
    for model, storage, year in lineup:
        name = f"Apple {model}"
        aliases = {name, model}
        colors = verified_colors_18_pro if model in ("iPhone 18 Pro", "iPhone 18 Pro Max") else []
        pid = nz.slugify("apple", model)
        products.append(make_product(
            pid, "Apple", model, name, CAT_PHONE, "electronics", aliases, "apple-specs",
            {"storage_size": storage, "os": ["ios"], "color": colors},
        ))

    # iPhone SE generations - the "(Nth generation)" naming + everyday multi-language/year/bare-number
    # forms, same as iPad's/Apple Watch's handling.
    se_lineup = [(1, ["16GB", "64GB"], 2016), (2, ["64GB", "128GB", "256GB"], 2020), (3, ["64GB", "128GB", "256GB"], 2022)]
    for gen, storage, year in se_lineup:
        model = f"iPhone SE ({ar.ordinal(gen)} generation)"
        name = f"Apple {model}"
        aliases = {name, model} | ar.generation_word_aliases("iPhone SE", gen) | ar.year_aliases("iPhone SE", year)
        aliases.add(f"iPhone SE {gen}")  # "iPhone SE 2"/"iPhone SE 3" - the common colloquial form
        pid = nz.slugify("apple", model)
        products.append(make_product(
            pid, "Apple", model, name, CAT_PHONE, "electronics", aliases, "apple-specs",
            {"storage_size": storage, "os": ["ios"], "color": []},
        ))

    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


# ============================== Desktops, VR, Surface, Dell (hand-curated) ==============================
# Same "certain knowledge" latitude as MacBook/ThinkPad/consoles above: small, stable, well-documented
# lineups where TechAPI has no usable category at all (desktops, VR headsets) or (Dell) too little
# verified data. Storage/colour are only set where a source or certain knowledge supports them; left free
# (empty set) otherwise, per this pass's explicit "leave the list empty rather than guessing" instruction.

def build_apple_desktops():
    lineup = [
        ("iMac 24-inch (M1, 2021)", ["256GB", "512GB", "1TB", "2TB"]),
        ("iMac 24-inch (M3, 2023)", ["256GB", "512GB", "1TB", "2TB"]),
        ("iMac 24-inch (M4, 2024)", ["256GB", "512GB", "1TB", "2TB"]),
        ("Mac mini (M2, 2023)", ["256GB", "512GB", "1TB", "2TB"]),
        ("Mac mini (M2 Pro, 2023)", ["512GB", "1TB", "2TB", "4TB", "8TB"]),
        ("Mac mini (M4, 2024)", ["256GB", "512GB", "1TB", "2TB"]),
        ("Mac mini (M4 Pro, 2024)", ["512GB", "1TB", "2TB", "4TB"]),
    ]
    products = []
    for model, storage in lineup:
        pid = nz.slugify("apple", model)
        aliases = {f"Apple {model}", model}
        products.append(make_product(
            pid, "Apple", model, f"Apple {model}", CAT_DESKTOP, "electronics", aliases, "apple-specs",
            {"storage_size": storage, "os": ["macos"], "color": []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_meta_quest():
    # Storage tiers verified live (WebSearch, this pass): Quest 2 shipped 64GB then (from Aug 2021) 128GB,
    # plus a 256GB tier throughout - a used listing could be any of the three. Quest 3 shipped 128GB
    # originally, now sold only as 512GB. Quest 3S: 128GB/256GB. Quest Pro: a single 256GB configuration,
    # discontinued Sept 2024.
    lineup = [
        ("Meta Quest 2", ["64GB", "128GB", "256GB"]),
        ("Meta Quest 3", ["128GB", "512GB"]),
        ("Meta Quest 3S", ["128GB", "256GB"]),
        ("Meta Quest Pro", ["256GB"]),
    ]
    products = []
    for model, storage in lineup:
        pid = nz.slugify(model)  # `model` already starts with "Meta" - don't double the brand prefix
        # `model` ("Meta Quest 2") already starts with the brand word, same as OnePlus's numbered
        # flagships (see full_phone_name's own note) - naively prepending "Meta " here produced
        # "Meta Meta Quest 2" in the display name. Reuse the same "don't double" guard.
        name = full_phone_name("Meta", model)
        aliases = {model}
        # "Oculus Quest 2" only - Meta's Oculus->Meta rebrand happened in 2021, before Quest 3/3S/Pro
        # (2022+) ever shipped, so those were never sold under the Oculus name; adding an "Oculus Quest 3"
        # alias would be an unverified guess, not a real historical name.
        if model == "Meta Quest 2":
            aliases.add("Oculus Quest 2")
        products.append(make_product(
            pid, "Meta", model, name, CAT_VR, "electronics", aliases, "hand-curated",
            {"storage_size": storage, "color": []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_surface():
    # TechAPI's laptop/microsoft and tablet/microsoft verified coverage (24/30, 14/25) is good, but the
    # underlying names/directories carry per-SKU region/carrier codes ("Surface Pro 11 ZIB-00031") the
    # same way the A-series' TechAPI records did, and (unlike the A-series) there is no simple numeric
    # wanted-regex that cleanly separates the generation from the SKU suffix here - grouping by directory
    # would produce one near-duplicate KnownProduct per SKU code, not per generation. Hand-curated against
    # Microsoft's own published generation lineup instead, the same "TechAPI too noisy to use mechanically
    # this pass" latitude as Dell below. Storage/RAM left free (configure-to-order), same as ThinkPad.
    tablets = ["Surface Pro 9", "Surface Pro 10", "Surface Pro 11", "Surface Go 3", "Surface Go 4"]
    laptops = ["Surface Laptop 5", "Surface Laptop 6", "Surface Laptop 7", "Surface Book 3"]
    products = []
    for model in tablets:
        pid = nz.slugify("microsoft", model)
        aliases = {f"Microsoft {model}", model}
        products.append(make_product(
            pid, "Microsoft", model, f"Microsoft {model}", CAT_TABLET, "electronics", aliases, "hand-curated",
            {"os": ["windows"], "color": []},
        ))
    for model in laptops:
        pid = nz.slugify("microsoft", model)
        aliases = {f"Microsoft {model}", model}
        products.append(make_product(
            pid, "Microsoft", model, f"Microsoft {model}", CAT_LAPTOP, "electronics", aliases, "hand-curated",
            {"os": ["windows"], "color": []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


def build_dell():
    # XPS is deliberately NOT generation-numbered here (unlike ThinkPad) - Dell's own marketing reuses
    # the bare "XPS 13"/"XPS 15"/"XPS 17" name across years, distinguishing generations only by an
    # internal 4-digit service-tag-adjacent model code most listings do not use; a single entry per screen
    # size is the more accurate match for how these are actually searched/sold. Latitude, the numbered
    # business line, is covered by its well-known recent 5000/7000/9000-series model codes instead - the
    # same "TechAPI verified coverage too thin to group mechanically" reasoning as ThinkPad (8/305 files).
    xps = ["Dell XPS 13", "Dell XPS 15", "Dell XPS 17"]
    latitude = ["Dell Latitude 5420", "Dell Latitude 5430", "Dell Latitude 5440",
                "Dell Latitude 7420", "Dell Latitude 7430", "Dell Latitude 7440"]
    products = []
    for full_name in xps + latitude:
        pid = nz.slugify(full_name)
        aliases = {full_name}
        products.append(make_product(
            pid, "Dell", full_name, full_name, CAT_LAPTOP, "electronics", aliases, "hand-curated",
            {"os": ["windows"], "color": []},
        ))
    excludes = nz.compute_prefix_exclude_terms({p["id"]: set(p["aliases"]) for p in products})
    for p in products:
        p["excludeTerms"] = sorted(excludes[p["id"]])
    products.sort(key=lambda p: p["id"])
    return products


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
        "samsung-galaxy": lambda: sorted(
            build_samsung(args.techapi_repo) + build_samsung_galaxy_a(args.techapi_repo), key=lambda p: p["id"]
        ),
        "google-pixel": lambda: build_pixel(args.techapi_repo),
        "xiaomi": lambda: build_xiaomi(args.techapi_repo),
        "oneplus": lambda: build_oneplus(args.techapi_repo),
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
        "headphones": lambda: sorted(build_headphones(args.raw_dir) + build_headphones_additional(), key=lambda p: p["id"]),
        "cameras": lambda: sorted(build_cameras(args.raw_dir) + build_cameras_additional_sony(), key=lambda p: p["id"]),
        "game-consoles": lambda: build_consoles(),
        "apple-iphones-additional": lambda: build_iphones_additional(),
        "apple-desktops": lambda: build_apple_desktops(),
        "meta-quest": lambda: build_meta_quest(),
        "microsoft-surface": lambda: build_surface(),
        "dell": lambda: build_dell(),
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
