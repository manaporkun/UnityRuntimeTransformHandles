import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('sync_demo_ui', Path(__file__).with_name('sync-demo-ui.py'))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class DemoThemeTests(unittest.TestCase):
    def test_checked_in_themes_match_source(self):
        MODULE.sync(check=True)

    def test_drift_check_rejects_changed_copy_and_sync_preserves_guid(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source = root / 'ci/demo-ui/DemoTheme.uss'
            source.parent.mkdir(parents=True)
            source.write_text('.demo-shell { color: white; }\n')
            for name in MODULE.SAMPLES:
                path = root / 'Packages/com.orkunmanap.runtime-transform-handles.demos/Samples~' / name / 'UI/DemoTheme.uss'
                path.parent.mkdir(parents=True)
                path.write_text('stale')
                path.with_suffix('.uss.meta').write_text('sample-owned-guid')
            with self.assertRaises(ValueError):
                MODULE.sync(root, check=True)
            MODULE.sync(root)
            MODULE.sync(root, check=True)
            for path in root.rglob('*.meta'):
                self.assertEqual(path.read_text(), 'sample-owned-guid')
