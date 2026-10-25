# Modular Building: Wall Study

Import **Modular Building** from Package Manager > Runtime Transform Handles Demos > Samples. Run **Tools > Transform Handles > Setup Layer**, open `ModularBuilding.unity` and press Play. Supports Unity 2021.3+ and the Legacy input backend, or Input System 1.1.1+ when enabled. The sample uses built-in UI Toolkit and uGUI's EventSystem bridge; TMP and its Essential Resources are not required. Level Editor is a separate optional import. The scene shader does not require URP.

Arrange eight supplied straight-wall and L-corner modules on a foundation. Click any wall face to select the complete module. **W / Move** exposes X/Z arrows and the XZ plane pad; positions snap to a fixed 1-unit world grid. **E / Rotate** exposes the Y ring with fixed 90-degree steps from drag start. Height stays at ground level and scale stays one. This scene deliberately uses single selection and fixed constraints; it has no scale or World/Local toggle.

**Left-drag** orbits, **right-drag** pans, and scroll zooms. A press beginning on a handle manipulates the module instead of the camera; UI presses cannot move the scene underneath. WebGL preserves fractional wheel movement through the sample-local plugin. Native pinch is not implemented. **Handle size** adjusts the gizmo from 64 to 512 rendering pixels, initially 192; increase it for a high-DPI display. The white UI scales with viewport resolution independently of handle size.

**Reset scene** restores every surviving module's initial transform, camera framing and Move mode, and clears selection. It keeps the handle-size preference for this controller's lifetime. The single left inspector groups the selected module, Move/Rotate tools, grid constraints and always-visible handle-size controls. Size can be adjusted without a selection; tools, Reset and size edits are blocked during a handle drag. Enter or focus loss commits the size field; Escape cancels that edit without changing selection. A click into the scene while editing first commits and consumes the press. Outside text editing, Escape closes Help first, then clears selection.

**Help** in the header opens a white controls card with the camera/keyboard guide and informational notices. It starts closed, works without a selection and closes when the controller is disabled. Its dismissal press is consumed so it cannot select or move anything underneath. The previous bottom strip and size popover are replaced by the inspector; guidance does not float over the scene.

This is a finite arrangement demo: it has no catalog/spawning, deletion, multi-selection, collision validation, placement confirmation, save/load or Undo/Redo. Modules may overlap or move beyond the presentation foundation. Reset does not recreate modules destroyed by external code. Both this sample and Level Editor can be imported into one project and run separately; simultaneous active controllers in additive scenes are not supported because they configure the same manager.

## Scene structure

`ModularBuildingDemo` has serialized references to the scene camera, module root transforms, floor renderer, material, shortcuts-disabled handle settings, and the sample-local UXML layout and PanelSettings asset. Each module is an unparented transform at y=0, with unit scale, integer X/Z coordinates and quarter-turn yaw. Child box meshes and colliders make the wall or L shape. Invalid initial setup fails explicitly. Child collider selection resolves only to registered module roots.

Straight modules extend from local x=0 to x=2. Corner modules have mating faces at local (2,0,0) and (0,0,2); wall height is 1.3 and thickness 0.2 units. Their origin is the corner centerline intersection on the floor. The two corner boxes meet without overlapping volumes. Integer pivots and quarter-turn rotations keep connection faces on the same lattice. Placement constraints use Core `Handle.Axes`, `PositionSnap`, `RotationSnap` and `SnappingType`; the sample does not apply a second transform correction after the drag.

The controller owns only its own handles, UI, event system (if needed) and runtime material instances. It restores a borrowed manager's camera/settings/UI-blocking state on disable and restores child renderer materials on destruction. An owned manager survives disable/re-enable and is destroyed with the controller. Sample assets, shader names and WebGL symbols are independent of Level Editor; no shared demo framework or new Core API is introduced.

## UI Toolkit layout and typography

`UI/ModularBuilding.uxml` defines the visual tree and `UI/ModularBuilding.uss` supplies the white theme, spacing and control states. Open the UXML in UI Builder to inspect the layout. Keep its asset URLs relative to this sample so imports remain self-contained. `Workshop Panel.asset` scales the interface against a 1280×720 reference resolution; a compact class adjusts margins and hides the secondary header title in narrower windows. Text does not use auto-size to squeeze into controls. The panel uses its own text settings and the included Liberation Sans source font under `Fonts/LiberationSans - OFL.txt`; UI Toolkit creates the font rendering data without a TMP package or global TMP Settings asset. Glyph coverage is limited to the source font, including Turkish letters.

The existing controller binds the UXML controls to the same Core operations. It recreates bindings after the owned UIDocument is re-enabled and releases them on disable. Only visible UI surfaces intercept pointers; the root ignores picking so the scene remains interactive. The EventSystem bridge lets Core's UI blocking see Toolkit panels, while the demo's current-position check accepts both Toolkit and uGUI panels. Slider capture, text focus and Help dismissal retain input ownership; W/E tool shortcuts do not navigate focused UI controls.

## Validation and WebGL

The optional `Tests` assembly is guarded by `UNITY_INCLUDE_TESTS`. After importing exactly once and setting up the layer, run PlayMode tests with `-testFilter TransformHandles.Samples.ModularBuilding.Tests`. Tests include actual Core plane/ring interaction, child selection, geometry joins at all quarter turns, UI/input ownership, Reset and lifecycle cleanup.

The repository build helper imports without overwriting customized imports. On a clean CLI project, prepare in one invocation and build in the next, so Unity can load the newly compiled sample assembly before serializing the player. Building immediately after a first import is rejected with an actionable error:

```sh
Unity -batchmode -quit -buildTarget WebGL -projectPath PROJECT \
  -executeMethod BuildScript.PrepareModularBuildingWebGL -logFile IMPORT_LOG
Unity -batchmode -quit -buildTarget WebGL -projectPath PROJECT \
  -executeMethod BuildScript.BuildModularBuildingWebGL -buildPath OUTPUT -logFile LOG
```

Install WebGL Build Support and use the repository's WebGL template/build helper. Serve OUTPUT over HTTP. The public Pages entry point remains the original showcase. Before replacing an existing sample import, back up customizations and remove the old copy explicitly to avoid duplicate GUIDs and assemblies.
