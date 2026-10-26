#!/usr/bin/env python3
"""Fail closed on incomplete, stale or non-input-driven consumer player evidence."""
import argparse
from datetime import datetime
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import re
import struct
import zlib
import xml.etree.ElementTree as ET

PLAYER_STAGES = ('selection', 'hover', 'move', 'rotation-mode', 'rotate', 'scale-mode',
                 'scale', 'local', 'world', 'group', 'group-move', 'disable-cleanup',
                 'reenable', 'scene-cleanup')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_tests(xml, log, inventory, expected, stage, started, finished):
    names = [line.strip() for line in inventory.read_text().splitlines() if line.strip() and not line.startswith('#')]
    require(names and len(names) == len(set(names)), 'empty/duplicate reviewed consumer test inventory')
    root = ET.parse(xml).getroot()
    cases = list(root.iter('test-case'))
    actual = [case.get('fullname') for case in cases]
    require(len(actual) == len(set(actual)) and set(actual) == set(names), 'consumer/sample test discovery mismatch')
    require(root.get('result') == 'Passed' and all(case.get('result') == 'Passed' for case in cases), 'failed or unexpectedly skipped consumer/sample tests')
    require(all(int(root.get(key, '-1')) == len(cases) for key in ('total', 'passed', 'testcasecount')), 'test summary counts differ from discovered cases')
    for key in ('failed', 'skipped', 'inconclusive'):
        require(int(root.get(key, '0')) == 0, 'unexpected nonpassing consumer/sample tests')
    begin = datetime.fromisoformat(root.attrib['start-time'].replace('Z', '+00:00')).timestamp()
    end = datetime.fromisoformat(root.attrib['end-time'].replace('Z', '+00:00')).timestamp()
    require(started - 1 <= begin <= end <= finished + 1 and xml.stat().st_mtime >= started - 1, 'stale consumer/sample test XML')
    text = log.read_text(errors='replace')
    require(set(re.findall(r'Initialize engine version: ([\w.]+)', text)) == {expected['unity']}, 'test Editor startup version mismatch')
    require('CONSUMER_INVOCATION ' + expected['runId'] + ' ' + stage in text, 'test log is not bound to this run/stage')
    return {'passed': len(cases), 'xml_sha256': digest(xml), 'log_sha256': digest(log)}


def read_json(path):
    return json.loads(path.read_text(), parse_constant=lambda value: (_ for _ in ()).throw(ValueError('nonfinite JSON number: ' + value)))


def read_frame(path):
    """Decode Unity's noninterlaced RGB/RGBA PNG using only the standard library."""
    data = path.read_bytes()
    require(data.startswith(b'\x89PNG\r\n\x1a\n'), 'invalid captured PNG')
    offset, payload, header, ended = 8, bytearray(), None, False
    while offset < len(data):
        require(offset + 12 <= len(data), 'truncated PNG chunk')
        size = struct.unpack('>I', data[offset:offset + 4])[0]
        kind = data[offset + 4:offset + 8]
        chunk = data[offset + 8:offset + 8 + size]
        crc = data[offset + 8 + size:offset + 12 + size]
        require(len(chunk) == size and len(crc) == 4 and struct.unpack('>I', crc)[0] == zlib.crc32(kind + chunk), 'PNG checksum mismatch')
        offset += size + 12
        if kind == b'IHDR':
            require(header is None and len(chunk) == 13, 'invalid PNG header')
            header = struct.unpack('>IIBBBBB', chunk)
        elif kind == b'IDAT':
            payload.extend(chunk)
        elif kind == b'IEND':
            require(size == 0 and offset == len(data), 'PNG trailing data')
            ended = True
    require(header and ended, 'missing PNG header/end')
    width, height, bits, color, compression, filtering, interlace = header
    require(bits == 8 and color in (2, 6) and compression == filtering == interlace == 0 and
            320 <= width <= 1920 and 240 <= height <= 1080, 'unsupported player frame format/dimensions')
    channels = 3 if color == 2 else 4
    stride = width * channels
    raw = zlib.decompress(payload)
    require(len(raw) == (stride + 1) * height, 'incorrect PNG pixel data length')
    previous, pixels = bytearray(stride), []
    for row in range(height):
        filter_type = raw[row * (stride + 1)]
        require(filter_type <= 4, 'unsupported PNG filter')
        current = bytearray(raw[row * (stride + 1) + 1:(row + 1) * (stride + 1)])
        for index in range(stride):
            left = current[index - channels] if index >= channels else 0
            above = previous[index]
            corner = previous[index - channels] if index >= channels else 0
            if filter_type == 1: predictor = left
            elif filter_type == 2: predictor = above
            elif filter_type == 3: predictor = (left + above) // 2
            elif filter_type == 4:
                prediction = left + above - corner
                distances = (abs(prediction - left), abs(prediction - above), abs(prediction - corner))
                predictor = (left, above, corner)[distances.index(min(distances))]
            else: predictor = 0
            current[index] = (current[index] + predictor) & 255
        pixels.append(current)
        previous = current
    return width, height, channels, pixels


def region(frame, point, radius):
    width, height, channels, pixels = frame
    x, y = point['x'], point['y']
    require(math.isfinite(x) and math.isfinite(y) and 0 <= x < width and 0 <= y < height, 'observation point outside captured frame')
    return [tuple(pixels[row][column * channels:column * channels + 3])
            for row in range(max(0, round(y) - radius), min(height, round(y) + radius + 1))
            for column in range(max(0, round(x) - radius), min(width, round(x) + radius + 1))]


