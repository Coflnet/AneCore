#!/usr/bin/env python3
"""Build KnownProducts/LegoSetInfo.json: Rebrickable set title, theme, year, pieces and localized search words.

No seed row is touched and no catalogue entry or alias is added: nothing here can change an id, name, alias,
set_number or the seed version. Output is a separate embedded resource keyed by the printed set NUMBER ("10874"),
covering every unambiguous Rebrickable set, catalogue member or not. A set is used only when its base number has
exactly one Rebrickable row and that row is "<n>-1" and the base is a 4-5 digit number without leading zero - the only
shape that can be a LEGO set identity in listing titles. Dropped: 6+ digit / zero-led numbers (books, ISBN-like
merchandise, polybags), alphanumeric numbers (promos, stickers, DBASE rows) and 1-3 digit vintage numbers.
Localized words come from lego_title_glossary.tsv (reviewed); theme words are stored once per theme, not per set.
They are search words only and are never matcher aliases. Input is data only (never executed).

  python3 -I enrich_lego_titles.py --sets sets.csv.gz --themes themes.csv.gz
"""
import argparse
import csv
import gzip
import html
import io
import json
import re
from collections import Counter
from pathlib import Path

LANGUAGES = ("de", "fr", "nl", "it", "es", "pl")
HERE = Path(__file__).resolve().parent
SEED_DIR = HERE.parent.parent / "KnownProducts" / "Seed"
OUTPUT = HERE.parent.parent / "KnownProducts" / "LegoSetInfo.json"
GLOSSARY = HERE / "lego_title_glossary.tsv"
WORD = re.compile(r"[A-Za-z][A-Za-z'\-]*")


def read_csv(path):
    raw = Path(path).read_bytes()
    if str(path).endswith(".gz"):
        raw = gzip.decompress(raw)
    return list(csv.DictReader(io.StringIO(raw.decode("utf-8"))))


def load_glossary(path=GLOSSARY):
    """Returns (words, themes, phrases): lowercase english word / exact theme name / two-word phrase -> {lang: [terms]}."""
    words, themes, phrases, target = {}, {}, {}, None
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        if line.startswith("["):
            target = {"[words]": words, "[phrases]": phrases}.get(line.strip(), themes)
            continue
        cells = line.split("\t")
        key = cells[0] if target is themes else cells[0].lower()
        terms = {lang: [t for t in cell.split("|") if t] for lang, cell in zip(LANGUAGES, cells[1:])}
        target[key] = terms
    return words, themes, phrases


def load_themes(rows):
    return {r["id"]: (r["name"], r["parent_id"]) for r in rows}


def root_theme(theme_id, themes):
    seen = set()
    while theme_id in themes and theme_id not in seen:
        seen.add(theme_id)
        name, parent = themes[theme_id]
        if not parent:
            return name
        theme_id = parent
    return None


def unique_base_sets(set_rows):
    """Base number -> row for numbers with exactly one Rebrickable row, and that row is the "-1" one."""
    counts = Counter(r["set_num"].rsplit("-", 1)[0] for r in set_rows)
    return {r["set_num"][:-2]: r for r in set_rows if r["set_num"].endswith("-1") and counts[r["set_num"][:-2]] == 1}


def is_listing_identity(base):
    """True for 4-5 digit numbers without leading zero, the only printed set-number shape used as a listing identity."""
    return re.fullmatch(r"[1-9]\d{3,4}", base) is not None


def dropped_reason(base):
    if base.isdigit():
        return "6+ digit or zero-led number" if len(base) > 3 or base.startswith("0") else "1-3 digit vintage number"
    return "alphanumeric number"


def lookup_word(token, words):
    token = token.lower()
    if token.endswith("'s"):
        return None
    return words.get(token) or (words.get(token[:-1]) if token.endswith("s") else None)


def merge_terms(entries):
    result = {lang: [] for lang in LANGUAGES}
    for entry in entries:
        for lang in LANGUAGES:
            result[lang].extend(t for t in (entry or {}).get(lang, []) if t not in result[lang])
    return {lang: terms for lang, terms in result.items() if terms}


def title_terms(title, words, phrases):
    tokens = [t.lower() for t in WORD.findall(title)]
    entries = [phrases.get(f"{a} {b}") for a, b in zip(tokens, tokens[1:])]
    entries += [lookup_word(t, words) for t in tokens]
    return merge_terms(entries)


def set_info(row, themes, words, phrases):
    leaf = themes.get(row["theme_id"], (None, ""))[0]
    root = root_theme(row["theme_id"], themes)
    parent = root if root and root != leaf else None
    title = html.unescape(row["name"]).strip()
    info = {"title": title}
    if leaf:
        info["theme"] = leaf
    if parent:
        info["parentTheme"] = parent
    if row["year"].isdigit() and int(row["year"]) > 0:
        info["year"] = int(row["year"])
    if row["num_parts"].isdigit() and int(row["num_parts"]) > 0:
        info["pieces"] = int(row["num_parts"])
    terms = title_terms(title, words, phrases)
    if terms:
        info["terms"] = terms
    return info


def catalogue_models(seed_dir):
    models = {}
    for path in sorted(Path(seed_dir).glob("lego-*.json")):
        for p in json.loads(path.read_text(encoding="utf-8"))["products"]:
            if p.get("vertical") == "lego":
                models[p["id"]] = p["model"]
    return models


def build(sets, themes, words, phrases):
    result = {}
    for base, row in sorted(sets.items()):
        if not is_listing_identity(base):
            continue
        info = set_info(row, themes, words, phrases)
        if info["title"]:
            result[base] = info
    return result


def theme_terms(infos, theme_glossary):
    names = {n for i in infos.values() for n in (i.get("theme"), i.get("parentTheme")) if n}
    return {n: t for n in sorted(names) if (t := merge_terms([theme_glossary.get(n)]))}


def coverage(infos, themes_terms):
    total = len(infos)
    def has(info, lang):
        return bool(info.get("terms", {}).get(lang)) or any(themes_terms.get(info.get(k), {}).get(lang) for k in ("theme", "parentTheme"))
    return {lang: sum(1 for i in infos.values() if has(i, lang)) / total for lang in LANGUAGES}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sets", required=True)
    parser.add_argument("--themes", required=True)
    parser.add_argument("--seed-dir", type=Path, default=SEED_DIR)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    themes = load_themes(read_csv(args.themes))
    words, theme_glossary, phrases = load_glossary()
    sets = unique_base_sets(read_csv(args.sets))
    infos = build(sets, themes, words, phrases)
    terms = theme_terms(infos, theme_glossary)
    args.output.write_text(json.dumps({"themes": terms, "sets": infos}, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    dropped = Counter(dropped_reason(b) for b in sets if not is_listing_identity(b))
    models = set(catalogue_models(args.seed_dir).values())
    print(f"{len(infos)} sets written; {len(models & set(infos))}/{len(models)} catalogue sets covered; dropped {dict(dropped)}")
    for lang, share in coverage(infos, terms).items():
        print(f"  {lang}: {share:.1%} of sets have a translated word")


if __name__ == "__main__":
    main()
