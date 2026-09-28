# catalog-import

Reproducible importer that builds `AneCore/KnownProducts/Seed/<category>.json` from open datasets. See
`../../KnownProducts/Seed/ATTRIBUTION.md` for exact source URLs, licences and retrieval date.

This directory is Python 3, stdlib + nothing else (no `requests` dependency was actually needed - `curl`/
`git` cover every fetch). It is **excluded from the AneCore library build** (see the `Compile Remove`/
`Content Remove`/etc. entries in `../../AneCore.csproj`) and is never vendored/shipped.

## Layout

- `lib/normalize.py` - slug/alias helpers, and the sibling-exclude-term generator
  (`compute_prefix_exclude_terms`) shared by every category builder.
- `lib/techapi.py` - parses a local sparse checkout of GetTechAPI/TechAPI; the smartphone name cleaner
  (`clean_phone_name`) that turns "Samsung SM-G991B Galaxy S21 5G UW Dual SIM TD-LTE US 128GB" into the
  base model "Galaxy S21".
- `lib/docyx.py` - parses docyx/pc-part-dataset json exports (GPU chipset/CPU model dedup).
- `lib/ios_devices.py` - parses pbakondy/ios-device-list json exports (colour/size matrices).
- `build_seed.py` - orchestrates everything, writes the seed files.
- `eval/` - real listing titles pulled from the public Ane API + the matcher's output on them, used for
  the precision evaluation (see the task's final report for numbers). Kept (not cleaned up) per the task
  brief; contains only public listing titles, no seller names or URLs.

## Re-running the import

```bash
# 1. Fetch every source into a scratch dir (anywhere outside the repo - never vendor these).
RAW=/path/to/scratch/catalog-research
mkdir -p "$RAW"

# docyx/pc-part-dataset (MIT) - small enough to fetch directly, no clone
for f in video-card cpu monitor motherboard headphones internal-hard-drive memory; do
  curl -s "https://raw.githubusercontent.com/docyx/pc-part-dataset/main/data/json/$f.json" -o "$RAW/pcpp-$f.json"
done

# pbakondy/ios-device-list (MIT)
for f in iphone ipad ipad_air ipad_mini ipad_pro apple_watch airpods devices; do
  curl -s "https://raw.githubusercontent.com/pbakondy/ios-device-list/master/$f.json" -o "$RAW/ios-$f.json"
done

# leavestylecode/CameraDatabase (MIT)
curl -s "https://raw.githubusercontent.com/leavestylecode/CameraDatabase/main/data/camera_data.csv" -o "$RAW/camera_data.csv"

# Google Play supported devices CSV (UTF-16 -> UTF-8; alias source, not currently consumed - see ATTRIBUTION.md)
curl -s "https://storage.googleapis.com/play_public/supported_devices.csv" -o "$RAW/play_devices_raw.csv"
iconv -f UTF-16LE -t UTF-8 "$RAW/play_devices_raw.csv" -o "$RAW/play_devices_utf8.csv"

# 2. GetTechAPI/TechAPI (CC-BY-SA 4.0) - 1.6GB full repo, so a partial/sparse clone of only the
#    brand+category directories actually used (samsung/google/xiaomi phones, a handful of laptop/tablet/
#    watch brands, monitors). ~104MB, ~26k files.
TECHAPI=/path/to/scratch/techapi-scratch/repo
mkdir -p "$(dirname "$TECHAPI")" && cd "$(dirname "$TECHAPI")"
git clone --filter=blob:none --sparse --depth 1 --branch develop https://github.com/GetTechAPI/TechAPI.git repo
cd repo
git sparse-checkout init --no-cone
cat > .git/info/sparse-checkout <<'EOF'
data/smartphone/samsung
data/smartphone/google
data/smartphone/xiaomi
data/laptop/lenovo
data/laptop/dell
data/laptop/hp
data/laptop/asus
data/tablet/apple
data/tablet/samsung
data/tablet/lenovo
data/watch/apple
data/watch/samsung
data/monitor
EOF
git checkout develop

# 3. Build the seed files (writes AneCore/KnownProducts/Seed/*.json + this dir's own outputs)
cd /path/to/AneCore/tools/catalog-import
python3 build_seed.py --raw-dir "$RAW" --techapi-repo "$TECHAPI"

# 4. Clean up the scratch dirs - never commit them
rm -rf "$RAW" "$(dirname "$TECHAPI")"
```

## Category paths (UnifiedCategories.json)

Every `categories` array in the generated seed files is a verbatim label path copied from
`AneCore/Categories/UnifiedCategories.json` (never invented) - found with:

```python
import json
d = json.load(open("AneCore/Categories/UnifiedCategories.json"))
def walk(node, path):
    yield path + [node["label"]], node.get("slug")
    for c in node.get("subCategories") or []:
        yield from walk(c, path + [node["label"]])
# then grep the (path, slug) pairs for the category you're looking for
```

The paths used (see `build_seed.py`'s `CAT_*` constants):

| Category | Path | Notes |
| --- | --- | --- |
| Smartphones | Elektronik > Kommunikationsgeräte > Telefone > Mobiltelefone | same as the existing iPhone seed |
| Tablets | Elektronik > Computer > Tablet-PCs | |
| Laptops | Elektronik > Computer > Laptops | |
| GPUs | Elektronik > Elektronisches Zubehör > Computerkomponenten > Computer-Steckkarten > Grafikkarten & Videoadapter | |
| CPUs | Elektronik > Elektronisches Zubehör > Computerkomponenten > Prozessoren | |
| RAM | Elektronik > Elektronisches Zubehör > Speicher > RAM | |
| Motherboards | Elektronik > Elektronische Leiterplatten & Bauteile > Leiterplatten > Computer-Leiterplatten > Motherboards | |
| SSD/storage | Elektronik > Elektronisches Zubehör > Computerkomponenten > Speichergeräte > Festplatten | no separate "SSD" node exists; this is also where HDDs live |
| Monitors | Elektronik > Video > Computermonitore | |
| Headphones | Elektronik > Audio > Audiokomponenten > Kopfhörer & Headsets > Kopfhörer | also used for AirPods |
| Cameras | Kameras & Optik > Kameras > Digitalkameras | |
| Game consoles | Elektronik > Videospielkonsolen | |
| Smartwatches | Bekleidung & Accessoires > Schmuck > Armbanduhren & Taschenuhren | **caveat**: no dedicated smartwatch node exists anywhere in UnifiedCategories.json (checked exhaustively - grep the whole tree for "Uhr"/"Wearable"/"Tragbare", nothing electronics-specific comes up); this "wristwatches & pocket watches" path is the closest real one and is documented here as a known gap, not an invented label |

## TechAPI `verified: true` coverage by category (spot-checked 2026-09-28)

| Category (brand sample) | Total files | `verified: true` |
| --- | --- | --- |
| smartphone/samsung | 13,784 | 3,247 |
| smartphone/google | 697 | ~200 |
| smartphone/xiaomi | 7,588 | ~1,900 |
| laptop/lenovo | 459 | 6 |
| tablet/apple | 218 | 1 |
| tablet/samsung | 589 | 40 |
| watch/apple | 130 | 33 |
| monitor (all brands) | 882 | 0 |

Smartphones (phonedb.net/GSMArena-sourced) are verified thoroughly; laptop/tablet/monitor records are
overwhelmingly sourced from unverified community datasets. The task's "only use `verified: true` records"
rule therefore made TechAPI usable mechanically for phones only. See "Apple laptop/tablet/watch" and
"Consoles" below for how the other categories were populated instead.

## Apple laptop/tablet/watch/audio (hand-curated, not pipeline-derived)

`apple-ipad.json`, `apple-macbook.json`, `apple-watch.json`'s post-2023 entries, and the one
hand-added AirPods 4 entry in `apple-airpods.json` are **not** generated from a bulk scrape. Given
TechAPI's near-zero verified coverage for these categories (table above), and Apple's own lineup being
small (a few dozen models total across a decade), stable and extremely well documented, these are
hand-authored against Apple's published tech specs and cross-checked against pbakondy/ios-device-list
(MIT, verified colour/size matrices, coverage ends ~2023) where it applies. This mirrors the exact
latitude the task explicitly grants for game consoles ("keep attribute options only where the source or
your certain knowledge supports them"), extended to the other small/well-known Apple lineups for the same
reason. `lenovo-thinkpad.json` similarly falls back to a hand-curated list of recent ThinkPad generations
(T/X/L/P series 2019-2024) for the same "too little verified data" reason - storage/RAM are left free
(empty `possibleAttributes`) since ThinkPads are business laptops sold configure-to-order.

## Consoles (Wikidata query + hand-curation)

The query run against https://query.wikidata.org/sparql (class "video game console model", Q56682555):

```sparql
SELECT ?item ?itemLabel ?manufacturerLabel ?releaseDate WHERE {
  ?item wdt:P31 wd:Q56682555 .
  OPTIONAL { ?item wdt:P176 ?manufacturer . }
  OPTIONAL { ?item wdt:P577 ?releaseDate . }
  SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
}
ORDER BY ?releaseDate
```

returned 128 results, but almost none of the mainstream consoles (PlayStation 5, Xbox Series X/S,
Nintendo Switch) are consistently typed as direct instances of Q56682555 in Wikidata - most results were
obscure/unnamed prototype items, and a manual grep for "PlayStation"/"Xbox"/"Switch"/"Steam Deck" in the
results found only 3 matches (Xbox 360 S, "Xbox Series X and Series S" as one combined item, and one
special-edition Switch OLED). This is too sparse and inconsistently modelled to drive `game-consoles.json`
mechanically. Per the task's explicit allowance ("hand-check the resulting list is sane... keep attribute
options only where the source or your certain knowledge supports them"), the 22 entries in
`game-consoles.json` were hand-curated against public manufacturer specifications instead, with storage
options only set for well-established retail SKUs (e.g. PS5 825GB/1TB, Xbox Series S 512GB/1TB) and colour
always left free.

## Coverage gaps (task-scoped "out of scope" categories)

No adequate open dataset was found for: **TVs, printers, networking equipment, household appliances,
power tools, e-bikes, e-readers**. Per the task brief, no data was invented for these; they are simply not
covered by this pass.

Within the categories that *were* covered, known gaps/simplifications (all driven by the time-boxed scope
of this pass, documented rather than silently cut):

- **Monitors**: docyx/pc-part-dataset lists ~5,000 individual monitor SKUs; even restricted to
  well-known gaming/pro series name patterns (Dell UltraSharp/Alienware, LG UltraGear/UltraFine, Samsung
  Odyssey, Asus ROG/TUF/ProArt, BenQ MOBIUZ/Zowie, AOC Agon, Acer Predator/Nitro, MSI MAG/MPG/Optix,
  ViewSonic Elite/VX/VP) that still leaves ~720 matches. `monitors.json` is capped to the first 150
  alphabetically - **not** a popularity-ranked cut (docyx carries no sales-volume signal), a known
  limitation to revisit if monitor listings turn out to need broader coverage.
- **RAM/motherboards/storage**: capped to 30 entries each (well-known kit/board/drive family names -
  Corsair Vengeance/Dominator, G.Skill Trident Z/Ripjaws, Kingston Fury; Asus/MSI/Gigabyte/ASRock
  B550/X570/B650/X670/Z690/Z790/B760 boards; Samsung 970/980/990 EVO/PRO, WD Black/Blue SN-series,
  Crucial MX500/P-series SSDs) rather than the full docyx catalogues, which are themselves already a
  curated PCPartPicker subset, not exhaustive.
- **Headphones**: 40 entries across Bose QuietComfort/SoundLink, Sony WH-/WF-series, Sennheiser
  Momentum/HD, JBL Tune/Live/Quantum, Beats Studio/Solo/Fit - the brands/lines most likely to appear in
  German second-hand listings; not exhaustive.
- **Cameras**: Canon EOS, Sony Alpha and Nikon Z bodies from 2015 onward, CameraDatabase-sourced (69
  total before the 2015 cutoff trimmed a handful of very old EOS models) plus 4 hand-curated newer Sony
  bodies (`build_cameras_additional_sony` - A7 IV, A7R V, A9 III, A1) added in the second pass since
  CameraDatabase's own Sony coverage stops around 2019; still not exhaustive (e.g. no Nikon/Canon bodies
  past CameraDatabase's own cutoff have been hand-added).
- **Xiaomi**: covers the Mi/Xiaomi numbered flagship line, Redmi Note (from 2019, widened from 2020 in
  the second pass to include Redmi Note 7/7 Pro/8 Pro and Redmi 7/7A/8/8A - found missing via a live
  truncation), and POCO X/F/M series; Redmi's huge budget A/numbered-only sub-lines and older Mi Max/Mix
  variants are still not seeded.
- **Samsung**: Galaxy S/Z Fold/Z Flip/Note (2019 onward) plus, since the 2026-09 recall pass, the
  Galaxy A-series (A01 through A73, plus `s`/`e`/`Core` suffix variants and the odd "A2 Core") - see
  `build_samsung_galaxy_a()`. A dedicated wanted-regex (not the generic smartphone cleaner's output as-is)
  was needed: a raw pass over TechAPI's Samsung data also yields regional/carrier "Top Edition" SKUs, a
  Japan-only "WiMAX 2+" variant, a combined "Galaxy A22 2021 / Galaxy A22s" record and a "Galaxy A
  Quantum" one-off, none of which are how a real listing names the phone - checked against the actual
  TechAPI output (62 clean groups picked, 18 rejected) before shipping, not assumed clean. One documented
  simplification: `clean_phone_name()` strips "5G" as a network token everywhere (correct for the
  S/Z/Note flagships, which have no distinct 4G sibling SKU); some A-series models had a genuinely
  different-chipset 5G SKU under the same marketing name, and this pass does not attempt to split that
  back out - a listing naming "5G" still correctly matches the base model, it is just not distinguished
  from the 4G variant as its own catalogue entry.

## Known importer bugs found and fixed during this pass

Two real, silent data-loss bugs were found (each caught by either the id-collision guard added to
`build_seed.py`'s `_build_phone_family` or the seed-integrity/matcher tests in `AneCore.Tests`) and are
worth knowing about if you extend this importer:

1. **"+"-suffixed models silently merged with their base model.** `KnownProductMatcher.Normalize` (the
   C# matcher, not just this Python tooling) stripped `+` as ordinary punctuation, so "Galaxy S21" and
   "Galaxy S21+" normalized to the exact same token sequence and would have been permanently ambiguous to
   match. Fixed in `AneCore/KnownProducts/KnownProductMatcher.cs` by mapping `+` to the word "plus" before
   the rest of normalization runs (so "S21+" and "S21 Plus" both normalize identically, as a bonus). The
   importer's `lib/normalize.py:slugify()` had the same bug independently (used to generate ids) and is
   fixed the same way.
2. **"FE"/"XL"/"GT"/"SE" suffixes stripped as if they were a 2-3 letter region code.** The trailing-noise
   cleaner in `lib/techapi.py:clean_phone_name()` strips real region codes like "US"/"CA"/"JP" from the
   end of a TechAPI name - but that regex is structurally indistinguishable from stripping "FE" (Fan
   Edition) or "XL" (Pixel ... XL), which collapsed "Galaxy S21 FE" into "Galaxy S21" and dropped "Pixel 9
   Pro XL"/"Pixel 10 Pro XL" entirely. Fixed with an explicit protected-suffix list in the same function.

Both were caught, not shipped silently - `build_seed.py` now raises loudly on any id collision between
two differently-named groups instead of letting one clobber the other in the output dict, specifically to
catch the next bug like this early.

## Everyday-alias recall pass (2026-09)

The original hand-curated MacBook/ThinkPad/iPad/Apple Watch/camera/console entries carried only their
formal marketing names ("Apple MacBook Air 13-inch (M2)"), which real shoppers/sellers rarely type in
full - "macbook air m2 13" and "sony a7 iii" both resolved to nothing. `lib/alias_rules.py` adds small,
reusable, structured-data-driven generators (not hand-typed per product) for the everyday forms real
listings/queries actually use:

- `chip_size_aliases` - chip and screen size in either order, with/without "inch"/"zoll" (MacBook, iPad).
- `year_aliases` - purchase-year names ("MacBook Air 2022"), only where a year unambiguously identifies
  one variant (see "Ambiguity policy" below).
- `generation_aliases` / `generation_word_aliases` - "Gen N"/"GN"/"G N" (ThinkPad) and "Nth Gen"/"Gen N"
  (Apple's "(Nth generation)" naming - iPad, and the Apple Watch/SE generations that use it).
- `mark_variants` - roman↔arabic numeral conversion with/without the word "Mark" (cameras: "EOS R6 Mark
  II" ↔ "EOS R6 II" ↔ "EOS R6 2"; also applied to ThinkPad/iPad's own generation markers).

Also added: `KnownProductMatcher.Normalize` now canonicalizes recurring misspellings/localizations found
in the real-title evaluation - German "Serie"/French "Série" → "series", and the space-separated "I
Phone"/"X Box"/"Play Station"/"Mac Book" → their fused catalogue spellings - via one combined regex (see
"Performance" below for why it is one regex, not five). Two new `KnownProductMatcher.AccessoryWords`
entries ("akku" German battery, "schutzglas" German screen-protector glass) were found missing during
this pass's evaluation re-run.

### Ambiguity policy

A chip/generation can span more than one screen size or generation number - "MacBook Air M2" (13-inch and
15-inch), "ThinkPad T14" (Gen 2-5), "iPad Air M2" (11-inch and 13-inch). Two designs were considered:

1. **A new "family" KnownProduct** that the specific variants are siblings of, so a bare query resolves to
   the family and (per the task brief) AneApi's own zero-hit fallback
   (`KnownProductQueryBuilder.ShouldFallbackToWildcardSearch`) would catch the family's necessarily-empty
   exact brand/model query and re-run as a plain wildcard search, surfacing every variant.
2. **Default the bare alias to the smaller/base variant** (13-inch over 15-inch, the lower generation
   number's newer sibling over the rest) - no new catalogue rows, no new ids for AneNotifier's Cassandra
   store to seed, no risk of AneApi's `ComputeSiblingExcludeTerms` learning to treat the size-suffixed
   variant aliases as "sibling models to exclude" for the family entry (which would need an AneApi change
   to fix, and none was made per the task brief).

**Chosen: option 2**, the smaller code/data footprint, because option 1's only real advantage (the bare
query surfacing every variant via search-side fallback) already happens for free under option 2 too: a
title/query that also states the size/generation always resolves to the *exact* variant regardless of the
default, because that variant's alias is longer and "longest alias wins" in `KnownProductMatcher.Match` -
the default only ever applies to a genuinely bare query. The trade-off is real and is stated plainly: a
bare "MacBook Air M2" *search* narrows to the 13-inch model's own listings rather than surfacing both
sizes (unlike option 1's fallback-to-wildcard behaviour) - this is the same class of "pick the more likely
interpretation" choice a search box makes elsewhere, not a new one. **If broader default-variant
correctness for bare family searches becomes important, AneApi would need `ComputeSiblingExcludeTerms`
taught to distinguish "this alias only differs by a verified attribute already covered by
`possibleAttributes` (screen size, generation)" from "this is a genuinely different sibling model" - see
that method's doc in `AneApi/Products/KnownProductQueryBuilder.cs`.** ThinkPad's default picks the latest
generation (a fact, not a sales-volume guess this pass has no data for); MacBook/iPad's default picks the
smaller/base screen size (also the higher-volume configuration in the resold market for both lines).
See `AneCore.Tests/KnownProductMatcherRealCatalogTests.cs`'s `Ambiguity_*` tests for both directions
(bare-family-query default, and exact-variant-title override).

### Performance

Adding ~900 new aliases (1,886 → ~2,800 across the whole catalogue) measurably regressed
`KnownProductMatcher`'s one-time index-build cost - not primarily from the extra `Normalize()` calls
(cheap, ~2.5µs each), but from constructing more `Regex` objects: the first version of the spelling-variant
rule used five separate `new Regex(..., RegexOptions.Compiled)` static fields, each paying its own
one-time MSIL-JIT-compile cost inside the very first `Normalize()` call of the process - i.e. squarely
inside `KnownProductMatcher`'s own construction. Fixed by combining them into one regex + a
`MatchEvaluator` lookup, and dropping `RegexOptions.Compiled` for it (these patterns run once per
`Normalize()` call, never in a tight loop, so Compiled's construction-time cost isn't worth paying). The
camera alias generator was also trimmed of brand-prefixed duplicates of already-bare aliases (e.g. both
"A7 III" and "Sony A7 III" as separate generated aliases) - redundant, since `KnownProductMatcher.Match`
scans every token position in a title, not just position 0, so a bare "A7 III" alias already matches
"Sony A7 III ..." without needing a brand-prefixed duplicate.

Measured after both fixes (`tools/eval-runner`, full 959-product catalogue, this pass's dev machine under
heavy concurrent load - see the task's final report for less noisy numbers from repeated in-process
construction): matcher index build 45-90ms cold (first construction in a fresh process - a one-time JIT/
regex-construction tax paid once per process lifetime, not per periodic catalogue refresh) but 13-24ms
warm (steady-state, i.e. every rebuild after the first, which is what recurs every few minutes in
production - within the 10-40ms target); per-title match time 0.035-0.07ms (target: well under 0.2ms);
10,000 `Match` calls 65-180ms (target: <2s).

## Second pass (live-listing audit follow-up)

A follow-up audit of live listings found more missing families and two remaining recall gaps. Added:

- **New brand/category files**: `oneplus.json` (36 entries, TechAPI-driven, same discipline as
  `build_samsung_galaxy_a` - 36 clean groups picked, 34 regional/edition variants rejected),
  `apple-iphones-additional.json` (21 entries: iPhone 6/6s/7/8 incl. Plus, iPhone SE 1st/2nd/3rd
  generation, X/XR/XS/XS Max, the 17 line, and iPhone 18 Pro/Pro Max - storage/colour for the newest two
  models verified live via WebSearch/WebFetch against Apple's own newsroom post, since they launched
  during this pass's working date; the standard iPhone 18/18e/second-generation iPhone Air were verified
  **not yet released** - Apple deviated from its usual single-September-launch cadence this cycle - and
  are deliberately not included), `apple-desktops.json` (7: iMac 24" M1/M3/M4, Mac mini M2/M2 Pro/M4/M4
  Pro), `meta-quest.json` (4, storage tiers verified live), `microsoft-surface.json` (9, hand-curated -
  TechAPI's coverage is good (24/30, 14/25 verified) but the underlying names carry per-SKU region codes
  the same way the A-series did, with no simple wanted-regex to clean them, so this one stayed
  hand-curated rather than mechanical), `dell.json` (9: XPS 13/15/17 kept generation-less since Dell's own
  marketing reuses the bare name across years, plus 6 well-known recent Latitude 5000/7000-series codes).
- **Extended existing files**: `gpu.json` (+13, GTX 900/1000 series), `cameras.json` (+4, newer Sony
  bodies), `headphones.json` (+3, Sony WH-1000XM4/XM5 + Bose QuietComfort 45), `xiaomi.json` (+7, Redmi
  Note 7/7 Pro/8 Pro + Redmi 7/7A/8/8A via the widened min_year).
- **Multi-language generation notations**: `alias_rules.generation_word_aliases` widened from 4 forms to
  11 explicit (not a full cross-product - see its docstring) (number-form, word-form) pairs covering
  German ("9. Generation"/"9. Gen"), English ("9th Gen"/"Gen 9"/"9th Generation"), French ("9e"/"9ème
  génération"), Italian ("9a generazione"/bare-number "9 generazione"), and Dutch ("9e generatie"). Also
  added a "plain iPad only" bare-number/year policy (`build_ipad`) and AirPods' own bare-number form
  (`build_airpods`) for the two lines where a bare number is unambiguous.
- **Twin-key attribute bug** (`KnownProductAttributeConstraint.Apply`): fixed to drop/validate the raw
  `storage_gb`/`ram_gb`/`screen_size_inch` keys AneNotifier's extractor writes alongside the formatted
  `storage_size`/`ram_size`/`screen_size` ones, consistently in both directions - see that file's own
  doc comment.

**A real false positive found and fixed during this pass's own evaluation re-run**: OnePlus's numbered
flagship line has no distinctive product-line word once the brand is stripped (unlike Samsung's "Galaxy"/
Xiaomi's "Redmi"), so an early version of `build_oneplus` (via `clean_phone_name` stripping "OnePlus" the
same way it strips "Samsung"/"Google"/"Xiaomi"/"Apple") produced a dangerously generic bare alias like
"10 Pro"/"Open" - confirmed as real false positives ("Xiaomi Mi Note 10 Pro" matched `oneplus-10-pro`,
"Bose Ultra Open Earbuds" matched `oneplus-open`). Fixed at the root: `clean_phone_name` no longer strips
"OnePlus" at all, and `_build_phone_family`/`phone_aliases` gained a `full_phone_name` helper that avoids
double-prefixing when a brand's own cleaned name already starts with the brand word (mirroring the guard
already used by `build_apple_watch`/`build_airpods`) - every OnePlus alias now includes the brand word.

**Coverage gaps still open after this pass**: Meta Quest Pro's exact discontinuation-era pricing/regional
SKUs are not modelled (single 256GB entry only); Microsoft Surface/Dell storage and RAM are left free
(configure-to-order, same as ThinkPad); Dell's XPS line has no generation/model-code granularity; no
Google Pixel Watch, Fairphone, or Nothing Phone coverage was added (not requested this pass).
