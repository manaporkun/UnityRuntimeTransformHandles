#!/usr/bin/env python3
"""Verify the exact optional-sample suite against unchanged candidate/import bytes."""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import time
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location('candidate', Path(__file__).with_name('release-candidate.py'))
candidate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(candidate)
require = candidate.require


def prepare(root, out, unity, input_mode):
    require(not out.exists(), 'use fresh sample evidence')
    candidate.verify(root, root / 'artifacts/candidate')
    manifest = json.loads((root / 'Packages' / candidate.DEMOS / 'package.json').read_text())
    imports = root / 'Assets/Samples' / manifest['displayName'] / manifest['version']
    for sample in manifest['samples']:
        require(candidate.hashes(imports / sample['displayName']) ==
                candidate.hashes(root / 'Packages' / candidate.DEMOS / sample['path']), 'sample import differs')
    out.mkdir(parents=True)
    data = {'started_ns': time.time_ns(), 'unity': unity, 'input': input_mode,
            'imports': str(imports.resolve()), 'files': candidate.hashes(imports),
            'generated_folder_meta': [s['displayName'] + '.meta' for s in manifest['samples']],
            'candidate_sha256': candidate.digest(root / 'artifacts/candidate/candidate.json')}
    (out / 'metadata.json').write_text(json.dumps(data, indent=2) + '\n')


def verify(root, out, xml, log, required_dir=Path(__file__).parent):
    metadata = json.loads((out / 'metadata.json').read_text())
    require(xml.stat().st_mtime_ns >= metadata['started_ns'] and
            log.stat().st_mtime_ns >= metadata['started_ns'], 'stale sample results')
    require(candidate.digest(root / 'artifacts/candidate/candidate.json') == metadata['candidate_sha256'],
            'candidate metadata changed')
    candidate.verify(root, root / 'artifacts/candidate')
    imports = Path(metadata['imports'])
    actual = candidate.hashes(imports)
    require(all(actual.get(name) == digest for name, digest in metadata['files'].items()),
            'imported samples changed')
    extra = set(actual) - set(metadata['files'])
    require(extra <= set(metadata.get('generated_folder_meta', [])), 'unexpected imported sample files')
    # Unity creates metadata for the consumer's newly named top-level import folders.
    # These files are not part of the distributed sample. No source asset may change.
    for name in extra:
        text = (imports / name).read_text()
        require(re.search(r'^guid: [0-9a-f]{32}$', text, re.M) and
                re.search(r'^folderAsset: yes$', text, re.M) and
                (imports / name[:-5]).is_dir(), 'invalid generated import folder metadata')
    settings = (root / 'ProjectSettings/ProjectSettings.asset').read_text()
    require(re.findall(r'^  activeInputHandler: (\d+)$', settings, re.M) == [str(metadata['input'])],
            'sample input configuration mismatch')
    text = log.read_text(errors='replace')
    require(sorted(set(re.findall(r'Initialize engine version: ([\w.]+)', text))) == [metadata['unity']],
            'sample Editor version mismatch')
    tree = ET.parse(xml).getroot()
    cases = tree.findall('.//test-case')
    names = [line.strip() for line in (Path(required_dir) /
                f"required-samples-input{metadata['input']}.txt").read_text().splitlines()
                if line.strip() and not line.startswith('#')]
    require(names and len(set(names)) == len(names), 'invalid expected sample names')
    expected = set(names)
    actual = [case.get('fullname') for case in cases]
    require(len(actual) == len(set(actual)) and set(actual) == expected,
            f'sample suite mismatch: missing={expected - set(actual)}, extra={set(actual) - expected}')
    require(tree.get('result') == 'Passed' and all(case.get('result') == 'Passed' for case in cases),
            'sample failures or skips')
    result = {'passed': len(cases), 'unity': metadata['unity'], 'input': metadata['input'],
              'xml_sha256': candidate.digest(xml), 'log_sha256': candidate.digest(log)}
    (out / 'verified.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['prepare', 'verify'])
    parser.add_argument('--root', type=Path, default=Path('.'))
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--unity')
    parser.add_argument('--input', type=int, choices=[0, 1])
    parser.add_argument('--xml', type=Path)
    parser.add_argument('--log', type=Path)
    parser.add_argument('--required-dir', type=Path, default=Path(__file__).parent)
    args = parser.parse_args()
    if args.command == 'prepare':
        require(args.unity and args.input is not None, 'prepare requires Unity version and input mode')
        prepare(args.root, args.out, args.unity, args.input)
    else:
        verify(args.root, args.out, args.xml, args.log, args.required_dir)


if __name__ == '__main__':
    main()
