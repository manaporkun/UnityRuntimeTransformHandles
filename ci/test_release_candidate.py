import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('candidate', Path(__file__).with_name('release-candidate.py'))
candidate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(candidate)


class CandidateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'repo'
        self.root.mkdir()
        self.out = Path(self.temp.name) / 'candidate'
        self.git('init', '-q')
        self.git('config', 'user.name', 'Fixture')
        self.git('config', 'user.email', 'fixture@example.invalid')
        self.git('config', 'commit.gpgsign', 'false')
        for name, version in [(candidate.CORE, '3.3.1'), (candidate.DEMOS, '0.1.0-preview.1')]:
            folder = self.root / 'Packages' / name
            folder.mkdir(parents=True)
            manifest = {'name': name, 'version': version}
            if name == candidate.DEMOS:
                manifest['dependencies'] = {candidate.CORE: '3.3.1'}
            (folder / 'package.json').write_text(json.dumps(manifest))
            (folder / 'CHANGELOG.md').write_text('# Changelog\n\n## [Unreleased]\n\n### Added\n- Curated entry.\n\n## [3.3.1]\n\n### Fixed\n- Previous entry.\n')
        self.commit('chore: bump version to 3.3.1 [skip ci]')
        self.start = self.git('rev-parse', 'HEAD')

    def git(self, *args):
        return candidate.git(self.root, *args)

    def commit(self, message):
        self.git('add', '.')
        self.git('commit', '-qm', message)

    def change(self, name, message):
        (self.root / 'Packages' / name / 'code.cs').write_text(message)
        self.commit(message)

    def prepare(self):
        self.change(candidate.CORE, 'feat!: optional samples leave Core')
        return candidate.prepare(self.root, self.out)

    def test_frozen_pair_and_roundtrip(self):
        m = self.prepare()
        self.assertEqual((m['version'], m['demos_version'], m['bump']), ('4.0.0', '1.0.0', 'major'))
        self.assertFalse(m['publishable'])
        candidate.apply(self.root, self.out)
        candidate.verify(self.root, self.out)
        demos = json.loads((self.root / 'Packages' / candidate.DEMOS / 'package.json').read_text())
        self.assertEqual(demos['dependencies'][candidate.CORE], '4.0.0')
        core = self.root / 'Packages' / candidate.CORE
        self.assertIn('## [4.0.0]', (core / 'CHANGELOG.md').read_text())
        self.assertEqual((core / 'CHANGELOG.md').read_text().count('Curated entry.'), 1)

    def test_demo_only_commit_does_not_bump_core(self):
        self.change(candidate.DEMOS, 'feat!: new optional demo')
        m = candidate.prepare(self.root, self.out)
        self.assertEqual((m['bump'], m['version'], m['skip']), ('none', '3.3.1', True))

    def test_demo_breaking_commit_does_not_override_core_patch(self):
        self.change(candidate.CORE, 'fix: core lifecycle')
        self.change(candidate.DEMOS, 'feat!: new optional demo')
        self.assertEqual(candidate.version_bump(self.root, self.start, '3.3.1'), ('patch', '3.3.2'))

    def test_breaking_footer_in_full_message(self):
        self.change(candidate.CORE, 'feat: change distribution\n\nBREAKING CHANGE: samples moved')
        self.assertEqual(candidate.version_bump(self.root, self.start, '3.3.1'), ('major', '4.0.0'))

    def test_tests_only_chore_does_not_bump(self):
        folder = self.root / 'Packages' / candidate.CORE / 'Tests'
        folder.mkdir()
        (folder / 'test.cs').write_text('test')
        self.commit('test: add coverage')
        self.assertEqual(candidate.version_bump(self.root, self.start, '3.3.1'), ('none', '3.3.1'))

    def test_uncommitted_package_changes_rejected(self):
        self.change(candidate.CORE, 'feat!: release change')
        (self.root / 'Packages' / candidate.CORE / 'code.cs').write_text('uncommitted')
        with self.assertRaisesRegex(ValueError, 'commit package changes'):
            candidate.prepare(self.root, self.out)

    def test_apply_preserves_uncommitted_work(self):
        self.prepare()
        path = self.root / 'Packages' / candidate.CORE / 'code.cs'
        path.write_text('consumer work')
        with self.assertRaisesRegex(ValueError, 'refusing to overwrite'):
            candidate.apply(self.root, self.out)
        self.assertEqual(path.read_text(), 'consumer work')

    def test_different_source_rejected(self):
        self.prepare()
        self.change(candidate.CORE, 'fix: later change')
        with self.assertRaisesRegex(ValueError, 'checkout differs'):
            candidate.apply(self.root, self.out)

    def test_modified_or_extra_tested_files_rejected(self):
        self.prepare()
        candidate.apply(self.root, self.out)
        path = self.root / 'Packages' / candidate.CORE / 'unexpected.cs'
        path.write_text('untested')
        with self.assertRaisesRegex(ValueError, 'differs from tested'):
            candidate.verify(self.root, self.out)
        path.unlink()
        path = path.with_name('code.cs')
        path.write_text('untested')
        with self.assertRaisesRegex(ValueError, 'differs from tested'):
            candidate.verify(self.root, self.out)

    def test_corrupt_archive_and_notes_rejected(self):
        self.prepare()
        archive = self.out / (candidate.CORE + '.tgz')
        original = archive.read_bytes()
        archive.write_bytes(original + b'corruption')
        with self.assertRaisesRegex(ValueError, 'archive changed'):
            candidate.read_candidate(self.out)
        archive.write_bytes(original)
        (self.out / 'release-notes.md').write_text('different notes')
        with self.assertRaisesRegex(ValueError, 'notes changed'):
            candidate.read_candidate(self.out)

    def test_unsafe_archive_entry_rejected_even_with_matching_archive_digest(self):
        self.prepare()
        archive = self.out / (candidate.CORE + '.tgz')
        with tarfile.open(archive, 'w:gz') as tar:
            entry = tarfile.TarInfo('package/../../escaped')
            entry.size = 1
            tar.addfile(entry, io.BytesIO(b'x'))
        path = self.out / 'candidate.json'
        m = json.loads(path.read_text())
        m['packages'][candidate.CORE]['sha256'] = candidate.digest(archive)
        path.write_text(json.dumps(m))
        with self.assertRaisesRegex(ValueError, 'unsafe archive'):
            candidate.unpack(self.out, Path(self.temp.name) / 'unpacked')

    def test_recovery_freezes_old_package_source_and_notes(self):
        self.change(candidate.CORE, 'feat: newer unreleased change')
        with patch.object(candidate, 'release_exists', return_value=False):
            m = candidate.prepare(self.root, self.out, 'fixture/repo')
        self.assertTrue(m['recovery'])
        self.assertTrue(m['publishable'])
        self.assertEqual(m['package_source'], self.start)
        candidate.apply(self.root, self.out)
        self.assertFalse((self.root / 'Packages' / candidate.CORE / 'code.cs').exists())
        self.assertIn('Previous entry.', (self.out / 'release-notes.md').read_text())
        self.assertNotIn('Curated entry.', (self.out / 'release-notes.md').read_text())

    def test_stable_demos_dependency_cannot_change_without_new_version(self):
        path = self.root / 'Packages' / candidate.DEMOS / 'package.json'
        manifest = json.loads(path.read_text()); manifest['version'] = '1.0.0'
        path.write_text(json.dumps(manifest))
        self.commit('chore: bump version to 3.3.1 [skip ci]')
        self.change(candidate.CORE, 'fix: next Core patch')
        with self.assertRaisesRegex(ValueError, 'explicit new Demos version'):
            candidate.prepare(self.root, self.out)

    def test_api_failure_is_not_interpreted_as_missing_release(self):
        for status, exists in [(200, True), (404, False), (403, None), (500, None)]:
            response = subprocess.CompletedProcess([], 0 if status == 200 else 1,
                                                   stdout=f'HTTP/2.0 {status} Status\n', stderr='failure')
            with self.subTest(status=status), patch.object(candidate.subprocess, 'run', return_value=response):
                if exists is None:
                    with self.assertRaisesRegex(ValueError, 'release lookup failed'):
                        candidate.release_exists('fixture/repo', '3.3.1')
                else:
                    self.assertEqual(candidate.release_exists('fixture/repo', '3.3.1'), exists)

