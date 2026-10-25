# Lamp Assembly

Build an articulated desk lamp: attach the arm to a fixed base, then attach the head to the arm. Rotate the joints within their mechanical limits, detach parts, and move the resulting subassemblies. The sample uses the shared white UI Toolkit shell and public Core APIs.

Install Core and the optional Demos package, import **Lamp Assembly**, run **Tools > Transform Handles > Setup Layer**, and open `ProductConfigurator.unity`. The existing folder, scene and controller identities are retained for import/build compatibility. Unity 2021.3.0f1 or later is supported. No URP or other sample is required. The new input backend requires Input System 1.1.1+. Import the sample only once. If you previously imported Product Configurator, back up that import outside Assets and remove it before importing Lamp Assembly, because the sample GUIDs are retained. This sample requires the Core revision containing `RotationAxis.DragAngleLimits`; the development manifests are not a claim that published Core 3.3.0 includes this API.

## Assemble and articulate

Click the arm or head to select one part. Child colliders resolve to the nearest registered part; the base is fixed. Free parts expose XYZ movement. A compatible socket is highlighted; within 0.25 world units a translucent preview shows the final pose. Release nearby to align over 0.18 seconds. The handle is removed during alignment and rebuilt only when attachment commits. No orientation match is required.

The arm attaches to the base first. A new head attachment is allowed only while the arm is mounted. Attached parts expose a local X hinge at their authored root: arm −30° to +60°, head −60° to +60°, relative to the socket's neutral rotation. The part and rotation arc stop together. Reverse motion responds immediately after reaching a limit. The boundary is not a collision simulation.

**Detach part** preserves the world pose and attached children. A detached arm carries its attached head; that head can still articulate or detach. Reattaching the arm aligns its own hinge to neutral without resetting the head's relative angle. Each handle owns one root, so children follow their parent once. Free parts cannot rotate or scale; attached parts cannot translate or scale. World/Local switching, multi-selection and finish choices are absent from this scenario.

Left-drag orbits the camera; right-drag pans; scroll zooms. UI and handle gestures own the entire press. Handle size accepts 64–512 rendering pixels. W selects Move for a free part; E selects the hinge for an attached part. Help explains the flow. **Demos** returns to the gallery when included in the build; the link is hidden for independent imports.

Escape clears selection and cancels an active drag or alignment. Focus loss, disable, manager cancellation and gallery navigation restore the pre-drag hierarchy, pose and state for surviving parts. Destroying a parent normally destroys its children too; the sample does not reconstruct destroyed objects. Idle disable/re-enable preserves committed assembly. Reset restores the exploded layout and camera while keeping handle size.

## Implementation and validation

`AssemblyPart` records reference a root, parent root, socket and limits. Startup rejects cyclic attachments, non-unit scales and unsupported hinge ranges. State belongs to the sample; Core only supplies optional grab-relative axis limits. The sample converts absolute limits using `[minimum − currentAngle, maximum − currentAngle]` at interaction start. All demo hinge ranges span at most 180°. Preview meshes are drawn without scene objects or colliders and cannot intercept selection.

Run imported PlayMode tests with `-testFilter TransformHandles.Samples.ProductConfigurator.Tests`. Tests cover attachment ordering, socket distance, completion timing, articulation mode, parent-child movement, subtree detach, cancellation, idle re-enable, Reset, UI ownership and the actual authored scene. Core rotation-limit tests cover bounds, arc consistency, wrapping, snapping, World/Local axes and repeated grabs. The combined gallery build includes this scene; the standalone build helper retains the `ProductConfigurator` identifier.

No IK, collision resolution, catalog, save/load, Undo/Redo, export or mobile controls are included.