def changed_pixels(before, after):
    require(before[:3] == after[:3], 'workspace frame format changed during interaction')
    channels = before[2]
    return sum(a[index:index + 3] != b[index:index + 3] for a, b in zip(before[3], after[3])
               for index in range(0, len(a), channels))


def verify_rendered_visuals(observation, frame):
    found = set()
    width, height, channels, pixels = frame
    background = [observation['cameraBackground'][key] for key in ('r', 'g', 'b')]
    for visual in observation.get('visuals', []):
        kind, color = visual['kind'], visual['color']
        require(kind in ('target', 'handle'), 'unknown renderer observation')
        left, top, right, bottom = (visual[key] for key in ('left', 'top', 'right', 'bottom'))
        require(0 <= left <= right < width and 0 <= top <= bottom < height, 'renderer bounds outside actual viewport')
        rgb = [color[key] for key in ('r', 'g', 'b')]
        require(all(math.isfinite(value) and 0 <= value <= 1 for value in rgb), 'invalid observed renderer color')
        if kind == 'handle' and max(rgb) - min(rgb) < .3: continue
        energy = sum(value * value for value in rgb)
        if energy < .001: continue
        matches = 0
        for row in range(top, bottom + 1):
            for column in range(left, right + 1):
                pixel = [pixels[row][column * channels + index] / 255 for index in range(3)]
                if max(abs(pixel[index] - background[index]) for index in range(3)) <= .1: continue
                # Sample opaque shaders multiply base color by a .55..1 lighting factor.
                shade = sum(pixel[index] * rgb[index] for index in range(3)) / energy
                if .5 <= shade <= 1.12 and max(abs(pixel[index] - shade * rgb[index]) for index in range(3)) <= .1:
                    matches += 1
        if matches >= (8 if kind == 'handle' else 40): found.add(kind)
    required = {'target', 'handle'} if observation.get('liveHandles', 0) > 0 else {'target'}
    require(found >= required, 'sample frame has no visible target AND applicable handle pixels in observed renderer bounds')


def verify_rendered_text(observation, frame, text):
    labels = [label for label in observation.get('labelRegions', []) if text in label.get('text', '')]
    require(labels, 'empty gallery label has no observed screen bounds')
    width, height, channels, pixels = frame
    for label in labels:
        require(all(isinstance(label[key], (int, float)) and math.isfinite(label[key]) for key in ('x', 'y', 'width', 'height')), 'invalid label screen bounds')
        left, right = max(0, math.floor(label['x'] - label['width'] / 2)), min(width - 1, math.ceil(label['x'] + label['width'] / 2))
        top, bottom = max(0, math.floor(label['y'] - label['height'] / 2)), min(height - 1, math.ceil(label['y'] + label['height'] / 2))
        require(right - left >= 8 and bottom - top >= 4, 'empty gallery label is outside the rendered viewport')
        rows = [[sum(pixels[y][x * channels:x * channels + 3]) / 3 for x in range(left, right + 1)] for y in range(top, bottom + 1)]
        low, high = min(map(min, rows)), max(map(max, rows))
        if high - low < 40: continue
        midpoint = (low + high) / 2
        transitions = max(sum((row[index - 1] > midpoint) != (row[index] > midpoint) for index in range(1, len(row))) for row in rows)
        if transitions >= 8: return
    raise ValueError('empty gallery label has no rendered text pixels')


def vector_delta(after, before):
    require(set(after) == set(before) == {'x', 'y', 'z'}, 'invalid transform vector')
    result = {key: after[key] - before[key] for key in before}
    require(all(math.isfinite(v) for v in result.values()), 'nonfinite transform')
    return result


def snapped(value, step):
    return abs(value / step - round(value / step)) < .015


