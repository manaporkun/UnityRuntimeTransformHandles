# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- UI-occlusion guard: handle interactions no longer start while the pointer is over a uGUI
  element. Controlled by `TransformHandleManager.BlockWhenPointerOverUI` (serialized, default on);
  an interaction already in progress is never interrupted. Requires the uGUI package and an
  `EventSystem` in the scene — projects without uGUI are unaffected (the guard is a no-op via the
  `TH_UGUI` version define). Override `IsPointerOverUI()` to integrate a non-uGUI UI stack.

## [3.1.0] - 2026-06-11

### Added
- PascalCase public API across the package: `Handle.Target`/`Type`/`Space`/`Axes`/`SnappingType`/`PositionSnap`/`RotationSnap`/`ScaleSnap`/`AutoScale`/`HandleCamera`, `TransformHandleManager.MainCamera`, `HandleBase.Delta`, and accessors for the prefab-wired axis components (`PositionHandle.XAxis` etc.).
- PlayMode tests for `TransformHandleManager` create, multi-select, add/remove target, and destroy flows.
- PlayMode interaction test suite: Handle event contract (C# events + Inspector UnityEvents, ordering, destroy notification), `HandleBase` interaction lifecycle, group position updates through the ghost pivot, and ghost cleanup.
- CI compatibility-floor job that runs the package tests on Unity 2021.3 without the Input System or URP, verifying the declared floor, the legacy Input Manager path, and URP-free installs on every change.
- Root `.editorconfig` for consistent C# formatting, plus repository `CONTRIBUTING.md` and `SECURITY.md`.

### Changed
- URP is no longer a package dependency. The URP SubShaders are gated behind `PackageRequirements` and compile only when `com.unity.render-pipelines.universal` 12.1.0+ is installed; Built-in projects need no extra packages.
- `com.unity.modules.physics` is now declared explicitly as a built-in module dependency.
- `Third Party Notices.md` restores the upstream attribution: the package derives from [Runtime Transform Handle](https://github.com/pshtif/RuntimeTransformHandle) by Peter Stefcek (MIT), whose copyright and permission notice is now reproduced as the MIT license requires.
- Release notes are now generated from the curated `[Unreleased]` changelog section (with grouped Conventional Commit subjects as fallback) instead of raw commit logs.
- Clean consumer-facing changelog entries for 2.0.0–3.0.4 (remove merge-commit noise) and document 3.0.0 as a release-automation artifact with no breaking changes.
- README specific-version install example updated to `v3.0.5`.

### Deprecated
- The camelCase public members (`Handle.target`/`type`/`space`/`axes`/`snappingType`/`positionSnap`/`rotationSnap`/`scaleSnap`/`autoScale`/`handleCamera`, `TransformHandleManager.mainCamera`, `HandleBase.delta`, and the prefab wiring fields `xAxis`/`yAxis`/`zAxis`/`xPlane`/`yPlane`/`zPlane`/`globalScale`) are now `[Obsolete]` shims that forward to their PascalCase replacements. They keep compiling and serializing exactly as before and will be removed in the next major release.

### Removed
- Unused `Plane.mat` and `Object.mat` from `Runtime/Materials` (never referenced by any shipped prefab, sample, or scene).

### Fixed
- Handle component materials (instantiated per axis/plane/ring at handle creation) are now destroyed with their components — previously ~19 material instances leaked on every handle create/destroy cycle.
- Rotation arc mesh rebuilds no longer allocate fresh vertex/index arrays every drag frame (the package's main steady GC source, ~8 KB/frame while rotating).
- Static singleton state is reset when a play session starts, so the package works with Enter Play Mode Options (domain reload disabled); previously the quit flag survived and every `Instance` access returned null on the next run.
- Collider controllers destroy the previously generated mesh when rebuilding (leak on rebuild), and their development-only refresh hotkey is compiled out of release players.

## [3.0.5] - 2026-06-10

### Fixed
- Strip UTF-8 BOM from script `.meta` files so `NativeTransformHandle` script references stay stable after Unity restart.
- Stop scale axis line at the handle cube inner face so the line no longer extends through the cube during drag.

### Changed
- ci: reject UTF-8 BOM in package `.meta` files.

## [3.0.4] - 2026-06-10

### Fixed
- Align axis and uniform scale input with Unity Editor handle math (`CalcLineTranslation`, `GetHandleSize`).
- Keep scale axis line and cube gizmo visuals in sync (tube mesh length vs cube rest distance).
- Reset scale axis gizmo visuals on drag end and re-initialize; guard absolute snap when start scale is zero.
- Guard scale handles when the interaction camera is missing.

### Added
- `HandleTransformUtility` runtime port of Unity Editor scale handle math.
- Edit Mode tests for handle transform utility and line/cube visual reach parity.

### Changed
- Harden automated publish workflow: recover interrupted releases, recompute version against latest main, package the exact bump commit, and make version-bump pushes resilient to concurrent main updates.

## [3.0.3] - 2026-06-01

### Fixed
- Make global (uniform) scale speed framerate-independent.
- Clarify in docs that `handle.target` is the manipulation pivot (ghost), not the user's object.

### Added
- PlayMode tests covering TransformGroup apply paths (positions + scales).
- Min-API lint CI job guarding the Unity 2021.3 floor.

### Changed
- Harden publish workflow (bump filter, concurrency, Dependabot).

## [3.0.2] - 2026-06-01

### Fixed
- Changelog GITHUB_OUTPUT heredoc missing trailing newline.

### Added
- Tests covering GetBounds and world/self group rotation.

## [3.0.1] - 2026-06-01

### Fixed
- Correct world-space group rotation and multi-renderer bounds.
- Release version bump computed from entire git history instead of the last release range (CI).

## [3.0.0] - 2026-06-01

> **No breaking changes.** This major version number is a release-automation artifact: the
> version bump was computed from the entire git history (re-counting 2.0.0's `feat!` commit)
> instead of the commits since 2.0.0. The bug was fixed in 3.0.1. Upgrading from 2.x requires
> no code changes.

### Changed
- Consolidate the WebGL browser demo onto the package sample scene.
- Remove internal planning markdown files from the repository.

## [2.0.0] - 2026-06-01

This release contains **source-breaking** changes.
Migration steps: see `Documentation~/migration-1.x-to-2.0.md`.

### Breaking
- **Namespaces:** `Singleton<T>`, `ApplicationQuitManager`, and `PreserveScaleOnScreenExtension`
  moved out of the global namespace into `TransformHandles.Utils`. `ApplicationQuitManager` and
  `PreserveScaleOnScreenExtension` are now `internal`.
- **Read-only fields:** `Handle.target` and `Handle.handleCamera` are now read-only properties
  (set by the package when the handle is enabled); external code can no longer assign them.
- **Reduced public surface:** `Ghost` interaction callbacks
  (`OnInteractionStart`/`OnInteraction`/`UpdateGhostTransform`/`ResetGhostTransform`/`Terminate`)
  and `TransformGroup.UpdatePositions`/`UpdateRotations`/`UpdateScales`/`UpdateBounds` plus the
  `RenderersMap`/`BoundsMap`/`Transforms` collections are now `internal`.
- **Contract exceptions:** public manager methods now throw `ArgumentNullException` (and
  `ArgumentException`/`InvalidOperationException`) on contract violations instead of logging and
  returning; benign state conditions still log a warning.

### Deprecated
- `Handle.ChangeHandleType`, `ChangeHandleSpace`, and `ChangeAxes` are `[Obsolete]` forwarders;
  assign the `type`, `space`, and `axes` properties directly (their setters rebuild the child
  handles / apply the Scale→Self clamp).

### Added
- URP (Universal Render Pipeline) SubShader for `HandleShader` and `Origin` shaders so
  handles render correctly under URP instead of falling back to the magenta error shader.
- `com.unity.render-pipelines.universal` as a dependency so the URP SubShader's `Core.hlsl`
  include always resolves (a Built-in-only project without URP would otherwise hit a
  missing-include shader error). Built-in rendering is unaffected.
- `versionDefines` entry mapping `com.unity.inputsystem` to the package-private `TH_INPUTSYSTEM`
  symbol, making the New Input System an optional dependency.
- In-package `README.md`, `LICENSE.md`, `CHANGELOG.md`, and `Third Party Notices.md`.
- A registered UPM Sample (`Samples~/Demo`) with the full-featured runtime demo scene.
- EditMode + PlayMode test suites under `Tests/`, and a `SnapUtils.Snap` helper.

### Changed
- Declared minimum Unity version raised to **2021.3** to match the C# 9 language features and
  APIs the package actually uses (the previous `2019.4` floor could not compile).
- Input System guards in `InputWrapper` now require both `ENABLE_INPUT_SYSTEM` (Player Settings)
  and `TH_INPUTSYSTEM` (package present), so a project without the Input System package compiles
  cleanly on the legacy backend with no dropped-reference warnings.

### Fixed
- `TransformHandleManager` no longer destroys itself when no main camera is present for a frame
  (e.g. during scene loads or camera swaps). It logs a single warning, pauses raycasting, and
  recovers automatically once a camera is available.
- `DestroyAllHandles` no longer throws `InvalidOperationException` from mutating the handle map
  while iterating it; it now snapshots the keys and clears all internal maps.
- Aborted handle creation (`CreateHandle` / `CreateHandleFromList`) now fully tears down the
  partially-built handle and its ghost instead of leaking orphaned map entries.

## [1.20.0] - 2026-05-31

- Baseline release. See the
  [GitHub releases](https://github.com/manaporkun/UnityRuntimeTransformHandles/releases) for the
  history prior to this changelog.

[Unreleased]: https://github.com/manaporkun/UnityRuntimeTransformHandles/compare/v3.0.5...HEAD
[1.20.0]: https://github.com/manaporkun/UnityRuntimeTransformHandles/releases/tag/v1.20.0
