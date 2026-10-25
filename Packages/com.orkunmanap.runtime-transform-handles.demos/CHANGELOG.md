# Changelog

## [Unreleased]

### Added
- Shared white UI Toolkit presentation across all workspaces: common header navigation, scrollable inspector, Help, typography and pixel-size controls.
- Showcase advanced-tool foldouts, owned-only reset/clear behavior, a separate input-test region and standalone input/lifecycle regressions.
- Canonical demo stylesheet synchronization and CI drift checks while retaining independent sample imports.
- Pipeline-independent showcase target material so its cubes render correctly alongside URP workspaces in the combined player.
- Optional Demo Browser: a UI Toolkit gallery, in-player return navigation, and a combined WebGL build for the three independently imported workspaces.
- Scene-transition regression coverage for active handles, input ownership, and borrowed EventSystems; showcase teardown now releases its owned manager, settings, handles, and materials.
- Modular Building uses UI Toolkit with an editable UXML/USS layout: a single inspector groups selection, transform tools and handle size, with Help in the header.
- Independently importable Modular Building sample: compound wall/corner selection, fixed horizontal grid and quarter-turn rotation, Reset, responsive controls and PlayMode coverage.
- Optional demos package; the original showcase retains its asset GUIDs and scene name.

- Add the independently imported Level Editor Prop Workshop, with guarded commands, World/Local group movement and rotation, shared-center uniform scaling, snapping, reset and optional PlayMode tests.

- Responsive white editor UI, handle size popover, left-drag orbit, right-drag pan and fractional WebGL wheel zoom.

### Fixed
- Modular Building typography and spacing are defined together in the sample stylesheet; no TMP package or Essential Resources setup is required.
- WebGL builds now reject newly imported sample assemblies until Unity reloads them, preventing missing-script players. Modular Building has a separate CLI preparation command; Pages prepares the original showcase before starting Unity.
