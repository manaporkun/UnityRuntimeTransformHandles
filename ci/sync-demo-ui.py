#!/usr/bin/env python3
"""Distribute the canonical demo theme to independent sample imports, or verify drift."""
import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SAMPLES = ('Demo', 'LevelEditor', 'ModularBuilding', 'DemoBrowser')


def sync(root=ROOT, check=False):
    root = Path(root)
    theme = (root / 'ci/demo-ui/DemoTheme.uss').read_bytes()
    base = root / 'Packages/com.orkunmanap.runtime-transform-handles.demos/Samples~'
    for sample in SAMPLES:
        path = base / sample / 'UI/DemoTheme.uss'
        if check:
            if not path.exists() or path.read_bytes() != theme:
                raise ValueError(f'{path}: run python3 ci/sync-demo-ui.py')
        else:
            path.write_bytes(theme)  # Preserve each sample's independent meta GUID.


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    sync(check=args.check)
    print('PASS: shared theme matches all four independent samples')
