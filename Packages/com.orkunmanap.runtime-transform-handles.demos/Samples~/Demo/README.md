# Runtime Transform Handles — Demo

Open `RuntimeTransformHandlesDemo.unity` and press **Play**. The scene is self-contained:
`HandleDemo` spawns its own target cubes, ensures a camera, and draws an on-screen control panel.
The sample-local `Resources/HandleDemoTarget.mat` uses a pipeline-independent shader so the targets also render in the combined URP demo build. Runtime material instances are released when leaving the scene.

> First run only: create the physics layer the handles raycast against —
> `Tools > Transform Handles > Setup Layer`.

## Controls

| Input | Action |
|-------|--------|
| **Left click** a cube | Select it (creates/handles a gizmo) |
| **Shift + left click** | Add the cube to the current multi-selection |
| **Right click** a cube | Drop it from its handle |
| **Drag a gizmo axis/plane** | Move / rotate / scale the selection |
| **Middle-mouse drag** | Orbit the camera |
| **Scroll wheel** | Zoom |
| **W / E / R / A** | Position / Rotation / Scale / All modes |
| **X** | Toggle World / Local space |
| **Z** | Toggle Pivot / Center origin |

## What it demonstrates

- Single- and multi-object selection and grouped manipulation.
- Every `HandleType`, `HandleAxes` mask, `Space`, and `SnappingType` (driven live from the panel).
- Auto-scaling handles, handle scale multiplier, and a runtime `TransformHandleSettings` asset.
- All interaction events (`OnInteractionStart/Event/End`, `OnHandleDestroyed`).

The picking raycast uses a layer mask that excludes the handle-gizmo layer so clicks hit objects,
not the gizmos.

## Workspace interface

The UI Toolkit header and scrollable inspector match the workshops. **Advanced tools** contains every handle type, axis mask, snapping mode, the three snap amounts, scale multiplier, runtime settings, Group all and Clear handles. **Event log** shows interaction events. Camera gestures remain middle-drag orbit and wheel zoom; right-click drops a target.

**Handle size** is measured in rendering pixels (64–512, default 192). The separate advanced scale multiplier retains its original meaning. Turning auto-scale off disables the pixel control; its preference is preserved. Applying runtime settings adopts their multiplier once; later explicit slider edits remain effective.

**Reset scene** restores spawned target poses, default tools and camera, and clears this showcase's selection, handles and event log. It preserves the pixel-size preference and never removes another controller's handles. Clear handles and Group all also operate only on this showcase's handles.

**UI input test** can show a dedicated region in the viewport and allow input through that region. The header, inspector, Help and gallery navigation always consume their gestures, even when the experiment allows input through. The standalone sample provides the same protection without Demo Browser. Disable/re-enable releases owned handles, restores borrowed manager state, and rebinds the new UI tree.

Assign this sample's `UI/Showcase.uxml` and `UI/Showcase Panel.asset` when adding `HandleDemo` to another scene. Edit scenario styles in `UI/Showcase.uss`; the common `UI/DemoTheme.uss` is generated from the repository theme. No sample depends on another sample's assets or assembly.
