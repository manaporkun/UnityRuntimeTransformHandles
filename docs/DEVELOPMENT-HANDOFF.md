# Runtime handles and optional demos: development handoff

This branch is a development checkpoint, not a published package release. It contains the accumulated lifecycle fixes, Precision visuals, optional-package split, Level Editor UI/input work and group transforms. Continue from this branch rather than rebuilding the changes from older task branches.

## Latest checkpoint: Lamp Assembly (2026-10-25)

The owner approved replacing the repetitive desktop arrangement scenario with lamp assembly, then separately approved the small Core API needed to keep constrained joints and their arcs in sync. The optional sample now has a fixed base, a free arm and a free head. Keep the `ProductConfigurator` folder, scene, controller and build method identifiers; the user-facing name is **Lamp Assembly**. Existing Product Configurator imports must be backed up outside Assets and removed before importing the renamed sample, because GUIDs are preserved.

Free parts move in XYZ. Releasing within 0.25 units of the compatible socket starts a 0.18-second alignment with the owned handle removed. Only completion commits parentage and switches to a local X hinge. The arm uses −30°/+60°, the head −60°/+60°. A new head attachment requires a mounted arm. Detaching an arm preserves its attached head and world pose; the head still articulates, and reattaching the arm preserves the head's relative angle. This is single selection, with no finish choices, World/Local switch or scale. The shared white Toolkit shell, pixel-size input, Help and Demos navigation remain.

`AssemblyPart` records use Transform references, not recursively serialized part references. Startup validates an acyclic unit-scale assembly. Snapshot rollback restores surviving roots, hierarchy and state after interruption. Missing idle sockets/parents release surviving attached parts into Free state; destroyed children are not reconstructed. Preview meshes use sample-owned shader/material assets, render without pickable objects, and reuse a mesh list. A stronger socket marker uses a cached MaterialPropertyBlock.

Core adds optional `RotationAxis.DragAngleLimits`: finite grab-relative bounds within [-180,180] containing zero. Configure before the first interaction step, including from interaction-start callbacks. Changes after that step throw until EndInteraction/cancellation. Null preserves existing unrestricted behavior. Bounds also cap arc work. Limits apply after snap, support immediate reversal after overshoot, and drive both pivot and arc. The sample converts authored joint bounds relative to the current angle at each grab. Keep this API narrow; no general joint/IK framework was introduced.

Validation: Core 198 PlayMode and 74 EditMode tests pass on Unity 2021.3.0f1 Legacy and Unity 6000.3.16f1 Legacy/Input System. The combined Core/sample PlayMode totals are 314, 314 and 317 respectively, with zero skips. Core test fullnames match the updated required manifest. Imported sample-only totals are 116, 116 and 119; independent Lamp Assembly import passes 23/23 on 2021. The final socket marker presentation was rechecked with the 2021 Legacy and Unity 6 Input System sample suites. Python CI tests (31), minimum API/meta, five-sample package boundaries, shared-theme synchronization, scroll bridges and whitespace checks pass. Local results do not imply remote CI success.

The combined WebGL player was rebuilt and checked in Chromium at 1440×900: gallery entry, visible socket marker, actual mouse-driven arm/head attachment, constrained hinge movement and reversal, subtree detach/move, Help and gallery return. The standalone player starts independently with the Demos slot hidden; 960×640 CSS at DPR 2 produces a 1920×1280 buffer with readable inspector and Help text. Browser error, page-error and failed-request arrays were empty. Existing hidden URP shader diagnostics remain at log level. No physical Mac trackpad validation was performed. The stronger final marker was checked in the rebuilt combined player; the standalone presentation check preceded that cosmetic marker adjustment.

Independent source review approved the bounded Core API and final sample logic after the lost-socket recovery fix. No public deployment, merge or release is authorized. Continue on `feat/modular-building-demo`. Core and demos development versions are not proof that published Core 3.3.0 contains this new API; align the release pair before publishing.

## Previous checkpoint: Product Configurator (2026-10-25)