def verify_input(checkpoint, entries, window, customized, observations, previous_received, sample_origin=None, allow_quit=False):
    stage = checkpoint['stage']
    requested, received = checkpoint.get('requestedSeconds'), checkpoint.get('receivedSeconds')
    require(isinstance(requested, (int, float)) and isinstance(received, (int, float)) and
            math.isfinite(requested) and math.isfinite(received) and requested <= received,
            'missing/invalid driver capture timestamps')
    before, previous_finish = [], 0
    for entry in entries:
        argv, start, finish = entry.get('argv', []), entry.get('seconds'), entry.get('finishedSeconds')
        require(argv[:1] == ['xdotool'] and isinstance(start, (int, float)) and isinstance(finish, (int, float)) and
                math.isfinite(start) and math.isfinite(finish) and previous_finish <= start <= finish,
                'invalid external input transcript')
        previous_finish = finish
        require(start >= previous_received, 'stage input predates its previous observed checkpoint')
        if finish <= requested:
            before.append(argv[1:])
        else:
            allowed = [['mouseup', '1']] if stage == 'disable-cleanup' else (
                [['mouseup', '1'], ['key', '--window', window, 'q']] if stage == 'scene-cleanup' else [])
            if allow_quit: allowed.append(['key', '--window', window, 'q'])
            require(start >= received and argv[1:] in allowed, 'input occurred during or after its captured checkpoint')
    keys = [args[3] for args in before if args[:3] == ['key', '--window', window] and len(args) == 4]
    moves = [args for args in before if args[:4] == ['mousemove', '--sync', '--window', window] and len(args) == 6]
    coordinates = []
    for move in moves:
        require(all(isinstance(value, str) and re.fullmatch(r'-?[0-9]+', value) for value in move[4:]), 'invalid external pointer coordinates')
        x, y = map(int, move[4:])
        require(0 <= x < observations[stage]['width'] and 0 <= y < observations[stage]['height'], 'external pointer left player viewport')
        coordinates.append((x, y))
    if sample_origin is not None:
        require(moves and before.count(['click', '1']) == 1, 'workspace control has no external pointer click')
        click = before.index(['click', '1'])
        before_click = [args for args in before[:click] if args in moves]
        require(before_click and math.dist(tuple(map(int, before_click[-1][4:])), (sample_origin['x'], sample_origin['y'])) <= 3,
                'workspace click did not hit the observed visible control')
        require(not keys, 'unexpected keyboard command during workspace click')
        return
    origins = {'selection': ('ready', 'targetPixels'), 'hover': ('selection', 'handleX'),
               'move': ('selection', 'handleX'), 'rotate': ('rotation-mode', 'handleRing'),
               'scale': ('scale-mode', 'handleX'), 'group-move': ('group', 'handleX'),
               'disable-cleanup': ('group-move', 'handleX'), 'scene-cleanup': ('reenable', 'handleX')}
    if stage in origins:
        previous_stage, field = origins[stage]
        point = observations[previous_stage][field]
        if field == 'targetPixels': point = point[0]
        require(coordinates and math.hypot(coordinates[0][0] - point['x'], coordinates[0][1] - point['y']) <= 3,
                'external pointer did not start at observed target/control: ' + stage)
    key_programs = {'rotation-mode': ['t' if customized else 'e'], 'scale-mode': ['r'], 'old-shortcut': ['e'],
                    'local': ['w'], 'world': ['x'], 'group': ['g'], 'disable-cleanup': ['d'],
                    'reenable': ['d'], 'scene-cleanup': ['n']}
    if stage in key_programs:
        require(keys == key_programs[stage], 'missing/wrong external keyboard input: ' + stage)
    else:
        require(not keys, 'unexpected keyboard input during pointer stage')
    if stage == 'selection':
        require(moves and ['click', '1'] in before, 'selection has no external pointer click')
    if stage == 'hover': require(moves, 'hover has no external pointer move')
    drag_stages = ('move', 'rotate', 'scale', 'group-move', 'disable-cleanup', 'scene-cleanup')
    if stage in drag_stages:
        require(before.count(['mousedown', '1']) == 1 and len(moves) >= 13, 'missing external pointer drag: ' + stage)
        down = before.index(['mousedown', '1'])
        if stage in ('disable-cleanup', 'scene-cleanup'):
            require(['mouseup', '1'] not in before, 'cleanup was not observed during an active pointer drag')
            end = before.index(['key', '--window', window, 'd' if stage == 'disable-cleanup' else 'n'])
        else:
            require(before.count(['mouseup', '1']) == 1 and before.index(['mouseup', '1']) > down,
                    'drag has no external pointer release')
            end = before.index(['mouseup', '1'])
        initial = [args for args in before[:down] if args in moves]
        held = [args for args in before[down + 1:end] if args in moves]
        require(initial and len(held) >= 12, 'drag lacks pointer motion while held')
        held_points = [tuple(map(int, args[4:])) for args in [initial[-1], *held]]
        require(math.hypot(held_points[0][0] - point['x'], held_points[0][1] - point['y']) <= 3,
                'pointer press did not start at observed handle control')
        require(len(set(held_points)) >= 5 and math.dist(held_points[0], held_points[-1]) >= 15,
                'pointer drag did not actually move across the window while held')


def sample_stages(sample, contract):
    require(set(contract['samples']) == {'Demo', 'LevelEditor', 'ModularBuilding', 'ProductConfigurator', 'DemoBrowser'}, 'unsupported frozen sample inventory')
    if sample == 'gallery':
        order = contract['ui']['galleryOrder']
        require(len(order) == 4 and set(order) == set(contract['samples']) - {'DemoBrowser'}, 'gallery scope cannot omit a workspace')
        return [stage + '-' + name for name in order for stage in ('open', 'help', 'close-help', 'return')]
    require(sample in contract['samples'], 'unsupported standalone sample')
    return ['empty-gallery'] if sample == 'DemoBrowser' else ['help', 'close-help']


def control_point(observation, identifier=None, title=None):
    controls = [control for control in observation.get('controls', []) if control.get('enabled') is True and
                (identifier is None or control['id'] == identifier) and (title is None or title in control['text'])]
    require(len(controls) == 1, 'missing/ambiguous visible workspace control')
    return {key: controls[0][key] for key in ('x', 'y')}


