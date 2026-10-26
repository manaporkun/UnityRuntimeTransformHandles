import copy
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import time
import unittest
import xml.etree.ElementTree as ET
import zlib


def load_module(name, file):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(file))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


verifier = load_module('consumer_verifier', 'verify-consumer-results.py')
driver = load_module('consumer_driver', 'run-consumer-player.py')


def vector(x=0, y=0, z=0):
    return dict(x=x, y=y, z=z)


def png(hover=False, custom=False, magenta=False, panel=False, blank=False, shade=1):
    width, height = 320, 240
    data = bytearray(bytes((30, 35, 45)) * width * height)
    def square(x, y, color, radius=20):
        for row in range(y - radius, y + radius + 1):
            for col in range(x - radius, x + radius + 1):
                offset = (row * width + col) * 3
                data[offset:offset + 3] = bytes(color)
    if not blank:
        for x in (80, 240): square(x, 120, (255, 0, 255) if magenta else tuple(round(value * shade) for value in (230, 140, 40)))
        square(120, 90, ((0, 255, 255) if custom else (255, 255, 255)) if hover else (255, 20, 20), 5)
    if panel: square(160, 120, (250, 250, 250), 50)
    raw = b''.join(b'\0' + data[row * width * 3:(row + 1) * width * 3] for row in range(height))
    def chunk(kind, value):
        return struct.pack('>I', len(value)) + kind + value + struct.pack('>I', zlib.crc32(kind + value))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b'')


class ConsumerEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.out = Path(self.temp.name)
        self.expected = dict(runId='fresh-run', source='a' * 40, packageSource='b' * 40,
                             unity='2021.3.0f1', input=0, urp=False, lifecycleStage='core-build',
                             buildId='c' * 32, buildSha256='d' * 64)

    def write(self, customized=False, withheld=False, session='session'):
        self.customized = customized
        observation = {**self.expected, 'schema': 1, 'session': session, 'stage': 'ready', 'nonce': session,
                       'pipeline': 'Built-in', 'platform': 'LinuxPlayer', 'frame': 30, 'width': 320, 'height': 240, 'focused': True,
                       'missingScripts': 0, 'cameraPresent': True,
                       'cameraBackground': dict(r=30/255, g=35/255, b=45/255, a=1),
                       'errors': [], 'shaderSupported': True, 'positions': [vector(-1.25), vector(1.25)],
                       'rotations': [vector(z=30), vector(z=30)], 'scales': [vector(1, 1, 1), vector(1, 1, 1)],
                       'targetPixels': [dict(x=80, y=120), dict(x=240, y=120)], 'handleX': dict(x=120, y=90),
                       'targetColors': [dict(r=1 if customized else .95, g=.6 if customized else .5, b=.1 if customized else .08, a=1)] * 2,
                       'handleRing': dict(x=120, y=90),
                       'starts': 0, 'changes': 0, 'ends': 0, 'destroyed': 0, 'selections': 0, 'members': 0,
                       'liveHandles': 0, 'liveGhosts': 0, 'grouped': False, 'handleEnabled': False,
                       'type': 'none', 'space': 'none', 'pivot': vector(), 'handleRotation': vector(), 'scene': 'Default',
                       'rotationShortcut': 'T' if customized else 'E',
                       'handleSize': 160 if customized else 192,
                       'highlightColor': dict(r=0.0 if customized else 1.0, g=1.0, b=1.0, a=1.0)}
        (self.out / 'ready.json').write_text(json.dumps(observation))
        (self.out / 'ready.png').write_bytes(png())
        report = {'schema': 1, 'runId': self.expected['runId'], 'session': session, 'customized': customized,
                  **{key: self.expected[key] for key in ('lifecycleStage', 'buildId', 'buildSha256')},
                  'withheld': withheld, 'driverCompleted': True, 'exitCode': 0, 'windowId': '123', 'pid': 456,
                  'readyReceivedSeconds': 0,
                  'checkpoints': [], 'input': []}
        stages = list(verifier.PLAYER_STAGES)
        if customized: stages.insert(stages.index('rotation-mode'), 'old-shortcut')
        for index, stage in enumerate(stages):
            observation = copy.deepcopy(observation)
            observation.update(stage=stage, nonce='nonce-' + str(index), frame=60 + index * 30)
            if not withheld:
                if stage == 'selection': observation.update(members=1, selections=1, handleEnabled=True, type='Position', space='World', liveHandles=1, liveGhosts=1)
                if stage == 'move':
                    observation['positions'][0]['x'] += 1
                    observation.update(starts=1, ends=1, changes=12)
                if stage == 'rotation-mode': observation['type'] = 'Rotation'
                if stage == 'rotate':
                    observation['rotations'][0]['z'] += 60
                    observation.update(starts=2, ends=2, changes=24)
                if stage == 'scale-mode': observation['type'] = 'Scale'
                if stage == 'scale':
                    observation['scales'][0]['x'] += .4
                    observation.update(starts=3, ends=3, changes=36)
                if stage == 'local': observation.update(type='Position', space='Self', handleRotation=vector(z=90))
                if stage == 'world': observation.update(space='World', handleRotation=vector())
                if stage == 'group': observation.update(members=2, selections=2, grouped=True, destroyed=1)
                if stage == 'group-move':
                    for position in observation['positions']: position['x'] += 1
                    observation.update(starts=4, ends=4, changes=48, pivot=vector(1.5))
                if stage == 'disable-cleanup': observation.update(starts=5, ends=5, changes=60, handleEnabled=False)
                if stage == 'reenable': observation['handleEnabled'] = True
                if stage == 'scene-cleanup': observation.update(starts=6, ends=6, changes=72, members=0, liveHandles=0, liveGhosts=0, destroyed=2, scene='Next')
            filename = stage + '-' + observation['nonce']
            (self.out / (filename + '.json')).write_text(json.dumps(observation))
            (self.out / (filename + '.png')).write_bytes(png(hover=stage == 'hover' and not withheld, custom=customized))
            report['checkpoints'].append(dict(stage=stage, nonce=observation['nonce'], json=filename + '.json', frame=filename + '.png', requestedSeconds=index + .8, receivedSeconds=index + .9))
            move = ['mousemove', '--sync', '--window', '123', '120', '90']
            commands = []
            if stage in ('selection', 'hover'):
                commands.append(['mousemove', '--sync', '--window', '123', '80', '120'] if stage == 'selection' else move)
            if stage == 'selection': commands.append(['click', '1'])
            if stage in ('move', 'rotate', 'scale', 'group-move', 'disable-cleanup', 'scene-cleanup'):
                commands += [move, ['mousedown', '1'], *[['mousemove', '--sync', '--window', '123', str(120 + step * 5), '90'] for step in range(1, 13)]]
                if stage not in ('disable-cleanup', 'scene-cleanup'): commands.append(['mouseup', '1'])
            keys = {'rotation-mode': ['t' if customized else 'e'], 'scale-mode': ['r'], 'old-shortcut': ['e'],
                    'local': ['w'], 'world': ['x'], 'group': ['g'], 'disable-cleanup': ['d'], 'reenable': ['d'], 'scene-cleanup': ['n']}
            commands += [['key', '--window', '123', key] for key in keys.get(stage, [])]
            for number, command in enumerate(commands):
                report['input'].append(dict(stage=stage, nonce=observation['nonce'], argv=['xdotool', *command],
                                            withheld=withheld, completed=not withheld,
                                            seconds=index + number * .01, finishedSeconds=index + number * .01 + .001))
        (self.out / 'player.log').write_text('Initialize engine version: ' + self.expected['unity'] + ' (revision)\n')
        (self.out / 'input.json').write_text(json.dumps(report))
        self.rehash()

    def rehash(self):
        path = self.out / 'input.json'
        report = json.loads(path.read_text())
        report['files'] = {p.name: verifier.digest(p) for p in self.out.iterdir() if p.name != 'input.json'}
        path.write_text(json.dumps(report))

    def change(self, stage, **values):
        path = next(self.out.glob(stage + '-nonce-*.json'))
        observation = json.loads(path.read_text())
        observation.update(values)
        path.write_text(json.dumps(observation))
        self.rehash()

    def verify(self):
        return verifier.verify_player(self.out, self.expected, self.customized)

    def test_complete_default_player_observations_pass(self):
        self.write()
        self.verify()

    def test_persisted_custom_shortcut_and_actual_hover_pixels_pass(self):
        self.write(customized=True)
        self.verify()

    def test_withheld_input_fails_interaction_but_proves_no_autonomous_changes(self):
        self.write(withheld=True)
        verifier.verify_negative_control(self.out, self.expected)
        with self.assertRaisesRegex(ValueError, 'input control'):
            self.verify()
        self.change('selection', positions=[vector(999), vector(1.25)])
        with self.assertRaisesRegex(ValueError, 'manufactured transforms'):
            verifier.verify_negative_control(self.out, self.expected)

    def test_unchanged_targets_cannot_pass_a_real_move(self):
        self.write()
        self.change('move', positions=[vector(-1.25), vector(1.25)])
        with self.assertRaisesRegex(ValueError, 'Move/snap'):
            self.verify()

    def test_unbalanced_public_events_are_rejected(self):
        self.write()
        self.change('rotate', ends=1)
        with self.assertRaisesRegex(ValueError, 'public drag events'):
            self.verify()

    def test_wrong_source_version_pipeline_or_unfocused_window_fail(self):
        self.write()
        for values in ({'source': 'wrong'}, {'unity': '2021.3.45f1'}, {'pipeline': 'URP'}, {'focused': False}, {'errors': ['Missing script']}):
            with self.subTest(values=values):
                original = next(self.out.glob('selection-nonce-*.json')).read_text()
                self.change('selection', **values)
                with self.assertRaises(ValueError): self.verify()
                next(self.out.glob('selection-nonce-*.json')).write_text(original)
                self.rehash()

    def test_missing_stage_or_duplicate_nonce_fails(self):
        self.write()
        path = self.out / 'input.json'
        original = path.read_text()
        report = json.loads(original); report['checkpoints'].pop()
        path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'player stage'): self.verify()
        report = json.loads(original); report['checkpoints'][1]['nonce'] = report['checkpoints'][0]['nonce']
        path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'duplicate input nonce'): self.verify()

    def test_changed_frames_and_magenta_targets_fail(self):
        self.write()
        frame = next(self.out.glob('hover-nonce-*.png'))
        frame.write_bytes(png(magenta=True))
        with self.assertRaisesRegex(ValueError, 'evidence files'): self.verify()
        self.rehash()
        with self.assertRaisesRegex(ValueError, 'magenta target'): self.verify()

    def test_no_rendered_hover_change_fails_even_with_claimed_color(self):
        self.write()
        next(self.out.glob('hover-nonce-*.png')).write_bytes(png())
        self.rehash()
        with self.assertRaisesRegex(ValueError, 'hover did not change'): self.verify()

    def test_nonzero_exit_is_rejected(self):
        self.write()
        path = self.out / 'input.json'
        report = json.loads(path.read_text()); report['exitCode'] = 1
        path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'exited nonzero'): self.verify()

    def test_shutdown_exception_outside_probe_lifetime_is_rejected(self):
        self.write()
        log = self.out / 'player.log'
        log.write_text(log.read_text() + 'InvalidOperationException: package teardown failed\n')
        self.rehash()
        with self.assertRaisesRegex(ValueError, 'exception'): self.verify()

    def test_read_only_commands_cannot_manufacture_interaction_evidence(self):
        self.write()
        path = self.out / 'input.json'
        report = json.loads(path.read_text())
        for item in report['input']: item['argv'] = ['xdotool', 'getactivewindow']
        path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'external pointer'): self.verify()

    def test_input_after_capture_cannot_justify_an_observation(self):
        self.write()
        path = self.out / 'input.json'
        report = json.loads(path.read_text())
        for item in report['input']:
            item['seconds'] += 100
            item['finishedSeconds'] += 100
        path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'after its captured checkpoint'): self.verify()

    def test_stationary_pointer_cannot_claim_a_drag(self):
        self.write()
        path = self.out / 'input.json'
        report = json.loads(path.read_text())
        for item in report['input']:
            if item['stage'] == 'move' and item['argv'][1] == 'mousemove': item['argv'][-2:] = ['120', '90']
        path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'did not actually move'): self.verify()

    def test_reversed_input_times_cannot_claim_a_drag(self):
        self.write()
        path = self.out / 'input.json'
        report = json.loads(path.read_text())
        items = [item for item in report['input'] if item['stage'] == 'move']
        times = [(item['seconds'], item['finishedSeconds']) for item in reversed(items)]
        for item, (start, finish) in zip(items, times): item.update(seconds=start, finishedSeconds=finish)
        path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'reordered external input'): self.verify()

    def test_motion_after_release_cannot_claim_a_drag(self):
        self.write()
        path = self.out / 'input.json'
        report = json.loads(path.read_text())
        indices = [index for index, item in enumerate(report['input']) if item['stage'] == 'move']
        items = [report['input'][index] for index in indices]
        up = next(item for item in items if item['argv'][1] == 'mouseup')
        commands = [item for item in items if item is not up]
        commands.insert(2, up)
        for number, item in enumerate(commands): item.update(seconds=2 + number * .01, finishedSeconds=2 + number * .01 + .001)
        for index, item in zip(indices, commands): report['input'][index] = item
        path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'motion while held'): self.verify()

    def test_reused_frame_numbers_cannot_claim_fresh_player_observations(self):
        self.write()
        self.change('selection', frame=30)
        with self.assertRaisesRegex(ValueError, 'stale rendered player frame'): self.verify()

    def test_real_subprocess_early_exit_leaves_failure_diagnostics(self):
        folder = self.out / 'build'
        folder.mkdir()
        executable = folder / 'failed-player'
        executable.write_text('#!/bin/sh\nexit 7\n')
        executable.chmod(0o755)
        out = self.out / 'failed-evidence'
        files = driver.build_hashes(folder)
        build = {**self.expected, 'files': files, 'buildSha256': driver.build_digest(files)}
        manifest = self.out / 'build.json'
        manifest.write_text(json.dumps(build))
        instance = driver.PlayerDriver(executable, out, 'fresh-run', deadline=.5, build_manifest=manifest)
        try:
            with self.assertRaisesRegex(ValueError, 'player exited'):
                instance.wait_json(out / 'ready.json')
        finally:
            instance.close(False)
        report = json.loads((out / 'input.json').read_text())
        self.assertFalse(report['driverCompleted'])
        self.assertEqual(report['exitCode'], 7)

    def write_sample(self):
        self.write()
        self.contract = json.loads((Path(__file__).with_name('consumer') / 'contract.json').read_text())
        ready = json.loads((self.out / 'ready.json').read_text())
        for path in self.out.iterdir():
            if path.name not in ('ready.json', 'ready.png', 'player.log'): path.unlink()
        ui = self.contract['ui']
        help_button = dict(id=ui['help'], text='Help', x=40, y=30, width=30, height=20, enabled=True)
        close_button = dict(id=ui['closeHelp'], text='Close', x=240, y=30, width=30, height=20, enabled=True)
        ready.update(scene=self.contract['samples']['Demo']['scene'], visibleRenderers=2, liveHandles=1,
                     visuals=[dict(kind='target', color=dict(r=230/255, g=140/255, b=40/255), left=60, right=100, top=100, bottom=140),
                              dict(kind='handle', color=dict(r=1, g=20/255, b=20/255), left=115, right=125, top=85, bottom=95)],
                     controls=[help_button], labels=['Handle showcase'])
        (self.out / 'ready.json').write_text(json.dumps(ready))
        report = dict(schema=1, runId=self.expected['runId'], session='session', customized=False, withheld=False,
                      **{key: self.expected[key] for key in ('lifecycleStage', 'buildId', 'buildSha256')},
                      driverCompleted=True, exitCode=0, windowId='123', pid=456, readyReceivedSeconds=0,
                      sample='Demo', checkpoints=[], input=[])
        for index, stage in enumerate(('help', 'close-help')):
            observation = copy.deepcopy(ready)
            observation.update(stage=stage, nonce='ui-' + str(index), frame=60 + 30 * index,
                               controls=[help_button, close_button] if stage == 'help' else [help_button])
            stem = stage + '-ui-' + str(index)
            (self.out / (stem + '.json')).write_text(json.dumps(observation))
            (self.out / (stem + '.png')).write_bytes(png(panel=stage == 'help'))
            report['checkpoints'].append(dict(stage=stage, nonce=observation['nonce'], json=stem + '.json', frame=stem + '.png', requestedSeconds=index + .8, receivedSeconds=index + .9))
            x = '40' if stage == 'help' else '240'
            for number, command in enumerate((['mousemove', '--sync', '--window', '123', x, '30'], ['click', '1'])):
                report['input'].append(dict(stage=stage, nonce=observation['nonce'], argv=['xdotool', *command],
                                           completed=True, withheld=False, seconds=index + number * .01, finishedSeconds=index + number * .01 + .001))
        (self.out / 'input.json').write_text(json.dumps(report))
        self.rehash()

    def test_workspace_help_requires_real_click_and_visible_state(self):
        self.write_sample()
        verifier.verify_sample_player(self.out, self.expected, self.contract, 'Demo')
        next(self.out.glob('help-ui-0.png')).write_bytes(png())
        self.rehash()
        with self.assertRaisesRegex(ValueError, 'no visible rendered change'):
            verifier.verify_sample_player(self.out, self.expected, self.contract, 'Demo')

    def test_workspace_click_at_another_coordinate_is_rejected(self):
        self.write_sample()
        path = self.out / 'input.json'
        report = json.loads(path.read_text())
        report['input'][0]['argv'][-2:] = ['150', '200']
        path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'did not hit'):
            verifier.verify_sample_player(self.out, self.expected, self.contract, 'Demo')

    def test_player_from_another_lifecycle_build_is_rejected(self):
        self.write()
        for key in ('lifecycleStage', 'buildId', 'buildSha256'):
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, 'another lifecycle build'):
                verifier.verify_player(self.out, {**self.expected, key: 'other'})
        self.change('selection', buildId='other')
        with self.assertRaisesRegex(ValueError, 'another lifecycle build'):
            self.verify()

    def test_lost_effective_material_customization_is_rejected(self):
        self.write(customized=True)
        self.change('selection', targetColors=[dict(r=.95, g=.5, b=.08, a=1)] * 2)
        with self.assertRaisesRegex(ValueError, 'material'):
            self.verify()

    def test_blank_world_with_working_help_panel_cannot_pass_showcase(self):
        self.write_sample()
        for path in self.out.glob('*.png'):
            path.write_bytes(png(blank=True, panel=path.name.startswith('help-')))
        self.rehash()
        with self.assertRaisesRegex(ValueError, 'visible target AND applicable handle pixels'):
            verifier.verify_sample_player(self.out, self.expected, self.contract, 'Demo')

    def test_dark_material_cannot_turn_background_pixels_into_a_target(self):
        self.write_sample()
        ready = json.loads((self.out / 'ready.json').read_text())
        ready.update(liveHandles=0, visuals=[dict(kind='target', color=dict(r=.1, g=.1, b=.1), left=60, right=100, top=100, bottom=140)])
        (self.out / 'ready.json').write_text(json.dumps(ready))
        (self.out / 'ready.png').write_bytes(png(blank=True))
        self.rehash()
        with self.assertRaisesRegex(ValueError, 'visible target'):
            verifier.verify_rendered_visuals(ready, verifier.read_frame(self.out / 'ready.png'))

    def test_existing_opaque_shader_minimum_shade_still_proves_visible_targets(self):
        self.write_sample()
        for path in self.out.glob('*.png'):
            path.write_bytes(png(shade=.55, panel=path.name.startswith('help-')))
        self.rehash()
        verifier.verify_sample_player(self.out, self.expected, self.contract, 'Demo')

    def test_checkpoint_names_follow_the_frozen_stage_nonce_contract(self):
        self.write()
        path = self.out / 'input.json'
        report = json.loads(path.read_text())
        checkpoint = report['checkpoints'][0]
        for field, suffix in (('json', '.json'), ('frame', '.png')):
            old = self.out / checkpoint[field]
            checkpoint[field] = 'arbitrary' + suffix
            old.rename(self.out / checkpoint[field])
        path.write_text(json.dumps(report))
        self.rehash()
        with self.assertRaisesRegex(ValueError, 'output contract'):
            self.verify()

    def test_gallery_contract_cannot_drop_a_workspace(self):
        contract = json.loads((Path(__file__).with_name('consumer') / 'contract.json').read_text())
        contract['ui']['galleryOrder'].pop()
        with self.assertRaisesRegex(ValueError, 'omit a workspace'):
            verifier.sample_stages('gallery', contract)


class ConsumerTestXmlTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        folder = Path(self.temp.name)
        self.xml, self.log, self.inventory = [folder / name for name in ('tests.xml', 'tests.log', 'required.txt')]
        self.started = time.time() - 2
        self.finished = time.time() + 2
        stamp = datetime.now(timezone.utc).isoformat()
        self.root = ET.Element('test-run', result='Passed', total='2', passed='2', testcasecount='2', failed='0', skipped='0', inconclusive='0',
                               **{'start-time': stamp, 'end-time': stamp})
        for name in ('Consumer.One', 'Consumer.Two'): ET.SubElement(self.root, 'test-case', fullname=name, result='Passed')
        self.inventory.write_text('Consumer.One\nConsumer.Two\n')
        self.expected = dict(runId='new-run', unity='2021.3.0f1')
        self.log.write_text('Initialize engine version: 2021.3.0f1 (revision)\nCONSUMER_INVOCATION new-run core-tests\n')

    def verify(self):
        ET.ElementTree(self.root).write(self.xml)
        return verifier.verify_tests(self.xml, self.log, self.inventory, self.expected, 'core-tests', self.started, self.finished)

    def test_exact_fresh_narrow_inventory_passes(self):
        self.assertEqual(self.verify()['passed'], 2)

    def test_summary_cannot_claim_another_test_count(self):
        for key in ('total', 'passed', 'testcasecount'):
            self.root.set(key, '3')
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, 'summary counts'): self.verify()
            self.root.set(key, '2')

    def test_extra_duplicate_missing_or_skipped_case_fails(self):
        case = self.root[0]
        for attribute, value in (('fullname', 'unknown'), ('fullname', 'Consumer.Two'), ('result', 'Skipped')):
            original = case.get(attribute)
            case.set(attribute, value)
            with self.subTest(value=value), self.assertRaises(ValueError): self.verify()
            case.set(attribute, original)

    def test_stale_or_wrong_run_log_fails(self):
        self.root.set('start-time', '2020-01-01T00:00:00Z')
        with self.assertRaisesRegex(ValueError, 'stale'): self.verify()
        self.root.set('start-time', self.root.get('end-time'))
        self.log.write_text(self.log.read_text().replace('new-run', 'old-run'))
        with self.assertRaisesRegex(ValueError, 'bound'): self.verify()


if __name__ == '__main__':
    unittest.main()
