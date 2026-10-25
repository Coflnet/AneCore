"""Run with: python3 -m unittest discover -s tools/catalog-import -p 'test_*.py'."""
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent / "lib"))
import normalize as nz


class PrefixExclusionTests(unittest.TestCase):
    def test_plus_is_a_variant_token_not_punctuation(self):
        self.assertEqual(nz.matcher_tokens("Asus PRIME B650-PLUS"), nz.matcher_tokens("Asus PRIME B650+"))
        exclusions = nz.compute_prefix_exclude_terms({"base": {"Asus PRIME B650"},
                                                    "plus": {"Asus PRIME B650+"},
                                                    "wifi": {"Asus PRIME B650+ WIFI"}})
        self.assertEqual(exclusions, {"base": {"plus"}, "plus": {"wifi"}, "wifi": set()})

    def test_only_other_products_strict_token_extensions_exclude(self):
        aliases = {"a": {"Dell U2723QE", "Dell U2723QE Monitor"},
                   "b": {"Dell U2723QEA"}, "c": {"Dell U2723QE-A"}, "other": {"HP U2723QE"}}
        self.assertEqual(nz.compute_prefix_exclude_terms(aliases),
                         {"a": {"a"}, "b": set(), "c": set(), "other": set()})


class ExpansionTests(unittest.TestCase):
    def test_reviewed_storage_spelling_preserves_aliases_and_capacity_variants(self):
        import expand_seed as expansion
        rows = [
            {"name": "Kingston SSD Now mS200", "capacity": 30, "type": "SSD", "form_factor": "mSATA", "interface": "mSATA"},
            {"name": "Kingston SSDNow mS200", "capacity": 480, "type": "SSD", "form_factor": "mSATA", "interface": "mSATA"},
            {"name": "Kingston SSDNow 200", "capacity": 120, "type": "SSD", "form_factor": "2.5", "interface": "SATA"},
        ]
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            seeds = directory / "seeds"
            seeds.mkdir()
            raw = json.dumps(rows).encode()
            (directory / "pcpp-internal-hard-drive.json").write_bytes(raw)
            inputs = {"internal-hard-drive": (hashlib.sha256(raw).hexdigest(), "storage", expansion.seed.CAT_STORAGE,
                                             ("type", "form_factor", "interface"))}
            with patch.object(expansion, "INPUTS", inputs), patch.object(expansion.seed, "SEED_DIR", str(seeds)):
                expansion.expand(directory)
                output = seeds / "storage-expanded.json"
                first = output.read_bytes()
                products = json.loads(first)["products"]
                self.assertEqual(len(products), 2)
                merged = next(p for p in products if p["id"] == "kingston-ssdnow-ms200")
                self.assertEqual(set(merged["aliases"]), {rows[0]["name"], rows[1]["name"]})
                self.assertEqual(merged["possibleAttributes"]["storage_size"], ["30GB", "480GB"])
                expansion.expand(directory)
                self.assertEqual(first, output.read_bytes())
                rows[1]["interface"] = "SATA"  # A reviewed spelling never overrides contradictory hardware.
                raw = json.dumps(rows).encode()
                (directory / "pcpp-internal-hard-drive.json").write_bytes(raw)
                inputs["internal-hard-drive"] = (hashlib.sha256(raw).hexdigest(), *inputs["internal-hard-drive"][1:])
                expansion.expand(directory)
                self.assertEqual([p["id"] for p in json.loads(output.read_bytes())["products"]], ["kingston-ssdnow-200"])

    def test_exact_models_preserve_existing_and_omit_conflicting_ram(self):
        import expand_seed as expansion
        rows = [
            {"name": "Crucial CT2K16G48C40U5 32 GB", "speed": [5, 4800], "modules": [2, 16], "cas_latency": 40},
            {"name": "Corsair Vengeance 32 GB", "speed": [5, 6000], "modules": [2, 16], "cas_latency": 36},
            {"name": "Kingston KF432 32 GB", "speed": [4, 3200], "modules": [2, 16], "cas_latency": 16},
            {"name": "Kingston KF432 32 GB", "speed": [4, 3600], "modules": [2, 16], "cas_latency": 16},
            {"name": "Crucial CT8G48 8 GB", "speed": [5, 4800], "modules": [1, 8], "cas_latency": 40},
        ]
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            seeds = directory / "seeds"
            seeds.mkdir()
            raw = json.dumps(rows).encode()
            (directory / "pcpp-memory.json").write_bytes(raw)
            original = json.dumps({"products": [{"id": "curated", "aliases": ["Crucial CT8G48 8 GB"]}]}).encode()
            (seeds / "curated.json").write_bytes(original)
            inputs = {"memory": (hashlib.sha256(raw).hexdigest(), "ram", expansion.seed.CAT_RAM,
                                 ("speed", "modules", "cas_latency"))}
            with patch.object(expansion, "INPUTS", inputs), patch.object(expansion.seed, "SEED_DIR", str(seeds)):
                expansion.expand(directory)
                first = (seeds / "ram-expanded.json").read_bytes()
                products = json.loads(first)["products"]
                self.assertEqual([p["name"] for p in products], ["Crucial CT2K16G48C40U5 32 GB"])
                self.assertEqual(products[0]["possibleAttributes"], {"ram_size": ["32GB"]})
                expansion.expand(directory)
                self.assertEqual(first, (seeds / "ram-expanded.json").read_bytes())
                self.assertEqual(original, (seeds / "curated.json").read_bytes())
                (directory / "pcpp-memory.json").write_bytes(raw + b" ")
                with self.assertRaisesRegex(ValueError, "input differs from pinned dataset"):
                    expansion.expand(directory)


