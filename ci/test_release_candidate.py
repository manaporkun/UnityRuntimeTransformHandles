import contextlib
import importlib.util
import io
import json
import shutil
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import test_verify_unity_results as core_tests
import test_verify_sample_results as sample_tests

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
        ci = self.root / 'ci'; ci.mkdir(exist_ok=True)
        for name in candidate.REQUIRED_FILES:
            shutil.copy2(Path(__file__).with_name(name), ci / name)
        for name in candidate.REQUIRED_FILES[:-1]:
            (ci / name).write_text(name + '.Shared\n' + name + '.Old\n')
        # A real immutable Core-only Git commit models the reviewed older release.
        self.git('add', 'Packages/' + candidate.CORE)
        tree = self.git('write-tree')
        core_tree = self.git('rev-parse', tree + ':Packages/' + candidate.CORE)
        baseline = self.git('commit-tree', core_tree, '-m', 'Baseline Core package')
        self.git('tag', 'v3.3.1', baseline)
        fixture = ci / 'consumer'
        shutil.copytree(Path(__file__).with_name('consumer'), fixture)
        contract_path = fixture / 'contract.json'
        contract = json.loads(contract_path.read_text())
        contract['baseline'].update(commit=baseline, tree=core_tree)
        contract_path.write_text(json.dumps(contract))
        demos = self.root / 'Packages' / candidate.DEMOS
        path = demos / 'package.json'
        manifest = json.loads(path.read_text())
        manifest.update(displayName='Fixture Demos', samples=[{'displayName': 'Fixture', 'path': 'Samples~/Fixture'}])
        path.write_text(json.dumps(manifest))
        sample = demos / 'Samples~/Fixture'; sample.mkdir(parents=True)
        (sample / 'fixture.cs').write_text('fixture sample')
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

    def test_inventory_uses_committed_bytes_despite_local_edits(self):
        self.change(candidate.CORE, 'fix: next patch')
        name = candidate.REQUIRED_FILES[0]
        original = (self.root / 'ci' / name).read_bytes()
        (self.root / 'ci' / name).write_bytes(b'uncommitted baseline\n')
        metadata = candidate.prepare(self.root, self.out)
        self.assertEqual((self.out / 'required' / name).read_bytes(), original)
        self.assertEqual(metadata['required'], candidate.hashes(self.out / 'required'))

    def test_changed_inventory_files_rejected(self):
        self.prepare()
        for name in candidate.REQUIRED_FILES:
            with self.subTest(name=name):
                path = self.out / 'required' / name
                original = path.read_bytes(); path.write_bytes(original + b'changed')
                with self.assertRaisesRegex(ValueError, 'required inventory changed'):
                    candidate.read_candidate(self.out)
                path.write_bytes(original)

    def test_missing_or_extra_inventory_files_rejected(self):
        self.prepare()
        path = self.out / 'required' / candidate.REQUIRED_FILES[0]
        original = path.read_bytes(); path.unlink()
        with self.assertRaisesRegex(ValueError, 'inventory files'):
            candidate.read_candidate(self.out)
        path.write_bytes(original)
        (self.out / 'required' / 'extra.txt').write_text('extra')
        with self.assertRaisesRegex(ValueError, 'inventory files'):
            candidate.read_candidate(self.out)

    def test_incomplete_inventory_manifest_rejected(self):
        self.prepare()
        path = self.out / 'candidate.json'
        metadata = json.loads(path.read_text())
        del metadata['required'][candidate.REQUIRED_FILES[0]]
        path.write_text(json.dumps(metadata))
        with self.assertRaisesRegex(ValueError, 'invalid candidate inventory'):
            candidate.read_candidate(self.out)

    def test_fixture_and_baseline_are_committed_immutable_bytes(self):
        path = self.root / 'ci/consumer/contract.json'
        committed = path.read_bytes()
        path.write_bytes(b'local invalid contract')
        metadata = candidate.prepare(self.root, self.out)
        self.assertEqual((self.out / 'consumer/contract.json').read_bytes(), committed)
        self.assertEqual(metadata['consumer']['files'], candidate.hashes(self.out / 'consumer'))
        with tarfile.open(self.out / 'baseline-core.tgz') as archive:
            self.assertEqual(json.load(archive.extractfile('package/package.json'))['version'], '3.3.1')
        candidate.read_candidate(self.out)

    def test_changed_missing_extra_fixture_and_baseline_rejected(self):
        self.prepare()
        path = self.out / 'consumer/contract.json'
        original = path.read_bytes()
        for action in ('change', 'remove', 'extra'):
            with self.subTest(action=action):
                extra = self.out / 'consumer/unexpected.txt'
                if action == 'change': path.write_bytes(original + b' ')
                elif action == 'remove': path.unlink()
                else: extra.write_text('unreviewed')
                with self.assertRaisesRegex(ValueError, 'consumer fixture'):
                    candidate.read_candidate(self.out)
                path.write_bytes(original)
                extra.unlink(missing_ok=True)
        archive = self.out / 'baseline-core.tgz'
        archive.write_bytes(archive.read_bytes() + b'changed')
        with self.assertRaisesRegex(ValueError, 'baseline archive'):
            candidate.read_candidate(self.out)

    def test_recovery_freezes_fixture_and_baseline_from_package_source(self):
        old_contract = (self.root / 'ci/consumer/contract.json').read_bytes()
        path = self.root / 'ci/consumer/contract.json'
        data = json.loads(path.read_text())
        data['scenarios'].append('future-scenario')
        path.write_text(json.dumps(data))
        self.change(candidate.CORE, 'fix: evolve consumer fixture')
        with patch.object(candidate, 'release_exists', return_value=False):
            recovery = candidate.prepare(self.root, self.out, 'fixture/repo')
        self.assertEqual((self.out / 'consumer/contract.json').read_bytes(), old_contract)
        normal_out = Path(self.temp.name) / 'normal'
        normal = candidate.prepare(self.root, normal_out)
        self.assertEqual((normal_out / 'consumer/contract.json').read_bytes(), path.read_bytes())
        self.assertNotEqual(recovery['consumer'], normal['consumer'])
        self.assertEqual(recovery['baseline']['files'], normal['baseline']['files'])

    def test_historical_recovery_does_not_use_head_fixture(self):
        fixture = self.root / 'ci/consumer'
        backup = Path(self.temp.name) / 'fixture'
        shutil.move(fixture, backup)
        self.commit('chore: bump version to 3.3.1 [skip ci]')
        shutil.copytree(backup, fixture)
        self.change(candidate.CORE, 'fix: restore fixture on newer source')
        with patch.object(candidate, 'release_exists', return_value=False):
            with self.assertRaisesRegex(ValueError, 'cannot read consumer fixture'):
                candidate.prepare(self.root, self.out, 'fixture/repo')

    def test_wrong_baseline_tree_rejected(self):
        path = self.root / 'ci/consumer/contract.json'
        contract = json.loads(path.read_text())
        contract['baseline']['tree'] = '0' * 40
        path.write_text(json.dumps(contract))
        self.commit('test: incorrect baseline')
        with self.assertRaisesRegex(ValueError, 'baseline tree mismatch'):
            candidate.prepare(self.root, self.out)

    def test_historical_recovery_does_not_fall_back_to_current_inventory(self):
        path = self.root / 'ci/required-unity-evidence.json'
        original = path.read_bytes(); path.unlink()
        self.commit('chore: bump version to 3.3.1 [skip ci]')
        path.write_bytes(original)
        self.change(candidate.CORE, 'fix: newer change with inventory restored')
        with patch.object(candidate, 'release_exists', return_value=False):
            with self.assertRaisesRegex(ValueError, 'cannot read release test inventory'):
                candidate.prepare(self.root, self.out, 'fixture/repo')


