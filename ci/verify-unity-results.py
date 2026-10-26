#!/usr/bin/env python3
"""Prepare fresh evidence, collect observations, and verify one isolated Unity test job."""
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import time
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read_expectations(required_dir):
    data = json.loads((Path(required_dir) / 'required-unity-evidence.json').read_text())
    require(isinstance(data, dict) and
            set(data) == {'schema', 'exceptions', 'session_cases', 'phases'} and
            type(data['schema']) is int and data['schema'] == 1, 'invalid evidence expectations schema')
    exceptions = data['exceptions']
    require(isinstance(exceptions, dict) and set(exceptions) == {'play', 'edit'} and
            all(isinstance(items, list) and all(isinstance(s, str) and s and '\n' not in s
                                               for s in items) for items in exceptions.values()),
            'invalid exception expectations')
    phases = data['phases']
    require(isinstance(phases, list) and len(phases) == 4 and
            all(isinstance(s, str) and s and not re.search(r'[\t\r\n]', s) for s in phases) and
            len(set(phases)) == 4, 'invalid session phases')
    cases = data['session_cases']
    require(isinstance(cases, list) and cases and all(
            isinstance(c, dict) and set(c) == {'name', 'require_quit_on_exit'} and
            isinstance(c['name'], str) and c['name'] and not re.search(r'[\t\r\n]', c['name']) and
            type(c['require_quit_on_exit']) is bool for c in cases), 'invalid session cases')
    require(len({c['name'] for c in cases}) == len(cases), 'duplicate session cases')
    return data


def package_hash(path):
    lines = ''.join(f'{digest(p)}  ./{p.relative_to(path).as_posix()}\n'
                    for p in sorted(Path(path).rglob('*'), key=lambda p: './' + p.relative_to(path).as_posix())
                    if p.is_file() and p.name != '.DS_Store')
    return hashlib.sha256(lines.encode()).hexdigest()


def settings(project):
    p = Path(project) / 'ProjectSettings/ProjectSettings.asset'
    text = p.read_text() if p.exists() else ''
    found = re.findall(r'^  activeInputHandler: (\d+)$', text, re.M)
    require(len(found) <= 1, 'ambiguous activeInputHandler')
    e = Path(project) / 'ProjectSettings/EditorSettings.asset'
    reloads = re.findall(r'^  m_EnterPlayModeOptions(?:Enabled)?: \d+$', e.read_text(), re.M) if e.exists() else []
    return {'input': int(found[0]) if found else None, 'reload': sorted(reloads)}


