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
- **Cameras**: Canon EOS, Sony Alpha and Nikon Z bodies from 2015 onward (69 total before the 2015 cutoff
  trimmed a handful of very old EOS models) - CameraDatabase's Sony coverage stops around 2019, so recent
  Alpha 7 IV/7R V-class bodies are not present; a gap to fill in a follow-up pass if Sony camera listings
  turn out to need it.
- **Xiaomi**: covers the Mi/Xiaomi numbered flagship line, Redmi Note, and POCO X/F/M series
  (2020 onward); Redmi's huge budget A/numbered-only sub-lines and older Mi Max/Mix variants are not
  seeded.
- **Samsung**: Galaxy S/Z Fold/Z Flip/Note (2019 onward) only - the A-series budget line (by far the
  highest unit volume, but its region-duplicated/reused-model-number naming in TechAPI needs a dedicated,
  more careful cleaning pass than this importer's generic smartphone cleaner currently does) is **not**
  seeded yet. This is the single largest coverage gap in the phone category and the top candidate for a
  follow-up pass.

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
