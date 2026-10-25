import importlib.util
import json
import shutil
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

    def test_toolkit_relative_reference_escape_rejected(self):
        folder = self.demos / 'Samples~/ModularBuilding/UI'
        for suffix, text in [('.uxml', '<UXML><Style src="../../LevelEditor/LevelEditorDemo.cs" /></UXML>'),
                             ('.uxml', '<UXML><VisualElement style="background-image: url(../../LevelEditor/LevelEditorDemo.cs);" /></UXML>'),
                             ('.uss', '.x { background-image: url("../../LevelEditor/LevelEditorDemo.cs"); }'),
                             ('.tss', '@import "../../LevelEditor/LevelEditorDemo.cs";')]:
            with self.subTest(suffix=suffix):
                path = folder / ('Escape' + suffix); path.write_text(text)
                with self.assertRaisesRegex(ValueError, 'UI reference escapes'): boundary.validate(self.root)
                path.unlink()

    def test_toolkit_external_or_missing_reference_rejected(self):
        path = self.demos / 'Samples~/ModularBuilding/UI/Bad.uss'
        for value in ('https://example.com/font.ttf', 'project://database/Assets/font.ttf', 'missing.ttf'):
            with self.subTest(value=value):
                path.write_text('.x { -unity-font: url("' + value + '"); }')
                with self.assertRaises(ValueError): boundary.validate(self.root)



# Release-history scoping and candidate integrity are exercised in test_release_candidate.py.
