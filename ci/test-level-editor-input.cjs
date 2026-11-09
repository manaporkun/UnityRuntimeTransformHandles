// Exercise the actual sample bridge without a browser or Unity license.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
for (const sample of ['LevelEditor', 'ModularBuilding', 'ProductConfigurator']) {
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
    `../Packages/com.orkunmanap.runtime-transform-handles.demos/Samples~/${sample}/Plugins/${sample}Input.jslib`), 'utf8'), context);
const api = Object.fromEntries(Object.entries(context.LibraryManager.library).map(([key, value]) =>
    [key.replace(sample, 'LevelEditor'), value]));
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
console.log(`PASS: ${sample} raw wheel fractions, modes, accumulation, consumption and lifecycle`);
}

// Co-import keeps independent symbols/state: disabling one must not detach the other.
const active = new Set();
const shared = { Module: { canvas: {
    addEventListener(_, listener) { active.add(listener); },
    removeEventListener(_, listener) { active.delete(listener); }
}}, LibraryManager: { library: {} }, mergeInto: Object.assign };
for (const sample of ['LevelEditor', 'ModularBuilding', 'ProductConfigurator']) {
    vm.runInNewContext(fs.readFileSync(path.join(__dirname,
        `../Packages/com.orkunmanap.runtime-transform-handles.demos/Samples~/${sample}/Plugins/${sample}Input.jslib`), 'utf8'), shared);
}
const both = shared.LibraryManager.library;
assert.equal(Object.keys(both).length, 9);
both.TH_LevelEditorReadScroll(); both.TH_ModularBuildingReadScroll(); both.TH_ProductConfiguratorReadScroll();
assert.equal(active.size, 3);
for (const listener of active) listener({deltaY: 50, deltaMode: 0});
both.TH_LevelEditorReleaseScroll();
assert.equal(active.size, 2);
assert.equal(both.TH_ProductConfiguratorReadScroll(), -.5);
assert.equal(both.TH_ModularBuildingReadScroll(), -.5);
both.TH_ModularBuildingReleaseScroll();
assert.equal(active.size, 1);
both.TH_ProductConfiguratorReleaseScroll();
assert.equal(active.size, 0);
console.log('PASS: co-import wheel symbols and cleanup are independent');