class RemainingFamilyExpansionTests(unittest.TestCase):
    def test_pinned_sources_keep_previous_expansion_and_reject_phone_rows(self):
        import expand_families as expansion
        gpu = json.dumps([{"chipset": "Quadro P2000"}, {"chipset": "RTX A2000 6GB"},
                          {"chipset": "RTX A2000 12GB"}]).encode()
        cameras = ("Brand,Model,Also known as\nNikon,D850,\nNokia,Lumia 1020,\n"
                   'Fujifilm,X-T5,"X-T05(Japan),X-T005(US),Shared99,Existing9,X-T6,Series X,X-T5(Typ100)"\n'
                   "Fujifilm,X-T6,Shared99\nPanasonic,Lumix,\n").encode()
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            seeds = directory / "seeds"
            seeds.mkdir()
            original = json.dumps({"products": [{"id": "preserved", "aliases": ["Nikon D850", "Fujifilm Existing9"]}]}).encode()
            (seeds / "earlier-expanded.json").write_bytes(original)
            (directory / "pcpp-video-card.json").write_bytes(gpu)
            (directory / "camera_data.csv").write_bytes(cameras)
            with patch.object(expansion, "GPU_SHA256", hashlib.sha256(gpu).hexdigest()), \
                    patch.object(expansion, "CAMERA_SHA256", hashlib.sha256(cameras).hexdigest()), \
                    patch.object(expansion.seed, "SEED_DIR", str(seeds)):
                expansion.expand(directory)
                paths = [seeds / f"{name}.json" for name in sorted(expansion.OUTPUTS)]
                first = [p.read_bytes() for p in paths]
                imported_cameras = json.loads(first[0])["products"]
                self.assertEqual([p["name"] for p in imported_cameras], ["Fujifilm X-T5", "Fujifilm X-T6"])
                self.assertEqual(imported_cameras[0]["aliases"], ["Fujifilm X-T005", "Fujifilm X-T05", "Fujifilm X-T5"])
                self.assertEqual(imported_cameras[1]["aliases"], ["Fujifilm X-T6"])
                self.assertEqual([p["name"] for p in json.loads(first[1])["products"]],
                                 ["Nvidia Quadro P2000", "Nvidia RTX A2000"])
                expansion.expand(directory)
                self.assertEqual(first, [p.read_bytes() for p in paths])
                self.assertEqual(original, (seeds / "earlier-expanded.json").read_bytes())
                (directory / "camera_data.csv").write_bytes(cameras + b" ")
                with self.assertRaisesRegex(ValueError, "input differs from pinned dataset"):
                    expansion.expand(directory)
