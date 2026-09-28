"""
Parses a local sparse checkout of GetTechAPI/TechAPI (https://github.com/GetTechAPI/TechAPI,
data CC-BY-SA 4.0). We never vendor the raw repo - fetch.sh does a `git clone --filter=blob:none
--sparse` shallow checkout of only the brand/category directories we need into a scratch dir, and this
module reads json files straight out of that checkout.

Two different groupings are needed because TechAPI's own data model differs by category:

- smartphone/*: every region+carrier+storage variant is its own independently-sourced record and
  `base_model_slug` usually equals `slug` (see tools/catalog-import/README.md for the verification that
  led to this) - so grouping into a base model is done here by cleaning the free-text `name` field with
  clean_phone_name().
- laptop/*, tablet/*, watch/*: files already live one directory per base model
  (data/<category>/<brand>/<year>/<model-slug>/<variant>.json) - grouping is just "directory == model".
"""
import glob
import json
import os
import re
from collections import defaultdict

EDITION_WORDS = r"\b(Standard|Premium|Enterprise|Special|Global|Open Market)\s+Edition\b"
NETWORK_TOKENS = r"\b(5G|4G|LTE-A|LTE|TD-LTE|FD-LTE|CDMA2000|CDMA|UW|Dual SIM|Triple SIM|Single SIM|eSIM)\b"
REGION_WORDS = r"\b(Global|LATAM|APAC|EMEA|MEA|NA|China|India|Europe|Unlocked|Open Market|US Version|Intl(?:'l)? Version|International)\b"

# Carrier/region/limited-edition trailers that identify the SAME retail model as the bare name - stripped
# iteratively (outside-in) so compounds like "... Thom Browne Limited Edition CN HK" fully reduce to the
# base model. These do not change which real device the group represents, only how many near-duplicate
# TechAPI source records get merged into that one KnownProduct.
JUNK_SUFFIXES = [
    r"CN HK", r"CN TW", r"HK", r"CN", r"TW", r"(?:JP\s+)?SC-?\d+[A-Z]?", r"(?:JP\s+)?SCG?\d+[A-Z]?",
    r"BTS Edition", r"Olympic Games Edition", r"Thom Browne Limited Edition", r"Thom Browne Edition",
    r"Bespoke Edition", r"Maison Margiela Limited Edition", r"Maison Margiela Edition",
    r"Maison Margiela", r"Retro Limited Edition", r"BMW M Edition", r"Jet Black", r"Star Wars",
    r"Premium", r"Standard",
    r"Fan Edition", r"/ S20 Fan Edition", r"/ S20 Lite", r"/ Galaxy S11\+?",
]
_JUNK_SUFFIX_RE = re.compile(r"\s+(?:" + "|".join(JUNK_SUFFIXES) + r")\s*$", re.I)