The new independently imported `Samples~/ProductConfigurator` adds a beveled desktop, monitor, lamp and accessory tray with a sample-local pipeline-independent material/shader setup. It is the fourth workspace and fifth optional sample. Core remains unchanged. The scene can be used alone or opened through Demo Browser; the combined build imports it, and separate `PrepareProductConfiguratorWebGL` / `BuildProductConfiguratorWebGL` repository-host helpers are available.

The controller follows the Modular Building lifecycle and Level Editor group selection. Product records contain root/name/finish-renderer references; cached child mesh bounds define conservative full-product surface containment. Movement is constrained to XZ, rotation to Y, with World/Local movement axes and last-selected group reference. There is no Scale or snapping. Groups rotate around the mean of product roots. Finish choices affect designated bodies only, preserve fixed detail materials and survive disable/re-enable. Reset restores layout, finishes and camera/tool defaults while keeping pixel-size preference.

An invalid release restores every selected product to its drag-start pose. Interrupted interactions cancel even when their last preview was valid; manager disable/navigation, focus loss and destroyed selection members restore survivors. Handle callbacks are detached before removal to avoid recursive cancellation. Controls never destroy handles owned by another controller. Bounds/settings/model validation is sample-local. The scene assumes a fixed horizontal desk and unparented unit-scale yaw-only product roots; collision solving, stacking, runtime geometry changes, catalog/spawning, saving, Undo and mobile controls remain out of scope.

Shared-theme copies now cover five samples. The gallery uses two columns for four installed workspaces, and its scrollbars use the shared visual language. Placement feedback stays beside the selection above scrolling settings. Sample-local assets and scroll-bridge symbols preserve independent imports.

Validation: all imported sample tests pass 115/115 on Unity 2021.3.0f1 Legacy, 115/115 on Unity 6000.3.16f1 Legacy and 118/118 on Unity 6000.3.16f1 Input System. The isolated Product Configurator 2021 import passes 22/22, including actual Core group translation/yaw, manager-owned cancellation, atomic bounds rollback, real supplied-scene geometry and material lifetime. Python CI tests (31), five-sample boundaries, theme drift, minimum API/meta and all three scroll bridges pass. Results are local, not a claim of remote CI success.

The combined WebGL player was checked at 1440×900 and Retina 960×640: four-card gallery, composite selection, finish changes and the mixed-selection indicator, actual group yaw, invalid placement preview and rollback, Help dismissal and gallery return. Browser error, page-error and failed-request arrays were empty. Existing URP hidden-shader diagnostics remain at log level. The standalone Product Configurator WebGL build also passes startup, rendering and selection checks with no browser errors; its Demos header slot is hidden. No physical Mac trackpad validation was performed.

## Previous checkpoint: consistent demo workspaces (2026-10-25)

All three workspaces now use UI Toolkit with the same white header, scrollable inspector, typography, transform controls, pixel-size field and Help presentation. Level Editor no longer creates a uGUI canvas; the original showcase no longer uses IMGUI. The Browser binds each scene's named `demo-gallery` header button, including after controller disable/re-enable. Standalone imports hide that slot. Core is unchanged.

The canonical stylesheet is `ci/demo-ui/DemoTheme.uss`. Run `python3 ci/sync-demo-ui.py` after editing it; CI checks that all four independent sample copies match. Local layouts, fonts, icons and panel settings keep every sample independently importable. See `ci/demo-ui/README.md` for the header-slot contract. This updates package sources, never consumer-owned imported Assets.

Showcase keeps its advanced handle types, axis masks, snapping, runtime settings and event log in foldouts. Pixel size is separate from its original scale multiplier. Reset restores owned target poses and tool/camera defaults, preserves pixel preference and never destroys another controller's handles. Only the explicit test region can bypass UI blocking; normal controls and Help consume their entire gesture. Existing Level Editor multi-selection and World/Local transform behavior remain covered.

The full sample suite passes 93/93 on Unity 2021.3.0f1 Legacy, 93/93 on Unity 6000.3.16f1 Legacy, and 96/96 on Unity 6000.3.16f1 Input System. Clean independent 2021 imports pass Showcase 5/5, Level Editor 45/45, Modular Building 36/36, and Browser's three applicable gallery tests. All 31 Python CI tests pass. New tests cover scene-header rebinding, pixel input cancellation, owned-only reset, actual camera movement, settings/scale interaction and UI gesture ownership.

