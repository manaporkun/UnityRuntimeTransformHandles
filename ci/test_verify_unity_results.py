import contextlib
import importlib.util
import io
import json
import tempfile
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location('verifier', Path(__file__).with_name('verify-unity-results.py'))
v = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)


def write_results(folder, required_dir):
    """Write synthetic Unity observations for a selected source-controlled inventory."""
    folder, required_dir = Path(folder), Path(required_dir)
    folder.mkdir(parents=True, exist_ok=True)
    expectations = json.loads((required_dir / 'required-unity-evidence.json').read_text())
    files = {}
    for mode in ('play', 'edit'):
        names = (required_dir / ('required-' + mode + 'mode.txt')).read_text().splitlines()
        count = str(len(names))
        root = ET.Element('test-run', result='Passed', total=count, passed=count,
                          testcasecount=count, failed='0', skipped='0')
        for name in names:
            ET.SubElement(root, 'test-case', fullname=name, result='Passed')
        p = folder / (mode + '.xml'); ET.ElementTree(root).write(p)
        files[mode + '_xml'] = str(p)
        p = folder / (mode + '.log')
        p.write_text('Initialize engine version: 6000.3.16f1 (abc)\n' +
                     ''.join(e + '\n' for e in expectations['exceptions'][mode]))
        files[mode + '_log'] = str(p)
    rows = []
    for case in expectations['session_cases']:
        name = case['name']
        for i in range(3):
            rows += [f'{name}\t{i}\t{phase}\tquit={quit}\tends={ends}'
                     for phase, quit, ends in zip(expectations['phases'],
                                                  ('False', 'False', 'False', 'True'), (0, 0, 1, 2))]
        rows.append(f"{name}\t3\tteardown-at-{expectations['phases'][-1]}\tquit=True\tends=2")
    p = folder / 'transitions.tsv'; p.write_text('\n'.join(rows) + '\n')
    files['transitions'] = str(p)
    p = folder / 'settings.tsv'
    p.write_text(''.join(c['name'] + '\tFalse\t3\tFalse\t3\n' for c in expectations['session_cases']))
    files['settings'] = str(p)
    return files


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.out = Path(self.temp.name)
        for mode in ('play', 'edit'):
            (self.out / ('required-' + mode + 'mode.txt')).write_text(mode + '.Case\n')
        (self.out / 'required-unity-evidence.json').write_bytes(
            Path(__file__).with_name('required-unity-evidence.json').read_bytes())
        files = write_results(self.out, self.out)
        p = self.out / 'run-id.txt'; p.write_text('a' * 32); files['run_id'] = str(p)
        self.meta = {'schema':1,'run_id':'a'*32,'commit':'b'*40,'project':str(self.out),
                     'expected':{'unity':'6000.3.16f1','input':0,'package_hash':'c'*64},
                     'observed':{'unity':{'play':['6000.3.16f1'],'edit':['6000.3.16f1']},'settings':{'input':0,'reload':['original']},'package_hash':'c'*64},
                     'before':{'input':0,'reload':['original']},'files':{k:{'path':p,'sha256':v.digest(p)} for k,p in files.items()}}
        self.save()

    def save(self):
        (self.out/'metadata.json').write_text(json.dumps(self.meta))

    def check(self):
        with contextlib.redirect_stdout(io.StringIO()): v.verify(self.out,self.out)

    def mutate(self, key, transform):
        f=self.meta['files'][key];p=Path(f['path']);p.write_text(transform(p.read_text()));f['sha256']=v.digest(p);self.save()

    def test_valid_evidence(self): self.check()

    def test_prepare_collect_roundtrip(self):
        project = self.out / 'project';(project/'ProjectSettings').mkdir(parents=True)
        (project/'ProjectSettings/ProjectSettings.asset').write_text('  activeInputHandler: 0\n')
        (project/'ProjectSettings/EditorSettings.asset').write_text('  m_EnterPlayModeOptions: 3\n  m_EnterPlayModeOptionsEnabled: 0\n')
        package = self.out/'package';package.mkdir();(package/'package.json').write_text('{}')
        output = self.out/'fresh'
        from unittest.mock import patch
        with patch.object(v.subprocess, 'check_output', return_value='b'*40):
            v.prepare(project, package, output, '6000.3.16f1', 0)
        paths = {}
        for key in ('play_xml','edit_xml','play_log','edit_log'):
            dest = output/key;dest.write_bytes(Path(self.meta['files'][key]['path']).read_bytes());paths[key] = dest
        for key in ('transitions','settings'):
            (project/('Library/P0E-Evidence/'+key+'.tsv')).write_bytes(Path(self.meta['files'][key]['path']).read_bytes())
        prepared=json.loads((output/'metadata.json').read_text())
        self.assertNotEqual(prepared['original_settings']['reload'],prepared['before']['reload'])
        self.assertEqual(prepared['before']['reload'], ['  m_EnterPlayModeOptions: 0', '  m_EnterPlayModeOptionsEnabled: 1'])
        v.collect(output, paths)
        with contextlib.redirect_stdout(io.StringIO()):v.verify(output,self.out)

    def test_missing_case(self):
        self.mutate('play_xml',lambda s:s.replace('play.Case','missing.Case'))
        with self.assertRaisesRegex(ValueError,'discovery'):self.check()

    def test_skipped_case(self):
        self.mutate('play_xml',lambda s:s.replace('skipped="0"','skipped="1"'))
        with self.assertRaisesRegex(ValueError,'failed/skipped'):self.check()

    def test_duplicate_cycle(self):
        self.mutate('transitions',lambda s:s+s.splitlines()[0]+'\n')
        with self.assertRaises(ValueError):self.check()

    def test_truncated_tsv(self):
        self.mutate('transitions',lambda s:'\n'.join(s.splitlines()[:-1])+'\n')
        with self.assertRaises(ValueError):self.check()

    def test_wrong_case_identity(self):
        self.mutate('transitions',lambda s:s.replace('None/authored','Unexpected/authored'))
        with self.assertRaisesRegex(ValueError,'identities'):self.check()

    def test_wrong_settings(self):
        self.meta['observed']['settings']['reload']=['changed'];self.save()
        with self.assertRaisesRegex(ValueError,'not restored'):self.check()

    def test_wrong_input(self):
        self.meta['observed']['settings']['input']=1;self.save()
        with self.assertRaisesRegex(ValueError,'input mismatch'):self.check()

    def test_wrong_version(self):
        self.meta['observed']['unity']['edit']=['3.5.0.0'];self.save()
        with self.assertRaisesRegex(ValueError,'version mismatch'):self.check()

    def test_duplicate_required_names(self):
        p=self.out/'required-playmode.txt';p.write_text(p.read_text()*2)
        with self.assertRaisesRegex(ValueError,'expected names'):self.check()

    def test_wrong_run_identity(self):
        self.mutate('run_id',lambda s:'d'*32)
        with self.assertRaisesRegex(ValueError,'binding'):self.check()

    def test_artifact_changed_after_collection(self):
        Path(self.meta['files']['edit_log']['path']).write_text('different')
        with self.assertRaisesRegex(ValueError,'changed'):self.check()

    def test_missing_file(self):
        Path(self.meta['files']['edit_xml']['path']).unlink()
        with self.assertRaises(FileNotFoundError):self.check()

    def test_wrong_quit_observation(self):
        self.mutate('transitions',lambda s:s.replace('entered-and-asserted\tquit=False','entered-and-asserted\tquit=True'))
        with self.assertRaisesRegex(ValueError,'quit reset'):self.check()

    def test_settings_case_not_restored(self):
        self.mutate('settings',lambda s:s.replace('False\t3\tFalse\t3','False\t3\tTrue\t3',1))
        with self.assertRaisesRegex(ValueError,'restore mismatch'):self.check()

    def test_unexpected_exception(self):
        self.mutate('edit_log',lambda s:s+'InvalidOperationException: unexpected\n')
        with self.assertRaisesRegex(ValueError,'exception log'):self.check()

    def test_invalid_expectations_rejected(self):
        path = self.out / 'required-unity-evidence.json'
        original = path.read_text()
        mutations = [
            ('schema', 2), ('schema', True), ('exceptions', {'play': [], 'edit': 'invalid'}),
            ('session_cases', []), ('session_cases', [{'name': 'case', 'require_quit_on_exit': 1}]),
            ('session_cases', [{'name': '', 'require_quit_on_exit': True}]),
            ('phases', []), ('phases', ['one', 'two', 'three', 'three']),
            ('phases', ['one', 'two', 'three', 4])]
        for key, value in mutations:
            with self.subTest(key=key, value=value):
                data = json.loads(original); data[key] = value
                path.write_text(json.dumps(data))
                with self.assertRaisesRegex(ValueError, 'invalid'):
                    self.check()

    def test_duplicate_session_case_rejected(self):
        path = self.out / 'required-unity-evidence.json'
        data = json.loads(path.read_text()); data['session_cases'].append(data['session_cases'][0])
        path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'duplicate session cases'):
            self.check()

    def test_exception_multiplicity_is_required(self):
        self.mutate('play_log', lambda s: s.replace('InvalidOperationException: end callback\n', '', 1))
        with self.assertRaisesRegex(ValueError, 'exception log mismatch'):
            self.check()

    def test_quit_assertion_uses_inventory_not_case_name(self):
        self.mutate('transitions', lambda s: s.replace('DisableDomainReload/authored', 'Renamed/authored'))
        self.mutate('settings', lambda s: s.replace('DisableDomainReload/authored', 'Renamed/authored'))
        path = self.out / 'required-unity-evidence.json'
        data = json.loads(path.read_text())
        for case in data['session_cases']:
            if case['name'] == 'DisableDomainReload/authored':
                case['name'] = 'Renamed/authored'
        path.write_text(json.dumps(data))
        self.check()
        self.mutate('transitions', lambda s: s.replace(
            'Renamed/authored\t0\texited-and-asserted\tquit=True',
            'Renamed/authored\t0\texited-and-asserted\tquit=False'))
        with self.assertRaisesRegex(ValueError, 'natural quit flag'):
            self.check()

    def test_full_reload_does_not_add_an_exit_quit_assertion(self):
        self.mutate('transitions', lambda s: s.replace(
            'None/authored\t0\texited-and-asserted\tquit=True',
            'None/authored\t0\texited-and-asserted\tquit=False'))
        self.check()


if __name__ == '__main__':unittest.main()
