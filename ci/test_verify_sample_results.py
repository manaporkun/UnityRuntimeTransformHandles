import importlib.util
import json
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location('samples', Path(__file__).with_name('verify-sample-results.py'))
samples = importlib.util.module_from_spec(spec)
spec.loader.exec_module(samples)


class SampleResultsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.out = self.root / 'evidence'; self.out.mkdir()
        self.imports = self.root / 'Assets/Samples'; self.imports.mkdir(parents=True)
        (self.imports / 'test.cs').write_text('sample')
        (self.root / 'ProjectSettings').mkdir()
        (self.root / 'ProjectSettings/ProjectSettings.asset').write_text('  activeInputHandler: 0\n')
        candidate = self.root / 'artifacts/candidate/candidate.json'
        candidate.parent.mkdir(parents=True); candidate.write_text('{}')
        metadata = {'started_ns': time.time_ns(), 'unity': '6000.3.16f1', 'input': 0,
                    'imports': str(self.imports), 'files': samples.candidate.hashes(self.imports),
                    'candidate_sha256': samples.candidate.digest(candidate)}
        (self.out / 'metadata.json').write_text(json.dumps(metadata))
        self.xml = self.root / 'results.xml'; self.log = self.root / 'results.log'
        self.tree = ET.Element('test-run', result='Passed')
        names = (Path(__file__).with_name('required-samples-input0.txt')).read_text().splitlines()
        for name in names:
            if name and not name.startswith('#'):
                ET.SubElement(self.tree, 'test-case', fullname=name, result='Passed')
        self.save()
        self.log.write_text('Initialize engine version: 6000.3.16f1 (revision)\n')
        patcher = patch.object(samples.candidate, 'verify')
        patcher.start(); self.addCleanup(patcher.stop)

    def save(self):
        ET.ElementTree(self.tree).write(self.xml)

    def verify(self):
        samples.verify(self.root, self.out, self.xml, self.log)

    def test_valid_exact_suite(self):
        self.verify()
        self.assertEqual(json.loads((self.out / 'verified.json').read_text())['passed'], 116)

    def test_incidental_expected_version_does_not_hide_wrong_editor(self):
        self.log.write_text('Command line: --expected 6000.3.16f1\nInitialize engine version: 2021.3.0f1\n')
        with self.assertRaisesRegex(ValueError, 'Editor version mismatch'):
            self.verify()

    def test_skip_rejected(self):
        self.tree[0].set('result', 'Skipped'); self.save()
        with self.assertRaisesRegex(ValueError, 'failures or skips'):
            self.verify()

    def test_missing_test_rejected(self):
        self.tree.remove(self.tree[0]); self.save()
        with self.assertRaisesRegex(ValueError, 'suite mismatch'):
            self.verify()

    def test_duplicate_test_rejected(self):
        ET.SubElement(self.tree, 'test-case', fullname=self.tree[0].get('fullname'), result='Passed'); self.save()
        with self.assertRaisesRegex(ValueError, 'suite mismatch'):
            self.verify()

    def test_changed_import_rejected(self):
        (self.imports / 'test.cs').write_text('changed')
        with self.assertRaisesRegex(ValueError, 'imported samples changed'):
            self.verify()

    def test_wrong_input_rejected(self):
        (self.root / 'ProjectSettings/ProjectSettings.asset').write_text('  activeInputHandler: 1\n')
        with self.assertRaisesRegex(ValueError, 'input configuration mismatch'):
            self.verify()

    def test_stale_results_rejected(self):
        import os
        os.utime(self.xml, (1, 1))
        with self.assertRaisesRegex(ValueError, 'stale sample results'):
            self.verify()