def prepare(project, package, out, unity, input_mode, repository=None):
    project, package, out = Path(project).resolve(), Path(package).resolve(), Path(out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    require(not (out / 'metadata.json').exists(), 'use a fresh artifact directory')
    evidence = project / 'Library/P0E-Evidence'
    if evidence.exists():
        shutil.rmtree(evidence)
    evidence.mkdir(parents=True)
    started_ns = time.time_ns()
    original = settings(project)
    editor = project / 'ProjectSettings/EditorSettings.asset'
    if editor.exists():
        text = editor.read_text()
        # Configure a stable isolated-job baseline before measuring restoration. Unity 6 otherwise
        # normalizes the deprecated enabled flag during serialization, even with equivalent reloads.
        for key, value in [('m_EnterPlayModeOptionsEnabled', 1), ('m_EnterPlayModeOptions', 0)]:
            text, count = re.subn(r'^  ' + key + r': \d+$', f'  {key}: {value}', text, flags=re.M)
            require(count == 1, 'missing or ambiguous reload setting: ' + key)
        editor.write_text(text)
    run_id = uuid.uuid4().hex
    (evidence / 'run-id.txt').write_text(run_id)
    metadata = {'schema': 1, 'run_id': run_id, 'started_ns': started_ns,
                'project': str(project), 'package': str(package),
                'commit': subprocess.check_output(['git', '-C', str(repository or package), 'rev-parse', 'HEAD'], text=True).strip(),
                'expected': {'unity': unity, 'input': input_mode, 'package_hash': package_hash(package)},
                'original_settings': original, 'before': settings(project)}
    (out / 'metadata.json').write_text(json.dumps(metadata, indent=2) + '\n')


def collect(out, paths):
    out = Path(out)
    m = json.loads((out / 'metadata.json').read_text())
    evidence = Path(m['project']) / 'Library/P0E-Evidence'
    require((evidence / 'run-id.txt').read_text() == m['run_id'], 'evidence run ID mismatch')
    for name in ('transitions.tsv', 'settings.tsv', 'run-id.txt'):
        shutil.copy2(evidence / name, out / name)
    artifacts = dict(paths, transitions=out / 'transitions.tsv', settings=out / 'settings.tsv', run_id=out / 'run-id.txt')
    resolved = [str(Path(p).resolve()) for p in artifacts.values()]
    require(len(set(resolved)) == len(resolved), 'duplicate artifact paths')
    files = {}
    for name, path in artifacts.items():
        p = Path(path).resolve()
        require(p.stat().st_mtime_ns >= m['started_ns'], f'stale evidence: {name}')
        files[name] = {'path': str(p), 'sha256': digest(p)}
    versions = {}
    for mode in ('play', 'edit'):
        versions[mode] = sorted(set(re.findall(r'Initialize engine version: ([\w.]+)', Path(paths[mode + '_log']).read_text())))
    m['files'] = files
    m['observed'] = {'unity': versions, 'settings': settings(m['project']), 'package_hash': package_hash(m['package'])}
    (out / 'metadata.json').write_text(json.dumps(m, indent=2) + '\n')


def verify(out, required_dir):
    out, required_dir = Path(out), Path(required_dir)
    expected = read_expectations(required_dir)
    session_cases = {c['name']: c['require_quit_on_exit'] for c in expected['session_cases']}
    phases = expected['phases']
    m = json.loads((out / 'metadata.json').read_text())
    require(m['schema'] == 1, 'metadata schema')
    require(re.fullmatch(r'[0-9a-f]{32}', m['run_id']) is not None, 'run ID')
    require(re.fullmatch(r'[0-9a-f]{40}', m['commit']) is not None, 'commit identity')
    require(Path(m['project']).is_absolute(), 'project identity')
    f = m['files']
    require(set(f) == {'play_xml', 'edit_xml', 'play_log', 'edit_log', 'transitions', 'settings', 'run_id'}, 'artifact set')
    require(len({str(Path(v['path']).resolve()) for v in f.values()}) == 7, 'duplicate evidence path')
    for name, value in f.items():
        require(digest(value['path']) == value['sha256'], f'artifact changed: {name}')
    require(Path(f['run_id']['path']).read_text() == m['run_id'], 'run ID binding')
    require(m['observed']['package_hash'] == m['expected']['package_hash'], 'package changed during tests')
    require(re.fullmatch(r'[0-9a-f]{64}', m['observed']['package_hash']) is not None, 'package identity')
    require(m['expected']['input'] in (0, 1), 'expected input')
    require(m['observed']['settings']['input'] == m['expected']['input'], 'observed input mismatch')
    if m['before']['input'] is not None:
        require(m['before']['input'] == m['expected']['input'], 'initial input mismatch')
    if m['before']['reload']:
        require(m['before']['reload'] == m['observed']['settings']['reload'], 'reload settings not restored')
    for mode in ('play', 'edit'):
        require(m['observed']['unity'][mode] == [m['expected']['unity']], 'observed Unity version mismatch')
        root = ET.parse(f[mode + '_xml']['path']).getroot()
        names = (required_dir / ('required-' + mode + 'mode.txt')).read_text().splitlines()
        require(len(names) > 0 and len(set(names)) == len(names), 'invalid expected names')
        cases = list(root.iter('test-case'))
        actual = [c.get('fullname') for c in cases]
        require(len(actual) == len(set(actual)) == len(names) and set(actual) == set(names), mode + ' discovery mismatch')
        require(root.get('result') == 'Passed', mode + ' root result')
        for key in ('total', 'passed', 'testcasecount'):
            require(int(root.get(key, '-1')) == len(names), mode + ' totals')
        for key in ('failed', 'skipped'):
            require(int(root.get(key, '-1')) == 0, mode + ' failed/skipped')
        require(all(c.get('result') == 'Passed' for c in cases), mode + ' case result')
        log = Path(f[mode + '_log']['path']).read_text()
        require(sorted(set(re.findall(r'Initialize engine version: ([\w.]+)', log))) == m['observed']['unity'][mode], 'version observation differs from log')
        require(not re.search(r'error CS\d+|Scripts have compiler errors|MissingReferenceException|NullReferenceException|Destroying object multiple times', log), 'unexpected error in ' + mode)
        # Deliberate subscriber exceptions are asserted precisely by the passing Unity tests.
        exceptions = [line for line in log.splitlines() if re.match(r'^\w+(?:\.\w+)*Exception:', line)]
        require(sorted(exceptions) == sorted(expected['exceptions'][mode]), 'exception log mismatch')
    rows = [r.split('\t') for r in Path(f['transitions']['path']).read_text().splitlines()]
    require(all(len(r) == 5 for r in rows) and len(rows) == len(session_cases) * 13, 'transition shape')
    require({r[0] for r in rows} == set(session_cases), 'transition case identities')
    for case, require_quit in session_cases.items():
        selected = [r for r in rows if r[0] == case]
        require(len(selected) == 13, 'duplicate/missing case rows')
        for i in range(3):
            segment = selected[i * 4:i * 4 + 4]
            require([r[1] for r in segment] == [str(i)] * 4 and [r[2] for r in segment] == phases, 'transition ordering')
            require(segment[1][3] == 'quit=False', 'entry quit reset')
            require(segment[2][3:] == ['quit=False', 'ends=1'], 'interaction cancellation')
            require(segment[3][4] == 'ends=2', 'exit interaction completion')
            if require_quit:
                require(segment[3][3] == 'quit=True', 'natural quit flag')
        require(selected[-1][1:3] == ['3', 'teardown-at-' + phases[-1]], 'teardown checkpoint')
    rows = [r.split('\t') for r in Path(f['settings']['path']).read_text().splitlines()]
    require(len(rows) == len(session_cases) and all(len(r) == 5 for r in rows), 'settings evidence shape')
    require({r[0] for r in rows} == set(session_cases), 'settings cases')
    require(all(r[1] in ('True', 'False') and r[2] in ('0', '1', '2', '3') and r[1:3] == r[3:5] for r in rows), 'settings restore mismatch')
    print(f'PASS: exact test suites, {len(session_cases) * 3} session cycles and observed settings/version')


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('command', choices=['prepare', 'collect', 'verify'])
    p.add_argument('--out', required=True)
    p.add_argument('--repository'); p.add_argument('--project'); p.add_argument('--package'); p.add_argument('--unity'); p.add_argument('--input', type=int)
    for name in ('play-xml', 'edit-xml', 'play-log', 'edit-log'):
        p.add_argument('--' + name)
    p.add_argument('--required-dir', default=str(Path(__file__).parent))
    a = p.parse_args()
    if a.command == 'prepare':
        prepare(a.project, a.package, a.out, a.unity, a.input, a.repository)
    elif a.command == 'collect':
        paths = {n: getattr(a, n) for n in ('play_xml', 'edit_xml', 'play_log', 'edit_log')}
        collect(a.out, paths)
    else:
        verify(a.out, a.required_dir)


if __name__ == '__main__':
    main()
