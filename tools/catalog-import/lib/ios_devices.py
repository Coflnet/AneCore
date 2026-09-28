"""
Parses pbakondy/ios-device-list (https://github.com/pbakondy/ios-device-list, MIT) json exports -
fetch.sh downloads iphone.json / ipad*.json / apple_watch.json / airpods.json directly. These are small,
hand-maintained, verified colour x storage/size matrices (through ~2023 per the repo's own history) used
as the colour/size source for Apple products - see task README for where the curated iPhone data stays
authoritative instead.
"""
import json
from collections import defaultdict


def load(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def group_by_generation(entries: list[dict]):
    """{generation_name: {"colors": set(), "sizes": set(), "storages": set()}}"""
    groups = defaultdict(lambda: {"colors": set(), "sizes": set(), "storages": set()})
    for e in entries:
        g = groups[e["Generation"]]
        for m in e.get("Models", []):
            if m.get("Color"):
                g["colors"].add(m["Color"])
            if m.get("Size"):
                g["sizes"].add(m["Size"])
            if m.get("Storage"):
                g["storages"].add(m["Storage"])
    return groups
