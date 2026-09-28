"""
Everyday alias-generation rules for product lines whose seed entries otherwise carry only their
formal/marketing name (e.g. "Apple MacBook Air 13-inch (M2)"), which real shoppers/sellers rarely type in
full. Shared by build_seed.py's build_macbook/build_thinkpad/build_ipad/build_apple_watch/build_consoles/
build_cameras so the everyday forms are generated from structured (chip, size, generation, ...) data
instead of hand-typed per product - see the task's final report for the precision/recall numbers this
produced and tools/catalog-import/README.md for the reproduction steps.

None of this needs to pre-normalize separators/case/diacritics the way KnownProductMatcher.Normalize does
(see normalize.py's module docstring) - it only needs to avoid emitting a form that would be wrong to
match on.
"""


def size_forms(size: str) -> list[str]:
    """'13' -> ['13', '13 inch', '13 zoll'] - the bare digit form already covers a literal 13" (the
    matcher strips the inch sign to nothing), so only the spelled-out English/German words need adding."""
    return [size, f"{size} inch", f"{size} zoll"]


def chip_size_aliases(base: str, chip: str, size: str) -> set[str]:
    """
    Every natural word-order combination of a chip/model label and a screen size for one product, e.g.
    chip_size_aliases("MacBook Air", "M2", "13") covers "MacBook Air M2 13", "MacBook Air 13 M2",
    "MacBook Air M2 13 inch", "MacBook Air 13 inch M2", "MacBook Air M2 13 zoll", "MacBook Air 13 zoll M2".
    """
    out = set()
    for size_form in size_forms(size):
        out.add(f"{base} {chip} {size_form}")
        out.add(f"{base} {size_form} {chip}")
    return out


def year_aliases(base: str, year) -> set[str]:
    """'MacBook Air', 2022 -> {'MacBook Air 2022'} - real shoppers often search by purchase year."""
    return {f"{base} {year}"}


def generation_aliases(base: str, model_num: str, gen: int) -> set[str]:
    """
    'ThinkPad', 'T14', 3 -> every everyday way of naming a generation: with/without 'Gen', spelled out,
    and the "gN"/"g N" short forms real listings use ("ThinkPad T14 Gen 3", "ThinkPad T14 G3",
    "ThinkPad T14 G 3"). Does NOT include a bare "ThinkPad T14" (no generation) - that is deliberately
    handled once per family by the caller (see build_thinkpad's "default variant" comment), not per
    generation, so it is only ever added to a single chosen product.
    """
    return {
        f"{base} {model_num} Gen {gen}",
        f"{base} {model_num} Gen{gen}",
        f"{base} {model_num} G{gen}",
        f"{base} {model_num} G {gen}",
    }


def ordinal(n: int) -> str:
    """9 -> '9th', 3 -> '3rd', 21 -> '21st' - for the "(Nth generation)" naming Apple uses."""
    if 10 <= n % 100 <= 20:
        suffix = "th"
    else:
        suffix = {1: "st", 2: "nd", 3: "rd"}.get(n % 10, "th")
    return f"{n}{suffix}"


def generation_word_aliases(base: str, n: int) -> set[str]:
    """
    'iPad', 9 -> the everyday ways to say "9th generation" that a listing/query uses instead of the full
    formal "(9th generation)" parenthetical: "iPad 9th Gen", "iPad Gen 9", "iPad 9 Gen", "iPad
    Generation 9". Does not include a bare "iPad 9" (no "Gen"/"Generation" word) - odd-numbered-only
    ambiguity risk aside, a bare trailing number reads as a storage/price fragment far too often in real
    titles to be worth the false-positive risk for a recall gain this rule already covers otherwise.
    """
    ord_ = ordinal(n)
    return {
        f"{base} {ord_} Gen",
        f"{base} Gen {n}",
        f"{base} {n} Gen",
        f"{base} Generation {n}",
    }


ARABIC_BY_ROMAN = {"II": "2", "III": "3", "IV": "4", "V": "5"}
# Deliberately excludes bare "I" - too common an English word/letter to safely treat as a generation
# marker (unlike a real first-gen camera body, which listings almost never suffix with "I" anyway).


def mark_variants(name: str) -> set[str]:
    """
    For a name ending in a roman-numeral generation marker, optionally preceded by the word "Mark" (e.g.
    "EOS R6 Mark II", "Z6 II", "A7 III"), returns every combination of roman/arabic numeral and
    with/without the word "Mark": "EOS R6 Mark II" -> {"EOS R6 II", "EOS R6 Mark II", "EOS R6 2",
    "EOS R6 Mark 2"}. Returns an empty set when the name does not end in a recognized roman numeral.
    """
    tokens = name.split()
    if not tokens:
        return set()
    roman = tokens[-1].upper()
    if roman not in ARABIC_BY_ROMAN:
        return set()
    arabic = ARABIC_BY_ROMAN[roman]
    body = tokens[:-1]
    if body and body[-1].lower() == "mark":
        body = body[:-1]
    base = " ".join(body)
    out = set()
    for num in (roman, arabic):
        out.add(f"{base} {num}".strip())
        out.add(f"{base} Mark {num}".strip())
    return out