class PublicationTests(unittest.TestCase):
    setUp = CandidateTests.setUp
    git = CandidateTests.git
    commit = CandidateTests.commit
    change = CandidateTests.change
    prepare = CandidateTests.prepare

    # Exercise the workflow's actual publication Python against a local bare remote.
    def publisher(self):
        text = (Path(__file__).parent.parent / '.github/workflows/publish-upm.yml').read_text()
        step = text.split('      - name: Commit only the tested candidate\n', 1)[1].split('      - name:', 1)[0]
        program = step.split("python3 - <<'PYTHON'\n", 1)[1].split('\n          PYTHON', 1)[0]
        import textwrap
        import os
        target = self.root / 'artifacts/candidate'
        import shutil
        shutil.copytree(self.out, target)
        # Only a CI main candidate is publishable; simulate its read-only preparation result.
        path = target / 'candidate.json'
        metadata = json.loads(path.read_text()); metadata['publishable'] = True
        path.write_text(json.dumps(metadata))
        ci = self.root / 'ci'; ci.mkdir()
        shutil.copy2(Path(__file__).with_name('release-candidate.py'), ci)
        output = self.root / 'github-output'
        candidate.apply(self.root, target)
        candidate.verify(self.root, target)
        result = subprocess.run(['python3', '-c', textwrap.dedent(program)], cwd=self.root,
                                env={**os.environ, 'GITHUB_OUTPUT': str(output)}, text=True, capture_output=True)
        return result, output.read_text()

    def remote(self):
        remote = Path(self.temp.name) / 'remote.git'
        subprocess.run(['git', 'init', '--bare', '-q', str(remote)], check=True)
        self.git('remote', 'add', 'origin', str(remote))
        self.git('push', '-q', 'origin', 'HEAD:refs/heads/main')
        return remote

    def test_publishes_only_candidate_packages(self):
        metadata = self.prepare()
        self.remote()
        result, output = self.publisher()
        self.assertEqual(result.returncode, 0, result.stderr)
        candidate.verify(self.root, self.root / 'artifacts/candidate')
        head = self.git('rev-parse', 'HEAD')
        self.assertEqual(self.git('rev-parse', 'refs/remotes/origin/main'), head)
        self.assertIn('release_ref=' + head, output)
        self.assertEqual(self.git('rev-parse', 'HEAD^'), metadata['source'])
        changed = self.git('diff-tree', '--no-commit-id', '--name-only', '-r', 'HEAD').splitlines()
        self.assertTrue(changed)
        self.assertTrue(all(name.startswith('Packages/') for name in changed))

    def test_advanced_main_is_not_recomputed_or_published(self):
        metadata = self.prepare()
        self.remote()
        (self.root / 'later.txt').write_text('concurrent main')
        self.commit('docs: concurrent main change')
        advanced = self.git('rev-parse', 'HEAD')
        self.git('push', '-q', 'origin', 'HEAD:refs/heads/main')
        self.git('reset', '--hard', metadata['source'])
        result, _ = self.publisher()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('main advanced', result.stderr)
        self.assertEqual(self.git('rev-parse', 'HEAD'), metadata['source'])
        self.assertEqual(self.git('rev-parse', 'refs/remotes/origin/main'), advanced)

    def test_recovery_does_not_push_old_packages_over_new_main(self):
        self.change(candidate.CORE, 'feat: newer unreleased change')
        with patch.object(candidate, 'release_exists', return_value=False):
            metadata = candidate.prepare(self.root, self.out, 'fixture/repo')
        self.remote()
        result, output = self.publisher()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('release_ref=' + self.start, output)
        self.assertEqual(self.git('rev-parse', 'HEAD'), metadata['source'])
        self.assertEqual(self.git('status', '--porcelain', '--', 'Packages'), '')


if __name__ == '__main__':
    unittest.main()
