// Sample-local raw browser wheel input. Unity's WebGL path can turn tiny
// trackpad deltas into whole ticks; retain the DOM magnitude for camera zoom.
mergeInto(LibraryManager.library, {
    TH_LevelEditorReadScroll: function () {
        var state = Module.THLevelEditorWheel;
        if (!state) {
            state = { canvas: Module.canvas, delta: 0, listener: null };
            state.listener = function (event) {
                var value = event.deltaY;
                if (!Number.isFinite(value)) return;
                if (event.deltaMode === 0) value /= 100;
                else if (event.deltaMode === 1) value /= 3;
                else if (event.deltaMode === 2) value *= 8;
                else return;
                state.delta -= value;
            };
            state.canvas.addEventListener("wheel", state.listener, { passive: true });
            Module.THLevelEditorWheel = state;
        }
        var delta = state.delta;
        state.delta = 0;
        return delta;
    },
    TH_LevelEditorResetScroll: function () {
        if (Module.THLevelEditorWheel) Module.THLevelEditorWheel.delta = 0;
    },
    TH_LevelEditorReleaseScroll: function () {
        var state = Module.THLevelEditorWheel;
        if (!state) return;
        state.canvas.removeEventListener("wheel", state.listener);
        delete Module.THLevelEditorWheel;
    }
});