The combined WebGL build was checked in Chromium at 1440×900 and Retina 960×640. The shared layouts, advanced dropdowns, inspector wheel/scrollbar dragging, Help dismissal and scene-header navigation render and respond correctly. Automated Unity tests cover text focus and the bypassable test-region gesture rules. No physical Mac trackpad validation was performed.

## Previous checkpoint: Demo Browser (2026-10-25)

The optional `DemoBrowser` sample adds a UI Toolkit gallery for Modular Building, Level Editor and the original showcase. Start its scene first and include the desired demo scenes in Build Settings. Cards resolve unique full scene paths from the build; missing demos are omitted. No cross-sample assembly or asset references were introduced, and Core is unchanged.

Each workspace has a bottom-right **Demos** button. Returning loads the actual gallery scene and discards workspace edits. A frame separates completed teardown from another load, preventing deferred destruction of the old manager from affecting a new workspace. The Browser preserves its UI and owns an EventSystem when needed; it recreates input after a borrowed scene-local system disappears.

Navigation claims its press before Core updates and keeps it through release. The legacy showcase's test-panel toggle cannot bypass navigation; gestures starting on navigation cannot later orbit its camera. Showcase teardown now releases owned handles, settings, manager and material instances and restores borrowed manager state. Its local target material uses a pipeline-independent shader so cubes render correctly in the combined URP player. Level Editor recognizes Toolkit raycasts and tolerates its canvas being destroyed before OnDisable during scene unload.

`BuildScript.PrepareDemoBrowserWebGL` imports all four samples; run `BuildScript.BuildDemoBrowserWebGL` after compilation in a separate CLI invocation. Existing imports are preserved. Individual build methods and the public Pages workflow are unchanged. See the [Browser README](../Packages/com.orkunmanap.runtime-transform-handles.demos/Samples~/DemoBrowser/README.md) for consumer and build instructions.

Validation: the complete imported sample suite passes 86 tests on Unity 2021.3.0f1 Legacy, 86 on Unity 6000.3.16f1 Legacy, and 88 on Unity 6000.3.16f1 Input System. This includes transitions while selected/dragging, returning repeatedly, borrowed manager restoration, EventSystem recovery, duplicate-load rejection, and same-frame navigation input with stale EventSystem hover. A clean Browser-only 2021 consumer without other sample imports passes its three applicable gallery/resolution tests. All 29 Python CI tests, package boundaries, minimum-API/meta checks and wheel bridge checks pass. The Built-in 2021 host retains the original showcase's known missing optional URP camera-script warning; it does not affect the Browser-only import.

The combined WebGL build passes Chromium checks at 1440×900 and Retina 960×640: gallery layout, all three workspaces, selected-target exits, Modular Building plane dragging, and returns to the gallery. The showcase target colors render correctly. With the showcase UI guard off, dragging from navigation with middle mouse and scrolling over navigation leave the scene pixels unchanged. Browser error/page-error/request-failure arrays are empty. The same three inherited URP hidden-shader diagnostics remain at log level; no physical Mac trackpad check was performed.

## Package boundaries and current behavior

- Core: `Packages/com.orkunmanap.runtime-transform-handles`. Stable package identity; runtime handles, assets, editor setup, tests and API documentation. No dependency on demos.
- Optional demos: `Packages/com.orkunmanap.runtime-transform-handles.demos`. Independently importable original feature showcase, Level Editor, Modular Building and Demo Browser under `Samples~`. Keep application-specific UI and scenario rules here.
- Level Editor: Shift-click toggles members; Move/Rotate use World or Local axes. Local references the last selected surviving object. Rotation uses the shared mean of object pivots. Group Scale exposes the center only and scales object sizes and pivot offsets together. Single-object scaling retains axis controls.
- `Handle.LocalSpaceReference` and `Handle.UniformGroupScale` are opt-in Core controls. Uniform group scaling requires a pure Scale handle, multiple targets and XYZ axes; partial masks disable its controls. Defaults preserve existing consumer behavior. See Core README/API docs for details.
- Left drag orbits, right drag pans, scroll zooms. UI and handle gestures consume selection/camera input. All three demos expose size controls in their UI Toolkit inspectors. They support 64–512 rendering pixels, default 192; Retina pixel ratio is not silently folded into that value.
- Precision handles keep axis-colored hover and picking volumes. Native remains available. Handle creation synchronizes visuals immediately to avoid an origin-to-target flash.
- Ownership, target destruction, disable/re-enable, callback reentrancy and Editor session restoration have regression coverage. Re-enabling a GameObject inside its OnDisable callback is unsupported by Unity; defer activation until the next frame. Component.enabled reactivation is separately covered.

