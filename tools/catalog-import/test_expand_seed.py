"""Run with: python3 -m unittest discover -s tools/catalog-import -p 'test_*.py'."""
import sys
import unittest
from pathlib import Path

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
