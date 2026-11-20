import unittest

import enrich_lego_titles as enrich

THEMES = {"1": ("City", ""), "2": ("Trains", "1"), "3": ("Duplo", "")}
WORDS = {"train": {"de": ["Zug", "Eisenbahn"], "pl": ["pociąg"]}, "truck": {"de": ["Lastwagen"]}, "station": {"de": ["Station"]}}
PHRASES = {"fire station": {"de": ["Feuerwehrstation"]}}
THEME_GLOSSARY = {"Trains": {"de": ["Eisenbahn"]}}


def row(num, name, theme="2", year="2018", parts="1226"):
    return {"set_num": num, "name": name, "theme_id": theme, "year": year, "num_parts": parts}


class EnrichTests(unittest.TestCase):
    def test_set_info_has_theme_parent_year_pieces_and_title_terms(self):
        info = enrich.set_info(row("60198-1", "Cargo &amp; Train"), THEMES, WORDS, PHRASES)
        self.assertEqual(info["title"], "Cargo & Train")
        self.assertEqual((info["theme"], info["parentTheme"], info["year"], info["pieces"]), ("Trains", "City", 2018, 1226))
        self.assertEqual(info["terms"], {"de": ["Zug", "Eisenbahn"], "pl": ["pociąg"]})

    def test_plural_falls_back_and_possessive_and_unknown_words_are_skipped(self):
        info = enrich.set_info(row("1-1", "Trucks Kai's Ninjago", theme="3", parts="0"), THEMES, WORDS, PHRASES)
        self.assertEqual(info["terms"], {"de": ["Lastwagen"]})
        self.assertNotIn("parentTheme", info)
        self.assertNotIn("pieces", info)

    def test_phrase_wins_over_the_context_free_station_word(self):
        info = enrich.set_info(row("1-1", "Fire Station"), THEMES, WORDS, PHRASES)
        self.assertEqual(info["terms"]["de"], ["Feuerwehrstation", "Station"])
        self.assertNotIn("Bahnhof", info["terms"]["de"])

    def test_only_unambiguous_dash_one_sets_are_used(self):
        rows = [row("1-1", "A"), row("2-1", "B"), row("2-2", "C"), row("3-2", "D")]
        self.assertEqual(set(enrich.unique_base_sets(rows)), {"1"})

    def test_only_four_or_five_digit_numbers_are_listing_identities(self):
        self.assertTrue(all(enrich.is_listing_identity(b) for b in ("1000", "10874", "60198")))
        self.assertFalse(any(enrich.is_listing_identity(b) for b in ("375", "0003977811", "123456", "K10", "1224A", "01234")))

    def test_build_covers_sets_outside_the_catalogue_and_stores_theme_terms_once(self):
        sets = {"10874": row("10874-1", "Steam Train", theme="3"), "0003977811": row("0003977811-1", "Book")}
        infos = enrich.build(sets, THEMES, WORDS, PHRASES)
        self.assertEqual(list(infos), ["10874"])
        self.assertEqual(enrich.theme_terms(infos, {"Duplo": {"de": ["X"]}}), {"Duplo": {"de": ["X"]}})

    def test_glossary_loads_and_has_no_context_dependent_station(self):
        words, themes, phrases = enrich.load_glossary()
        self.assertIn("treno", words["train"]["it"])
        self.assertNotIn("Bahnhof", words["station"]["de"])
        self.assertIn("Feuerwehrstation", phrases["fire station"]["de"])
        self.assertTrue(all(set(t) <= set(enrich.LANGUAGES) for t in words.values()))


if __name__ == "__main__":
    unittest.main()
