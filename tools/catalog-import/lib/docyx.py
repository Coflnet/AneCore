"""
Parses docyx/pc-part-dataset (https://github.com/docyx/pc-part-dataset, MIT) json exports. fetch.sh
downloads data/json/<category>.json straight from raw.githubusercontent.com - these are small enough
(≤2.3MB) to fetch directly over HTTP, no git clone needed.

These files list individual board-partner/kit SKUs ("MSI GeForce RTX 3060 Ventus 2X 12G"), not the chip
itself - for our purposes ("RTX 3060" is the product a listing names, not the specific AIB card) we group
by the underlying chipset/model line instead.
"""
import json
import re


def load(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def unique_chipsets(video_cards: list[dict]) -> dict[str, dict]:
    """chipset name -> {memory_gb: set(), count: n} for every distinct 'chipset' value."""
    out: dict[str, dict] = {}
    for card in video_cards:
        chip = (card.get("chipset") or "").strip()
        if not chip:
            continue
        g = out.setdefault(chip, {"memory_gb": set(), "count": 0})
        if card.get("memory"):
            g["memory_gb"].add(int(card["memory"]))
        g["count"] += 1
    return out


def unique_cpu_models(cpus: list[dict]) -> dict[str, dict]:
    out: dict[str, dict] = {}
    for cpu in cpus:
        name = (cpu.get("name") or "").strip()
        if not name:
            continue
        g = out.setdefault(name, {"core_count": cpu.get("core_count"), "count": 0})
        g["count"] += 1
    return out
