// Exercise the actual sample bridge without a browser or Unity license.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const listeners = new Set();
let registrations = 0;
const canvas = {
    addEventListener(name, listener, options) {
        assert.equal(name, 'wheel');
        assert.equal(options.passive, true);
        registrations++;
        listeners.add(listener);
    },
    removeEventListener(name, listener) {
        assert.equal(name, 'wheel');
        listeners.delete(listener);
    }
};
const context = { Module: { canvas }, LibraryManager: { library: {} }, mergeInto: Object.assign };
vm.runInNewContext(fs.readFileSync(path.join(__dirname,
    '../Packages/com.orkunmanap.runtime-transform-handles.demos/Samples~/LevelEditor/Plugins/LevelEditorInput.jslib'), 'utf8'), context);
const api = context.LibraryManager.library;
const emit = (deltaY, deltaMode = 0) => {
    // No preventDefault/stopPropagation methods: the bridge must only observe.
    for (const listener of listeners) listener({ deltaY, deltaMode });
};
const close = (actual, expected) => assert.ok(Math.abs(actual - expected) < 1e-10, `${actual} != ${expected}`);
api.TH_LevelEditorResetScroll();
api.TH_LevelEditorReleaseScroll();
assert.equal(registrations, 0, 'Cleanup cannot register a listener.');
assert.equal(api.TH_LevelEditorReadScroll(), 0);
assert.equal(api.TH_LevelEditorReadScroll(), 0);
assert.equal(registrations, 1);
emit(.01); close(api.TH_LevelEditorReadScroll(), -.0001);
emit(.5); close(api.TH_LevelEditorReadScroll(), -.005);
emit(100); close(api.TH_LevelEditorReadScroll(), -1);
assert.equal(api.TH_LevelEditorReadScroll(), 0, 'Each delta is consumed once.');
emit(20); emit(-5); close(api.TH_LevelEditorReadScroll(), -.15);
emit(3, 1); close(api.TH_LevelEditorReadScroll(), -1);
emit(1, 2); close(api.TH_LevelEditorReadScroll(), -8);
emit(NaN); emit(Infinity); emit(1, 99); assert.equal(api.TH_LevelEditorReadScroll(), 0);
emit(100); api.TH_LevelEditorResetScroll(); assert.equal(api.TH_LevelEditorReadScroll(), 0);
emit(100); api.TH_LevelEditorReleaseScroll(); assert.equal(listeners.size, 0);
emit(500); api.TH_LevelEditorReleaseScroll(); assert.equal(registrations, 1);
assert.equal(api.TH_LevelEditorReadScroll(), 0, 'Inactive input cannot replay on enable.');
assert.equal(listeners.size, 1); assert.equal(registrations, 2);
emit(.01); close(api.TH_LevelEditorReadScroll(), -.0001);
api.TH_LevelEditorReleaseScroll();
console.log('PASS: raw wheel fractions, modes, accumulation, consumption and lifecycle');
