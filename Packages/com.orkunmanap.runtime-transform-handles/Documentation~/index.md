# Runtime Transform Handles — Documentation

Runtime object manipulation through visual transform handles (position, rotation, scale).

## Architecture

```
TransformHandleManager (Singleton)
    ├── Creates/manages all Handles
    ├── Raycast detection & input processing
    └── Keyboard shortcuts (W/E/R/A/X/Z)

Handle (per-target controller)
    ├── PositionHandle → PositionAxis (X,Y,Z) + PositionPlane (XY,YZ,XZ)
    ├── RotationHandle → RotationAxis (X,Y,Z)
    └── ScaleHandle → ScaleAxis (X,Y,Z) + ScaleGlobal

Ghost (pivot transform for manipulation)
    └── Stores initial state, calculates deltas

TransformGroup (multi-object grouping)
    └── Applies transforms to all grouped targets
```

## Data flow

1. `TransformHandleManager` detects a raycast on a handle collider.
2. `HandleBase.StartInteraction()` → `Ghost.OnInteractionStart()` stores the initial state.
3. During drag, `Ghost.OnInteraction()` computes the delta.
4. `TransformHandleManager.UpdateGroup*()` → `TransformGroup` applies it to all targets.
5. `Handle` fires `OnInteractionStartEvent`, `OnInteractionEvent`, `OnInteractionEndEvent`.

## Render pipeline support

The handle shaders ship both a Built-in and a Universal Render Pipeline (URP) SubShader; Unity
auto-selects the matching one. HDRP is not supported out of the box.

## Input system

`InputWrapper` abstracts the legacy Input Manager and the New Input System. The New Input System is
used when `com.unity.inputsystem` is present (define `TH_INPUTSYSTEM`) and enabled in Player
Settings (`ENABLE_INPUT_SYSTEM`); otherwise the legacy Input Manager is used.

## Layer configuration

Handles require a dedicated physics layer (default name `TransformHandle`). Create it via
`Tools > Transform Handles > Setup Layer`.

See the package `README.md` for the API quick start and keyboard shortcuts.
