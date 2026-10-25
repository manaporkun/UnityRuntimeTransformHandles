// Sample-local raw browser wheel input. Unity's WebGL path can turn tiny
// trackpad deltas into whole ticks; retain the DOM magnitude for camera zoom.
mergeInto(LibraryManager.library, {
    TH_ProductConfiguratorReadScroll: function () {
        var state = Module.THProductConfiguratorWheel;
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
            Module.THProductConfiguratorWheel = state;
        }
        var delta = state.delta;
        state.delta = 0;
        return delta;
    },
    TH_ProductConfiguratorResetScroll: function () {
        if (Module.THProductConfiguratorWheel) Module.THProductConfiguratorWheel.delta = 0;
    },
    TH_ProductConfiguratorReleaseScroll: function () {
        var state = Module.THProductConfiguratorWheel;
        if (!state) return;
        state.canvas.removeEventListener("wheel", state.listener);
        delete Module.THProductConfiguratorWheel;
    }
});
