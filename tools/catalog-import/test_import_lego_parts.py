import unittest

import import_lego_parts as parts

CATEGORIES = {"11": "Bricks", "14": "Plates", "58": "Stickers", "13": "Minifigs", "53": "Technic Pins"}


def part(num, name="Brick 2 x 4", cat="11", material="Plastic"):
    return {"part_num": num, "name": name, "part_cat_id": cat, "part_material": material}


def colour(cid, name, rgb="C91A09", trans="False"):
    return {"id": cid, "name": name, "rgb": rgb, "is_trans": trans}


def element(eid, num, cid):
    return {"element_id": eid, "part_num": num, "color_id": cid, "design_id": ""}


class SelectPartsTests(unittest.TestCase):
    def test_only_plain_mould_numbers_are_parts(self):
        self.assertTrue(all(parts.is_plain_part(p) for p in ("3001", "3069b", "32523", "3626c")))
        self.assertFalse(any(parts.is_plain_part(p) for p in ("973", "3626cpr0001", "3068bpat0001", "003381", "2x4", "970c00x1", "12x")))

    def test_prints_stickers_minifig_bodies_and_paper_are_dropped(self):
        rows = [part("3001"), part("3626cpr0001", "Minifig Head Print"), part("30694", "Sticker Sheet", "58"),
                part("10057", "Body Gollum", "13"), part("4000", "Instruction Card", "11", "Cardboard/Paper")]
        self.assertEqual(list(parts.select_parts(rows, CATEGORIES, set())), ["3001"])

    def test_a_part_number_that_is_also_any_set_number_is_flagged(self):
        sets = parts.set_bases([{"set_num": "3001-1"}, {"set_num": "6020-2"}])
        result = parts.select_parts([part("3001"), part("3004", "Brick 1 x 2"), part("6020", "Plate")], CATEGORIES, sets)
        self.assertEqual(result["3001"], ["Brick 2 x 4", 11, 1])
        self.assertEqual(result["3004"][2], 0)
        self.assertEqual(result["6020"][2], 1)


class SelectElementsTests(unittest.TestCase):
    def test_elements_need_an_imported_part_a_real_colour_and_six_or_seven_digits(self):
        known = {"3001": ["Brick 2 x 4", 11, 1]}
        colors = {"4": {}, "0": {}}
        rows = [element("300121", "3001", "4"), element("4211388", "3001", "0"), element("12345", "3001", "4"),
                element("0300121", "3001", "4"), element("6123456", "3626cpr0001", "14"), element("6000001", "3001", "9999"),
                element("6000002", "3001", "-1")]
        self.assertEqual(parts.select_elements(rows, known, colors, set()), {"300121": ["3001", 4], "4211388": ["3001", 0]})

    def test_an_element_id_shared_with_a_set_or_part_number_is_dropped(self):
        known = {"3001": ["Brick 2 x 4", 11, 0], "300121": ["Odd", 11, 0]}
        rows = [element("300121", "3001", "4"), element("5005256", "3001", "4"), element("4211388", "3001", "4")]
        self.assertEqual(parts.select_elements(rows, known, {"4": {}}, {"5005256"}), {"4211388": ["3001", 4]})


class ColourTests(unittest.TestCase):
    def test_glossary_names_its_rebrickable_colours_exactly(self):
        with self.assertRaises(ValueError):
            parts.build_colors([colour("71", "Light Bluish Gray")], {"71": ("Light Gray", {"en": ["light bluish gray"]})})

    def test_colours_have_slugs_and_reviewed_terms_and_no_placeholder_colours(self):
        rows = [colour("-1", "[Unknown]"), colour("9999", "[No Color/Any Color]"), colour("4", "Red"),
                colour("47", "Trans-Clear", "FCFCFC", "True")]
        result = parts.build_colors(rows, {"4": ("Red", {"de": ["rot"]})})
        self.assertEqual(set(result), {"4", "47"})
        self.assertEqual(result["4"], {"name": "Red", "rgb": "C91A09", "trans": False, "slug": "red", "terms": {"de": ["rot"]}})
        self.assertEqual((result["47"]["slug"], result["47"]["trans"]), ("trans-clear", True))

    def test_reviewed_glossary_keeps_old_and_new_greys_apart(self):
        colors, ambiguous, words = parts.load_glossary()
        self.assertEqual(colors["71"][0], "Light Bluish Gray")
        self.assertEqual(colors["7"][0], "Light Gray")
        new_grey = {t for terms in colors["71"][1].values() for t in terms}
        old_grey = {t for terms in colors["7"][1].values() for t in terms}
        self.assertFalse(new_grey & old_grey)
        for plain in ("hellgrau", "dunkelgrau", "light gray", "dark gray", "grau", "braun", "dark green", "dunkelgrün"):
            self.assertIn(plain, ambiguous)
            self.assertFalse(any(plain in terms for _, by_lang in colors.values() for terms in by_lang.values()), plain)
        self.assertIn("rot", colors["4"][1]["de"])
        self.assertIn("czerwony", colors["4"][1]["pl"])
        self.assertIn("Fliese", words["tile"]["de"])

    def test_every_glossary_colour_has_one_owner_per_phrase(self):
        colors, _, _ = parts.load_glossary()
        seen = {}
        for cid, (_, by_lang) in colors.items():
            for phrase in {t.lower() for terms in by_lang.values() for t in terms}:
                self.assertEqual(seen.setdefault(phrase, cid), cid, phrase)


class PartWordTests(unittest.TestCase):
    def test_only_words_of_part_names_without_colour_words(self):
        known = {"3001": ["Brick 2 x 4 Red", 11, 0]}
        words = {"brick": {"de": ["Stein"]}, "red": {"de": ["rot"]}, "train": {"de": ["Zug"]}, "plates": {"de": ["Platten"]}}
        self.assertEqual(parts.used_part_words(known, {"14": "Plates"}, words, {"red"}),
                         {"brick": {"de": ["Stein"]}, "plates": {"de": ["Platten"]}})

    def test_a_word_of_a_longer_colour_phrase_is_no_colour_word(self):
        colour_words = parts.colour_words({"19": ("Tan", {"en": ["tan", "brick yellow"]})}, ["light gray", "braun"])
        self.assertEqual(colour_words, {"tan", "braun"})


if __name__ == "__main__":
    unittest.main()
