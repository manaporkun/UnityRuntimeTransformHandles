# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

This release contains **source-breaking** changes and is intended to ship as **2.0.0**.

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

## [1.20.0]

- Baseline release. See the
  [GitHub releases](https://github.com/manaporkun/UnityRuntimeTransformHandles/releases) for the
  history prior to this changelog.

[Unreleased]: https://github.com/manaporkun/UnityRuntimeTransformHandles/compare/v1.20.0...HEAD
[1.20.0]: https://github.com/manaporkun/UnityRuntimeTransformHandles/releases/tag/v1.20.0
