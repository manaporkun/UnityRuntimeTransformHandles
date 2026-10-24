import importlib.util
import json
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('boundary', ROOT / 'ci/verify-package-boundaries.py')
boundary = importlib.util.module_from_spec(spec)
spec.loader.exec_module(boundary)


class PackageBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for name in (boundary.CORE, boundary.DEMOS):
            shutil.copytree(ROOT / 'Packages' / name, self.root / 'Packages' / name)
        self.core = self.root / 'Packages' / boundary.CORE
        self.demos = self.root / 'Packages' / boundary.DEMOS

    def test_valid_packages(self):
        self.assertGreaterEqual(boundary.validate(self.root), 1)

    def test_reverse_dependency_rejected(self):
        p = self.core / 'package.json'
        m = json.loads(p.read_text()); m['dependencies'][boundary.DEMOS] = '0.1.0-preview.1'
        p.write_text(json.dumps(m))
        with self.assertRaisesRegex(ValueError, 'Core depends'): boundary.validate(self.root)

    def test_core_sample_registration_rejected(self):
        p = self.core / 'package.json'; m = json.loads(p.read_text()); m['samples'] = [{'path': 'Samples~/Bad'}]
        p.write_text(json.dumps(m))
        with self.assertRaisesRegex(ValueError, 'register samples'): boundary.validate(self.root)

    def test_duplicate_guid_rejected(self):
        p = next(self.core.rglob('*.cs.meta'))
        shutil.copy2(p, self.demos / 'duplicate.meta')
        with self.assertRaisesRegex(ValueError, 'duplicate GUID'): boundary.validate(self.root)

    def test_cross_sample_reference_rejected(self):
        scene = next((self.demos / 'Samples~/Demo').glob('*.unity'))
        scene.write_text(scene.read_text() + '\nreference: {guid: 123456789012345678901234567890ab}\n')
        with self.assertRaisesRegex(ValueError, 'escapes'): boundary.validate(self.root)

    def test_compiled_demo_assembly_outside_samples_rejected(self):
        (self.demos / 'Leaked.cs').write_text('class Leaked {}')
        with self.assertRaisesRegex(ValueError, 'always-compiled'): boundary.validate(self.root)

    def test_git_package_dependency_rejected(self):
        p = self.demos / 'package.json'; m = json.loads(p.read_text()); m['dependencies']['other'] = 'https://example.com/repo.git'
        p.write_text(json.dumps(m))
        with self.assertRaisesRegex(ValueError, 'project manifest'): boundary.validate(self.root)

    def test_non_version_dependencies_rejected_for_both_packages(self):
        for package in (self.core, self.demos):
            path = package / 'package.json'; original = path.read_text()
            for value in ('https://bitbucket.org/a/b', 'ssh://host/repo', '../other', '/absolute'):
                with self.subTest(package=package.name, value=value):
                    manifest = json.loads(original); manifest.setdefault('dependencies', {})['other'] = value
                    path.write_text(json.dumps(manifest))
                    with self.assertRaisesRegex(ValueError, 'must use versions'): boundary.validate(self.root)
            path.write_text(original)

    def test_non_script_compiled_assets_outside_samples_rejected(self):
        for extension in ('.asmdef', '.asmref', '.dll', '.unity', '.prefab', '.shader'):
            with self.subTest(extension=extension):
                path = self.demos / ('Leaked' + extension); path.write_text('')
                with self.assertRaisesRegex(ValueError, 'always-compiled'): boundary.validate(self.root)
                path.unlink()


class ReleaseScopeTests(unittest.TestCase):
    def test_workflow_queries_exclude_demo_only_history(self):
        # Exercise the actual three workflow git-log statements against disposable history.
        workflow = (ROOT / '.github/workflows/publish-upm.yml').read_text()
        self.assertIn('git log $RANGE --format=%B -- "$PACKAGE_PATH"', workflow)
        self.assertIn('git log $RANGE --no-merges --pretty=tformat:"%s" -- "$PACKAGE_PATH"', workflow)
        self.assertIn('git log $RANGE --name-only --pretty=format: -- "$PACKAGE_PATH"', workflow)
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            def git(*args):
                return subprocess.check_output(['git', '-C', folder, *args], text=True).strip()
            git('init', '-q'); git('config', 'commit.gpgsign', 'false'); git('config', 'user.name', 'Fixture'); git('config', 'user.email', 'fixture@example.invalid')
            cp = root / 'Packages' / boundary.CORE; dp = root / 'Packages' / boundary.DEMOS
            cp.mkdir(parents=True); dp.mkdir(parents=True)
            (cp / 'code.cs').write_text('initial'); git('add', '.'); git('commit', '-qm', 'initial')
            start = git('rev-parse', 'HEAD')
            for i, (subject, core_change, demo_change) in enumerate([
                ('feat: demo only', False, True), ('fix: core only', True, False),
                ('feat: mixed', True, True), ('docs: core docs', True, False),
                ('feat!: demo breaking', False, True), ('fix!: core breaking', True, False)]):
                if core_change: (cp / ('README.md' if subject.startswith('docs:') else 'code.cs')).write_text(str(i))
                if demo_change: (dp / 'code.cs').write_text(str(i))
                git('add', '.'); git('commit', '-qm', subject)
            scope = f'{start}..HEAD'
            assignments = re.findall(r'^\s*PACKAGE_PATH="([^"]+)"\s*$', workflow, re.M)
            self.assertTrue(assignments)
            self.assertEqual(set(assignments), {'Packages/' + boundary.CORE})
            path = assignments[0]
            for fmt in ['--format=%B', '--no-merges --pretty=tformat:%s']:
                text = git('log', scope, *fmt.split(), '--', path)
                for absent in ['feat: demo only', 'feat!: demo breaking']: self.assertNotIn(absent, text)
                for present in ['fix: core only', 'feat: mixed', 'docs: core docs', 'fix!: core breaking']: self.assertIn(present, text)
            names = git('log', scope, '--name-only', '--pretty=format:', '--', path)
            self.assertNotIn(boundary.DEMOS, names)