def load_player(out, expected, customized=False, withheld=False, sample=None, contract=None):
    report = read_json(out / 'input.json')
    require(report.get('schema') == 1 and report.get('runId') == expected['runId'], 'invalid/stale driver report')
    require(report.get('driverCompleted') is True and report.get('exitCode') == 0, 'player driver failed, timed out or exited nonzero')
    require(report.get('customized') is customized and report.get('withheld') is withheld, 'wrong player scenario/input control')
    require(report.get('sample') == sample, 'player report selected a different scenario')
    for key in ('lifecycleStage', 'buildId', 'buildSha256'):
        require(report.get(key) == expected[key], 'player evidence belongs to another lifecycle build: ' + key)
    require(isinstance(report.get('windowId'), str) and re.fullmatch(r'[0-9]+', report['windowId']), 'missing OS player window identity')
    files = {p.name: digest(p) for p in out.iterdir() if p.is_file() and p.name != 'input.json'}
    require(files == report.get('files'), 'player evidence files changed or missing')
    require(all(not p.is_symlink() and p.is_file() for p in out.iterdir()), 'unexpected player evidence directory/link')
    stages = list(PLAYER_STAGES) if sample is None else sample_stages(sample, contract)
    if customized and sample is None: stages.insert(stages.index('rotation-mode'), 'old-shortcut')
    checkpoints = report.get('checkpoints', [])
    require([c.get('stage') for c in checkpoints] == stages, 'missing, duplicate or reordered player stage')
    require(len({c['nonce'] for c in checkpoints}) == len(checkpoints), 'duplicate input nonce')
    last_input_finish = 0
    for item in report.get('input', []):
        require(item.get('stage') in ('startup', *stages), 'unexpected external input stage')
        if item.get('completed'):
            start, finish = item.get('seconds'), item.get('finishedSeconds')
            require(isinstance(start, (int, float)) and isinstance(finish, (int, float)) and
                    math.isfinite(start) and math.isfinite(finish) and last_input_finish <= start <= finish,
                    'reordered external input timestamps')
            last_input_finish = finish
    log = (out / 'player.log').read_text(errors='replace')
    require(set(re.findall(r'Initialize engine version: ([\w.]+)', log)) == {expected['unity']}, 'player startup version mismatch')
    require(not re.search(r'The referenced script.*missing|Missing \(Mono Script\)|\b[\w.]*Exception:|UnityEngine.Debug:LogError|ERROR: Shader|Shader error', log, re.I), 'player log contains missing script, exception or shader error')
    observations, frames = {}, {}
    previous_frame, previous_received = 0, report.get('readyReceivedSeconds')
    require(isinstance(previous_received, (float, int)) and math.isfinite(previous_received) and previous_received >= 0, 'missing external ready timestamp')
    ready = read_json(out / 'ready.json')
    require(ready.get('stage') == 'ready' and ready.get('nonce') == report['session'], 'missing player ready checkpoint')
    for checkpoint in [{'stage': 'ready', 'nonce': report['session'], 'json': 'ready.json', 'frame': 'ready.png'}, *checkpoints]:
        stage, nonce = checkpoint['stage'], checkpoint['nonce']
        require(re.fullmatch(r'[A-Za-z0-9-]+', nonce), 'invalid input nonce')
        stem = 'ready' if stage == 'ready' else stage + '-' + nonce
        require(checkpoint['json'] == stem + '.json' and checkpoint['frame'] == stem + '.png', 'player checkpoint output contract differs')
        for key in ('json', 'frame'):
            require(Path(checkpoint[key]).name == checkpoint[key], 'unsafe player artifact path')
        observation = read_json(out / checkpoint['json'])
        for key in ('runId', 'source', 'packageSource', 'unity', 'input'):
            require(observation.get(key) == expected[key], 'player identity mismatch: ' + key)
        for key in ('lifecycleStage', 'buildId'):
            require(observation.get(key) == expected[key], 'player snapshot belongs to another lifecycle build: ' + key)
        require(observation.get('platform') == 'LinuxPlayer', 'consumer acceptance requires an actual Linux player')
        require(observation.get('schema') == 1 and observation.get('stage') == stage and observation.get('nonce') == nonce and observation.get('session') == report['session'], 'player stage/nonce/session mismatch')
        require(observation.get('frame', 0) >= 30 and not observation.get('errors') and observation.get('shaderSupported') is True, 'player never ready or rendered with errors')
        require(observation.get('missingScripts') == 0 and (sample is not None or observation.get('cameraPresent') is True), 'missing runtime scripts or consumer camera')
        require(observation['frame'] > previous_frame, 'duplicate/stale rendered player frame')
        previous_frame = observation['frame']
        require((observation.get('pipeline') != 'Built-in') is expected['urp'] and
                (not expected['urp'] or observation['pipeline'] == 'UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset'), 'active runtime pipeline mismatch')
        if stage != 'ready': require(observation.get('focused') is True, 'player window was not focused')
        frame = read_frame(out / checkpoint['frame'])
        require((frame[0], frame[1]) == (observation['width'], observation['height']), 'frame/viewport dimensions differ')
        require(len(observation.get('targetPixels', [])) == len(observation.get('positions', [])) and
                (sample is not None or len(observation['positions']) == 2), 'missing target observations')
        for point in observation['targetPixels']:
            target = region(frame, point, 20)
            require(sum(r > (175 if sample is None else 220) and b > (175 if sample is None else 220) and g < (90 if sample is None else 30) for r, g, b in target) == 0, 'magenta target')
            if sample is None:
                require(sum(r > 150 and 60 < g < 210 and b < 130 for r, g, b in target) > 60, 'target is not visible in frame')
        observations[stage], frames[stage] = observation, frame
        if stage != 'ready':
            prior_received = previous_received
            requested, received = checkpoint.get('requestedSeconds'), checkpoint.get('receivedSeconds')
            require(isinstance(requested, (float, int)) and isinstance(received, (float, int)) and
                    math.isfinite(requested) and math.isfinite(received) and previous_received <= requested <= received,
                    'reordered external player checkpoint')
            previous_received = received
            inputs = [item for item in report.get('input', []) if item.get('stage') == stage]
            require(inputs and all(item.get('nonce') == nonce for item in inputs), 'no input transcript bound to player stage')
            if withheld:
                require(any(item.get('withheld') for item in inputs) and all(item.get('withheld') or item.get('argv', [])[-1:] == ['q'] for item in inputs), 'negative control delivered interaction input')
            else:
                require(all(item.get('completed') is True and not item.get('withheld') for item in inputs), 'no completed external input bound to player stage')
                origin = None
                if sample is not None:
                    previous = observations['ready'] if len(observations) == 2 else observations[stages[stages.index(stage) - 1]]
                    ui = contract['ui']
                    if stage == 'empty-gallery': origin = {'x': previous['width'] / 2, 'y': previous['height'] / 2}
                    elif stage.startswith('open-'):
                        origin = control_point(previous, title=contract['samples'][stage[len('open-'):]]['cardTitle'])
                    else:
                        identifier = ui['closeHelp'] if stage.startswith('close-help') else ui['help'] if stage.startswith('help') else ui['gallery']
                        origin = control_point(previous, identifier=identifier)
                verify_input(checkpoint, inputs, report['windowId'], customized, observations, prior_received, sample_origin=origin,
                             allow_quit=sample is not None and stage == stages[-1])
    return report, observations, frames


