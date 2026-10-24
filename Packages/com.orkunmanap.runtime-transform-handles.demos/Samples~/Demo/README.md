# Runtime Transform Handles — Demo

Open `RuntimeTransformHandlesDemo.unity` and press **Play**. The scene is self-contained:
`HandleDemo` spawns its own target cubes, ensures a camera, and draws an on-screen control panel.

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
