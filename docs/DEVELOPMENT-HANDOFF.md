# Runtime handles and optional demos: development handoff

This branch is a development checkpoint, not a published package release. It contains the accumulated lifecycle fixes, Precision visuals, optional-package split, Level Editor UI/input work and group transforms. Continue from this branch rather than rebuilding the changes from older task branches.

## Package boundaries and current behavior

- Core: `Packages/com.orkunmanap.runtime-transform-handles`. Stable package identity; runtime handles, assets, editor setup, tests and API documentation. No dependency on demos.
- Optional demos: `Packages/com.orkunmanap.runtime-transform-handles.demos`. Independently importable original feature showcase and Level Editor under `Samples~`. Keep application-specific UI and scenario rules here.
- Level Editor: Shift-click toggles members; Move/Rotate use World or Local axes. Local references the last selected surviving object. Rotation uses the shared mean of object pivots. Group Scale exposes the center only and scales object sizes and pivot offsets together. Single-object scaling retains axis controls.
- `Handle.LocalSpaceReference` and `Handle.UniformGroupScale` are opt-in Core controls. Uniform group scaling requires a pure Scale handle, multiple targets and XYZ axes; partial masks disable its controls. Defaults preserve existing consumer behavior. See Core README/API docs for details.
- Left drag orbits, right drag pans, scroll zooms. UI and handle gestures consume selection/camera input. The size popover supports 64–512 rendering pixels, default 192; Retina pixel ratio is not silently folded into that value.
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