def verify_outcomes(observations, frames, customized=False):
    selected = observations['selection']
    require(selected['members'] == 1 and selected['selections'] == 1 and selected['handleEnabled'], 'real pointer did not select target')
    require(selected['rotationShortcut'] == ('T' if customized else 'E'), 'persisted shortcut was not loaded')
    require(selected.get('handleSize') == (160 if customized else 192), 'consumer-owned handle prefab configuration was not loaded')
    expected_color = {'r': 1.0 if customized else .95, 'g': .6 if customized else .5, 'b': .1 if customized else .08, 'a': 1.0}
    require(len(selected.get('targetColors', [])) == 2 and all(all(abs(color[key] - value) < .0001 for key, value in expected_color.items()) for color in selected['targetColors']),
            'effective consumer material customization was not loaded')
    hover = observations['hover']
    before, after = region(frames['selection'], selected['handleX'], 14), region(frames['hover'], hover['handleX'], 14)
    require(sum(a != b for a, b in zip(before, after)) > 8, 'hover did not change rendered handle pixels')
    visible_hover = sum((r < 90 and g > 170 and b > 170) if customized else (r > 200 and g > 200 and b > 200) for r, g, b in after)
    require(visible_hover > 5, 'effective hover color is not visible')
    require(hover['highlightColor'] == ({'r': 0.0, 'g': 1.0, 'b': 1.0, 'a': 1.0} if customized else {'r': 1.0, 'g': 1.0, 'b': 1.0, 'a': 1.0}), 'saved hover color not loaded')

    for stage, before_stage in (('move', 'selection'), ('rotate', 'rotation-mode'), ('scale', 'scale-mode'), ('group-move', 'group')):
        current, previous = observations[stage], observations[before_stage]
        require(current['starts'] == previous['starts'] + 1 and current['ends'] == previous['ends'] + 1 and current['changes'] > previous['changes'], 'unbalanced/missing public drag events: ' + stage)
    movement = vector_delta(observations['move']['positions'][0], selected['positions'][0])
    require(abs(movement['x']) >= .5 and snapped(movement['x'], .5) and abs(movement['y']) < .005 and abs(movement['z']) < .005, 'pointer Move/snap outcome is wrong')
    rotation = (observations['rotate']['rotations'][0]['z'] - observations['rotation-mode']['rotations'][0]['z'] + 180) % 360 - 180
    require(abs(rotation) >= 14.9 and snapped(rotation, 15), 'pointer Rotation/snap outcome is wrong')
    scale = vector_delta(observations['scale']['scales'][0], observations['scale-mode']['scales'][0])
    require(abs(scale['x']) >= .099 and snapped(scale['x'], .1) and abs(scale['y']) < .005 and abs(scale['z']) < .005, 'pointer Scale/snap outcome is wrong')
    require(observations['rotation-mode']['type'] == 'Rotation' and observations['scale-mode']['type'] == 'Scale', 'keyboard mode change failed')
    if customized: require(observations['old-shortcut']['type'] == 'Position', 'old rotation shortcut still active')
    local, world = observations['local'], observations['world']
    require(local['space'] == 'Self' and world['space'] == 'World' and local['type'] == world['type'] == 'Position', 'World/Local keyboard change failed')
    require(abs((local['handleRotation']['z'] - local['rotations'][0]['z'] + 180) % 360 - 180) < .1 and abs(world['handleRotation']['z']) < .1, 'Local/World axes do not follow target orientation')
    group, moved = observations['group'], observations['group-move']
    require(group['members'] == 2 and group['grouped'], 'group ownership missing')
    deltas = [vector_delta(moved['positions'][i], group['positions'][i]) for i in range(2)]
    require(abs(deltas[0]['x']) >= .5 and snapped(deltas[0]['x'], .5) and all(abs(deltas[0][k] - deltas[1][k]) < .005 for k in deltas[0]), 'real group movement did not preserve target offsets')
    require(all(abs(moved['pivot'][key] - sum(p[key] for p in moved['positions']) / 2) < .005 for key in ('x', 'y', 'z')), 'group pivot does not match live targets')
    disabled, enabled, scene = observations['disable-cleanup'], observations['reenable'], observations['scene-cleanup']
    require(not disabled['handleEnabled'] and disabled['members'] == 2 and disabled['starts'] == moved['starts'] + 1 and disabled['ends'] == moved['ends'] + 1, 'disable did not balance active drag/retain ownership')
    require(enabled['handleEnabled'] and enabled['members'] == 2 and enabled['starts'] == disabled['starts'] and enabled['ends'] == disabled['ends'], 'reenable lost handle ownership or duplicated events')
    require(scene['scene'] == 'Next' and scene['liveHandles'] == scene['liveGhosts'] == scene['members'] == 0 and scene['starts'] == enabled['starts'] + 1 and scene['ends'] == enabled['ends'] + 1 and scene['destroyed'] == 2, 'scene unload did not clean active drag/ownership')


