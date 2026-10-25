# Changelog

## [Unreleased]

### Added
- Modular Building now groups guidance in an on-demand Help card and keeps a compact selection strip at the bottom.
- Independently importable Modular Building sample: compound wall/corner selection, fixed horizontal grid and quarter-turn rotation, Reset, responsive controls and PlayMode coverage.
- Optional demos package; the original showcase retains its asset GUIDs and scene name.

- Add the independently imported Level Editor Prop Workshop, with guarded commands, World/Local group movement and rotation, shared-center uniform scaling, snapping, reset and optional PlayMode tests.

- Responsive white editor UI, handle size popover, left-drag orbit, right-drag pan and fractional WebGL wheel zoom.

### Fixed
- Modular Building uses sample-local SDF typography and a TMP numeric field; selection-row labels share vertical alignment. This sample now requires the version-appropriate TMP package and Essential Resources.
- WebGL builds now reject newly imported sample assemblies until Unity reloads them, preventing missing-script players. Modular Building has a separate CLI preparation command; Pages prepares the original showcase before starting Unity.
