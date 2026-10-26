import importlib.util
import copy
import hashlib
import json
from pathlib import Path
import shutil
import tarfile
import unittest

import test_release_candidate as release_tests
import test_verify_consumer_results as player_tests

spec = importlib.util.spec_from_file_location('consumer_project', Path(__file__).with_name('consumer-project.py'))
consumer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(consumer)


class ConsumerProjectTests(unittest.TestCase):
    setUp = release_tests.CandidateTests.setUp
    git = release_tests.CandidateTests.git
    commit = release_tests.CandidateTests.commit
    change = release_tests.CandidateTests.change

    def prepare(self, profile='2021-legacy', transport='tarball'):
        self.metadata = consumer.candidate.prepare(self.root, self.out)
        self.work = Path(self.temp.name) / 'work'
        self.config = consumer.prepare(self.out, self.work, profile, transport)
        self.project = self.work / 'projects/core'
        return self.config

    def observation(self):
        folder = self.project / 'Library/PackageCache' / (consumer.CORE + '@fixture')
        consumer.unpack_archive(self.out / (consumer.CORE + '.tgz'), folder, self.metadata['packages'][consumer.CORE]['files'])
        host = json.loads((self.work / 'candidate/consumer/contract.json').read_text())['profiles'][self.config['profile']]['dependencies']
        consumer.write_json(self.project / 'Packages/packages-lock.json', {'dependencies': {
            consumer.CORE: {'source': 'local-tarball', 'depth': 0, 'version': 'file:../.journey/distributions/' + consumer.CORE + '.tgz'}}})
        return {**{key: self.config[key] for key in ('runId', 'source', 'packageSource', 'unity', 'input')},
                'schema': 1, 'assemblies': [], 'stage': 'core-resolve', 'packages': [
                    {'name': consumer.CORE, 'version': self.metadata['version'], 'source': 'LocalTarball',
                     'resolvedPath': str(folder.resolve()), 'packageId': consumer.CORE + '@file:' + str(self.project.resolve() / '.journey/distributions' / (consumer.CORE + '.tgz'))},
                    *[{'name': name, 'version': version, 'source': 'Registry', 'resolvedPath': '/registry/' + name,
                       'packageId': name + '@' + version} for name, version in host.items()]]}

    def test_reviewed_eight_entry_coverage_is_current_policy(self):
        scopes = [consumer.scope_for(profile, transport) for profile in consumer.PROFILES for transport in ('tarball', 'git')]
        self.assertEqual(scopes.count('full'), 5)
        self.assertEqual(scopes.count('probe'), 3)
        with self.assertRaisesRegex(ValueError, 'matrix'):
            consumer.scope_for('unknown', 'git')

    def test_fresh_core_only_minimum_project_uses_upm_tarball(self):
        self.prepare()
        manifest = json.loads((self.project / 'Packages/manifest.json').read_text())
        self.assertTrue(manifest['dependencies'][consumer.CORE].startswith('file:../.journey/distributions/'))
        for name in (consumer.DEMOS, 'com.unity.ugui', 'com.unity.inputsystem', 'com.unity.render-pipelines.universal'):
            self.assertNotIn(name, manifest['dependencies'])
        for name in ('Library', 'Packages/packages-lock.json', 'Packages/' + consumer.CORE):
            self.assertFalse((self.project / name).exists())
        self.assertEqual((self.project / 'ProjectSettings/ProjectVersion.txt').read_text(), 'm_EditorVersion: 2021.3.0f1\n')
        self.assertTrue((self.project / 'Assets/ConsumerJourney/Bootstrap/ConsumerBootstrap.cs').is_file())
        self.assertFalse((self.project / 'Assets/ConsumerJourney/Runtime').exists())

    def test_refuses_existing_project_instead_of_reusing_library(self):
        self.prepare()
        with self.assertRaisesRegex(ValueError, 'fresh consumer'):
            consumer.prepare(self.out, self.work, '2021-legacy', 'tarball')
        with self.assertRaisesRegex(ValueError, 'already exists'):
            consumer.create_project(self.work, 'core')

    def test_git_distribution_shapes_use_real_committed_matching_bytes(self):
        self.prepare('6-input', 'git')
        self.assertIn('?path=/Packages/' + consumer.CORE + '#', self.config['pairCoreUrl'])
        self.assertEqual(self.config['pairCoreUrl'].split('#')[-1], self.config['pairDemosUrl'].split('#')[-1])
        root = self.work / 'distribution'
        self.assertEqual(consumer.candidate.git(root / 'pair', 'rev-parse', 'HEAD'), self.config['pairCommit'])
        self.assertEqual(consumer.candidate.hashes(root / 'pair/Packages' / consumer.CORE), self.metadata['packages'][consumer.CORE]['files'])
        self.assertEqual(consumer.candidate.git(root / 'core', 'rev-parse', 'v' + self.metadata['version']), self.config['coreCommit'])

    def test_scope_in_frozen_contract_cannot_downgrade_current_policy(self):
        self.prepare('6-urp', 'tarball')
        self.assertEqual(self.config['scope'], 'full')

    def test_same_candidate_core_is_retained_when_installing_demos(self):
        self.prepare('6-legacy', 'git')
        consumer.install_dependencies(self.work, self.project, demos=True)
        dependencies = json.loads((self.project / 'Packages/manifest.json').read_text())['dependencies']
        self.assertEqual(dependencies[consumer.CORE], self.config['pairCoreUrl'])
        self.assertEqual(dependencies[consumer.DEMOS], self.config['pairDemosUrl'])
        self.assertEqual(dependencies['com.unity.ugui'], '2.0.0')

    def test_actual_resolved_package_evidence_passes(self):
        self.prepare()
        consumer.verify_resolved(self.work, self.project, self.observation())

    def test_raw_resolution_rejects_conflicting_editor_manifest_and_lock(self):
        self.prepare()
        observation = self.observation()
        installed = consumer.verify_resolved(self.work, self.project, observation)
        snapshot = dict(observation=observation, manifest=json.loads((self.project / 'Packages/manifest.json').read_text()),
                        lock=json.loads((self.project / 'Packages/packages-lock.json').read_text()),
                        files={name: info['files'] for name, info in installed.items()})
        contract = json.loads((self.work / 'candidate/consumer/contract.json').read_text())
        def verify(value, stage='core-resolve'):
            return consumer.verify_resolution_snapshot(self.config, self.metadata, contract, 'core', stage, value)
        self.assertEqual(verify(snapshot), installed)
        for field in ('identity', 'cache', 'manifest', 'lock'):
            bad = copy.deepcopy(snapshot)
            if field == 'identity': bad['observation']['unity'] = 'wrong'
            if field == 'cache': bad['observation']['packages'][0]['resolvedPath'] = '/foreign/Library/PackageCache/' + consumer.CORE + '@fixture'
            if field == 'manifest': bad['manifest']['dependencies'][consumer.CORE] = None; bad['manifest']['dependencies']['com.unity.test-framework'] = 'wrong'
            if field == 'lock': bad['lock']['dependencies'][consumer.CORE]['source'] = 'embedded'
            with self.subTest(field=field), self.assertRaises(ValueError): verify(bad)
        removed = copy.deepcopy(snapshot)
        removed['observation'].update(stage='uninstall-verify', packages=observation['packages'][1:], assemblies=['TransformHandles.Runtime'])
        removed['manifest']['dependencies'].pop(consumer.CORE)
        removed['lock']['dependencies'].pop(consumer.CORE)
        removed['files'] = {}
        with self.assertRaisesRegex(ValueError, 'assembly remains'): verify(removed, 'uninstall-verify')
        removed['observation']['assemblies'] = []
        self.assertEqual(verify(removed, 'uninstall-verify'), {})
        removed['lock']['dependencies'][consumer.CORE] = {}
        with self.assertRaisesRegex(ValueError, 'reference remains'): verify(removed, 'uninstall-verify')

    def test_exact_import_inventory_and_customized_scene_preservation(self):
        contract = json.loads((Path(__file__).with_name('consumer') / 'contract.json').read_text())
        for name, sample in contract['samples'].items():
            folder = self.root / 'Packages' / consumer.DEMOS / 'Samples~' / name
            folder.mkdir()
            (folder / (sample['scene'] + '.unity')).write_text('original scene')
            (folder / (name + '.asmdef')).write_text(json.dumps({'name': 'TransformHandles.Samples.' + name}))
        self.commit('test: add reviewed import fixture')
        self.prepare()
        samples = []
        for name, sample in contract['samples'].items():
            relative = 'Assets/Samples/Fixture/' + name
            shutil.copytree(self.work / 'distribution/Packages' / consumer.DEMOS / 'Samples~' / name, self.project / relative)
            samples.append(dict(name=name, path=relative, scene=relative + '/' + sample['scene'] + '.unity', assembly='TransformHandles.Samples.' + name))
        path = self.project / '.journey/imports.json'
        original = dict(runId=self.config['runId'], stage='gallery-import', samples=samples)
        consumer.write_json(path, original)
        self.assertEqual(len(consumer.verify_imports(self.work, self.project)), 5)
        for inventory in ([], samples[:-1], samples + [samples[0]], samples[:-1] + [{**samples[-1], 'name': 'unknown'}]):
            consumer.write_json(path, {**original, 'samples': inventory})
            with self.assertRaisesRegex(ValueError, 'inventory'): consumer.verify_imports(self.work, self.project)
        consumer.write_json(path, original)
        showcase = samples[0]
        scene = self.project / showcase['scene']
        scene.write_text('consumer customization')
        state = json.loads((self.work / 'run.json').read_text())
        state['customImports'] = {s['path']: consumer.hashes(self.project / s['path']) for s in samples}
        consumer.write_json(self.work / 'run.json', state)
        consumer.verify_imports(self.work, self.project, customized=True)
        scene.write_text('original scene')
        with self.assertRaisesRegex(ValueError, 'overwritten'): consumer.verify_imports(self.work, self.project, customized=True)

    def test_stale_missing_identity_source_and_embedded_install_fail(self):
        self.prepare()
        observation = self.observation()
        for key, wrong in (('runId', 'old'), ('source', 'f' * 40), ('unity', '2021.3.45f1'), ('input', 1)):
            with self.subTest(key=key):
                bad = {**observation, key: wrong}
                with self.assertRaisesRegex(ValueError, 'mismatch'):
                    consumer.verify_resolved(self.work, self.project, bad)
        for key, value in (('source', 'Embedded'), ('resolvedPath', str(self.root / 'Packages' / consumer.CORE))):
            with self.subTest(package_key=key):
                bad = {**observation, 'packages': [{**observation['packages'][0], key: value}]}
                with self.assertRaises(ValueError):
                    consumer.verify_resolved(self.work, self.project, bad)

    def test_changed_package_files_and_unexpected_optional_package_fail(self):
        self.prepare()
        observation = self.observation()
        path = Path(observation['packages'][0]['resolvedPath']) / 'CHANGELOG.md'
        path.write_text('changed bytes')
        with self.assertRaisesRegex(ValueError, 'package bytes differ'):
            consumer.verify_resolved(self.work, self.project, observation)

    def test_only_generated_unity6_fingerprint_may_change(self):
        self.prepare('6-legacy')
        observation = self.observation()
        path = Path(observation['packages'][0]['resolvedPath']) / 'package.json'
        manifest = json.loads(path.read_text())
        manifest['_fingerprint'] = 'generated-by-unity'
        path.write_text(json.dumps(manifest))
        consumer.verify_resolved(self.work, self.project, observation)
        manifest['version'] = '999.0.0'
        path.write_text(json.dumps(manifest))
        with self.assertRaisesRegex(ValueError, 'version'):
            consumer.verify_resolved(self.work, self.project, observation)

    def test_duplicate_package_observations_fail(self):
        self.prepare()
        observation = self.observation()
        observation['packages'] *= 2
        with self.assertRaisesRegex(ValueError, 'duplicate'):
            consumer.verify_resolved(self.work, self.project, observation)

    def test_foreign_project_cache_and_symlink_escape_fail(self):
        self.prepare()
        observation = self.observation()
        folder = Path(observation['packages'][0]['resolvedPath'])
        bad = json.loads(json.dumps(observation))
        bad['packages'][0]['resolvedPath'] = '/unrelated/checkout/Library/PackageCache/' + folder.name
        with self.assertRaisesRegex(ValueError, 'outside this project'):
            consumer.verify_resolved(self.work, self.project, bad)
        moved = self.work / 'foreign-cache'
        folder.rename(moved)
        folder.symlink_to(moved, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, 'symlink'):
            consumer.verify_resolved(self.work, self.project, observation)

    def test_baseline_archive_content_is_verified_before_project_generation(self):
        self.prepare()
        baseline = self.work / 'distribution/baseline'
        self.assertEqual(consumer.hashes(baseline), self.metadata['baseline']['files'])
        with tarfile.open(self.work / 'candidate/baseline-core.tgz') as archive:
            self.assertEqual(json.load(archive.extractfile('package/package.json'))['version'], '3.3.1')

    def test_sample_standalone_browser_filter_has_exact_three_reviewed_cases(self):
        contract = json.loads((Path(__file__).with_name('consumer') / 'contract.json').read_text())
        filters = contract['samples']['DemoBrowser']['filter'].split(';')
        self.assertEqual(len(filters), 3)
        for mode in (0, 1):
            path = Path(__file__).with_name('consumer') / 'required' / f'DemoBrowser-input{mode}.txt'
            names = [line for line in path.read_text().splitlines() if line and not line.startswith('#')]
            self.assertEqual(set(names), set(filters))

    def test_full_policy_requires_every_independent_sample_and_destructive_stage(self):
        stages = consumer.required_stages('6-input', 'tarball')
        self.assertEqual(len(stages), 37)
        for sample in ('Demo', 'LevelEditor', 'ModularBuilding', 'ProductConfigurator', 'DemoBrowser'):
            for phase in ('import', 'build', 'tests'):
                self.assertIn('sample-' + sample + '-' + phase, stages)
        for stage in ('customize', 'custom-gallery-build', 'retained-core-build', 'retained-gallery-build',
                      'remove-imports-build', 'uninstall-core', 'uninstall-verify', 'upgrade-baseline',
                      'upgrade-resolve', 'upgrade-setup', 'upgrade-import', 'upgrade-tests', 'upgrade-build'):
            self.assertIn(stage, stages)
        self.assertEqual(consumer.required_stages('2021-legacy', 'git'), ['core-resolve', 'core-setup', 'core-build', 'pair-build'])

    def test_workflow_sequence_cannot_skip_to_a_build(self):
        self.prepare()
        with self.assertRaisesRegex(ValueError, 'reviewed order'):
            consumer.before_stage(self.work, 'core-build')
        result = consumer.before_stage(self.work, 'core-resolve')
        self.assertEqual(result['method'], 'ConsumerJourney.ConsumerBootstrap.Run')
        with self.assertRaisesRegex(ValueError, 'not complete'):
            consumer.before_stage(self.work, 'core-resolve')

    def test_incomplete_or_self_downgraded_journey_cannot_pass(self):
        self.prepare()
        verifier = consumer.load_verifier()
        with self.assertRaisesRegex(ValueError, 'lifecycle stage missing'):
            verifier.verify_journey(self.work, self.out, '2021-legacy', 'tarball')
        path = self.work / 'run.json'
        state = json.loads(path.read_text()); state['scope'] = 'probe'
        path.write_text(json.dumps(state))
        with self.assertRaisesRegex(ValueError, 'workflow-selected scope'):
            verifier.verify_journey(self.work, self.out, '2021-legacy', 'tarball')

    def test_controlled_removal_backs_up_only_named_dependent_assets(self):
        self.prepare()
        owned = self.project / 'Assets/UserConsumer'
        owned.mkdir(); (owned / 'Settings.asset').write_text('Core-dependent settings')
        (self.project / 'Assets/UserConsumer.meta').write_text('fileFormatVersion: 2\nguid: ' + 'a' * 32 + '\n')
        note = (self.project / 'Assets/UserNotes.txt').read_bytes()
        result = consumer.backup_remove(self.work, self.project, 'Assets/UserConsumer', 'explicit-core-backup')
        self.assertFalse(owned.exists())
        backup = self.work / result['backup']
        self.assertEqual(consumer.hashes(backup), result['files'])
        self.assertEqual(consumer.digest(backup.with_suffix('.meta')), result['meta_sha256'])
        self.assertEqual((self.project / 'Assets/UserNotes.txt').read_bytes(), note)
        with self.assertRaisesRegex(ValueError, 'unsafe removal'):
            consumer.backup_remove(self.work, self.project, '../UserNotes.txt', 'escape')

    def test_wrong_host_dependency_and_git_lock_revision_fail(self):
        self.prepare('6-input', 'git')
        observation = self.observation()
        observation['packages'][0].update(source='Git', packageId=consumer.CORE + '@' + self.config['coreUrl'])
        lock = {'dependencies': {consumer.CORE: {'source': 'git', 'version': self.config['coreUrl'], 'hash': self.config['coreCommit']}}}
        consumer.write_json(self.project / 'Packages/packages-lock.json', lock)
        consumer.verify_resolved(self.work, self.project, observation)
        lock['dependencies'][consumer.CORE]['hash'] = '0' * 40
        consumer.write_json(self.project / 'Packages/packages-lock.json', lock)
        with self.assertRaisesRegex(ValueError, 'lock revision'):
            consumer.verify_resolved(self.work, self.project, observation)

    def test_core_only_git_install_cannot_substitute_the_pair_route(self):
        self.prepare('6-input', 'git')
        observation = self.observation()
        observation['packages'][0].update(source='Git', packageId=consumer.CORE + '@' + self.config['pairCoreUrl'])
        manifest = json.loads((self.project / 'Packages/manifest.json').read_text())
        manifest['dependencies'][consumer.CORE] = self.config['pairCoreUrl']
        consumer.write_json(self.project / 'Packages/manifest.json', manifest)
        consumer.write_json(self.project / 'Packages/packages-lock.json', {'dependencies': {consumer.CORE: {
            'source': 'git', 'version': self.config['pairCoreUrl'], 'hash': self.config['pairCommit']}}})
        with self.assertRaisesRegex(ValueError, 'stage-selected distribution route'):
            consumer.verify_resolved(self.work, self.project, observation)

    def test_aggregate_probe_rejects_raw_editor_conflicts_and_reused_players(self):
        self.prepare('6-legacy', 'git')
        verifier = consumer.load_verifier()
        state = json.loads((self.work / 'run.json').read_text())
        stages = consumer.required_stages('6-legacy', 'git')
        state['completed'] = stages
        consumer.write_json(self.work / 'run.json', state)
        manifest = json.loads((self.project / 'Packages/manifest.json').read_text())
        contract = json.loads((self.work / 'candidate/consumer/contract.json').read_text())
        writer = player_tests.ConsumerEvidenceTests()
        writer.setUp()
        self.addCleanup(writer.doCleanups)
        for index, stage in enumerate(stages):
            out = self.work / 'evidence' / stage
            out.mkdir(parents=True)
            phase_manifest = copy.deepcopy(manifest)
            if stage == 'pair-build': phase_manifest['dependencies'].update({consumer.CORE: state['pairCoreUrl'], consumer.DEMOS: state['pairDemosUrl']})
            packages, locked, files = [], {}, {}
            for name, url in phase_manifest['dependencies'].items():
                if name in (consumer.CORE, consumer.DEMOS):
                    packages.append(dict(name=name, version=self.metadata['version'] if name == consumer.CORE else self.metadata['demos_version'], source='Git',
                                         resolvedPath=str(self.project.resolve() / 'Library/PackageCache' / (name + '@fixture')), packageId=name + '@' + url))
                    locked[name] = dict(source='git', version=url, hash=state['coreCommit'] if url == state['coreUrl'] else state['pairCommit'])
                    files[name] = self.metadata['packages'][name]['files']
                else: packages.append(dict(name=name, version=url, source='Registry'))
            observation = {key: state[key] for key in ('runId', 'source', 'packageSource', 'unity', 'input')}
            observation.update(schema=1, stage=stage, packages=packages, assemblies=[])
            resolution = dict(observation=observation, manifest=phase_manifest, lock=dict(dependencies=locked), files=files)
            record = dict(schema=1, runId=state['runId'], source=state['source'], packageSource=state['packageSource'], project='core', stage=stage,
                          started=index * 10, editorFinished=index * 10 + 1, finished=index * 10 + 2, editorPids=[str(index + 100)], builds=[], players=[], tests=None)
            record['installed'] = consumer.verify_resolution_snapshot(state, self.metadata, contract, 'core', stage, resolution)
            if stage.endswith('-build'):
                build_files = {'Consumer.x86_64': 'd' * 64}
                build_sha = hashlib.sha256(json.dumps(build_files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
                identity = dict(runId=state['runId'], lifecycleStage=stage, buildId=str(index) * 32, buildSha256=build_sha, files=build_files)
                observation.update(buildId=identity['buildId'], buildPath='.journey/build/' + stage + '/Consumer.x86_64', scenes=['Assets/Default.unity'])
                record['builds'] = [dict(path=observation['buildPath'], scenes=observation['scenes'], files=build_files, buildId=identity['buildId'], buildSha256=build_sha)]
                consumer.write_json(out / 'build.json', identity)
                for negative in ([False, True] if stage == 'core-build' else [False]):
                    destination = 'negative' if negative else 'player'
                    writer.out = out / destination
                    writer.out.mkdir()
                    writer.expected = {**state, **identity}
                    writer.write(withheld=negative, session=stage + '-' + destination)
                    record['players'].append(dict(path=destination, sample=None, customized=False, negative=negative))
            consumer.write_json(out / 'resolution.json', resolution)
            consumer.write_json(out / 'editor.json', observation)
            (out / 'editor.log').write_text('CONSUMER_ENGINE ' + state['unity'] + ' ' + state['runId'] + ' ' + stage + '\nCONSUMER_PID ' + str(index + 100) + '\nCONSUMER_INVOCATION ' + state['runId'] + ' ' + stage + '\n')
            record['files'] = consumer.hashes(out)
            consumer.write_json(out / 'record.json', record)
        summary = verifier.verify_journey(self.work, self.out, '6-legacy', 'git')
        self.assertEqual((summary['editorInvocations'], summary['nativeBuilds'], summary['playerRuns']), (4, 2, 3))
        out = self.work / 'evidence/pair-build'
        raw = json.loads((out / 'editor.json').read_text())
        consumer.write_json(out / 'editor.json', {**raw, 'unity': 'wrong', 'packages': []})
        def rehash():
            path = out / 'record.json'
            record = json.loads(path.read_text())
            record['files'] = {name: value for name, value in consumer.hashes(out).items() if name != 'record.json'}
            consumer.write_json(path, record)
        rehash()
        with self.assertRaisesRegex(ValueError, 'raw Editor observation differs'):
            verifier.verify_journey(self.work, self.out, '6-legacy', 'git')
        consumer.write_json(out / 'editor.json', raw)
        shutil.rmtree(out / 'player')
        shutil.copytree(self.work / 'evidence/core-build/player', out / 'player')
        rehash()
        with self.assertRaisesRegex(ValueError, 'another lifecycle build'):
            verifier.verify_journey(self.work, self.out, '6-legacy', 'git')


if __name__ == '__main__':
    unittest.main()