## Start a consumer or demo

Install both package folders through explicit local file dependencies for development. For a Git consumer, pin both project manifest URLs to the same branch commit; the optional demos README includes the two-entry example. Git dependencies inside a package manifest do not recursively install sibling Git packages.

Import **Level Editor** from Package Manager > Runtime Transform Handles Demos > Samples. Run **Tools > Transform Handles > Setup Layer**, open the imported LevelEditor scene, and enter Play mode. Imported Assets copies are consumer-owned: back up changes before replacing imports, and do not leave duplicate sample imports with identical GUIDs/assemblies.

In this repository, `Assets/Editor/BuildScript.cs` provides `BuildScript.BuildLevelEditorWebGL`. Run the Editor with WebGL selected at startup:

```sh
Unity -batchmode -quit -buildTarget WebGL -projectPath PROJECT \
  -executeMethod BuildScript.BuildLevelEditorWebGL -buildPath OUTPUT -logFile LOG
```

Install WebGL Build Support first. Serve OUTPUT with a local HTTP server and check actual interactions. `BuildScript.BuildWebGLForPages` still builds the original showcase; it does not silently switch to Level Editor. Published Pages is not this local demo build.

## Validation checkpoint (2026-10-24, macOS ARM64)

| Editor / input configuration | Core PlayMode | EditMode | Level Editor PlayMode |
| --- | ---: | ---: | ---: |
| Unity 2021.3.0f1 / Legacy / Built-in, no URP or Input System package |176|74|44|
| Unity 6000.3.16f1 / Legacy |176|74|44|
| Unity 6000.3.16f1 / Input System |176|74|44|

All tests passed with no skips. Exact-suite verification also passed all 24 Editor session cycles per configuration, observed versions/settings and package hashes. Runtime source has not changed since this validation; final handoff edits only correct descriptions, add this document and normalize trailing whitespace without changing non-whitespace content. Unity 2021.3.45f1 passed an earlier smaller suite, not this exact revision. These are local results, not a claim of remote CI success.

The final Level Editor WebGL build passed automated Chromium selection, group rotation in World/Local, uniform size-and-spacing scaling, complete invalid-scale rollback and recovery, single-object axes, camera gestures, popover handling and responsive layout checks. Final browser errors were empty. No physical Mac trackpad test was performed. Builds and raw logs live outside Git and are disposable; the following commands and checked-in tests are the reproducible source of truth.

```sh
python3 -m unittest discover -s ci -p 'test_*.py'
python3 ci/verify-package-boundaries.py
node ci/test-level-editor-input.cjs
git diff --check
```

Run the static checks in `.github/workflows/min-api-lint.yml`, workflow syntax validation with actionlint, and manifest validation in `publish-upm.yml`. Follow `ci/README.md` for prepare/run/collect/verify. Use fresh artifact directories. Core runs use `-testFilter TransformHandles.Tests.`; separately run imported sample tests with `-testPlatform playmode -testFilter TransformHandles.Samples.LevelEditor.Tests`. Use disposable consumer projects; the isolated session-test flag must not be used in a project containing unsaved work.

## Next work and boundaries

The owner wants more demos, rather than Undo/Redo. Undo/Redo is outside the intended scope. The Modular Building plan was independently reviewed and approved. Its implementation is on `feat/modular-building-demo`; see the sample README for controls and constraints. Validation results for this addition are recorded below.

