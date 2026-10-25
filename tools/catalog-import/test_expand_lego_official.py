import gzip
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import expand_lego_official as expansion


class OfficialLegoTests(unittest.TestCase):
    def test_pinned_import_is_repeatable_and_keeps_only_unique_official_set_identities(self):
        urls = ["https://www.lego.com/en-us/product/space-10497",
                "https://www.lego.com/en-us/product/existing-10000",
                "https://www.lego.com/en-us/product/reused-12345",
                "https://www.lego.com/en-us/product/unknown-54321",
                "https://www.lego.com/en-us/product/a-22222",
                "https://www.lego.com/en-us/product/b-22222",
                "https://www.lego.com/en-us/product/merchandise-5001234",
                "https://other.test/en-us/product/space-10498"]
        xml = ('<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">'
               + ''.join('<url><loc>' + url + '</loc></url>' for url in urls) + '</urlset>').encode()
        current = gzip.compress(b"set_num\n10497-1\n10000-1\n12345-1\n12345-2\n22222-1\n10498-1\n")
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            (directory / "existing.json").write_text(json.dumps({"products": [{"id": "lego-10000"}]}))
            sitemap, veto = directory / "source.xml", directory / "sets.csv.gz"
            sitemap.write_bytes(xml)
            veto.write_bytes(current)
            with patch.object(expansion.seed, "SEED_DIR", str(directory)), \
                    patch.object(expansion, "SITEMAP_SHA256", hashlib.sha256(xml).hexdigest()), \
                    patch.object(expansion, "CURRENT_SHA256", hashlib.sha256(current).hexdigest()):
                expansion.expand(sitemap, veto)
                output = directory / expansion.OUTPUT
                first = output.read_bytes()
                products = json.loads(first)["products"]
                self.assertEqual([p["id"] for p in products], ["lego-10497"])
                self.assertEqual(products[0]["name"], "LEGO 10497")
                self.assertEqual(set(products[0]["aliases"]), {"LEGO 10497", "LEGO Set 10497"})
                expansion.expand(sitemap, veto)
                self.assertEqual(first, output.read_bytes())
                sitemap.write_bytes(xml + b" ")
                with self.assertRaisesRegex(ValueError, "pinned dataset"):
                    expansion.expand(sitemap, veto)
                self.assertEqual(first, output.read_bytes())


if __name__ == "__main__":
    unittest.main()