def verify_player(out, expected, customized=False):
    report, observations, frames = load_player(out, expected, customized)
    verify_outcomes(observations, frames, customized)
    return report


def verify_negative_control(out, expected, customized=False):
    report, observations, frames = load_player(out, expected, customized, withheld=True)
    ready = observations['ready']
    for observation in observations.values():
        require(all(observation[key] == 0 for key in ('starts', 'changes', 'ends', 'selections', 'members', 'liveHandles')), 'no-input player manufactured selection/interaction')
        for key in ('positions', 'rotations', 'scales'):
            require(observation[key] == ready[key], 'no-input player manufactured transforms')
    try:
        verify_outcomes(observations, frames, customized)
    except ValueError as error:
        require(str(error) == 'real pointer did not select target', 'negative control failed outside the interaction stage')
    else:
        raise ValueError('negative control unexpectedly passed interaction acceptance')
    return report


def verify_sample_player(out, expected, contract, sample, customized=False):
    report, observations, frames = load_player(out, expected, customized, sample=sample, contract=contract)
    ui, samples = contract['ui'], contract['samples']
    stages = sample_stages(sample, contract)
    for stage, observation in observations.items():
        if sample == 'DemoBrowser':
            require(observation['scene'] == samples['DemoBrowser']['scene'] and not observation['controls'] and
                    any(ui['emptyGalleryText'] in label for label in observation['labels']), 'Browser-only player did not render its empty gallery')
            verify_rendered_text(observation, frames[stage], ui['emptyGalleryText'])
            continue
        gallery_stage = sample == 'gallery' and (stage == 'ready' or stage.startswith('return-'))
        if gallery_stage:
            require(observation['scene'] == samples['DemoBrowser']['scene'], 'return click did not reach gallery scene')
            for name in ui['galleryOrder']: control_point(observation, title=samples[name]['cardTitle'])
            continue
        name = sample if sample != 'gallery' else next(name for name in ui['galleryOrder'] if stage.endswith('-' + name))
        require(observation['scene'] == samples[name]['scene'] and observation.get('cameraPresent') is True and
                observation.get('visibleRenderers', 0) > 0, 'workspace click did not show a rendered scene/camera')
        if stage.startswith('help'):
            control_point(observation, identifier=ui['closeHelp'])
            previous = 'ready' if stages.index(stage) == 0 else stages[stages.index(stage) - 1]
            require(changed_pixels(frames[previous], frames[stage]) > observation['width'] * observation['height'] * .01,
                    'workspace help state has no visible rendered change')
        else:
            require(not any(control['id'] == ui['closeHelp'] for control in observation['controls']), 'help-close did not close workspace help')
            control_point(observation, identifier=ui['help'])
            verify_rendered_visuals(observation, frames[stage])
        if name == 'Demo':
            require(observation.get('liveHandles', 0) > 0 and observation.get('positions'), 'showcase did not render/own its supplied targets and handles')
            if customized:
                require(all(abs(observation['cameraBackground'][key] - value) < .0001 for key, value in {'r': .08, 'g': .11, 'b': .16}.items()), 'customized imported scene property did not survive reopen/build')
    return report


