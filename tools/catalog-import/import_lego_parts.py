#!/usr/bin/env python3
"""Build KnownProducts/LegoPartInfo.json: plain LEGO part numbers, colours and element ids for single-part pages.

Output is a separate embedded resource; no seed row, catalogue entry or alias is touched. Contents:
- parts: plain part number -> [Rebrickable name, category id, set collision flag]. Plain means a mould number such as
  "3001" or "3069b": digits, an optional letter and digits (no print "pr", pattern or assembly suffix), at least four
  characters, no leading zero and no "x" (a "2x4" is a size). Prints are their own items and are never imported. Dropped
  categories: sticker sheets and whole bodies of the "Minifigs" category; dropped material: cardboard/paper. The collision
  flag is 1 when the number is also the base number of any Rebrickable set (e.g. 3001 is also a promotional set): such a
  number is a part only when the title says so.
- elements: LEGO element id (6-7 digits, no leading zero) -> [part number, colour id], for imported parts only. An element id
  that is also a set number or a part number is dropped.
- colors: Rebrickable colour id -> name, rgb, transparency, page slug and the reviewed localized phrases of
  lego_part_glossary.tsv. ambiguousColorWords: phrases that make a title's colour unknown.
- categories: category id -> name. partWords: localized search words of the English words that occur in part names.
Input is data only (never executed).

  python3 -I import_lego_parts.py --parts parts.csv.gz --colors colors.csv.gz --elements elements.csv.gz \
      --categories part_categories.csv.gz --sets sets.csv.gz
"""
import argparse
import html
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))  # the sibling importer, also under python3 -I
import enrich_lego_titles as titles  # noqa: E402

LANGUAGES = titles.LANGUAGES
HERE = Path(__file__).resolve().parent
OUTPUT = HERE.parent.parent / "KnownProducts" / "LegoPartInfo.json"
GLOSSARY = HERE / "lego_part_glossary.tsv"
PLAIN_PART = re.compile(r"[1-9]\d*[a-wyz]?\d*")
ELEMENT_ID = re.compile(r"[1-9]\d{5,6}")
DROPPED_CATEGORIES = {"Stickers", "Minifigs"}
DROPPED_MATERIALS = {"Cardboard/Paper"}
NO_COLOUR_IDS = {"-1", "9999"}
WORD = re.compile(r"[A-Za-z]+")


def load_glossary(path=GLOSSARY):
    """Returns (colors, ambiguous, part_words): colour id -> (name, {lang: [phrases]}), [phrases], word -> {lang: [terms]}."""
    colors, ambiguous, part_words, section = {}, [], {}, None
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        if line.startswith("["):
            section = line.strip()
            continue
        cells = line.split("\t")
        if section == "[colors]":
            colors[cells[0]] = (cells[1], split_cells(cells[2:], ("en",) + LANGUAGES))
        elif section == "[ambiguous]":
            ambiguous.append(cells[0].strip())
        elif section == "[part words]":
            part_words[cells[0].lower()] = split_cells(cells[1:], LANGUAGES)
        else:
            raise ValueError(f"line outside a known section: {line!r}")
    return colors, ambiguous, part_words


def split_cells(cells, languages):
    terms = {lang: [t.strip() for t in cell.split("|") if t.strip()] for lang, cell in zip(languages, cells)}
    return {lang: values for lang, values in terms.items() if values}


def is_plain_part(part_num):
    return len(part_num) >= 4 and PLAIN_PART.fullmatch(part_num) is not None


def set_bases(set_rows):
    return {r["set_num"].rsplit("-", 1)[0] for r in set_rows}


def select_parts(part_rows, categories, sets):
    """Plain part number -> [name, category id, collision flag]."""
    result = {}
    for row in part_rows:
        number = row["part_num"]
        if not is_plain_part(number) or categories.get(row["part_cat_id"]) in DROPPED_CATEGORIES:
            continue
        if row.get("part_material") in DROPPED_MATERIALS:
            continue
        name = html.unescape(row["name"]).strip()
        if name:
            result[number] = [name, int(row["part_cat_id"]), 1 if number in sets else 0]
    return dict(sorted(result.items()))


