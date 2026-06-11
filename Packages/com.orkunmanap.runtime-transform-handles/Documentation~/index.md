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

## API reference

### `TransformHandleManager` (singleton — `TransformHandleManager.Instance`)

| Member | Description |
|--------|-------------|
| `Handle CreateHandle(Transform target)` | Create a handle for one object. Throws `ArgumentNullException` if `target` is null; warns + returns null if it already has a handle. |
| `Handle CreateHandleFromList(List<Transform> targets)` | Create one handle controlling several objects. Throws on null/empty list. |
| `bool AddTarget(Transform target, Handle handle)` | Add an object to an existing handle. Throws if `handle` is null/unmanaged. |
| `void RemoveTarget(Transform target, Handle handle)` | Remove an object; destroys the handle when its last target leaves. |
| `void RemoveHandle(Handle handle)` | Stop managing + tear down a handle. |
| `void DestroyAllHandles()` | Destroy every handle and clear all state. |
| `static void ChangeHandleType(Handle, HandleType)` | Set a handle's type. |
| `void ChangeHandleSpace(Handle, Space)` | Set a handle's space and re-center the ghost. |
| `void ChangeHandlePivot(TransformGroup, bool originToCenter)` | Toggle pivot vs. bounds-center origin. |
| `Camera MainCamera` | Camera used for raycasting (settable; falls back to `Camera.main`). |
| `IReadOnlyCollection<Transform> GetTargets(Handle)` | The objects a handle manipulates (live, read-only). |
| `TransformHandleSettings Settings` | Optional settings asset (overrides serialized defaults). |

### `Handle`

| Member | Description |
|--------|-------------|
| `IReadOnlyCollection<Transform> Targets` | The manipulated objects (**read-only**, live view). |
| `Transform Pivot` | The manipulation pivot — the ghost the handle moves around (**read-only**; not your object). |
| `Camera HandleCamera` | Camera for screen math (**read-only**; set when enabled). |
| `HandleType Type` | Property; assigning rebuilds the child handles. |
| `HandleAxes Axes` | Property; assigning rebuilds the child handles. |
| `Space Space` | Property; setter clamps Scale handles to `Space.Self`. |
| `SnappingType SnappingType` | `Relative` or `Absolute`. |
| `Vector3 PositionSnap` / `float RotationSnap` / `Vector3 ScaleSnap` | Snap increments (0 = off). |
| `bool AutoScale` / `float ScaleMultiplier` / `AutoScaleSizeInPixels` | Constant on-screen sizing. |
| events `OnInteractionStartEvent` / `OnInteractionEvent` / `OnInteractionEndEvent` / `OnHandleDestroyedEvent` | `Action<Handle>`. Inspector-friendly `*UnityEvent` mirrors exist. |
| `void ApplySettings(TransformHandleSettings)` | Apply scale/appearance from a settings asset. |

> `Handle.Target` is `[Obsolete]` — it returns the **pivot**, not the selected object. Use `Pivot`
> for the pivot or `Targets` for the manipulated objects.
>
> The camelCase spellings of these members (`target`, `type`, `space`, `axes`, `snappingType`,
> `positionSnap`, `rotationSnap`, `scaleSnap`, `autoScale`, `handleCamera`, `mainCamera`) and the
> `ChangeHandleType`/`ChangeHandleSpace`/`ChangeAxes` methods are `[Obsolete]` shims — they keep
> working but will be removed in the next major release.

### Enums

- `HandleType`: `Position, Rotation, Scale, PositionRotation, PositionScale, RotationScale, All`
- `HandleAxes`: `X, Y, Z, XY, XZ, YZ, XYZ`
- `SnappingType`: `Relative, Absolute`
- `Space`: `Self` (local), `World`
- `Origin`: `Pivot, Center`

### `TransformHandleSettings` (ScriptableObject — `Assets > Create > Transform Handles > Settings`)

Keyboard shortcuts (enable/disable + rebind), default handle type/space/axes for new handles, and
highlight color. Assign to `TransformHandleManager.Instance.Settings`.

## Upgrading

See [migration-1.x-to-2.0.md](migration-1.x-to-2.0.md) for the 2.0 breaking-change migration steps.

See the package `README.md` for the quick start and keyboard shortcuts.