class InventoryRecoveryTests(unittest.TestCase):
    setUp = CandidateTests.setUp
    git = CandidateTests.git
    commit = CandidateTests.commit
    change = CandidateTests.change

    def evidence(self, required_dir, label, input_mode):
        """Run real prepare/collect/CLI verification around synthetic Unity observations."""
        settings = self.root / 'ProjectSettings/ProjectSettings.asset'
        settings.parent.mkdir(exist_ok=True)
        settings.write_text(f'  activeInputHandler: {input_mode}\n')
        out = self.root / f'artifacts/{label}-core-{input_mode}'
        core_tests.v.prepare(self.root, self.root / 'Packages' / candidate.CORE,
                             out, '6000.3.16f1', input_mode)
        files = core_tests.write_results(self.root / 'Library/P0E-Evidence', required_dir)
        core_tests.v.collect(out, {k: v for k, v in files.items() if k.endswith(('_xml', '_log'))})
        command = ['python3', str(Path(__file__).with_name('verify-unity-results.py').resolve()),
                   'verify', '--out', str(out), '--required-dir', str(required_dir)]
        result = subprocess.run(command, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        sample_out = self.root / f'artifacts/{label}-samples-{input_mode}'
        sample_tests.samples.prepare(self.root, sample_out, '6000.3.16f1', input_mode)
        xml, log = sample_tests.write_results(sample_out, required_dir, input_mode)
        command = ['python3', str(Path(__file__).with_name('verify-sample-results.py').resolve()),
                   'verify', '--root', str(self.root), '--out', str(sample_out),
                   '--xml', str(xml), '--log', str(log), '--required-dir', str(required_dir)]
        result = subprocess.run(command, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        return out, sample_out, xml, log

    def check_recovery(self, operation):
        for name in candidate.REQUIRED_FILES[:-1]:
            path = self.root / 'ci' / name
            names = path.read_text().splitlines()
            if operation == 'add':
                names.append(name + '.Added')
            elif operation == 'remove':
                names.pop()
            else:
                names[-1] = name + '.Renamed'
            path.write_text('\n'.join(names) + '\n')
        path = self.root / 'ci/required-unity-evidence.json'
        data = json.loads(path.read_text())
        if operation == 'add':
            data['session_cases'].append({'name': 'New/session', 'require_quit_on_exit': True})
            data['exceptions']['play'].append(data['exceptions']['play'][0])
        elif operation == 'remove':
            data['session_cases'].pop()
            data['exceptions']['play'].pop()
        else:
            data['session_cases'][0]['name'] = 'Renamed/session'
            data['exceptions']['play'][0] = 'InvalidOperationException: renamed callback'
        data['phases'] = ['next-' + phase for phase in data['phases']]
        path.write_text(json.dumps(data))
        self.change(candidate.CORE, 'fix: evolve tests after interrupted release')
        head = self.git('rev-parse', 'HEAD')
        self.out = self.root / 'artifacts/candidate'
        with patch.object(candidate, 'release_exists', return_value=False):
            recovery = candidate.prepare(self.root, self.out, 'fixture/repo')
        self.assertTrue(recovery['recovery'])
        self.assertEqual(recovery['package_source'], self.start)
        candidate.apply(self.root, self.out)
        candidate.import_samples(self.root, ['all'])
        old = {mode: self.evidence(self.out / 'required', 'recovery', mode) for mode in (0, 1)}

        self.git('restore', '--source=HEAD', '--worktree', '--', 'Packages')
        normal_out = Path(self.temp.name) / 'normal'
        with patch.object(candidate, 'release_exists', return_value=True):
            normal = candidate.prepare(self.root, normal_out, 'fixture/repo')
        self.assertFalse(normal['recovery'])
        self.assertEqual(normal['package_source'], head)
        self.assertEqual(normal['version'], '3.3.2')
        candidate.apply(self.root, self.out)
        for mode, (core_out, sample_out, xml, log) in old.items():
            (self.root / 'ProjectSettings/ProjectSettings.asset').write_text(f'  activeInputHandler: {mode}\n')
            with self.assertRaisesRegex(ValueError, 'discovery mismatch'):
                core_tests.v.verify(core_out, normal_out / 'required')
            with self.assertRaisesRegex(ValueError, 'sample suite mismatch'):
                sample_tests.samples.verify(self.root, sample_out, xml, log, normal_out / 'required')

        self.git('restore', '--source=HEAD', '--worktree', '--', 'Packages')
        shutil.rmtree(self.out)
        shutil.copytree(normal_out, self.out)
        candidate.apply(self.root, self.out)
        candidate.import_samples(self.root, ['all'])
        for mode in (0, 1):
            self.evidence(self.out / 'required', 'normal', mode)

    def test_added_tests_and_expectations_use_their_package_source(self):
        self.check_recovery('add')

    def test_removed_tests_and_expectations_use_their_package_source(self):
        self.check_recovery('remove')

    def test_renamed_tests_and_expectations_use_their_package_source(self):
        self.check_recovery('rename')

    def test_workflow_selects_frozen_inventory_for_both_verifiers(self):
        workflow = (Path(__file__).parent.parent / '.github/workflows/publish-upm.yml').read_text()
        for script in ('verify-unity-results.py', 'verify-sample-results.py'):
            lines = [line for line in workflow.splitlines() if script + ' verify ' in line]
            self.assertEqual(len(lines), 1)
            self.assertIn('--required-dir artifacts/candidate/required', lines[0])


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
        ci = self.root / 'ci'; ci.mkdir(exist_ok=True)
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
