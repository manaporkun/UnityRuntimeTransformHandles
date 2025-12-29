# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity Runtime Transform Handles - A UPM package for runtime object manipulation through visual transform handles (position, rotation, scale). Supports both Legacy Input Manager and New Input System.

**Package Location:** `Packages/runtime-transform-handles/`
**Namespace:** `TransformHandles`, `TransformHandles.Utils`, `TransformHandles.Editor`
**Unity Version:** 2019.4+

## Architecture

### Core Components

```
TransformHandleManager (Singleton)
    ├── Creates/manages all Handles
    ├── Raycast detection & input processing
    └── Keyboard shortcuts (W/E/R/A/X/Z)

Handle (Per-target controller)
    ├── PositionHandle → PositionAxis (X,Y,Z) + PositionPlane (XY,YZ,XZ)
    ├── RotationHandle → RotationAxis (X,Y,Z)
    └── ScaleHandle → ScaleAxis (X,Y,Z) + ScaleGlobal

Ghost (Pivot transform for manipulation)
    └── Stores initial state, calculates deltas

TransformGroup (Multi-object grouping)
    └── Applies transforms to all grouped targets
```

### Data Flow

1. `TransformHandleManager` detects raycast on handle collider
2. `HandleBase.StartInteraction()` → `Ghost.OnInteractionStart()` stores initial state
3. During drag: `Ghost.OnInteraction()` calculates delta
4. `TransformHandleManager.UpdateGroup*()` → `TransformGroup` applies to all targets
5. `Handle` fires events (`OnInteractionStartEvent`, `OnInteractionEvent`, `OnInteractionEndEvent`)

### Key Relationships

- Manager maintains: `Dictionary<Handle, TransformGroup>` and `Dictionary<Ghost, TransformGroup>`
- Each Handle has one Ghost and one TransformGroup
- TransformGroup prevents parent-child relationships in same group

## Input System Compatibility

Uses `InputWrapper` (`Runtime/Scripts/Utils/InputWrapper.cs`) for dual input system support:
- `ENABLE_INPUT_SYSTEM` preprocessor detects New Input System
- Provides drop-in replacements: `InputWrapper.MousePosition`, `InputWrapper.GetMouseButton()`, `InputWrapper.GetKeyDown()`, etc.

## Layer Configuration

Handles require a dedicated physics layer for raycasting:
- Default layer name: `"TransformHandle"`
- Runtime: `TransformHandleManager.InitializeHandleLayer()` looks up by name, warns if missing
- Editor: `Tools > Transform Handles > Setup Layer` creates the layer

## GitHub Actions

**Workflow:** `.github/workflows/publish-upm.yml`
- Triggers on push to `main`
- Auto-versions using conventional commits (feat: = minor, fix: = patch, BREAKING: = major)
- Updates `package.json`, creates `upm` branch, tags release, creates GitHub Release

## Sample Implementation

`Assets/Scripts/ObjSelector.cs` demonstrates:
- Creating handles: `TransformHandleManager.Instance.CreateHandle(target)`
- Multi-selection: `CreateHandleFromList(targets)`, `AddTarget()`, `RemoveTarget()`
- Event subscription: `handle.OnInteractionStartEvent += callback`

## Key Enums

- `HandleType`: Position, Rotation, Scale, PositionRotation, PositionScale, RotationScale, All
- `HandleAxes`: X, Y, Z, XY, XZ, YZ, XYZ
- `SnappingType`: Relative, Absolute
- `Space`: Self (local), World (global)

## Commit Convention

Use conventional commits for automatic versioning:
- `feat:` → minor bump
- `fix:`, `perf:`, `refactor:` → patch bump
- `feat!:` or `BREAKING CHANGE:` → major bump
