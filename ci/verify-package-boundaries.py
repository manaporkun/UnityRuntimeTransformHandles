#!/usr/bin/env python3
"""Validate the Core/demos installation boundary without requiring a Unity license."""
import argparse
import json
import re
from pathlib import Path

CORE = 'com.orkunmanap.runtime-transform-handles'
DEMOS = CORE + '.demos'
# Optional Unity assembly references already used by the showcase/Core asmdefs.
UNITY_ASSEMBLIES = {'2bafac87e7f4b9b418d9448d219b01ab', '75469ad4d38634e559750d17036d5f7c'}
# The unchanged legacy showcase retains its documented URP camera component.
LEGACY_URP = {'a79441f348de89743a2939f4d699eac1'}
# Official TMP runtime assembly, font asset script and Essential Resources mobile SDF shader.
MODULAR_TMP = {'6055be8ebefd69e48b49212b09b47b2f', '71c1514a6bd24e1e882cebbe1904ce04',
               'fe393ace9b354375a9cb14cdbbc28be4'}
GUID = re.compile(r'(?:guid:\s*|GUID:)([0-9a-f]{32})')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate(root):
    root = Path(root)
    core, demos = (root / 'Packages' / name for name in (CORE, DEMOS))
    cm, dm = (json.loads((path / 'package.json').read_text()) for path in (core, demos))
    require(cm['name'] == CORE and dm['name'] == DEMOS, 'package identity')
    require(not cm.get('samples'), 'Core must not register samples')
    require(not (core / 'Samples~').exists(), 'Core must not ship samples')
    require(DEMOS not in cm.get('dependencies', {}), 'Core depends on demos')
    require(dm.get('dependencies', {}).get(CORE) == cm['version'], 'demos/Core version pair')
    version = re.compile(r'\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?')
    for manifest in (cm, dm):
        for name, value in manifest.get('dependencies', {}).items():
            require(isinstance(value, str) and version.fullmatch(value),
                    f'package dependencies must use versions: {name}={value!r}; install Git pairs in the project manifest')
    for path in core.rglob('*'):
        if path.suffix in ('.cs', '.asmdef', '.unity', '.prefab', '.asset'):
            text = path.read_text()
            require(DEMOS not in text and 'TransformHandles.Samples.' not in text, f'Core demo reference: {path}')
        require(path.suffix != '.unity', f'Core demo scene: {path}')
    root_files = {'package.json', 'README.md', 'LICENSE.md', 'CHANGELOG.md', 'Third Party Notices.md'}
    for path in demos.rglob('*'):
        rel = path.relative_to(demos)
        if path.is_dir() or rel.parts[0] == 'Samples~' or path.suffix == '.meta':
            continue
        require(len(rel.parts) == 1 and path.name in root_files, f'always-compiled demo asset: {path}')
    for package in (core, demos):
        for name in ('README.md', 'LICENSE.md', 'CHANGELOG.md', 'Third Party Notices.md'):
            require((package / name).is_file(), f'missing package documentation: {package / name}')
    all_guids = {}
    for package in (core, demos):
        for path in package.rglob('*.meta'):
            require(not path.read_bytes().startswith(b'\xef\xbb\xbf'), f'meta BOM: {path}')
            match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.M)
            require(match is not None, f'invalid meta GUID: {path}')
            guid = match[1]
            require(guid not in all_guids, f'duplicate GUID: {path} and {all_guids.get(guid)}')
            all_guids[guid] = path
    core_guids = {guid for guid, path in all_guids.items() if path.is_relative_to(core)}
    samples = dm.get('samples', [])
    require(samples and len({s['path'] for s in samples}) == len(samples), 'sample paths missing/duplicated')
    require(len({s['displayName'] for s in samples}) == len(samples), 'duplicate sample name')
    for sample in samples:
        folder = (demos / sample['path']).resolve()
        require(folder.is_relative_to((demos / 'Samples~').resolve()) and folder.is_dir(), 'invalid sample path')
        local_guids = {guid for guid, path in all_guids.items() if path.resolve().is_relative_to(folder)}
        allowed = core_guids | local_guids | UNITY_ASSEMBLIES
        if sample['path'] == 'Samples~/ModularBuilding':
            allowed |= MODULAR_TMP
        if sample['path'] == 'Samples~/Demo':
            allowed |= LEGACY_URP
        require(any(folder.glob('*.unity')) and any(folder.glob('*.asmdef')), 'sample scene/assembly missing')
        for path in folder.rglob('*'):
            if path.suffix not in ('.unity', '.prefab', '.asset', '.mat', '.asmdef', '.asmref', '.shader', '.anim', '.controller', '.inputactions', '.renderTexture'):
                continue
            for guid in GUID.findall(path.read_text()):
                require(guid.startswith('0000000000000000') or guid in allowed,
                        f'sample reference escapes its import/Core/Unity dependencies: {path}: {guid}')
    return len(samples)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parent.parent)
    print(f'PASS: Core boundary and {validate(parser.parse_args().root)} optional samples')
