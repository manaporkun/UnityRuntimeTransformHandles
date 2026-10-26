#!/usr/bin/env python3
"""Create and inspect disposable UPM consumers of a frozen release candidate."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tarfile
import time
import uuid

spec = importlib.util.spec_from_file_location('release_candidate', Path(__file__).with_name('release-candidate.py'))
candidate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(candidate)
CORE, DEMOS = candidate.CORE, candidate.DEMOS
require, digest, hashes = candidate.require, candidate.digest, candidate.hashes
PROFILES = ('2021-legacy', '6-legacy', '6-input', '6-urp')
PROFILE_POLICY = {
    '2021-legacy': ('2021.3.0f1', 0, False),
    '6-legacy': ('6000.3.16f1', 0, False),
    '6-input': ('6000.3.16f1', 1, False),
    '6-urp': ('6000.3.16f1', 1, True)
}
OUTPUT_POLICY = {
    'phase': ['editor.log', 'resolution.json', 'record.json'],
    'editor': ['editor.json'],
    'tests': ['{mode}-results.xml', '{mode}.log'],
    'imports': ['imports.json'],
    'build': ['build.json', 'player/input.json', 'player/player.log', 'player/ready.json', 'player/ready.png'],
    'checkpoint': ['{stage}-{nonce}.json', '{stage}-{nonce}.png'],
    'journey': ['summary.json']
}


def scope_for(profile, transport):
    require(profile in PROFILES and transport in ('tarball', 'git'), 'unsupported consumer matrix entry')
    return 'full' if transport == 'tarball' or profile == '6-input' else 'probe'


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + '\n')


def required_stages(profile, transport):
    base = ['core-resolve', 'core-setup']
    if scope_for(profile, transport) == 'probe':
        return [*base, 'core-build', 'pair-build']
    base += ['core-tests', 'core-build']
    for sample in ('Demo', 'LevelEditor', 'ModularBuilding', 'ProductConfigurator', 'DemoBrowser'):
        base += ['sample-' + sample + '-' + phase for phase in ('import', 'build', 'tests')]
    return base + ['gallery-import', 'gallery-tests', 'gallery-build', 'customize', 'custom-build', 'custom-gallery-build',
                   'remove-demos', 'retained-core-build', 'retained-gallery-build', 'remove-imports-build',
                   'uninstall-core', 'uninstall-verify', 'upgrade-baseline', 'upgrade-resolve', 'upgrade-setup',
                   'upgrade-import', 'upgrade-tests', 'upgrade-build']


def project_for(stage):
    if stage.startswith('sample-'):
        return stage.rsplit('-', 1)[0]
    if stage.startswith('upgrade-'): return 'upgrade'
    if stage.startswith('uninstall-'): return 'uninstall'
    if stage == 'remove-imports-build': return 'core-clean'
    return 'core'


def file_guids(folder):
    result = {}
    for path in folder.rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.M)
        require(match, 'invalid consumer GUID: ' + str(path))
        require(match[1] not in result.values(), 'duplicate consumer GUID: ' + str(path))
        result[path.relative_to(folder).as_posix()] = match[1]
    return result


def backup_remove(work, project, relative, label):
    """Explicitly remove only a named test-owned path after verifying an external backup."""
    relative = PurePosixPath(relative)
    require(not relative.is_absolute() and '..' not in relative.parts and relative.parts[0] == 'Assets', 'unsafe removal path')
    source = project / relative
    require(source.exists() and source.resolve().is_relative_to(project.resolve() / 'Assets'), 'removal escaped disposable Assets')
    backup = work / 'backups' / label / relative
    require(not backup.exists(), 'never overwrite a dependency-bearing backup')
    backup.parent.mkdir(parents=True, exist_ok=True)
    expected = hashes(source) if source.is_dir() else {'file': digest(source)}
    if source.is_dir(): shutil.copytree(source, backup)
    else: shutil.copy2(source, backup)
    require((hashes(backup) if backup.is_dir() else {'file': digest(backup)}) == expected, 'backup verification failed')
    meta = source.with_suffix(source.suffix + '.meta')
    if meta.exists():
        shutil.copy2(meta, backup.with_suffix(backup.suffix + '.meta'))
        require(digest(meta) == digest(backup.with_suffix(backup.suffix + '.meta')), 'backup meta mismatch')
        meta.unlink()
    if source.is_dir(): shutil.rmtree(source)
    else: source.unlink()
    backup_meta = backup.with_suffix(backup.suffix + '.meta')
    return {'path': relative.as_posix(), 'backup': backup.relative_to(work).as_posix(), 'files': expected,
            'meta_sha256': digest(backup_meta) if backup_meta.exists() else None}


def controlled_copy(work, source_name, name):
    source, target = work / 'projects' / source_name, work / 'projects' / name
    require(not target.exists(), 'controlled project copy already exists')
    target.mkdir(parents=True)
    for directory in ('Assets', 'Packages', 'ProjectSettings', '.journey'):
        shutil.copytree(source / directory, target / directory,
                        ignore=shutil.ignore_patterns('build', 'editor', 'distributions') if directory == '.journey' else None)
    # Tarball dependencies are project-relative, and must continue to resolve in this copy.
    shutil.copytree(source / '.journey/distributions', target / '.journey/distributions')
    require(not (target / 'Library').exists(), 'controlled copy restored a Library')
    return target


def unpack_archive(archive_path, destination, expected):
    require(not destination.exists(), 'archive destination already exists')
    destination.mkdir(parents=True)
    with tarfile.open(archive_path) as archive:
        seen = set()
        for entry in archive.getmembers():
            path = PurePosixPath(entry.name)
            require(not path.is_absolute() and len(path.parts) > 1 and path.parts[0] == 'package'
                    and '..' not in path.parts and entry.isfile(), 'unsafe consumer archive entry')
            relative = PurePosixPath(*path.parts[1:]).as_posix()
            require(relative in expected and relative not in seen, 'unexpected/duplicate consumer archive entry')
            seen.add(relative)
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.extractfile(entry).read())
    require(hashes(destination) == expected, 'consumer archive contents mismatch')


def git_repository(folder):
    for args in (('init', '-q'), ('config', 'user.name', 'Consumer CI'),
                 ('config', 'user.email', 'consumer-ci@example.invalid'),
                 ('config', 'commit.gpgsign', 'false'), ('add', '.'),
                 ('commit', '-qm', 'ci: freeze consumer distribution')):
        subprocess.run(['git', '-C', str(folder), *args], check=True, capture_output=True)
    return candidate.git(folder, 'rev-parse', 'HEAD')


def prepare(out, work, profile, transport, mount_root=None, build_target='StandaloneLinux64'):
    metadata = candidate.read_candidate(out)
    scope = scope_for(profile, transport)  # Policy comes from current code, never a report.
    contract = json.loads((out / 'consumer/contract.json').read_text())
    require(contract.get('schema') == 1 and set(contract['profiles']) == set(PROFILES), 'unsupported consumer profiles')
    require(contract.get('outputs') == OUTPUT_POLICY, 'unsupported consumer output contract')
    for key, expected in PROFILE_POLICY.items():
        require(tuple(contract['profiles'][key][field] for field in ('unity', 'input', 'urp')) == expected,
                'frozen profile differs from current workflow policy: ' + key)
    require(not work.exists(), 'use a fresh consumer work directory; never restore Library or package locks')
    work.mkdir(parents=True)
    frozen = work / 'candidate'
    shutil.copytree(out, frozen)
    package_root = work / 'distribution'
    candidate.unpack(frozen, package_root)
    unpack_archive(frozen / 'baseline-core.tgz', package_root / 'baseline', metadata['baseline']['files'])
    # The Core-only root/tag and the same-SHA subpath pair match the documented routes.
    core_repo = package_root / 'core'
    shutil.copytree(package_root / 'Packages' / CORE, core_repo)
    core_commit = git_repository(core_repo)
    subprocess.run(['git', '-C', str(core_repo), 'tag', 'v' + metadata['version']], check=True)
    pair_repo = package_root / 'pair'
    shutil.copytree(package_root / 'Packages', pair_repo / 'Packages')
    pair_commit = git_repository(pair_repo)
    root_on_editor = Path(mount_root) if mount_root else work.resolve()
    require(root_on_editor.is_absolute(), 'Editor mount root must be absolute')
    config = {
        'schema': 1, 'runId': uuid.uuid4().hex, 'source': metadata['source'],
        'packageSource': metadata['package_source'], 'profile': profile, 'transport': transport,
        'scope': scope, **{k: v for k, v in contract['profiles'][profile].items() if k != 'dependencies'},
        'buildTarget': build_target,
        'editorWorkRoot': root_on_editor.as_posix(),
        'coreUrl': 'git+' + (root_on_editor / 'distribution/core').as_uri() + '#v' + metadata['version'],
        'pairCoreUrl': 'git+' + (root_on_editor / 'distribution/pair').as_uri() + '?path=/Packages/' + CORE + '#' + pair_commit,
        'pairDemosUrl': 'git+' + (root_on_editor / 'distribution/pair').as_uri() + '?path=/Packages/' + DEMOS + '#' + pair_commit,
        'coreCommit': core_commit, 'pairCommit': pair_commit, 'editorLaunches': [], 'builds': []
    }
    write_json(work / 'run.json', config)
    create_project(work, 'core')
    return config


def install_dependencies(work, project, demos=False, baseline=False, pair=False):
    config = json.loads((work / 'run.json').read_text())
    contract = json.loads((work / 'candidate/consumer/contract.json').read_text())
    manifest_path = project / 'Packages/manifest.json'
    dependencies = (json.loads(manifest_path.read_text())['dependencies'] if manifest_path.exists()
                    else dict(contract['profiles'][config['profile']]['dependencies']))
    # Only explicitly selected host modules. In particular, no uGUI in Core-only consumers.
    for module in ('jsonserialize', 'imageconversion', 'screencapture', 'imgui'):
        dependencies['com.unity.modules.' + module] = '1.0.0'
    archives = project / '.journey/distributions'
    archives.mkdir(parents=True, exist_ok=True)
    if baseline:
        shutil.copy2(work / 'candidate/baseline-core.tgz', archives / 'baseline-core.tgz')
        dependencies[CORE] = 'file:../.journey/distributions/baseline-core.tgz'
    else:
        for name in (CORE, DEMOS) if demos else (CORE,):
            shutil.copy2(work / 'candidate' / (name + '.tgz'), archives)
            if config['transport'] == 'tarball':
                dependencies[name] = 'file:../.journey/distributions/' + name + '.tgz'
            else:
                dependencies[name] = config['pairDemosUrl' if name == DEMOS else ('pairCoreUrl' if pair or demos else 'coreUrl')]
    if demos:
        dependencies['com.unity.ugui'] = '1.0.0' if config['unity'].startswith('2021.') else '2.0.0'
        dependencies['com.unity.modules.uielements'] = '1.0.0'
    write_json(manifest_path, {'dependencies': dependencies})


def create_project(work, name, demos=False, baseline=False):
    require(re.fullmatch(r'[a-zA-Z0-9-]+', name), 'invalid consumer project name')
    project = work / 'projects' / name
    require(not project.exists(), 'consumer project already exists')
    (project / 'Assets/ConsumerJourney').mkdir(parents=True)
    shutil.copytree(work / 'candidate/consumer/Bootstrap', project / 'Assets/ConsumerJourney/Bootstrap')
    shutil.copy2(work / 'candidate/consumer/Bootstrap.meta', project / 'Assets/ConsumerJourney/Bootstrap.meta')
    settings = project / 'ProjectSettings'
    settings.mkdir()
    config = json.loads((work / 'run.json').read_text())
    (settings / 'ProjectVersion.txt').write_text('m_EditorVersion: ' + config['unity'] + '\n')
    (settings / 'ProjectSettings.asset').write_text(
        '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\nPlayerSettings:\n'
        '  m_ObjectHideFlags: 0\n  serializedVersion: 26\n  companyName: Consumer CI\n'
        '  productName: Consumer Journey\n  activeInputHandler: ' + str(config['input']) + '\n')
    # Unrelated consumer-owned content must survive customization, upgrades and removal.
    (project / 'Assets/UserNotes.txt').write_text('User-owned content. Preserve byte-for-byte.\n')
    install_dependencies(work, project, demos, baseline)
    set_stage(work, name, name + '-resolve')
    return project


def set_stage(work, name, stage, sample=''):
    require(re.fullmatch(r'[a-zA-Z0-9-]+', stage), 'unsafe stage name')
    project = work / 'projects' / name
    config = json.loads((work / 'run.json').read_text())
    config.update(stage=stage, sample=sample)
    config['assetRoot'] = 'Assets/UserUpgrade' if name == 'upgrade' else 'Assets/UserConsumer'
    write_json(project / '.journey/config.json', config)
    require(not (project / '.journey/editor' / (stage + '.json')).exists(), 'stage already recorded')
    return project


def before_stage(work, stage):
    state = json.loads((work / 'run.json').read_text())
    stages = required_stages(state['profile'], state['transport'])
    completed = state.get('completed', [])
    require(len(completed) < len(stages) and stage == stages[len(completed)], 'consumer stages must execute in reviewed order')
    require('pending' not in state, 'previous consumer stage is not complete')
    name = project_for(stage)
    project = work / 'projects' / name
    sample = name[len('sample-'):] if name.startswith('sample-') else ''
    if stage == 'core-setup': stage_fixture(work, name)
    elif stage.startswith('sample-') and stage.endswith('-import'):
        create_project(work, name, demos=True)
        stage_fixture(work, name)
    elif stage.startswith('sample-') and stage.endswith('-build'):
        stage_sample_fixture(work, name)
    elif stage in ('gallery-import', 'pair-build'):
        install_dependencies(work, project, demos=True)
    elif stage == 'gallery-build': stage_sample_fixture(work, name)
    elif stage == 'remove-imports-build':
        project = controlled_copy(work, 'core', name)
        removals = [backup_remove(work, project, 'Assets/Samples', 'removed-imports')]
        write_json(work / 'evidence/removal-imports.json', {'runId': state['runId'], 'removed': removals})
    elif stage == 'uninstall-core':
        project = controlled_copy(work, 'core-clean', name)
        paths = ['Assets/UserConsumer'] + ['Assets/ConsumerJourney/' + folder for folder in ('Runtime', 'Editor', 'Tests', 'URP', 'Samples')
                                          if (project / 'Assets/ConsumerJourney' / folder).exists()]
        removals = [backup_remove(work, project, path, 'core-dependencies') for path in paths]
        write_json(work / 'evidence/removal-core.json', {'runId': state['runId'], 'removed': removals})
    elif stage == 'upgrade-baseline': create_project(work, name, baseline=True)
    elif stage == 'upgrade-resolve':
        baseline = json.loads((project / '.journey/upgrade-baseline.json').read_text())
        require(baseline['runId'] == state['runId'] and baseline['importPath'].startswith('Assets/Samples/'), 'invalid old import cleanup target')
        removed = backup_remove(work, project, baseline['importPath'], 'upgrade-old-import')
        write_json(work / 'evidence/upgrade-backup.json', {'runId': state['runId'], 'removed': [removed]})
        install_dependencies(work, project, demos=True)
    elif stage == 'upgrade-setup': stage_fixture(work, name)
    if stage == 'upgrade-import': sample = 'all'
    if stage == 'gallery-import': sample = 'all'
    if stage == 'upgrade-tests': sample = ''
    set_stage(work, name, stage, sample)
    method = 'ConsumerJourney.ConsumerBootstrap.Run'
    test_mode, test_filter, inventory = '', '', ''
    if stage == 'core-setup': method = 'ConsumerJourney.ConsumerPipeline.Setup' if state['urp'] else 'ConsumerJourney.ConsumerProject.Setup'
    elif stage == 'upgrade-baseline': method = 'ConsumerJourney.ConsumerBootstrap.PrepareUpgradeBaseline'
    elif stage == 'upgrade-setup': method = 'ConsumerJourney.ConsumerPipeline.Upgrade' if state['urp'] else 'ConsumerJourney.ConsumerUpgrade.Apply'
    elif stage.endswith('-import'):
        method = 'ConsumerJourney.ConsumerPipeline.ImportSamples' if state['urp'] else 'ConsumerJourney.ConsumerSamples.Import'
    elif stage == 'customize': method = 'ConsumerJourney.ConsumerCustomization.Apply'
    elif stage == 'remove-demos': method = 'ConsumerJourney.ConsumerRemoval.RemoveDemos'
    elif stage == 'uninstall-core': method = 'ConsumerJourney.ConsumerBootstrap.RemoveCore'
    elif stage in ('core-tests', 'upgrade-tests'):
        test_mode, test_filter, inventory = 'editmode', 'ConsumerJourney.Tests.', 'consumer-editmode.txt'
    elif stage.endswith('-tests'):
        test_mode = 'playmode'
        contract = json.loads((work / 'candidate/consumer/contract.json').read_text())
        test_filter = contract['samples'][sample]['filter'] if sample else 'TransformHandles.Samples.'
        inventory = f'{sample}-input{state["input"]}.txt' if sample else f'required-samples-input{state["input"]}.txt'
    elif stage.endswith('-build'):
        if stage in ('gallery-build', 'custom-gallery-build', 'retained-gallery-build') or stage.startswith('sample-'):
            method = 'ConsumerJourney.ConsumerSampleBuild.Build'
        elif stage == 'upgrade-build': method = 'ConsumerJourney.ConsumerUpgrade.Build'
        elif stage in ('custom-build', 'retained-core-build', 'remove-imports-build'):
            method = 'ConsumerJourney.ConsumerCustomization.Build'
        else: method = 'ConsumerJourney.ConsumerProject.Build'
    require(not test_filter or re.fullmatch(r'[A-Za-z0-9_.;]+', test_filter), 'unsafe test filter')
    state = json.loads((work / 'run.json').read_text())
    state['pending'] = {'stage': stage, 'project': name, 'method': method, 'testMode': test_mode,
                        'testFilter': test_filter, 'inventory': inventory, 'started': time.time()}
    if test_mode:
        reports = list((project / '.journey/editor').glob('*.json'))
        require(reports, 'tests require previously observed real UPM resolution')
        state['pending']['packagesBeforeTests'] = json.loads(max(reports, key=lambda path: path.stat().st_mtime).read_text())
        state['pending']['manifestBeforeTests'] = json.loads((project / 'Packages/manifest.json').read_text())
        state['pending']['lockBeforeTests'] = json.loads((project / 'Packages/packages-lock.json').read_text())
    write_json(work / 'run.json', state)
    return {'project': project.as_posix(), 'method': method, 'testMode': test_mode, 'testFilter': test_filter,
            'artifacts': (work / 'evidence' / stage).as_posix(), 'stage': stage}


def stage_fixture(work, name):
    project = work / 'projects' / name
    config = json.loads((work / 'run.json').read_text())
    folders = ('Runtime', 'Editor', 'Tests', 'URP') if config['urp'] else ('Runtime', 'Editor', 'Tests')
    for folder in folders:
        source = work / 'candidate/consumer' / folder
        if source.exists():
            shutil.copytree(source, project / 'Assets/ConsumerJourney' / folder)
            shutil.copy2(source.with_suffix('.meta'), project / 'Assets/ConsumerJourney' / (folder + '.meta'))


def stage_sample_fixture(work, name):
    project = work / 'projects' / name
    dependencies = json.loads((project / 'Packages/manifest.json').read_text())['dependencies']
    require('com.unity.ugui' in dependencies and 'com.unity.modules.uielements' in dependencies,
            'explicit host UI packages required before staging sample observers')
    source = work / 'candidate/consumer/Samples'
    shutil.copytree(source, project / 'Assets/ConsumerJourney/Samples')
    shutil.copy2(source.with_suffix('.meta'), project / 'Assets/ConsumerJourney/Samples.meta')


def verify_resolved(work, project, observation, baseline=False):
    config = json.loads((work / 'run.json').read_text())
    metadata = candidate.read_candidate(work / 'candidate')
    for key in ('runId', 'source', 'packageSource', 'unity', 'input'):
        require(observation.get(key) == config[key], 'Editor observation mismatch: ' + key)
    dependencies = json.loads((project / 'Packages/manifest.json').read_text())['dependencies']
    expected_packages = [p for p in dependencies if p in (CORE, DEMOS)]
    require(not baseline or expected_packages == [CORE], 'previous baseline must contain Core only')
    observed = {p['name']: p for p in observation['packages']}
    require(len(observed) == len(observation['packages']), 'duplicate package observations')
    require({p for p in observed if p in (CORE, DEMOS)} == set(expected_packages), 'Core/Demos resolution mismatch')
    contract = json.loads((work / 'candidate/consumer/contract.json').read_text())
    for host, version in contract['profiles'][config['profile']]['dependencies'].items():
        require(host in observed and observed[host]['version'] == version, 'host dependency version differs from frozen profile: ' + host)
    installed = {}
    for name in expected_packages:
        info = observed[name]
        require(info['source'] == ('LocalTarball' if baseline or config['transport'] == 'tarball' else 'Git'), 'incorrect UPM package source')
        if not baseline and config['transport'] == 'git':
            require(info['packageId'] == name + '@' + dependencies[name], 'installed Git revision/route differs from manifest')
            lock = json.loads((project / 'Packages/packages-lock.json').read_text())['dependencies'][name]
            expected_commit = config['coreCommit'] if dependencies[name] == config['coreUrl'] else config['pairCommit']
            require(lock['source'] == 'git' and lock['version'] == dependencies[name] and lock['hash'] == expected_commit,
                    'UPM Git lock revision differs from frozen distribution commit')
        # GameCI reports container paths; retain only the project-relative PackageCache suffix.
        cache_prefix = (PurePosixPath(config['editorWorkRoot']) / project.relative_to(work).as_posix() / 'Library/PackageCache').as_posix() + '/'
        require(info['resolvedPath'].startswith(cache_prefix), 'installed package is outside this project Library/PackageCache')
        suffix = info['resolvedPath'][len(cache_prefix):]
        require(len(PurePosixPath(suffix).parts) == 1 and suffix.startswith(name + '@'), 'unexpected PackageCache location')
        resolved = project / 'Library/PackageCache' / suffix
        require(resolved.resolve().is_relative_to(project.resolve() / 'Library/PackageCache') and not resolved.is_symlink(),
                'installed PackageCache escapes project through a symlink')
        expected = metadata['baseline']['files'] if baseline else metadata['packages'][name]['files']
        manifest = json.loads((resolved / 'package.json').read_text())
        version = metadata['baseline']['version'] if baseline else (metadata['version'] if name == CORE else metadata['demos_version'])
        require(manifest['version'] == version and info['version'] == version, 'incorrect installed package version')
        actual = hashes(resolved)
        if actual.get('package.json') != expected['package.json'] and config['unity'].startswith('6000.'):
            # Unity 6 may append exactly one generated property. No other file is normalized.
            original = work / 'distribution/baseline/package.json' if baseline else work / 'distribution/Packages' / name / 'package.json'
            expected_manifest = json.loads(original.read_text())
            require('_fingerprint' in manifest and isinstance(manifest['_fingerprint'], str), 'unexpected package.json modification')
            del manifest['_fingerprint']
            require(manifest == expected_manifest, 'installed package manifest changed')
            actual['package.json'] = expected['package.json']
        require(actual == expected, 'installed package bytes differ: ' + name)
        installed[name] = {'version': info['version'], 'source': info['source'], 'resolvedPath': info['resolvedPath'],
                           'packageId': info['packageId'], 'files': actual}
    if observation['stage'] == 'core-resolve' and config['profile'] in ('2021-legacy', '6-legacy'):
        require(not set(observed).intersection((DEMOS, 'com.unity.ugui', 'com.unity.inputsystem', 'com.unity.render-pipelines.universal')), 'Core-only profile acquired optional packages')
    snapshot = {'observation': observation, 'manifest': json.loads((project / 'Packages/manifest.json').read_text()),
                'lock': json.loads((project / 'Packages/packages-lock.json').read_text()),
                'files': {name: value['files'] for name, value in installed.items()}}
    verify_resolution_snapshot(config, metadata, contract, project.name, observation['stage'], snapshot)
    return installed


def verify_resolution_snapshot(state, metadata, contract, project_name, stage, snapshot, expected_observed_stage=None):
    observation, manifest, lock = (snapshot[key] for key in ('observation', 'manifest', 'lock'))
    for key in ('runId', 'source', 'packageSource', 'unity', 'input'):
        require(observation.get(key) == state[key], 'raw Editor identity differs: ' + key)
    require(observation.get('schema') == 1 and observation.get('stage') == (expected_observed_stage or stage), 'raw Editor phase differs')
    packages = observation['packages']
    by_name = {package['name']: package for package in packages}
    require(len(by_name) == len(packages), 'duplicate raw package observations')
    dependencies = manifest['dependencies']
    expected_names = {name for name in dependencies if name in (CORE, DEMOS)}
    require({name for name in by_name if name in (CORE, DEMOS)} == expected_names == set(snapshot['files']), 'raw package inventory differs from resolution')
    for name, version in contract['profiles'][state['profile']]['dependencies'].items():
        require(name in by_name and by_name[name]['version'] == version and dependencies.get(name) == version, 'raw host dependency version differs')
    baseline = stage == 'upgrade-baseline'
    result = {}
    for name in expected_names:
        package = by_name[name]
        source = 'LocalTarball' if baseline or state['transport'] == 'tarball' else 'Git'
        if source == 'Git':
            initial = stage in ('core-resolve', 'core-setup', 'core-tests', 'core-build')
            url = state['coreUrl' if initial else 'pairCoreUrl'] if name == CORE else state['pairDemosUrl']
        else:
            url = 'file:../.journey/distributions/' + ('baseline-core' if baseline else name) + '.tgz'
        require(dependencies[name] == url, 'raw UPM URL differs from the stage-selected distribution route')
        version = metadata['baseline']['version'] if baseline else metadata['version'] if name == CORE else metadata['demos_version']
        files = metadata['baseline']['files'] if baseline else metadata['packages'][name]['files']
        require(package['source'] == source and package['version'] == version and snapshot['files'][name] == files, 'raw resolved package source/version/bytes differ')
        prefix = (PurePosixPath(state['editorWorkRoot']) / 'projects' / project_name / 'Library/PackageCache').as_posix() + '/'
        require(package['resolvedPath'].startswith(prefix), 'raw cache path is outside the selected project')
        suffix = package['resolvedPath'][len(prefix):]
        require(len(PurePosixPath(suffix).parts) == 1 and suffix.startswith(name + '@'), 'invalid raw PackageCache path')
        locked = lock['dependencies'][name]
        if source == 'Git':
            expected_commit = state['coreCommit'] if initial else state['pairCommit']
            require(package['packageId'] == name + '@' + dependencies[name] and locked['version'] == dependencies[name] and
                    locked['source'] == 'git' and locked['hash'] == expected_commit, 'raw Git URL/revision differs')
        else:
            require(locked['source'] == 'local-tarball' and locked['depth'] == 0 and locked['version'] == url, 'raw tarball resolution differs')
            archive = (PurePosixPath(state['editorWorkRoot']) / 'projects' / project_name / '.journey/distributions' / url.rsplit('/', 1)[-1]).as_posix()
            require(package['packageId'] == name + '@file:' + archive, 'raw tarball package ID differs from the stage-selected archive')
        result[name] = {key: package[key] for key in ('version', 'source', 'resolvedPath', 'packageId')} | {'files': files}
    if stage == 'core-resolve' and state['profile'] in ('2021-legacy', '6-legacy'):
        require(not set(by_name).intersection((DEMOS, 'com.unity.ugui', 'com.unity.inputsystem', 'com.unity.render-pipelines.universal')), 'raw Core-only resolution acquired optional packages')
    if stage in ('remove-imports-build', 'uninstall-verify'):
        require(not any(name.startswith('TransformHandles.Samples.') for name in observation['assemblies']), 'sample assembly remains after import removal')
    if stage == 'uninstall-verify':
        require(not expected_names and not set(lock['dependencies']).intersection((CORE, DEMOS)), 'raw Core/Demos lock/manifest reference remains')
        require(not any(name.startswith(('com.orkunmanap.runtime-transform-handles', 'TransformHandles.')) for name in observation['assemblies']), 'raw Core/sample assembly remains after uninstall')
    return result


def verify_import_snapshot(work, project_name, record, files, customized=False):
    state = json.loads((work / 'run.json').read_text())
    require(record['runId'] == state['runId'], 'stale public import record')
    metadata = candidate.read_candidate(work / 'candidate')
    contract = json.loads((work / 'candidate/consumer/contract.json').read_text())
    expected_names = {project_name[len('sample-'):]} if project_name.startswith('sample-') else set(contract['samples'])
    names = [sample['name'] for sample in record['samples']]
    require(len(names) == len(set(names)) and set(names) == expected_names, 'public import inventory missing, extra or duplicated')
    paths = [sample['path'] for sample in record['samples']]
    require(len(paths) == len(set(paths)) and set(paths) == set(files), 'public import paths missing or duplicated')
    results = {}
    for sample in record['samples']:
        path = PurePosixPath(sample['path'])
        require(not path.is_absolute() and '..' not in path.parts and path.parts[:2] == ('Assets', 'Samples'), 'unsafe public import location')
        prefix = 'Samples~/' + sample['name'] + '/'
        expected = {name[len(prefix):]: value for name, value in metadata['packages'][DEMOS]['files'].items() if name.startswith(prefix)}
        require(expected, 'sample is absent from frozen candidate')
        scene = PurePosixPath(sample['scene'])
        require(scene.parent == path and scene.name == contract['samples'][sample['name']]['scene'] + '.unity', 'public sample scene identity differs')
        source_folder = work / 'distribution/Packages' / DEMOS / 'Samples~' / sample['name']
        assemblies = list(source_folder.glob('*.asmdef'))
        require(len(assemblies) == 1 and sample['assembly'] == json.loads(assemblies[0].read_text())['name'], 'public sample assembly identity differs')
        actual = files[sample['path']].copy()
        if customized and sample['name'] == 'Demo':
            saved = state.get('customImports', {}).get(sample['path'], {}).get(scene.name)
            require(saved is not None and actual.get(scene.name) == saved, 'customized sample was overwritten after save/reopen')
            actual[scene.name] = expected[scene.name]
        require(actual == expected, 'imported sample bytes changed: ' + sample['name'])
        results[sample['path']] = files[sample['path']]
    return results


def verify_imports(work, project, customized=False):
    record = json.loads((project / '.journey/imports.json').read_text())
    files = {}
    for sample in record['samples']:
        path = PurePosixPath(sample['path'])
        require(not path.is_absolute() and '..' not in path.parts and path.parts[:2] == ('Assets', 'Samples'), 'unsafe public import location')
        folder = project / path
        require(folder.resolve().is_relative_to(project.resolve() / 'Assets/Samples'), 'public import escaped Assets through symlink')
        files[sample['path']] = hashes(folder)
    return verify_import_snapshot(work, project.name, record, files, customized)


def load_verifier():
    spec = importlib.util.spec_from_file_location('consumer_results', Path(__file__).with_name('verify-consumer-results.py'))
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    return verifier


def verify_editor_log(path, state, stage, tests=False):
    text = path.read_text(errors='replace')
    observed = re.findall(r'^CONSUMER_ENGINE ([^ ]+) ' + re.escape(state['runId']) + ' ' + re.escape(stage) + r'$', text, re.M)
    require(observed and set(observed) == {state['unity']}, 'missing/wrong actual Editor initialization readback')
    require('CONSUMER_INVOCATION ' + state['runId'] + ' ' + stage in text, 'Editor log belongs to another invocation')
    if not tests:
        require(not re.search(r'^(?:Error|Exception|Assert):', text, re.M), 'Editor logged an error/exception/assertion')


def after_stage(work, stage):
    state = json.loads((work / 'run.json').read_text())
    pending = state['pending']
    require(pending['stage'] == stage, 'wrong stage completion')
    project = work / 'projects' / pending['project']
    out = work / 'evidence' / stage
    out.mkdir(parents=True, exist_ok=True)
    config = json.loads((project / '.journey/config.json').read_text())
    require(config['stage'] == stage and config['runId'] == state['runId'], 'project config changed during invocation')
    finished = time.time()
    record = {'schema': 1, 'runId': state['runId'], 'stage': stage, 'project': pending['project'],
              'source': state['source'], 'packageSource': state['packageSource'], 'started': pending['started'],
              'editorFinished': finished, 'builds': [], 'players': [], 'tests': None}
    log = project / '.journey/editor' / (stage + '.log')
    require(log.exists(), 'Editor never initialized the frozen consumer bootstrap')
    shutil.copy2(log, out / 'editor.log')
    verify_editor_log(log, state, stage, tests=bool(pending['testMode']))
    record['editorPids'] = sorted(set(re.findall(r'^CONSUMER_PID ([0-9]+)$', log.read_text(), re.M)))
    require(record['editorPids'], 'missing actual Editor process identity')
    verifier = load_verifier()
    if pending['testMode']:
        require(json.loads((project / 'Packages/manifest.json').read_text()) == pending['manifestBeforeTests'] and
                json.loads((project / 'Packages/packages-lock.json').read_text()) == pending['lockBeforeTests'], 'test runner changed real UPM resolution')
        record['installed'] = verify_resolved(work, project, pending['packagesBeforeTests'])
        observation = pending['packagesBeforeTests']
        required_dir = work / 'candidate/consumer/required' if pending['inventory'] == 'consumer-editmode.txt' or stage.startswith('sample-') else work / 'candidate/required'
        record['tests'] = verifier.verify_tests(out / (pending['testMode'] + '-results.xml'), out / (pending['testMode'] + '.log'),
                                                required_dir / pending['inventory'], state, stage, pending['started'], finished)
    else:
        source = project / '.journey/editor' / (stage + '.json')
        observation = json.loads(source.read_text())
        require(observation['stage'] == stage, 'wrong observed Editor stage')
        require(not stage.endswith('-build') or observation.get('buildPath'), 'mandatory native build was omitted')
        record['installed'] = verify_resolved(work, project, observation, baseline=stage == 'upgrade-baseline')
        shutil.copy2(source, out / 'editor.json')
        if observation.get('buildPath'):
            location = PurePosixPath(observation['buildPath'])
            require(not location.is_absolute() and '..' not in location.parts and location.parts[:2] == ('.journey', 'build'), 'unsafe built player path')
            player = project / location
            require(player.is_file() and os.access(player, os.X_OK), 'missing/nonexecutable Linux native player')
            files = hashes(player.parent)
            build_sha = hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
            require(re.fullmatch(r'[0-9a-f]{32}', observation.get('buildId', '')), 'missing fresh native build identity')
            expected_player = {**state, 'lifecycleStage': stage, 'buildId': observation['buildId'], 'buildSha256': build_sha}
            record['builds'] = [{'path': location.as_posix(), 'scenes': observation['scenes'], 'files': files,
                                  'buildId': observation['buildId'], 'buildSha256': build_sha}]
            write_json(out / 'build.json', {key: expected_player[key] for key in ('runId', 'lifecycleStage', 'buildId', 'buildSha256') } | {'files': files})
            sample = 'gallery' if stage in ('gallery-build', 'custom-gallery-build', 'retained-gallery-build') else (
                pending['project'][len('sample-'):] if stage.startswith('sample-') else None)
            custom = stage in ('custom-build', 'custom-gallery-build', 'retained-core-build', 'retained-gallery-build', 'remove-imports-build', 'upgrade-build')
            command = ['xvfb-run', '-a', '-s', '-screen 0 1280x1024x24', 'python3', str(Path(__file__).with_name('run-consumer-player.py')),
                       '--player', str(player), '--out', str(out / 'player'), '--run-id', state['runId'], '--build-manifest', str(out / 'build.json')]
            if custom: command.append('--customized')
            contract_path = work / 'candidate/consumer/contract.json'
            if sample: command += ['--sample', sample, '--contract', str(contract_path)]
            subprocess.run(command, check=True, timeout=600)
            if sample:
                verifier.verify_sample_player(out / 'player', expected_player, json.loads(contract_path.read_text()), sample, custom)
            else:
                verifier.verify_player(out / 'player', expected_player, custom)
            record['players'].append({'path': 'player', 'sample': sample, 'customized': custom, 'negative': False})
            if stage == 'core-build':
                negative = command.copy()
                negative[negative.index('--out') + 1] = str(out / 'negative')
                negative.append('--withhold-input')
                subprocess.run(negative, check=True, timeout=600)
                verifier.verify_negative_control(out / 'negative', expected_player)
                record['players'].append({'path': 'negative', 'sample': None, 'customized': False, 'negative': True})
    write_json(out / 'resolution.json', {'observation': observation,
               'manifest': json.loads((project / 'Packages/manifest.json').read_text()),
               'lock': json.loads((project / 'Packages/packages-lock.json').read_text()),
               'files': {name: info['files'] for name, info in record['installed'].items()}})
    # The same user-owned note survives every installation, import, upgrade and controlled removal.
    require((project / 'Assets/UserNotes.txt').read_text() == 'User-owned content. Preserve byte-for-byte.\n', 'unrelated user content changed')
    record['unrelated'] = {'UserNotes.txt': digest(project / 'Assets/UserNotes.txt')}
    meta = project / 'Assets/UserNotes.txt.meta'
    require(meta.exists(), 'unrelated user asset lost its GUID')
    record['unrelated']['UserNotes.txt.meta'] = digest(meta)
    saved = state.setdefault('unrelated', {}).get(pending['project'])
    if saved: require(saved == record['unrelated'], 'unrelated user asset bytes/GUID changed')
    else: state['unrelated'][pending['project']] = record['unrelated']
    user_folder = project / ('Assets/UserUpgrade' if pending['project'] == 'upgrade' else 'Assets/UserConsumer')
    if user_folder.exists():
        record['userGuids'] = file_guids(user_folder)
        prior = state.setdefault('userGuids', {}).get(pending['project'])
        if prior: require(prior == record['userGuids'], 'consumer settings/prefab/scene GUIDs changed')
        else: state['userGuids'][pending['project']] = record['userGuids']
    imports_file = project / '.journey/imports.json'
    if imports_file.exists() and pending['project'] not in ('core-clean', 'uninstall'):
        if stage == 'customize':
            imports = json.loads(imports_file.read_text())
            state['customImports'] = {sample['path']: hashes(project / sample['path']) for sample in imports['samples']}
            write_json(work / 'run.json', state)
        custom_imports = stage in ('customize', 'custom-build', 'custom-gallery-build', 'remove-demos', 'retained-core-build', 'retained-gallery-build')
        record['imports'] = verify_imports(work, project, customized=custom_imports)
        shutil.copy2(imports_file, out / 'imports.json')
    if stage == 'upgrade-baseline':
        shutil.copy2(project / '.journey/upgrade-baseline.json', out / 'upgrade-baseline.json')
    if stage in ('remove-imports-build', 'uninstall-verify'):
        require(not (project / 'Assets/Samples').exists(), 'test-owned imports remain in controlled removal copy')
    if stage == 'uninstall-verify':
        for path in (project / 'Packages/manifest.json', project / 'Packages/packages-lock.json'):
            text = path.read_text()
            require(all('"' + name + '"' not in text for name in (CORE, DEMOS)), 'Core/Demos manifest/lock reference remains')
    record['finished'] = time.time()
    record['files'] = {p.relative_to(out).as_posix(): digest(p) for p in out.rglob('*') if p.is_file() and p.name != 'record.json'}
    write_json(out / 'record.json', record)
    state.pop('pending')
    state.setdefault('completed', []).append(stage)
    state['editorLaunches'].append({'stage': stage, 'started': pending['started'], 'seconds': finished - pending['started'], 'exitCode': 0})
    state['builds'] += [{'stage': stage, **build} for build in record['builds']]
    write_json(work / 'run.json', state)
    if stage.startswith('sample-') and stage.endswith('-tests'):
        for directory in ('Library', 'Temp', '.journey/build'):
            shutil.rmtree(project / directory, ignore_errors=True)
    elif record['builds']:
        shutil.rmtree(project / '.journey/build')
    return record


def run_editor(work, name, unity, method='ConsumerJourney.ConsumerBootstrap.Run', timeout=900):
    project = work / 'projects' / name
    config = json.loads((project / '.journey/config.json').read_text())
    log = work / 'evidence' / name / (config['stage'] + '.log')
    log.parent.mkdir(parents=True, exist_ok=True)
    command = [str(unity), '-batchmode', '-nographics', '-projectPath', str(project.resolve()),
               '-executeMethod', method, '-quit', '-logFile', str(log.resolve())]
    if method.endswith('.Build'):
        command += ['-buildTarget', config['buildTarget']]
    started = time.time()
    result = None
    build = None
    try:
        result = subprocess.run(command, timeout=timeout)
        require(result.returncode == 0, 'Editor failed; inspect ' + str(log))
        require(re.search(r'Initialize engine version: ' + re.escape(config['unity']) + r'(?:\s|$)', log.read_text()), 'Editor startup version differs from profile')
        observation = json.loads((project / '.journey/editor' / (config['stage'] + '.json')).read_text())
        require(observation['stage'] == config['stage'], 'wrong Editor stage')
        verify_resolved(work, project, observation, baseline=config['stage'] == 'upgrade-baseline')
        if observation.get('buildPath'):
            path = PurePosixPath(observation['buildPath'])
            require(not path.is_absolute() and '..' not in path.parts and path.parts[:2] == ('.journey', 'build'), 'unsafe reported build path')
            location = project / path
            require(location.exists(), 'Editor reported a missing native build')
            build = {'project': name, 'stage': config['stage'], 'path': path.as_posix(),
                     'scenes': observation['scenes'], 'files': hashes(location.parent)}
    finally:
        state = json.loads((work / 'run.json').read_text())
        state['editorLaunches'].append({'project': name, 'stage': config['stage'], 'started': started,
                                        'seconds': time.time() - started,
                                        'exitCode': result.returncode if result else None})
        if build:
            state['builds'].append(build)
        write_json(work / 'run.json', state)
    return observation


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('prepare', 'create', 'stage', 'before', 'after', 'fixture', 'sample-fixture', 'run-editor'))
    parser.add_argument('--candidate', type=Path, default=Path('artifacts/candidate'))
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--profile', choices=PROFILES)
    parser.add_argument('--transport', choices=('tarball', 'git'))
    parser.add_argument('--mount-root', type=Path, help='Absolute work directory as seen by the Editor container')
    parser.add_argument('--build-target', choices=('StandaloneLinux64', 'StandaloneOSX'), default='StandaloneLinux64')
    parser.add_argument('--project', default='core')
    parser.add_argument('--demos', action='store_true')
    parser.add_argument('--baseline', action='store_true')
    parser.add_argument('--stage')
    parser.add_argument('--sample', default='')
    parser.add_argument('--unity', type=Path)
    parser.add_argument('--method', default='ConsumerJourney.ConsumerBootstrap.Run')
    parser.add_argument('--timeout', type=int, default=900)
    parser.add_argument('--github-output', type=Path)
    args = parser.parse_args()
    if args.command == 'prepare':
        require(args.profile and args.transport, '--profile and --transport required')
        prepare(args.candidate, args.work, args.profile, args.transport, args.mount_root, args.build_target)
    elif args.command == 'create':
        create_project(args.work, args.project, args.demos, args.baseline)
    elif args.command == 'fixture':
        stage_fixture(args.work, args.project)
    elif args.command == 'sample-fixture':
        stage_sample_fixture(args.work, args.project)
    elif args.command == 'stage':
        require(args.stage, '--stage required')
        set_stage(args.work, args.project, args.stage, args.sample)
    elif args.command == 'before':
        require(args.stage, '--stage required')
        values = before_stage(args.work, args.stage)
        if args.github_output:
            with args.github_output.open('a') as output:
                for key, value in values.items():
                    require('\n' not in value and '\r' not in value, 'unsafe action output')
                    output.write(key + '=' + value + '\n')
        print(json.dumps(values, indent=2))
    elif args.command == 'after':
        require(args.stage, '--stage required')
        after_stage(args.work, args.stage)
    else:
        require(args.unity, '--unity required')
        run_editor(args.work, args.project, args.unity, args.method, args.timeout)


if __name__ == '__main__':
    main()