def _load(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def clean_phone_name(name: str) -> str:
    """Strips region/carrier/storage/model-code noise from a TechAPI smartphone `name` down to a base model line."""
    n = name
    n = re.sub(r"\s*\([^)]*\)\s*$", "", n)  # trailing "(Samsung Miracle 3)" internal codename
    n = re.sub(r"\s*/\s*SM-[A-Z0-9/]+$", "", n)  # "US / SM-G990U3/DS" trailing alt model code
    n = re.sub(r"(\s*/\s*[A-Z0-9-]{4,})+$", "", n)  # "/ SM-S948V" alt model code suffixes
    n = re.sub(r"^(Samsung|Google|Xiaomi|Apple)\s+", "", n, flags=re.I)
    n = re.sub(r"^[A-Z]{1,4}-[A-Z0-9/]{2,}\s+", "", n)  # leading manufacturer model code, e.g. "SM-S948W ", "SM-A076B/DS "
    n = re.sub(r"^[A-Z]{1,3}\d[A-Z0-9-]{2,}\s+", "", n)  # leading model code without hyphen, e.g. "M2101K6G "
    n = re.sub(r"\s*\d+(\.\d+)?\s*(GB|TB)\b", "", n, flags=re.I)  # storage
    n = re.sub(EDITION_WORDS, "", n, flags=re.I)
    n = re.sub(NETWORK_TOKENS, "", n, flags=re.I)
    n = re.sub(REGION_WORDS, "", n, flags=re.I)
    n = re.sub(r"\s*-\s*$", "", n)  # dangling hyphen left after stripping "LTE-A" etc
    n = re.sub(r"\b(Fold|Flip)\s+(\d)", r"\1\2", n)  # "Z Flip 4" -> "Z Flip4" (matches Samsung's own naming)
    n = re.sub(r"\s+", " ", n).strip(" -")
    # Trailing region codes ("US", "CA", "JP") and trailing carrier/device codes (Google puts these at
    # the END of the name, e.g. "Pixel 6 GB7N6" / "Pixel 7 Pro US AU GE2AE") plus the JUNK_SUFFIXES
    # phrases can all stack up on one record, so strip outside-in until nothing more changes.
    # Model-meaningful 2-3 letter suffixes that must never be stripped as if they were a trailing region
    # code, even though they're structurally identical (2-3 uppercase letters at the end): FE (Fan
    # Edition), XL (Pixel ... XL), GT, SE. Found via a real regression - "Galaxy S21 FE" and "Pixel 9 Pro
    # XL" were both silently collapsing into their base model because "FE"/"XL" matched \b[A-Z]{2,3}\b
    # just like "US"/"CA"/"JP" do.
    trailing_region = re.compile(r"\s+\b(?!FE\b|XL\b|GT\b|SE\b)[A-Z]{2,3}\b\s*$")
    trailing_device_code = re.compile(r"\s+(?=[A-Z0-9]*\d)[A-Z0-9]{4,10}\s*$")
    for _ in range(6):
        new_n = _JUNK_SUFFIX_RE.sub("", n)
        new_n = trailing_device_code.sub("", new_n)
        new_n = trailing_region.sub("", new_n)
        new_n = new_n.strip(" -/")
        if new_n == n:
            break
        n = new_n
    return n


def group_smartphones(repo_root: str, brand_dir: str, min_year: int | None = None):
    """
    Returns {group_name: {"records": [...], "years": set(), "storage_gb": set(), "ram_gb": set(),
    "display_in": set(), "model_codes": set(), "soc": set()}} for verified==true files under
    data/smartphone/<brand_dir>, optionally dropping groups whose newest record predates min_year.
    """
    files = glob.glob(os.path.join(repo_root, "data", "smartphone", brand_dir, "**", "*.json"), recursive=True)
    groups: dict[str, dict] = defaultdict(lambda: {
        "records": [], "years": set(), "storage_gb": set(), "ram_gb": set(), "display_in": set(),
        "model_codes": set(), "soc": set(),
    })
    for f in files:
        d = _load(f)
        if not d.get("verified"):
            continue
        name = d.get("name", "")
        cleaned = clean_phone_name(name)
        if not cleaned:
            continue
        g = groups[cleaned]
        g["records"].append(d)
        rd = d.get("release_date")
        if rd:
            g["years"].add(int(rd[:4]))
        for s in d.get("storage_options_gb") or []:
            g["storage_gb"].add(int(s))
        if d.get("ram_gb"):
            g["ram_gb"].add(round(float(d["ram_gb"]), 1))
        disp = (d.get("display") or {}).get("size_inch")
        if disp:
            g["display_in"].add(round(float(disp), 1))
        if d.get("soc"):
            g["soc"].add(d["soc"])
        slug = d.get("slug", "")
        m = re.match(r"^(?:samsung-)?(sm-[a-z0-9]+)", slug, flags=re.I)
        if m:
            g["model_codes"].add(m.group(1).upper())

    groups = _merge_case_variants(groups)
    if min_year is not None:
        groups = {k: v for k, v in groups.items() if not v["years"] or max(v["years"]) >= min_year}
    return groups


def _merge_case_variants(groups: dict[str, dict]) -> dict[str, dict]:
    """
    clean_phone_name() is case-preserving, so the same real model can come out as two different dict
    keys when TechAPI's underlying source datasets disagree on casing (seen for real: "POCO X7 Pro" vs
    "Poco X7 Pro" - Xiaomi's own sub-brand stylization vs a source that title-cased it). Merges those
    into one group, keyed by whichever exact casing has the most source records (ties broken
    alphabetically, for a deterministic result across runs).
    """
    by_lower: dict[str, list[str]] = defaultdict(list)
    for key in groups:
        by_lower[key.lower()].append(key)

    merged: dict[str, dict] = {}
    for variants in by_lower.values():
        variants.sort(key=lambda k: (-len(groups[k]["records"]), k))
        canonical = variants[0]
        target = groups[canonical]
        for other in variants[1:]:
            src = groups[other]
            target["records"].extend(src["records"])
            target["years"] |= src["years"]
            target["storage_gb"] |= src["storage_gb"]
            target["ram_gb"] |= src["ram_gb"]
            target["display_in"] |= src["display_in"]
            target["model_codes"] |= src["model_codes"]
            target["soc"] |= src["soc"]
        merged[canonical] = target
    return merged


def group_by_directory(repo_root: str, category: str, brand_dir: str, min_year: int | None = None):
    """
    For laptop/tablet/watch: one KnownProduct per model directory
    (data/<category>/<brand_dir>/<year>/<model-slug>/*.json). Returns the same shape as
    group_smartphones(), keyed by the model-slug directory name.
    """
    base = os.path.join(repo_root, "data", category, brand_dir)
    groups: dict[str, dict] = defaultdict(lambda: {
        "records": [], "years": set(), "storage_gb": set(), "ram_gb": set(), "display_in": set(),
        "model_codes": set(), "soc": set(), "names": defaultdict(int),
    })
    for year_dir in glob.glob(os.path.join(base, "*")):
        year = os.path.basename(year_dir)
        for model_dir in glob.glob(os.path.join(year_dir, "*")):
            if not os.path.isdir(model_dir):
                continue
            model_slug = os.path.basename(model_dir)
            g = groups[model_slug]
            for f in glob.glob(os.path.join(model_dir, "*.json")):
                d = _load(f)
                if not d.get("verified"):
                    continue
                g["records"].append(d)
                if year.isdigit():
                    g["years"].add(int(year))
                for s in (d.get("storage_options_gb") or ([d["storage_gb"]] if d.get("storage_gb") else [])):
                    g["storage_gb"].add(int(s))
                if d.get("ram_gb"):
                    g["ram_gb"].add(round(float(d["ram_gb"]), 1))
                disp = (d.get("display") or {}).get("size_inch")
                if disp:
                    g["display_in"].add(round(float(disp), 1))
                g["names"][d.get("name", "")] += 1

    groups = {k: v for k, v in groups.items() if v["records"]}
    if min_year is not None:
        groups = {k: v for k, v in groups.items() if not v["years"] or max(v["years"]) >= min_year}
    return groups