def select_elements(element_rows, parts, colors, sets):
    """Element id -> [part number, colour id] for imported parts with a real colour; ids shared with a set or part are dropped."""
    result = {}
    for row in element_rows:
        element, part, colour = row["element_id"], row["part_num"], row["color_id"]
        if not ELEMENT_ID.fullmatch(element) or part not in parts or colour in NO_COLOUR_IDS or colour not in colors:
            continue
        if element in sets or element in parts:
            continue
        result[element] = [part, int(colour)]
    return dict(sorted(result.items()))


def slug(name):
    return re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")


def build_colors(color_rows, glossary):
    """Colour id -> {name, rgb, trans, slug[, terms]}; every glossary row must name its colour exactly."""
    result = {}
    for row in color_rows:
        if row["id"] in NO_COLOUR_IDS:
            continue
        info = {"name": row["name"], "rgb": row["rgb"].upper(), "trans": row["is_trans"] == "True", "slug": slug(row["name"])}
        if row["id"] in glossary:
            expected, terms = glossary[row["id"]]
            if expected != row["name"]:
                raise ValueError(f"glossary colour {row['id']} is {expected!r}, Rebrickable says {row['name']!r}")
            info["terms"] = terms
        result[row["id"]] = info
    missing = set(glossary) - set(result)
    if missing:
        raise ValueError(f"glossary colours not in Rebrickable: {sorted(missing)}")
    slugs = [c["slug"] for c in result.values()]
    if len(slugs) != len(set(slugs)):
        raise ValueError("colour slugs are not unique")
    return result


def used_part_words(parts, categories, words, colour_words):
    """Glossary words that occur in a part or category name; colour words are left out (in a part name they describe a print)."""
    tokens = {t.lower() for name, _, _ in parts.values() for t in WORD.findall(name)}
    tokens |= {t.lower() for name in categories.values() for t in WORD.findall(name)}
    return {w: terms for w, terms in sorted(words.items()) if w in tokens and w not in colour_words and terms}


def colour_words(colors_glossary, ambiguous):
    """English one-word colour phrases ("red", "gray", "light"); a word of a longer phrase ("brick" of "brick yellow") stays."""
    phrases = [p for _, terms in colors_glossary.values() for p in terms.get("en", [])] + ambiguous
    return {p.lower() for p in phrases if WORD.fullmatch(p)}


def build(parts_rows, color_rows, element_rows, category_rows, set_rows, glossary=GLOSSARY):
    colors_glossary, ambiguous, part_words = load_glossary(glossary)
    categories = {r["id"]: r["name"] for r in category_rows}
    sets = set_bases(set_rows)
    colors = build_colors(color_rows, colors_glossary)
    parts = select_parts(parts_rows, categories, sets)
    elements = select_elements(element_rows, parts, colors, sets)
    words = {**titles.load_glossary()[0], **part_words}
    used = {cid for name, cid, _ in parts.values()}
    return {
        "categories": {k: v for k, v in sorted(categories.items(), key=lambda kv: int(kv[0])) if int(k) in used},
        "partWords": used_part_words(parts, categories, words, colour_words(colors_glossary, ambiguous)),
        "colors": colors,
        "ambiguousColorWords": ambiguous,
        "parts": parts,
        "elements": elements,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("parts", "colors", "elements", "categories", "sets"):
        parser.add_argument(f"--{name}", required=True)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    data = build(titles.read_csv(args.parts), titles.read_csv(args.colors), titles.read_csv(args.elements),
                 titles.read_csv(args.categories), titles.read_csv(args.sets))
    args.output.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    collisions = sum(1 for p in data["parts"].values() if p[2])
    print(f"{len(data['parts'])} parts ({collisions} share a set number), {len(data['elements'])} elements, "
          f"{len(data['colors'])} colours ({sum(1 for c in data['colors'].values() if 'terms' in c)} with reviewed terms), "
          f"{len(data['partWords'])} part words; {args.output.stat().st_size} bytes")


if __name__ == "__main__":
    main()
