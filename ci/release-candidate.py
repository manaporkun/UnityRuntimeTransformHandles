#!/usr/bin/env python3
"""Freeze versioned UPM artifacts before testing; never fetch or publish code."""
import argparse
from datetime import date
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tarfile
import tempfile

CORE = 'com.orkunmanap.runtime-transform-handles'
DEMOS = CORE + '.demos'
PACKAGES = (CORE, DEMOS)
REQUIRED_FILES = ('required-playmode.txt', 'required-editmode.txt',
                  'required-samples-input0.txt', 'required-samples-input1.txt',
                  'required-unity-evidence.json')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args], text=True).strip()


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def hashes(folder):
    require(not any(p.is_symlink() for p in folder.rglob('*')), 'package symlinks are unsupported')
    return {p.relative_to(folder).as_posix(): digest(p) for p in sorted(folder.rglob('*'))
            if p.is_file() and p.name != '.DS_Store'}


def release_exists(repository, version):
    result = subprocess.run(['gh', 'api', '--include', f'repos/{repository}/releases/tags/v{version}'],
                            text=True, capture_output=True)
    if result.returncode == 0:
        return True
    require(bool(re.search(r'^HTTP/\S+ 404(?: |$)', result.stdout, re.M)),
            'release lookup failed; refusing to infer an unpublished version: ' + result.stderr)
    return False


def version_bump(root, start, current):
    scope = f'{start}..HEAD' if start else 'HEAD'
    path = 'Packages/' + CORE
    messages = git(root, 'log', scope, '--format=%B', '--', path)
    if re.search(r'^(BREAKING CHANGE:|[a-z]+(?:\(.+\))?!:)', messages, re.M | re.I):
        bump = 'major'
    elif re.search(r'^feat(?:\(.+\))?:', messages, re.M | re.I):
        bump = 'minor'
    elif re.search(r'^(fix|perf|refactor)(?:\(.+\))?:', messages, re.M | re.I):
        bump = 'patch'
    elif re.search(r'^(docs|style|chore|ci|test|build)(?:\(.+\))?:', messages, re.M | re.I):
        files = git(root, 'log', scope, '--name-only', '--pretty=format:', '--', path).splitlines()
        bump = 'patch' if any('/Tests/' not in f and '/Samples~/' not in f and
                              (f.endswith('.cs') or f.endswith('package.json')) for f in files) else 'none'
    else:
        bump = 'none'
    major, minor, patch = map(int, current.split('.'))
    version = {'major': f'{major + 1}.0.0', 'minor': f'{major}.{minor + 1}.0',
               'patch': f'{major}.{minor}.{patch + 1}', 'none': current}[bump]
    return bump, version


def finalize_changelog(path, version, fallback):
    text = path.read_text()
    match = re.search(r'^## \[Unreleased\][^\n]*\n(.*?)(?=^## \[|\Z)', text, re.M | re.S)
    require(match is not None, 'missing Unreleased changelog: ' + str(path))
    entries = match[1].strip() or fallback
    section = f'## [Unreleased]\n\n## [{version}] - {date.today().isoformat()}\n\n{entries}\n\n'
    path.write_text(text[:match.start()] + section + text[match.end():])
    return entries


