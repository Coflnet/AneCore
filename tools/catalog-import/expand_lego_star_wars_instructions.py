#!/usr/bin/env python3
"""Rebuild the R61 Star Wars identities from reviewed official instruction panels."""
import argparse
import json
from pathlib import Path
import re
from urllib.parse import urlparse

import build_seed as seed
import normalize as nz
from expand_lego import CATEGORIES

PROOF = Path(__file__).parent / 'proofs' / 'lego-star-wars-instructions-2026-11-05-r61.json'
OUTPUT = Path(seed.SEED_DIR) / 'lego-star-wars-instructions.json'


def products():
    existing = [row for path in sorted(OUTPUT.parent.glob('*.json')) if path != OUTPUT
                for row in json.loads(path.read_text())['products']]
    existing_numbers = {str(number) for row in existing
                        for number in row.get('possibleAttributes', {}).get('set_number', [])}
    existing_models = {row['model'] for row in existing
                       if nz.matcher_tokens(row.get('brand', '')) == ['lego']}
    rows = []
    for fact in json.loads(PROOF.read_text())['facts']:
        number, name, metadata = fact['number'], fact['name'], fact['metadata']
        url = urlparse(fact['source_url'])
        if not re.fullmatch(r'75\d{3}', number) or name != name.strip() or metadata['h1'] != name or not name:
            raise ValueError(f'Invalid exact identity: {number}')
        if url.scheme != 'https' or url.netloc != 'www.lego.com' or url.path != '/en-us/service/building-instructions/' + number:
            raise ValueError(f'Invalid official URL: {number}')
        if fact['evidence_type'] != 'instruction_panel' or metadata['printed_number'] != '#' + number:
            raise ValueError(f'Missing printed number: {number}')
        if metadata['theme'] not in ('LEGO® Star Wars™',) or not re.fullmatch(r'\d+(?:-\d+|\+) \| #' + number + r' \| \d+ pieces', metadata['panel']):
            raise ValueError(f'Invalid official panel: {number}')
        if number in existing_numbers or number in existing_models:
            raise ValueError(f'Existing set number or LEGO model: {number}')
        row = seed.make_product(f'lego-{number}', 'LEGO', number, f'LEGO {number}', CATEGORIES,
                                'lego', {f'LEGO {number}', f'LEGO Set {number}', f'LEGO {number} {name}'},
                                'unused', {'set_number': [number]})
        row['source'] = 'seed:lego-official-star-wars-instructions-2026-11-05'
        rows.append(row)
    ids, aliases = set(), {}
    for row in existing + rows:
        if row['id'] in ids:
            raise ValueError(f'Duplicate id: {row["id"]}')
        ids.add(row['id'])
        for alias in row['aliases']:
            key = tuple(nz.matcher_tokens(alias))
            if key in aliases and aliases[key] != row['id']:
                raise ValueError(f'Normalized alias collision: {alias}')
            aliases[key] = row['id']
    return sorted(rows, key=lambda row: row['id'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Require the seed to match the reviewed proof')
    args = parser.parse_args()
    expected = {'products': products()}
    if args.check:
        if json.loads(OUTPUT.read_text()) != expected:
            raise SystemExit('Star Wars seed differs from reviewed official headers')
    else:
        seed.write_seed('lego-star-wars-instructions', expected['products'])
    print(f'Verified {len(expected["products"])} distinct official LEGO Star Wars identities')