Suggested order:

1. Modular Building now supplies eight wall/corner modules with fixed horizontal grid and quarter-turn yaw, single selection and Reset. No catalog, collision checks or confirmation flow. Keep it independently importable; run demo controllers separately.
2. Vehicle customization: attached accessories, Local axes, constrained movement and parent-child handling. Verify rotated/nonuniform parent support before exposing combinations that imply distortion-free scaling.
3. Mechanical alignment: orthographic views, fine snapping and pivot behavior. Document units/tolerances; do not imply CAD-grade accuracy or add numeric input without an explicit interaction plan.

Each should be an independently importable sample in the optional package, following LevelEditor scene/controller/asmdef/README patterns. Do not create a shared demo framework until a second concrete scenario establishes reuse. Plan, independently review, obtain owner approval, implement, independently review the diff, then run Unity and actual visual/input QC.

## Known follow-ups

- The previous cancellation notice can remain after a later successful interaction. Clearing it on the next valid drag is UI polish; immediate rollback errors must remain visible.
- The original showcase retains pre-existing URP magenta-target/shader-diagnostic and stale shortcut-HUD issues. Relocation did not fix them. Level Editor has its own opaque shader and current UI labels.
- Group rotation can move objects through the sample floor; no collision constraint is promised. Group nonuniform scaling and individual-pivot rotation are not implemented by the new demo controls.
- Extreme handle sizes can extend outside the viewport; 64 rendering pixels is small on Retina.
- Core/demos manifest versions are a local development pair, not a claim that an older published Core 3.3.0 contains these changes. Align and test release versions before publishing. Sample relocation is a distribution breaking change; preserve migration docs, GUIDs and explicit import cleanup guidance.
- Do not merge into main or dispatch publishing merely to continue development. Existing workflows publish on main. This checkpoint is a branch push only.

## Modular Building checkpoint (2026-10-25)

`feat/modular-building-demo` adds a third independently importable sample. Import **Modular Building** and open `ModularBuilding.unity`. Eight supplied wall/corner roots use child meshes/colliders; clicking any face selects the module. Move uses world XZ and a fixed 1-unit grid; Rotate exposes only Y with 90-degree steps. Reset restores the authored layout, camera and Move mode while retaining handle size. The scene starts without overlapping modules. Corner 8 is placed left of the partial room so its default plane pad stays above the bottom UI at both tested viewport sizes.

The sample adapts Level Editor's established UI/input/ownership pattern with independent asset GUIDs, shader and browser symbols. It intentionally does not introduce a shared framework or change Core. The README documents the serialized scene structure and finite-module scope: no catalog, spawning, scale, multi-selection, collision rejection, persistence or Undo/Redo. Co-import is supported; run demo controllers separately because they configure the same singleton manager.

| Configuration | Modular Building PlayMode | Level Editor regression |
| --- | ---: | ---: |
| Unity 2021.3.0f1 / Legacy / Built-in | 34 | 44 |
| Unity 6000.3.16f1 / Legacy | 34 | 44 |
| Unity 6000.3.16f1 / Input System | 34 | 44 |

All passed without skips. The final Modular Building scene/test revision also passed all 34 tests as a standalone import in the Unity 2021 consumer. A fresh Core regression run passed 176 PlayMode and 74 EditMode tests in that consumer; Core files are unchanged. New coverage exercises real ray/plane and ring snapping, compound selection, module joins at every quarter turn, initial non-overlap, immediate non-origin handle placement, input ownership, reset, popover/size behavior and resource cleanup.

The Unity 6 WebGL build was tested in Chromium: plane and axis movement, quarter-turn rotation, orbit/pan/scroll, numeric handle sizing, popover dismissal, Reset and compound-module dragging. Reset restored a scene crop pixel-for-pixel after transforms and camera gestures. Layout checks cover 1440×900, 960×640 and device pixel ratio 2; final Corner 8 plane drags passed at the default 192 px size. No JavaScript exceptions or Unity script errors were observed. The URP WebGL host logs unsupported-shader diagnostics for `Hidden/CoreSRP/CoreCopy`, `Hidden/Universal Render Pipeline/StencilDitherMaskSeed` and `Hidden/Universal/HDRDebugView` at normal console-log level, plus Chromium GPU ReadPixels performance warnings. The same three shader diagnostics occur in the preceding foundation build; they are not introduced by this sample. Sample materials and handles render correctly, but this is not a claim of a diagnostic-free URP browser build. No physical trackpad test was performed.

