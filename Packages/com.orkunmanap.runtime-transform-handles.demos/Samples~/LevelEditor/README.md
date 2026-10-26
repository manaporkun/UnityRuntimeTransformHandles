# Level Editor: Prop Workshop

Import **Level Editor** from Runtime Transform Handles Demos in Package Manager, run **Tools > Transform Handles > Setup Layer**, then open `LevelEditor.unity` and press Play. This sample supports Built-in and URP rendering without requiring URP. It requires Core, uGUI and UI Toolkit through the optional Demos package. The Input System is optional; when using its backend, this sample requires version 1.1.1 or later for runtime UI default actions. Core's compatibility requirements are unchanged.

Click a prop to select it. Shift-click adds or removes props. W/E/R select Move/Rotate/Scale; X switches World/Local. Drag a handle axis or plane. Left-drag orbits the camera and the wheel zooms. Escape ends the gesture and clears selection, keeping the last valid layout. Reset restores the initial transforms; there is no undo or persistence.

Groups move and rotate around the arithmetic mean of selected object pivots. World uses scene axes; Local uses the last selected surviving prop’s orientation. Group Scale exposes only the center control: it multiplies every prop’s size and distance from the shared center by the same factor. The space label becomes Uniform, because this operation is independent of axis orientation. Returning to Move/Rotate restores the previous World/Local preference. Removing or destroying the active prop keeps surviving selections. World movement snaps the pivot to an absolute 0.5-unit grid, preserving group offsets. Local movement has no grid snapping. Rotation uses relative 15-degree steps; scaling uses 0.1 drag steps. Scale components must remain between 0.25 and 4: exceeding that range restores the complete drag-start transform and rebuilds the handle on the next Update. Mode, space, snapping and reset commands are blocked during dragging. If external code has already set an unsupported scale before a gesture starts, that gesture is cancelled without resetting the prop's position or rotation; Reset is the explicit recovery action.

`LevelEditorDemo` owns only its own handles, materials and UI. It temporarily overrides an existing manager's camera/settings/UI blocking and restores them on disable. A manager created by the sample is retained across disable/re-enable and destroyed with the controller; this avoids reusing a manager scheduled for deferred destruction. Imported assets are independent of the Demos package, so uninstalling Demos does not delete this sample. Keep explicit Core and UI dependencies while retaining the import.

The serialized settings disable Core's global shortcuts. Buttons and sample shortcuts share the same validation, so unsupported All mode cannot be enabled through the keyboard; group Rotate and Scale are available from both keyboard and buttons. Selection is resolved on release after manager updates; UI gestures, orbit gestures and handle drags cannot select another prop underneath them.

The `Tests` folder contains optional Unity Test Framework PlayMode tests guarded by `UNITY_INCLUDE_TESTS`. Tests exercise this controller without changing Core. The original Demo is a separate import with its original controller and layout. Both samples use the manager’s default Precision handle unless a scene manager selects Native.

The UI Toolkit header contains navigation, Reset scene and Help. The scrollable left inspector
shows the selection, active tool, effective World/Local space, snap increment and handle size.
The interface scales from a 1280×720 reference with equal width/height weighting. The shared
theme uses 20 px base text, 18 px descriptions and 44 px buttons; Move/Rotate/Scale buttons
override the height to 48 px. Desktop validation covers 960×640 through 3840×2160, with browser
device pixel ratios 1 and 2; high-resolution output grows text and controls. UI labels update
only when their state changes. A `UIDocument` loads `UI/LevelEditor.uxml`; icons come from the
PNG references in `UI/LevelEditor.uss`, and `UI/DemoTheme.uss` selects the bundled
`Fonts/LiberationSans.ttf`. No TMP package or Essential Resources setup is required.

## Light theme and handle size

The white left inspector groups the transform tools; the viewport uses a light neutral grid and muted
props. The sample settings use a blue hover highlight for contrast on pale surfaces. The imported
scene owns its camera background; a borrowed manager's settings and camera reference are restored
as before. The original showcase and Core appearance defaults are unchanged.

The **Handle size** slider and integer field accept **64–512 px**, initially **192 px**. This sets
`Handle.AutoScaleSizeInPixels` directly and enables `AutoScale` on this sample's handle. Values are
rendering pixels, independent of UI Toolkit panel scaling; on Retina/high-DPI displays, choose a larger value
if needed. The number is not silently multiplied by UI scale or device pixel ratio.

The preference survives selection, grouping, tool changes, invalid-scale recovery, Reset and
component disable/re-enable for this controller's lifetime. Set it before selecting a prop if you
wish. It is not saved between runs and does not modify the object scale, other handles, manager
prefabs or Core defaults. Controls are disabled during a handle drag. Valid integers clamp to the
range; invalid/empty/overflow entries restore the previous value. While entering a number,
W/E/R/X and Escape do not operate on the scene, including the frame in which editing ends.

The sample emphasizes each component's own hue on hover: axes darken slightly and plane fills become stronger. Precision plane pads use a stronger translucent fill and a thin rounded border in the same color for visibility on the light viewport. Their position and picking volumes are unchanged.

### Camera controls and trackpads

Click to select; Shift-click adds to the selection. Left-drag orbits the camera after a small movement threshold, except when the press starts on a handle, which manipulates the selection. Right-drag pans; middle-drag also orbits. Trackpads can use their configured secondary-click gesture for right-drag. Scroll (including two-finger scroll) zooms proportionally with bounded per-frame changes; fractional deltas are retained. This does not implement native pinch gestures.

Camera gestures cannot start over UI or during numeric editing/manipulation. A drag returning to its start point cannot become a click. Focus loss/disable discard camera gestures. The controller uses execution order -100 so its LateUpdate processes camera motion after manager input and before default-order handle visual synchronization; custom script-order overrides should preserve that relationship. The repository WebGL template suppresses context menus only on the player canvas to support right-drag.

The sample's `Plugins/LevelEditorInput.jslib` retains raw browser wheel magnitude on WebGL, where the engine's input path can quantize tiny deltas. It reads only the player canvas and leaves Unity UI events intact. The listener detaches on disable/destruction and pending input clears on focus changes. Native players continue using their Unity input backend. No Core browser plugin is required.

### Shared workspace layout

The UI Toolkit header places **Demos** (when launched from Demo Browser), the workspace name, **Reset scene** and **Help** in the same positions as the other demos. The scrollable left inspector contains selection, Move/Rotate/Scale, World/Local, Snap and always-visible handle-size controls. Status and snapping explanations stay inside the inspector. Long selection names use ellipsis and a tooltip without interpreting rich text.

Help contains the keyboard and camera instructions. Clicking outside Help consumes the entire dismissal press. Escape cancels numeric editing first, then closes Help, then clears selection. Enter or focus loss commits valid size input. Disable/re-enable rebuilds the UI bindings and closes Help while keeping the size preference.

Edit `UI/LevelEditor.uxml` and scenario styles in `UI/LevelEditor.uss`. The common `UI/DemoTheme.uss` is generated from the repository theme; see `ci/demo-ui/README.md`. Imported samples have local assets and remain independent.