def prepare(root, out, repository=None):
    require(not out.exists(), 'use a fresh candidate directory')
    require(not git(root, 'status', '--porcelain', '--untracked-files=all', '--', 'Packages'),
            'commit package changes before preparing a candidate')
    source = git(root, 'rev-parse', 'HEAD')
    current = json.loads((root / 'Packages' / CORE / 'package.json').read_text())['version']
    start = git(root, 'log', '--grep=^chore: bump version to', '--format=%H', '-n', '1', 'HEAD')
    recovery = bool(repository and start and not release_exists(repository, current))
    package_source = start if recovery else source
    bump, version = ('none', current) if recovery else version_bump(root, start, current)
    out.mkdir(parents=True)
    required = out / 'required'
    required.mkdir()
    for name in REQUIRED_FILES:
        result = subprocess.run(['git', '-C', str(root), 'show', package_source + ':ci/' + name],
                                capture_output=True)
        require(result.returncode == 0,
                f'cannot read release test inventory {name} at {package_source}; '
                'see docs/RELEASE-PREPARATION.md for historical recovery')
        (required / name).write_bytes(result.stdout)
    # git archive excludes consumer imports, caches, untracked files and OS metadata.
    with tempfile.TemporaryDirectory() as temp:
        staging = Path(temp)
        for name in PACKAGES:
            prefix = 'Packages/' + name
            raw = subprocess.check_output(['git', '-C', str(root), 'archive', package_source, prefix])
            with tarfile.open(fileobj=io.BytesIO(raw)) as archive:
                for member in archive.getmembers():
                    relative = PurePosixPath(member.name)
                    require(not relative.is_absolute() and '..' not in relative.parts and
                            (member.isdir() or member.isfile()), 'unsafe source archive entry')
                    if member.isfile():
                        target = staging / member.name
                        target.parent.mkdir(parents=True, exist_ok=True)
                        target.write_bytes(archive.extractfile(member).read())
        core, demos = (staging / 'Packages' / name for name in PACKAGES)
        if not recovery and bump != 'none':
            cm = json.loads((core / 'package.json').read_text())
            cm['version'] = version
            (core / 'package.json').write_text(json.dumps(cm, indent=2) + '\n')
            dm = json.loads((demos / 'package.json').read_text())
            # First stable optional package. Later Demos versions are owned by its manifest.
            if dm['version'] == '0.1.0-preview.1':
                dm['version'] = '1.0.0'
                finalize_changelog(demos / 'CHANGELOG.md', dm['version'], '### Added\n- Optional demos.')
            elif dm['dependencies'][CORE] != version:
                previous = json.loads(git(root, 'show', start + ':Packages/' + DEMOS + '/package.json')) if start else dm
                require(dm['version'] != previous['version'],
                        'Core dependency changes require an explicit new Demos version; update its manifest and changelog')
            dm['dependencies'][CORE] = version
            (demos / 'package.json').write_text(json.dumps(dm, indent=2) + '\n')
            scope = f'{start}..HEAD' if start else 'HEAD'
            subjects = git(root, 'log', scope, '--no-merges', '--format=- %s', '--', 'Packages/' + CORE)
            notes = finalize_changelog(core / 'CHANGELOG.md', version, '### Changed\n' + subjects)
        else:
            text = (core / 'CHANGELOG.md').read_text()
            section = re.search(r'^## \[' + re.escape(version) + r'\][^\n]*\n(.*?)(?=^## \[|\Z)',
                                text, re.M | re.S)
            notes = section[1].strip() if section else ''
        metadata = {'schema': 1, 'source': source, 'package_source': package_source,
                    'publishable': bool(repository), 'recovery': recovery,
                    'skip': not recovery and bump == 'none', 'version': version, 'bump': bump,
                    'demos_version': json.loads((demos / 'package.json').read_text())['version'],
                    'required': hashes(required), 'packages': {}}
        for name, folder in zip(PACKAGES, (core, demos)):
            target = out / (name + '.tgz')
            with tarfile.open(target, 'w:gz') as archive:
                for relative in hashes(folder):
                    archive.add(folder / relative, arcname='package/' + relative, recursive=False)
            metadata['packages'][name] = {'sha256': digest(target), 'files': hashes(folder)}
        (out / 'release-notes.md').write_text(notes + '\n')
        metadata['notes_sha256'] = digest(out / 'release-notes.md')
        (out / 'candidate.json').write_text(json.dumps(metadata, indent=2) + '\n')
    return metadata


