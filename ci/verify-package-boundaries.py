#!/usr/bin/env python3
"""Validate the Core/demos installation boundary without requiring a Unity license."""
import argparse
import json
import re
from pathlib import Path
from urllib.parse import unquote
import xml.etree.ElementTree as ET

CORE = 'com.orkunmanap.runtime-transform-handles'
DEMOS = CORE + '.demos'
# Optional Unity assembly references already used by the showcase/Core asmdefs.
UNITY_ASSEMBLIES = {'2bafac87e7f4b9b418d9448d219b01ab', '75469ad4d38634e559750d17036d5f7c'}
GUID = re.compile(r'(?:guid:\s*|GUID:)([0-9a-f]{32})')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_ui_references(path, text, folder):
    # Toolkit references are often relative URLs rather than serialized YAML GUIDs.
    if path.suffix == '.uxml':
        refs = [value for node in ET.fromstring(text).iter() for key, value in node.attrib.items()
                if key == 'src']
    else:
        refs = []
    text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
    refs += re.findall(r'url\(\s*[\"\']?([^\)\"\']+)[\"\']?\s*\)', text)
    refs += re.findall(r'@import\s+[\"\']([^\"\']+)[\"\']', text)
    for value in refs:
        value = unquote(value.strip())
        if path.suffix == '.tss' and value == 'unity-theme://default':
            continue
        require(not any(c in value for c in (':', '?', '#', '\\')),
                f'unsupported UI asset reference: {path}: {value}; use sample-relative paths')
        target = (path.parent / value).resolve()
        require(target.is_relative_to(folder) and target.is_file(),
                f'UI reference escapes its sample or is missing: {path}: {value}')


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
    samples_root = (demos / 'Samples~').resolve()
    listed_folders = {(demos / s['path']).resolve() for s in samples}
    if samples_root.is_dir():
        actual_folders = {path.resolve() for path in samples_root.iterdir() if path.is_dir()}
        require(actual_folders == listed_folders,
                f'Samples~ folders must all be registered in package.json: '
                f'unlisted={sorted(str(p) for p in actual_folders - listed_folders)} '
                f'missing={sorted(str(p) for p in listed_folders - actual_folders)}')
    # Parse every asmdef. The GUID text scan below cannot see name-based references, so a
    # sample referencing another sample (or a nonexistent assembly) would otherwise ship
    # green and only fail in a consumer project that imports that single sample.
    asmdefs = {}
    assembly_names = {}
    asmdef_by_guid = {}
    for package in (core, demos):
        for path in package.rglob('*.asmdef'):
            try:
                asmdef = json.loads(path.read_text())
            except json.JSONDecodeError as error:
                raise ValueError(f'invalid asmdef JSON: {path}: {error}') from error
            name = asmdef.get('name')
            require(isinstance(name, str) and name, f'asmdef missing name: {path}')
            require(name not in assembly_names, f'duplicate assembly name: {name}: {path}')
            assembly_names[name] = path
            asmdefs[path] = asmdef
            meta = Path(str(path) + '.meta')
            if meta.is_file():
                match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
                if match:
                    asmdef_by_guid[match[1]] = path
    for sample in samples:
        folder = (demos / sample['path']).resolve()
        require(folder.is_relative_to((demos / 'Samples~').resolve()) and folder.is_dir(), 'invalid sample path')
        local_guids = {guid for guid, path in all_guids.items() if path.resolve().is_relative_to(folder)}
        allowed = core_guids | local_guids | UNITY_ASSEMBLIES
        require(any(folder.glob('*.unity')) and any(folder.glob('*.asmdef')), 'sample scene/assembly missing')
        for path in folder.rglob('*'):
            if path.suffix not in ('.unity', '.prefab', '.asset', '.mat', '.asmdef', '.asmref', '.shader', '.anim', '.controller', '.inputactions', '.renderTexture', '.uxml', '.uss', '.tss'):
                continue
            text = path.read_text()
            if path.suffix in ('.uxml', '.uss', '.tss'):
                validate_ui_references(path, text, folder)
            for guid in GUID.findall(text):
                require(guid.startswith('0000000000000000') or guid in allowed,
                        f'sample reference escapes its import/Core/Unity dependencies: {path}: {guid}')
    unity_named = {'UnityEngine.TestRunner', 'UnityEditor.TestRunner'}

    def sample_folder_of(path):
        for sample in samples:
            folder = (demos / sample['path']).resolve()
            if path.resolve().is_relative_to(folder):
                return folder
        return None

    for path, asmdef in asmdefs.items():
        own_sample = sample_folder_of(path) if path.resolve().is_relative_to(demos.resolve()) else None
        for reference in asmdef.get('references', []):
            if reference.startswith('GUID:'):
                guid = reference[5:]
                if guid in UNITY_ASSEMBLIES:
                    continue
                target = asmdef_by_guid.get(guid)
                require(target is not None, f'asmdef reference does not resolve to an assembly: {path}: {reference}')
            elif reference in unity_named:
                continue
            else:
                target = assembly_names.get(reference)
                require(target is not None, f'asmdef reference does not resolve to an assembly: {path}: {reference}')
            if target.resolve().is_relative_to(core.resolve()):
                continue
            target_sample = sample_folder_of(target)
            require(own_sample is not None and target_sample == own_sample,
                    f'asmdef reference crosses the Core/sample boundary: {path}: {reference}')
    return len(samples)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parent.parent)
    print(f'PASS: Core boundary and {validate(parser.parse_args().root)} optional samples')
