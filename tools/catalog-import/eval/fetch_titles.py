#!/usr/bin/env python3
"""
Pulls real public listing titles from the Ane API for the matcher precision evaluation. No auth, max 2
requests/second (task constraint). Only `title` is kept - no seller names, no listing URLs (task
constraint: "do not store seller names or URLs").
"""
import json
import time
import urllib.request
import urllib.parse

BASE = "https://ane.coflnet.com/api/product"
RATE_LIMIT_SECONDS = 0.55  # < 2 req/s with margin

TERMS = [
    # electronics - matches expected
    "iphone", "samsung galaxy", "pixel", "xiaomi", "ipad", "macbook", "thinkpad", "rtx", "ryzen",
    "playstation", "xbox", "switch", "canon eos", "sony alpha", "airpods", "bose", "apple watch",
    "monitor", "ssd", "nvidia", "intel core", "corsair", "samsung tv", "lenovo",
    # negatives - non-electronics, must not match
    "nike", "pokemon", "tommy hilfiger", "lego", "barbie", "adidas", "chanel",
]


def get_json(url):
    req = urllib.request.Request(url, headers={"User-Agent": "AneCore-catalog-import-eval/1.0"})
    with urllib.request.urlopen(req, timeout=20) as resp:
        return json.loads(resp.read().decode("utf-8"))


def main():
    titles = []
    seen_seo_ids = set()
    for term in TERMS:
        url = f"{BASE}/search?query={urllib.parse.quote(term)}&pageSize=50"
        time.sleep(RATE_LIMIT_SECONDS)
        try:
            data = get_json(url)
        except Exception as e:
            print(f"search failed for '{term}': {e}")
            continue
        products = data.get("products", [])
        print(f"'{term}': {len(products)} products")
        for p in products[:8]:  # cap products per term to bound total request volume
            seo_id = p.get("seoId")
            if not seo_id or seo_id in seen_seo_ids:
                continue
            seen_seo_ids.add(seo_id)
            time.sleep(RATE_LIMIT_SECONDS)
            try:
                matches = get_json(f"{BASE}/{urllib.parse.quote(seo_id)}/matches")
            except Exception as e:
                print(f"  matches failed for '{seo_id}': {e}")
                continue
            for m in matches:
                t = (m.get("title") or "").strip()
                if t:
                    titles.append({"term": term, "seoId": seo_id, "title": t})

    print(f"TOTAL titles collected: {len(titles)} from {len(seen_seo_ids)} products")
    with open("titles.json", "w", encoding="utf-8") as f:
        json.dump(titles, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
