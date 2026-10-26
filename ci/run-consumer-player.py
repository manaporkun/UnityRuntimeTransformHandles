#!/usr/bin/env python3
"""Drive a real Linux Unity window with xdotool; keep nonce-bound input/frame evidence."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import signal
import subprocess
import time
import uuid


def require(condition, message):
    if not condition:
        raise ValueError(message)


def write_json(path, data):
    temporary = path.with_suffix('.pending')
    temporary.write_text(json.dumps(data, indent=2) + '\n')
    temporary.replace(path)


def build_hashes(folder):
    require(not any(p.is_symlink() for p in folder.rglob('*')), 'native build contains symlinks')
    return {p.relative_to(folder).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(folder.rglob('*')) if p.is_file() and p.name != '.DS_Store'}


def build_digest(files):
    return hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


class PlayerDriver:
    def __init__(self, player, out, run_id, customized=False, withhold=False, deadline=30, build_manifest=None):
        require(build_manifest is not None, 'an externally selected native build manifest is required')
        self.build = json.loads(build_manifest.read_text())
        require(self.build['runId'] == run_id and build_hashes(player.resolve().parent) == self.build['files'], 'executed player bytes differ from selected build')
        require(build_digest(self.build['files']) == self.build['buildSha256'], 'native build manifest digest mismatch')
        require(not out.exists(), 'use a fresh player evidence directory')
        out.mkdir(parents=True)
        self.out, self.deadline = out.resolve(), deadline
        self.session, self.run_id = uuid.uuid4().hex, run_id
        self.customized, self.withhold = customized, withhold
        self.transcript, self.checkpoints = [], []
        self.stage, self.nonce = 'startup', self.session
        self.window = None
        self.ready_received = None
        self.process = subprocess.Popen([
            str(player.resolve()), '-popupwindow', '-screen-fullscreen', '0', '-screen-width', '960',
            '-screen-height', '720', '-force-glcore', '-noaudio', '-consumerEvidence', str(self.out),
            '-consumerSession', self.session, '-logFile', str(self.out / 'player.log')],
            env={**os.environ, 'LIBGL_ALWAYS_SOFTWARE': '1'}, start_new_session=True)

    def wait_json(self, path):
        deadline = time.monotonic() + self.deadline
        while time.monotonic() < deadline:
            require(self.process.poll() is None, 'player exited before checkpoint: ' + self.stage)
            if path.is_file():
                data = json.loads(path.read_text())
                require(data.get('runId') == self.run_id and data.get('session') == self.session,
                        'stale or mismatched player checkpoint')
                return data
            time.sleep(.05)
        raise TimeoutError('player checkpoint timed out: ' + self.stage)

    def command(self, *arguments, interaction=True):
        entry = {'stage': self.stage, 'nonce': self.nonce, 'seconds': time.monotonic(),
                 'argv': ['xdotool', *map(str, arguments)], 'withheld': bool(self.withhold and interaction)}
        self.transcript.append(entry)
        if entry['withheld']:
            return ''
        result = subprocess.run(entry['argv'], check=True, text=True, capture_output=True, timeout=5)
        entry['completed'] = True
        entry['finishedSeconds'] = time.monotonic()
        return result.stdout.strip()

    def begin(self, stage):
        self.stage, self.nonce = stage, uuid.uuid4().hex

    def capture(self):
        requested = time.monotonic()
        write_json(self.out / 'request.json', {'stage': self.stage, 'nonce': self.nonce})
        stem = self.stage + '-' + self.nonce
        data = self.wait_json(self.out / (stem + '.json'))
        require(data.get('nonce') == self.nonce and data.get('stage') == self.stage, 'wrong checkpoint nonce/stage')
        require((self.out / (stem + '.png')).is_file(), 'checkpoint has no captured frame')
        self.checkpoints.append({'stage': self.stage, 'nonce': self.nonce, 'json': stem + '.json', 'frame': stem + '.png',
                                 'requestedSeconds': requested, 'receivedSeconds': time.monotonic()})
        self.last_frame = data['frame']
        return data

    def move(self, point):
        self.command('mousemove', '--sync', '--window', self.window, round(point['x']), round(point['y']))
        time.sleep(.18)

    def key(self, key):
        self.command('key', '--window', self.window, key)
        time.sleep(.25)

    def drag(self, start, end, release=True):
        self.move(start)
        self.command('mousedown', '1')
        time.sleep(.15)
        for step in range(1, 13):
            fraction = step / 12
            self.command('mousemove', '--sync', '--window', self.window,
                         round(start['x'] + (end['x'] - start['x']) * fraction),
                         round(start['y'] + (end['y'] - start['y']) * fraction))
            time.sleep(.035)
        if release:
            self.command('mouseup', '1')
            time.sleep(.2)

    def ready_window(self):
        ready = self.wait_json(self.out / 'ready.json')
        self.ready_received = time.monotonic()
        self.last_frame = ready['frame']
        require(ready['stage'] == 'ready' and ready['nonce'] == self.session, 'invalid player readiness')
        windows = self.command('search', '--onlyvisible', '--pid', self.process.pid, interaction=False).splitlines()
        require(len(windows) == 1, 'expected one visible player window')
        self.window = windows[0]
        self.command('windowfocus', '--sync', self.window, interaction=False)
        return ready

    def run(self):
        ready = self.ready_window()
        self.begin('selection')
        self.move(ready['targetPixels'][0])
        self.command('click', '1')
        time.sleep(.3)
        selected = self.capture()

        self.begin('hover')
        self.move(selected['handleX'])
        self.capture()

        self.begin('move')
        point = selected['handleX']
        self.drag(point, {'x': point['x'] + 95, 'y': point['y']})
        moved = self.capture()

        if self.customized:
            self.begin('old-shortcut')
            self.key('e')
            self.capture()
        self.begin('rotation-mode')
        self.key('t' if self.customized else 'e')
        rotation = self.capture()
        self.begin('rotate')
        point, center = rotation['handleRing'], rotation['pivotPixel']
        dx, dy = point['x'] - center['x'], point['y'] - center['y']
        angle = math.radians(65)
        end = {'x': center['x'] + dx * math.cos(angle) - dy * math.sin(angle),
               'y': center['y'] + dx * math.sin(angle) + dy * math.cos(angle)}
        self.drag(point, end)
        self.capture()

        self.begin('scale-mode')
        self.key('r')
        scale = self.capture()
        self.begin('scale')
        point, center = scale['handleX'], scale['pivotPixel']
        length = max(1, math.hypot(point['x'] - center['x'], point['y'] - center['y']))
        self.drag(point, {'x': point['x'] + 90 * (point['x'] - center['x']) / length,
                          'y': point['y'] + 90 * (point['y'] - center['y']) / length})
        self.capture()

        self.begin('local')
        self.key('w')
        self.capture()
        self.begin('world')
        self.key('x')
        self.capture()
        self.begin('group')
        self.key('g')
        group = self.capture()
        self.begin('group-move')
        point = group['handleX']
        self.drag(point, {'x': point['x'] + 80, 'y': point['y']})
        group_moved = self.capture()

        self.begin('disable-cleanup')
        point = group_moved['handleX']
        self.drag(point, {'x': point['x'] + 35, 'y': point['y']}, release=False)
        self.key('d')
        self.capture()
        self.command('mouseup', '1')
        self.begin('reenable')
        self.key('d')
        enabled = self.capture()
        self.begin('scene-cleanup')
        point = enabled['handleX']
        self.drag(point, {'x': point['x'] + 35, 'y': point['y']}, release=False)
        self.key('n')
        time.sleep(.7)
        self.capture()
        self.command('mouseup', '1')

    def close(self, success):
        if self.process.poll() is None and self.window:
            try:
                self.command('key', '--window', self.window, 'q', interaction=False)
                self.process.wait(timeout=10)
            except (subprocess.SubprocessError, OSError):
                success = False
        if self.process.poll() is None:
            os.killpg(self.process.pid, signal.SIGTERM)
            try:
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                os.killpg(self.process.pid, signal.SIGKILL)
                self.process.wait(timeout=5)
        report = {'schema': 1, 'runId': self.run_id, 'session': self.session, 'customized': self.customized,
                  'withheld': self.withhold, 'driverCompleted': success, 'exitCode': self.process.returncode,
                  'windowId': self.window, 'pid': self.process.pid,
                  'readyReceivedSeconds': self.ready_received,
                  'sample': getattr(self, 'sample', None),
                  'checkpoints': self.checkpoints, 'input': self.transcript}
        report.update({key: self.build[key] for key in ('lifecycleStage', 'buildId', 'buildSha256')})
        report['files'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in self.out.iterdir() if p.is_file() and p.name != 'input.json'}
        write_json(self.out / 'input.json', report)


class SamplePlayerDriver(PlayerDriver):
    def __init__(self, player, out, run_id, contract, sample, customized=False, deadline=30, build_manifest=None):
        self.contract, self.sample = contract, sample
        super().__init__(player, out, run_id, customized=customized, deadline=deadline, build_manifest=build_manifest)

    def control(self, observation, identifier=None, title=None):
        matches = [control for control in observation['controls'] if control['enabled'] and
                   (identifier is None or control['id'] == identifier) and
                   (title is None or title in control['text'])]
        require(len(matches) == 1, 'expected one visible/enabled workspace control')
        return {'x': matches[0]['x'], 'y': matches[0]['y']}

    def wait_ui(self, scene, required=None, absent=None):
        deadline = time.monotonic() + self.deadline
        while time.monotonic() < deadline:
            require(self.process.poll() is None, 'sample player exited while loading workspace')
            path = self.out / 'status.json'
            if path.exists():
                state = json.loads(path.read_text())
                require(state.get('runId') == self.run_id and state.get('session') == self.session, 'stale sample player status')
                if state['scene'] == scene and state['frame'] > self.last_frame and (
                    required is None or required in state['controls']) and (absent is None or absent not in state['controls']):
                    return
            time.sleep(.05)
        raise TimeoutError('sample UI never reached its input checkpoint: ' + self.stage)

    def click(self, point):
        self.move(point)
        self.command('click', '1')
        time.sleep(.2)

    def help_cycle(self, observation, suffix=''):
        ui = self.contract['ui']
        scene = observation['scene']
        self.begin('help' + suffix)
        self.click(self.control(observation, identifier=ui['help']))
        self.wait_ui(scene, required=ui['closeHelp'])
        opened = self.capture()
        self.begin('close-help' + suffix)
        self.click(self.control(opened, identifier=ui['closeHelp']))
        self.wait_ui(scene, required=ui['help'], absent=ui['closeHelp'])
        return self.capture()

    def run(self):
        ready = self.ready_window()
        ui, samples = self.contract['ui'], self.contract['samples']
        if self.sample == 'DemoBrowser':
            self.begin('empty-gallery')
            self.click({'x': ready['width'] / 2, 'y': ready['height'] / 2})
            self.capture()
        elif self.sample != 'gallery':
            require(ready['scene'] == samples[self.sample]['scene'], 'wrong standalone sample scene')
            self.help_cycle(ready)
        else:
            gallery = ready
            require(gallery['scene'] == samples['DemoBrowser']['scene'], 'gallery build did not start in Demo Browser')
            for sample in ui['galleryOrder']:
                self.begin('open-' + sample)
                self.click(self.control(gallery, title=samples[sample]['cardTitle']))
                self.wait_ui(samples[sample]['scene'], required=ui['help'])
                workspace = self.capture()
                workspace = self.help_cycle(workspace, '-' + sample)
                self.begin('return-' + sample)
                self.click(self.control(workspace, identifier=ui['gallery']))
                self.wait_ui(samples['DemoBrowser']['scene'], required=ui['firstCard'])
                gallery = self.capture()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--player', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--build-manifest', type=Path, required=True)
    parser.add_argument('--customized', action='store_true')
    parser.add_argument('--withhold-input', action='store_true', help='Negative control: no mouse or interaction keys are delivered')
    parser.add_argument('--stage-timeout', type=int, default=30)
    parser.add_argument('--sample', choices=('gallery', 'Demo', 'DemoBrowser', 'LevelEditor', 'ModularBuilding', 'ProductConfigurator'))
    parser.add_argument('--contract', type=Path, help='Frozen candidate consumer contract for sample UI selectors')
    args = parser.parse_args()
    if args.sample:
        require(args.contract and not args.withhold_input, 'sample driver needs frozen selectors; no-input control uses Core consumer')
        driver = SamplePlayerDriver(args.player, args.out, args.run_id, json.loads(args.contract.read_text()), args.sample, args.customized, args.stage_timeout, args.build_manifest)
    else:
        driver = PlayerDriver(args.player, args.out, args.run_id, args.customized, args.withhold_input, args.stage_timeout, args.build_manifest)
    success = False
    try:
        driver.run()
        success = True
    finally:
        driver.close(success)


if __name__ == '__main__':
    main()