Clean first-import testing exposed a build-helper defect: importing a new assembly and immediately building in the same Editor invocation could serialize a missing controller. Build entry points now reject an unloaded sample assembly before changing settings or writing output. For a clean Modular Building CLI consumer, run `BuildScript.PrepareModularBuildingWebGL`, then `BuildScript.BuildModularBuildingWebGL` in a new Unity invocation. The fresh-import rejection and prepare/build sequence were verified. Pages CI prepares the original showcase before Unity starts; its public scene selection is unchanged. The actual CI preparation script was executed against a disposable checkout, with all original file bytes preserved and overwrite rejection checked.

Static verification passed: 27 Python CI tests, three-sample package/GUID boundary checks, both raw-wheel bridges plus co-import isolation, minimum Unity API/meta checks, manifest/assembly JSON validation, actionlint and whitespace checks. Independent plan, code and visual reviews completed. These are local results, not a claim of remote CI or deployment. Builds/logs are disposable and excluded from Git; use the sample README and `ci/README.md` to repeat the checks. No publishing or main merge is part of this checkpoint.

## Help card refinement (2026-10-25)

Modular Building now keeps the viewport free of floating footer paragraphs. The bottom strip contains the selection and constraints, or a short selection cue. The lower-right Help button opens a rounded white card with the control guide and contextual notices. Body text remains 20 reference pixels; the card grows to fit instead of shrinking type. Help works without selection, starts closed and closes on disable.

Help and handle-size popovers are mutually exclusive. The first click on another launcher dismisses the current popup; the next click opens the other. The dismissal backdrop remains active through Core input until LateUpdate, and Escape closes the popup before deselecting. This follows the existing size-popover ownership pattern without new components or Core changes.

All 37 sample PlayMode tests passed in Unity 2021.3.0f1 Legacy and Unity 6000.3.16f1 Legacy/Input System. New regressions cover Help raycasts, deferred dismissal, mutual exclusion, Escape, disable/re-enable, drag restrictions and measured text fit. Independent source review, package boundaries, minimum-API/meta and whitespace checks passed.

Final WebGL visual checks passed at 1440×900, 960×640 and device pixel ratio 2, including the 20 px body text. Actual pointer checks covered both popup launchers, dismissal without click-through, and Escape preserving selection. Console diagnostics match the inherited URP shader messages listed above; no new diagnostics were observed.

## Modular Building typography (2026-10-25, superseded)

This historical TMP checkpoint is superseded by the UI Toolkit implementation below. TMP is no longer required by Modular Building.

The Modular Building UI now uses TextMeshPro for every label and the handle-size numeric field. The existing `Label` factory stays in the controller; no shared UI framework was introduced. Selection, mode and constraints share a 32-unit row and middle alignment. Help control rows are centered, while explanatory paragraphs remain top-aligned. Font sizes stay explicit. The numeric field has a masked text viewport, caret and selection styling; Enter commits and Escape cancels as before.

The sample owns a dynamic `Workshop Sans SDF` font asset generated from the bundled Liberation Sans source at 90 points, with 9-pixel padding and a 1024-square atlas. Its font source and generated atlas are covered by the included SIL Open Font License. The scene references the font explicitly. TMP code, Essential Resources shaders and global TMP Settings are not copied into the sample; consumers keep their own standard Essentials import. Source-font-supported names, including Turkish characters, can populate the atlas dynamically; arbitrary Unicode coverage is not promised.