def read_candidate(out):
    metadata = json.loads((out / 'candidate.json').read_text())
    require(metadata['schema'] == 1 and set(metadata['packages']) == set(PACKAGES), 'invalid candidate')
    require(set(metadata.get('required', {})) == set(REQUIRED_FILES), 'invalid candidate inventory')
    required = out / 'required'
    require(required.is_dir() and {p.name for p in required.iterdir()} == set(REQUIRED_FILES),
            'invalid candidate inventory files')
    require(hashes(required) == metadata['required'], 'required inventory changed')
    require(digest(out / 'release-notes.md') == metadata['notes_sha256'], 'release notes changed')
    for name, info in metadata['packages'].items():
        require(digest(out / (name + '.tgz')) == info['sha256'], 'archive changed: ' + name)
    return metadata


def unpack(out, destination):
    metadata = read_candidate(out)
    for name, info in metadata['packages'].items():
        folder = destination / 'Packages' / name
        require(not folder.exists(), 'package destination already exists: ' + str(folder))
        folder.mkdir(parents=True)
        with tarfile.open(out / (name + '.tgz')) as archive:
            seen = set()
            for member in archive.getmembers():
                parts = PurePosixPath(member.name).parts
                require(len(parts) > 1 and parts[0] == 'package' and '..' not in parts and member.isfile(),
                        'unsafe archive entry: ' + member.name)
                relative = PurePosixPath(*parts[1:]).as_posix()
                require(relative in info['files'] and relative not in seen, 'unexpected/duplicate archive entry')
                seen.add(relative)
                target = folder / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.extractfile(member).read())
        require(hashes(folder) == info['files'], 'unpacked package mismatch: ' + name)
    return metadata


def apply(root, out):
    metadata = read_candidate(out)
    require(git(root, 'rev-parse', 'HEAD') == metadata['source'], 'checkout differs from candidate source')
    try:
        verify(root, out)
        return
    except ValueError:
        require(not git(root, 'status', '--porcelain', '--untracked-files=all', '--', 'Packages'),
                'refusing to overwrite local package changes')
    with tempfile.TemporaryDirectory() as temp:
        staging = Path(temp)
        unpack(out, staging)
        for name in PACKAGES:
            folder = root / 'Packages' / name
            shutil.rmtree(folder)
            shutil.copytree(staging / 'Packages' / name, folder)
    verify(root, out)


def verify(root, out):
    metadata = read_candidate(out)
    for name, info in metadata['packages'].items():
        require(hashes(root / 'Packages' / name) == info['files'], 'package differs from tested candidate: ' + name)
    return metadata


def import_samples(root, samples):
    package = root / 'Packages' / DEMOS
    manifest = json.loads((package / 'package.json').read_text())
    selected = manifest['samples'] if samples == ['all'] else [s for s in manifest['samples'] if s['path'].split('/')[-1] in samples]
    require(samples == ['all'] or len(selected) == len(set(samples)), 'unknown sample')
    for sample in selected:
        target = root / 'Assets/Samples' / manifest['displayName'] / manifest['version'] / sample['displayName']
        shutil.copytree(package / sample['path'], target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['prepare', 'apply', 'verify', 'unpack', 'import-samples'])
    parser.add_argument('--root', type=Path, default=Path('.'))
    parser.add_argument('--out', type=Path, default=Path('artifacts/candidate'))
    parser.add_argument('--repository', help='GitHub owner/repo; absent means local preview, never publishable')
    parser.add_argument('--samples', nargs='+', default=['all'])
    args = parser.parse_args()
    if args.command == 'import-samples':
        import_samples(args.root, args.samples)
        return
    if args.command == 'prepare':
        metadata = prepare(args.root, args.out, args.repository)
    elif args.command == 'unpack':
        metadata = unpack(args.out, args.root)
    else:
        metadata = globals()[args.command](args.root, args.out)
        if metadata is None:
            metadata = read_candidate(args.out)
    print(json.dumps({k: v for k, v in metadata.items() if k != 'packages'}, indent=2))


if __name__ == '__main__':
    main()