def verify_journey(work, candidate_dir, profile, transport):
    spec = importlib.util.spec_from_file_location('consumer_project_policy', Path(__file__).with_name('consumer-project.py'))
    project = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(project)
    state = read_json(work / 'run.json')
    metadata = project.candidate.read_candidate(candidate_dir)
    require(project.candidate.read_candidate(work / 'candidate') == metadata, 'journey candidate identity changed')
    require((state['profile'], state['transport'], state['scope']) == (profile, transport, project.scope_for(profile, transport)), 'workflow-selected scope/profile differs from report')
    require(state['source'] == metadata['source'] and state['packageSource'] == metadata['package_source'], 'journey source identity differs from frozen candidate')
    require(tuple(state[key] for key in ('unity', 'input', 'urp')) == project.PROFILE_POLICY[profile], 'journey actual profile differs from current policy')
    stages = project.required_stages(profile, transport)
    require(state.get('completed') == stages and not state.get('pending'), 'mandatory consumer lifecycle stage missing, skipped or reordered')
    contract = read_json(work / 'candidate/consumer/contract.json')
    require(contract.get('outputs') == project.OUTPUT_POLICY, 'unsupported consumer output contract')
    records, sessions, build_ids = {}, set(), set()
    previous_finished = 0
    for stage in stages:
        out = work / 'evidence' / stage
        record = read_json(out / 'record.json')
        require(all(record.get(key) == value for key, value in {'schema': 1, 'runId': state['runId'], 'stage': stage,
                'source': state['source'], 'packageSource': state['packageSource'], 'project': project.project_for(stage)}.items()), 'wrong/stale lifecycle record')
        require(previous_finished <= record['started'] <= record['editorFinished'] <= record['finished'], 'reordered lifecycle timestamps')
        previous_finished = record['finished']
        require(not any(p.is_symlink() for p in out.rglob('*')), 'symlink in lifecycle evidence')
        actual = {p.relative_to(out).as_posix(): digest(p) for p in out.rglob('*') if p.is_file() and p.name != 'record.json'}
        require(actual == record['files'], 'lifecycle artifact missing, extra or changed')
        project.verify_editor_log(out / 'editor.log', state, stage, tests=stage.endswith('-tests'))
        if stage == 'customize':
            text = (out / 'editor.log').read_text()
            require(all(marker + ' ' + state['runId'] in text for marker in ('CONSUMER_IMPORT_PRESERVED', 'CONSUMER_DUPLICATE_REJECTED')),
                    'public importer did not prove customization preservation/duplicate rejection')
        require(record.get('editorPids') and len(record['editorPids']) == len(set(record['editorPids'])), 'missing/duplicate Editor process observations')
        installed = record.get('installed', {})
        resolution = read_json(out / 'resolution.json')
        observed_stage = stages[stages.index(stage) - 1] if stage.endswith('-tests') else stage
        derived = project.verify_resolution_snapshot(state, metadata, contract, record['project'], stage, resolution, observed_stage)
        require(derived == installed, 'recorded resolution differs from raw Editor/manifest/lock evidence')
        if not stage.endswith('-tests'):
            require(read_json(out / 'editor.json') == resolution['observation'], 'raw Editor observation differs from resolution snapshot')
        imports_required = (stage.startswith('sample-') or stage.startswith('upgrade-') and stage in ('upgrade-import', 'upgrade-tests', 'upgrade-build') or
                            stage in ('gallery-import', 'gallery-tests', 'gallery-build', 'customize', 'custom-build', 'custom-gallery-build',
                                      'remove-demos', 'retained-core-build', 'retained-gallery-build'))
        if imports_required:
            custom_imports = stage in ('customize', 'custom-build', 'custom-gallery-build', 'remove-demos', 'retained-core-build', 'retained-gallery-build')
            imports = project.verify_import_snapshot(work, record['project'], read_json(out / 'imports.json'), record.get('imports', {}), custom_imports)
            require(imports == record['imports'], 'raw public import evidence differs')
            if not stage.endswith(('-import', '-tests')):
                assemblies = resolution['observation']['assemblies']
                require(all(sample['assembly'] in assemblies for sample in read_json(out / 'imports.json')['samples']), 'imported sample assembly never compiled')
        expected_packages = {project.CORE, project.DEMOS}
        if stage in ('core-resolve', 'core-setup', 'core-tests', 'core-build', 'remove-demos', 'retained-core-build', 'retained-gallery-build', 'remove-imports-build', 'upgrade-baseline'):
            expected_packages = {project.CORE}
        if stage.startswith('uninstall-'): expected_packages = set()
        require(set(installed) == expected_packages, 'missing/extra resolved lifecycle packages')
        for name, info in installed.items():
            baseline = stage == 'upgrade-baseline'
            expected_files = metadata['baseline']['files'] if baseline else metadata['packages'][name]['files']
            version = metadata['baseline']['version'] if baseline else metadata['version'] if name == project.CORE else metadata['demos_version']
            require(info['files'] == expected_files and info['version'] == version and
                    info['source'] == ('LocalTarball' if baseline or transport == 'tarball' else 'Git'), 'wrong lifecycle package source/version/bytes')
        if stage.endswith('-tests'):
            sample = project.project_for(stage)[len('sample-'):] if stage.startswith('sample-') else None
            mode = 'editmode' if stage in ('core-tests', 'upgrade-tests') else 'playmode'
            if mode == 'editmode': inventory = work / 'candidate/consumer/required/consumer-editmode.txt'
            elif sample: inventory = work / 'candidate/consumer/required' / f'{sample}-input{state["input"]}.txt'
            else: inventory = work / 'candidate/required' / f'required-samples-input{state["input"]}.txt'
            tests = verify_tests(out / (mode + '-results.xml'), out / (mode + '.log'), inventory, state, stage, record['started'], record['editorFinished'])
            require(record['tests'] == tests, 'test evidence identity changed')
        expected_players = []
        if stage.endswith('-build'):
            require(len(record['builds']) == 1 and record['builds'][0]['files'], 'mandatory native build missing')
            require(record['builds'][0]['path'] == '.journey/build/' + stage + '/Consumer.x86_64', 'not a native Linux Mono player build')
            build = record['builds'][0]
            require(re.fullmatch(r'[0-9a-f]{32}', build.get('buildId', '')) and build['buildId'] not in build_ids, 'reused/missing native build identity across lifecycle stages')
            build_ids.add(build['buildId'])
            build_sha = hashlib.sha256(json.dumps(build['files'], sort_keys=True, separators=(',', ':')).encode()).hexdigest()
            require(build_sha == build.get('buildSha256'), 'native build digest differs from recorded bytes')
            expected_player = {**state, 'lifecycleStage': stage, 'buildId': build['buildId'], 'buildSha256': build_sha}
            require(read_json(out / 'build.json') == {key: expected_player[key] for key in ('runId', 'lifecycleStage', 'buildId', 'buildSha256')} | {'files': build['files']}, 'raw native build manifest differs')
            require(resolution['observation']['buildId'] == build['buildId'] and resolution['observation']['buildPath'] == build['path'] and
                    resolution['observation']['scenes'] == build['scenes'], 'raw Editor build identity differs')
            sample = 'gallery' if stage in ('gallery-build', 'custom-gallery-build', 'retained-gallery-build') else (
                project.project_for(stage)[len('sample-'):] if stage.startswith('sample-') else None)
            custom = stage in ('custom-build', 'custom-gallery-build', 'retained-core-build', 'retained-gallery-build', 'remove-imports-build', 'upgrade-build')
            expected_players.append({'path': 'player', 'sample': sample, 'customized': custom, 'negative': False})
            if sample: verify_sample_player(out / 'player', expected_player, contract, sample, custom)
            else: verify_player(out / 'player', expected_player, custom)
            if stage == 'core-build':
                expected_players.append({'path': 'negative', 'sample': None, 'customized': False, 'negative': True})
                verify_negative_control(out / 'negative', expected_player)
        else:
            require(not record['builds'], 'unexpected lifecycle build')
        require(record['players'] == expected_players, 'missing, extra or downgraded player journey')
        for player in expected_players:
            session = read_json(out / player['path'] / 'input.json')['session']
            require(session not in sessions, 'reused player process session across lifecycle evidence')
            sessions.add(session)
        records[stage] = record
    if state['scope'] == 'full':
        original = records['core-setup']['userGuids']
        for stage in ('customize', 'custom-build', 'retained-core-build', 'remove-imports-build'):
            require(records[stage]['userGuids'] == original, 'consumer customization GUIDs did not survive lifecycle')
        note = records['core-resolve']['unrelated']
        for stage in stages:
            if project.project_for(stage) in ('core', 'core-clean', 'uninstall'):
                require(records[stage]['unrelated'] == note, 'unrelated user asset changed across controlled copies')
        require(records['upgrade-baseline']['userGuids'] == records['upgrade-build']['userGuids'], 'upgrade replaced old user GUIDs')
        for filename in ('removal-imports.json', 'removal-core.json', 'upgrade-backup.json'):
            removal = read_json(work / 'evidence' / filename)
            require(removal['runId'] == state['runId'] and removal['removed'], 'missing explicit removal/backup evidence')
            for item in removal['removed']:
                path = Path(item['backup'])
                require(not path.is_absolute() and '..' not in path.parts and path.parts[0] == 'backups', 'unsafe external backup location')
                backup = work / path
                require(backup.is_dir() and project.hashes(backup) == item['files'], 'dependency-bearing backup was lost or edited')
                if item.get('meta_sha256'):
                    require(digest(backup.with_suffix(backup.suffix + '.meta')) == item['meta_sha256'], 'backed-up asset root GUID changed')
        current_imports = records['customize']['imports']
        for stage in ('custom-build', 'custom-gallery-build', 'remove-demos', 'retained-core-build', 'retained-gallery-build'):
            require(records[stage]['imports'] == current_imports, 'customized imports were overwritten or removed implicitly')
    summary = {'schema': 1, 'runId': state['runId'], 'source': state['source'], 'packageSource': state['packageSource'],
               'profile': profile, 'transport': transport, 'scope': state['scope'], 'candidate_sha256': digest(candidate_dir / 'candidate.json'),
               'fixture': metadata['consumer'], 'baseline': {key: metadata['baseline'][key] for key in ('ref', 'commit', 'tree', 'sha256')},
               'editorInvocations': sum(len(record['editorPids']) for record in records.values()),
               'nativeBuilds': sum(len(record['builds']) for record in records.values()),
               'playerRuns': sum(len(record['players']) for record in records.values()),
               'journeySeconds': records[stages[-1]]['finished'] - records[stages[0]]['started'],
               'stages': stages}
    (work / 'evidence/summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--player-dir', type=Path)
    parser.add_argument('--expected', type=Path, help='Current orchestrator-selected config, never a player report')
    parser.add_argument('--work', type=Path)
    parser.add_argument('--candidate', type=Path)
    parser.add_argument('--profile')
    parser.add_argument('--transport', choices=('tarball', 'git'))
    parser.add_argument('--customized', action='store_true')
    parser.add_argument('--negative-control', action='store_true')
    parser.add_argument('--sample', choices=('gallery', 'Demo', 'DemoBrowser', 'LevelEditor', 'ModularBuilding', 'ProductConfigurator'))
    parser.add_argument('--contract', type=Path)
    args = parser.parse_args()
    if args.work:
        require(args.candidate and args.profile and args.transport, 'journey audit requires workflow-selected candidate/profile/transport')
        print(json.dumps(verify_journey(args.work, args.candidate, args.profile, args.transport), indent=2))
        return
    require(args.player_dir and args.expected, 'player audit requires evidence directory and externally selected expected config')
    if args.sample:
        require(args.contract and not args.negative_control, 'sample acceptance needs frozen contract; Core owns no-input control')
        verify_sample_player(args.player_dir, read_json(args.expected), read_json(args.contract), args.sample, args.customized)
    else:
        verifier = verify_negative_control if args.negative_control else verify_player
        verifier(args.player_dir, read_json(args.expected), args.customized)
    print('PASS: no-input control rejected by interaction acceptance' if args.negative_control else
          'PASS: real pointer/key outcomes, event balance and rendered consumer frames')


if __name__ == '__main__':
    main()
