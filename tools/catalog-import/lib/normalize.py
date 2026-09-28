"""Shared helpers: slugs, alias-generation rules, storage/ram formatting.

Mirrors (loosely - this is a grouping/authoring aid, not a byte-identical port of) the matching-relevant
parts of AneCore's KnownProductMatcher.Normalize: lowercase, strip diacritics, split letter/digit joins.
The C# matcher does its own normalization on both title and alias at match time, so aliases here are
written in a natural human form ("iphone 14", "sm-g991b") - they do NOT need to pre-normalize
separators/case, only need to avoid emitting aliases that would be wrong to ever match on.
"""
import re
import unicodedata

COMMON_WORDS = {
    "pro", "max", "plus", "mini", "air", "lite", "ultra", "edge", "note", "fold", "flip",
    "se", "fe", "gt", "s", "a", "z", "the", "new", "classic", "sport", "active", "edition",
}


def strip_diacritics(s: str) -> str:
    decomposed = unicodedata.normalize("NFD", s)
    return "".join(c for c in decomposed if unicodedata.category(c) != "Mn")


def slugify(*parts: str) -> str:
    text = "-".join(p for p in parts if p)
    text = strip_diacritics(text).lower()
    # "+" is a real distinguishing character in model names (Galaxy S21 vs S21+, Note10+) - collapsing
    # it to a plain separator like every other punctuation mark would silently alias two different
    # products onto the same id (found via a real regression: "Galaxy S21+" and a same-named "Galaxy
    # S21" both slugified to "samsung-galaxy-s21", so the dict built from them silently dropped one).
    text = text.replace("+", "-plus")
    text = re.sub(r"[^a-z0-9]+", "-", text)
    return re.sub(r"-+", "-", text).strip("-")


def is_distinctive(model: str) -> bool:
    """
    Alias-generation rule (task spec): a model-only alias may only be emitted when the model string is
    distinctive - contains both letters and digits, or has at least two tokens, and is not a common word
    on its own. Guards against emitting a bare alias like "air" or "5" that would match unrelated titles.
    """
    m = model.strip().lower()
    if not m or m in COMMON_WORDS:
        return False
    tokens = m.split()
    has_letter = any(c.isalpha() for c in m)
    has_digit = any(c.isdigit() for c in m)
    if has_letter and has_digit:
        return True
    if len(tokens) >= 2:
        return True
    return False


def fmt_storage(gb) -> str:
    """256 -> '256GB', 1000/1024 -> '1TB'."""
    gb = int(round(float(gb)))
    if gb >= 1000 and gb % 1000 == 0:
        return f"{gb // 1000}TB"
    if gb >= 1024 and gb % 1024 == 0:
        return f"{gb // 1024}TB"
    return f"{gb}GB"


def fmt_ram(gb) -> str:
    gb = int(round(float(gb)))
    return f"{gb}GB"


def matcher_tokens(s: str) -> list[str]:
    """
    Ports the token-boundary-relevant part of KnownProductMatcher.Normalize: lowercase, strip
    diacritics, split letter/digit joins, drop non-alnum. Used here only to compute sibling exclude
    terms (see compute_prefix_exclude_terms) exactly the way the matcher will tokenize the same alias.
    """
    s = strip_diacritics(s.lower()).replace("ß", "ss")
    s = re.sub(r"[-_/]+", " ", s)
    s = re.sub(r"(?<=[a-z])(?=[0-9])", " ", s)
    s = re.sub(r"(?<=[0-9])(?=[a-z])", " ", s)
    s = re.sub(r"[^a-z0-9 ]", " ", s)
    return s.split()


def compute_prefix_exclude_terms(aliases_by_id: dict[str, set[str]]) -> dict[str, set[str]]:
    """
    For every pair of products in the same call where one product's alias is a strict token-prefix of
    another's (e.g. "iphone 14" / "iphone 14 pro"), add the immediate next token ("pro") as an exclude
    term on the shorter one - the same sibling-exclusion pattern used throughout the hand-curated
    apple-iphones.json seed. Call once per "family" of products that could plausibly collide (e.g. all
    Galaxy S aliases together) - not over the whole catalogue, which would be O(n^2) for no benefit since
    unrelated products never share a token prefix anyway... but is still safe/cheap enough at the sizes
    used by this importer (a few hundred products per family at most).
    """
    tokenized = {pid: [matcher_tokens(a) for a in aliases] for pid, aliases in aliases_by_id.items()}
    excludes: dict[str, set[str]] = {pid: set() for pid in aliases_by_id}
    ids = list(tokenized)
    for i, pid_a in enumerate(ids):
        for pid_b in ids:
            if pid_a == pid_b:
                continue
            for a_tokens in tokenized[pid_a]:
                if not a_tokens:
                    continue
                for b_tokens in tokenized[pid_b]:
                    if len(b_tokens) > len(a_tokens) and b_tokens[: len(a_tokens)] == a_tokens:
                        excludes[pid_a].add(b_tokens[len(a_tokens)])
    return excludes