Installation now requires TMP 3.0.6+ on Unity 2021.3–2023.1, or uGUI 2.0's integrated TMP on Unity 2023.2+ / Unity 6. Import TMP Essential Resources once before Play/build. Sample and test asmdefs stay disabled until the compatible dependency is available. Core and the other optional samples are unchanged. A Unity 2021 consumer with TMP removed passed the no-TMP import/compilation check. The WebGL helper rejects absent Essential Resources before changing build settings or writing output.

All 39 Modular Building PlayMode tests passed without skips on Unity 2021.3.0f1 Legacy and Unity 6000.3.16f1 Legacy/Input System. Added integration coverage verifies shared-font rendering, actual selection-row baselines, supported name glyphs, numeric viewport ownership and Enter/Escape editing. Package boundaries, minimum API, metadata and whitespace checks passed. Independent source review found no blocking issues.

The rebuilt WebGL player was checked in Chromium at 1440×900 and 960×640, at device-pixel ratios 1 and 2 (1440×900 CSS produces a 2880×1800 backing buffer at DPR 2). Screenshots confirm the aligned selection row, help text fit, numeric selection and clipped horizontal scrolling. Browser interaction confirmed Escape restores 192 after a long edit, and Enter applies 320 to both the label and visible handle. No new console diagnostics appeared: the same three inherited hidden URP shader diagnostics documented above remain. Physical Mac trackpad testing was not part of this typography check.

## Modular Building UI Toolkit checkpoint (2026-10-25)

Modular Building now uses a slim header and one white left inspector containing selection, Move/Rotate, grid constraints and always-visible handle size. Help opens from the header. The former bottom strip and size popup are removed. The source layout is `UI/ModularBuilding.uxml`; its stylesheet is `UI/ModularBuilding.uss`. `Workshop Panel.asset`, `Workshop Text.asset` and the default-runtime theme import are sample-local. The existing controller binds the controls directly to its existing Core operations, without a shared UI framework or new Core types. Core is unchanged.

The sample uses the licensed Liberation Sans source font through UI Toolkit, without TMP or Essential Resources. The previous generated TMP font asset and panel sprite are removed. Demos explicitly depends on the built-in UIElements module; uGUI remains for the EventSystem bridge. Toolkit assets were generated/imported with Unity 2021 and checked in Unity 6. The boundary verifier now checks UXML/USS/TSS references, including inline URLs, and rejects cross-sample, missing or external asset paths. Keep package source references relative when editing in UI Builder.

The owned UIDocument recreates its tree after disable/enable; references and callbacks are rebound once and detached on disable. Runtime panel settings are cloned rather than mutating the serialized asset. Empty panel roots ignore picking. Both Toolkit PanelRaycaster and existing uGUI GraphicRaycaster surfaces block demo input. An uninitialized input module blocks scene navigation during re-enable until Unity is ready. Help dismissal remains deferred until LateUpdate. Text edits commit on Enter or focus loss, cancel on Escape and consume the editing-end frame; a scene press while editing commits without selecting or navigating. Slider capture and W/E ownership remain intact.

Final PlayMode results: Unity 2021.3.0f1 Legacy 36/36; Unity 6000.3.16f1 Legacy 36/36; Unity 6000.3.16f1 Input System 37/37. No skips. The 2021 consumer has no TMP package, and the final Unity 6 runs passed with TMP Essential Resources removed. The additional Input System test hits a real Core plane handle, then sends a same-update pointer-enter/down over a Toolkit overlay and runs Core before the demo; no drag starts. Its cloned test input settings use both IgnoreFocus and AlwaysGameView so batch mode processes player actions, then restore the original settings. Production default actions are used. The early test failure was Editor-only event routing, not a Core change.

All 12 package-boundary tests, minimum-API/meta checks and whitespace checks passed. Independent source and visual review approved the result. WebGL screenshots at 1440×900 and 960×640, DPR 1/2, show no clipped or overlapping text. Browser checks covered Move/Rotate, numeric Enter/Escape/Tab and long-value clipping, slider dragging outside the inspector, Help close/outside dismissal, and actual plane movement. Dragging on Help above an enlarged handle left the scene region pixel-identical before/after. The previous three hidden URP shader diagnostics remain; there are no new Toolkit, font or exception diagnostics. Physical Mac trackpad testing was not performed.
