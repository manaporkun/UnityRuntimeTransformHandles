# Runtime Transform Handles — Documentation

Runtime object manipulation through visual transform handles (position, rotation, scale).

## Validated compatibility

Local macOS ARM64 runs pass all 176 PlayMode and 74 EditMode tests on Unity 2021.3.0f1
and 6000.3.16f1. Each run includes 24 real Editor Play Mode cycles across all four domain/scene
reload combinations and both manager creation paths. Unity 6 is tested separately with legacy
input and the Input System. The 2021.3.0 consumer uses Test Framework 1.1.33, legacy input, no
Input System package and no URP package. The optional Level Editor passes its 44 PlayMode tests
in each configuration. Unity 2021.3.45f1 passed the previous 134/70 suite before the Precision
appearance changes; this revision has not been rerun on that patch. These are measured
configurations, not a claim that every intermediate Editor or rendering configuration was tested.

The Unity 6 canonical sample also builds and supports selection, move, rotation and scale in a
local Chromium WebGL run. Visual validation remains open: target cubes render magenta in the URP
build, hidden URP shaders report unsupported variants, and keyboard mode changes leave the demo
HUD label stale. These sample defects and shader diagnostics are tracked
separately; this is not a clean WebGL visual signoff.

The repository CI checks exact test discovery, zero skips, session evidence and observed settings.
Local results do not establish remote CI success. See `ci/README.md` in the repository for the
isolated-run procedure and evidence requirements.

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

## Lifecycle and ownership

`TransformHandleManager` is a `DontDestroyOnLoad` singleton, so put it on a root GameObject. It owns
the handles and ghost pivots it creates. During teardown it does not parent, move, or destroy your
selected target objects; normal interaction still applies the requested transforms to them.

Across active-scene changes the manager keeps ownership of handles that are still alive. Handles
created in a scene that unloads unregister as they are destroyed, so scene unload is the normal way to
remove a whole scene's handles. A handle can also be removed explicitly with `RemoveHandle`, while
`DestroyAllHandles` removes every handle the manager owns and leaves the manager usable (the demo uses
this during reset).

An active drag is cancelled exactly once when its handle is removed, its manager is destroyed, the
handle's GameObject is destroyed, its scene unloads, or the handle is disabled with `SetActive(false)`.
The `OnInteractionEndEvent` can therefore arrive without a mouse release. Teardown does not refresh
target bounds, so it also works when a target has already been destroyed independently.

Targets destroyed independently are pruned from the owning group and no longer appear in `Targets`.
If other targets survive, the handle stays alive and its ghost pivot is recentered on the survivors;
the surviving target hierarchy and TRS are not changed. If the last target is destroyed, the handle
and ghost are torn down and `OnHandleDestroyedEvent` fires once. `GetTargets` is prune-only: it removes
destroyed membership immediately but does not cancel a drag, recenter, or destroy; that pending
cleanup runs on the next enabled manager `Update`. A handle with pending target loss cannot start a
new interaction until then. `BeginInteraction` rejects such a handle.

Adding an ancestor replaces all contained descendants in the same group; no target is reparented or
transformed. `RemoveTarget` validates its handle and membership before changing ownership, and a
target owned by a different handle throws `InvalidOperationException` without corrupting either
group. Explicit `null` arguments keep their P0-a contracts; destroyed Unity objects are treated as
absent/pruned, not as explicit caller null.

Destroying a manager tears down its handles and ghost pivots; consumer targets keep their parent,
local/world transform and active state. Teardown is safe when an event callback throws or asks the
manager to create/destroy handles: creation is rejected while a bulk cleanup runs and permanently after
final teardown. During application shutdown the same guarded cleanup runs; Unity does not guarantee
which objects are still alive at that point, so treat shutdown events as best effort.

Disabling a handle component or its GameObject cancels an active drag once, retains manager ownership
and restores the pivot/axes/camera when re-enabled. A component `enabled=true` from an end callback is
immediate. Reopening a handle **GameObject** from that callback is not: request it in the callback and
call `SetActive(true)` on the following frame, after Unity finishes deactivation. No scheduler is added.
Public `Handle.Disable()` is an ownership-retaining local suspension; resume it with
`Enable(pivotCapturedBeforeDisable)` if needed. `RemoveHandle` is the explicit detach path. Disabling
the manager component or GameObject keeps handles/ghosts owned but pauses input; re-enabling resumes
them without rebuilding ownership. A null or destroyed `MainCamera` pauses raycasting safely; assigning
a camera (or a fallback appearing when the current one is null) recovers. An explicitly assigned
disabled/inactive camera remains valid. Replacing `MainCamera` cancels an active drag once so a frame
cannot mix two camera projections.

`RemoveHandle` and `GetTargets` treat an already-destroyed handle reference as a no-op/empty result;
an explicit `null` argument still throws.

`Handle.Targets` and `Handle.OnEnable` are passive: they read `TransformHandleManager.ExistingInstance`
and never create a manager. A standalone, detached, or dead handle reports an empty `Targets`
collection. With no live manager, re-enabling assigns no camera and performs no restore. An existing
manager may supply its camera, but never auto-registers the handle. During application quit `Targets` stays empty and `OnEnable` performs no camera
assignment or restore, preserving the old lazy behavior without dereferencing a null manager. The
explicit `TransformHandleManager.Instance` factory and `Ghost.Initialize` keep their semantics:
`Instance` may create or find a manager, is null during quit, and creates a fresh manager after a
completed teardown. This slice adds no public API, event, or signature change.

Explicit factory access during cleanup is different from a passive query. `DestroyAllHandles()`
keeps its manager, but that manager rejects new handles until the cleanup finishes. Calling
`Instance` from a destruction callback does not bypass that guard. During final manager destruction,
a callback may obtain the dying manager or, after its reference has been released, a replacement;
`Instance` is not a readiness check. In nested cleanup, a replacement can exist before the old cleanup
returns, and the old cleanup does not own the replacement's handles. To recreate handles reliably,
request `Instance` after destruction has completed, from a surviving controller (for example on a
later frame). The package does not schedule this request. During application quit `Instance` returns
null. `Ghost.Initialize()` also requests a manager explicitly, but does not register a standalone
ghost with that manager or transfer ownership to it.

A throwing destruction subscriber does not bypass handle cleanup: the manager releases its target
registration and ghost pivot, so surviving targets can be registered again. Event delivery still
stops at the first exception; a failing C# destruction subscriber prevents the following UnityEvent
from running. If a destruction callback also destroys the manager, manager cleanup avoids destroying
the same handle twice while still releasing its other owned objects. Exceptions remain visible in
Unity's log. This does not guarantee recovery from arbitrary overrides that throw during cleanup.


The Editor session tests cover three consecutive Play/Stop cycles with domain and scene reload
both enabled, either one disabled, and both disabled. They cover a manager authored in the scene and
one created during Play Mode, checking manager readiness, handle ownership, scene-change cancellation
and cleanup between sessions. The session-entry quit reset runs naturally; tests do not reset package
state between cycles. Local validation uses Unity 6000.3.16f1 with both input backends. Interaction
checks use the manager's test seam, so this does not establish mouse/touch-device recovery, private
event subscription counts, player shutdown behavior or compatibility with every Editor version.



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
| `Handle CreateHandleFromList(List<Transform> targets)` | Create one handle controlling several objects. Throws on null/empty list; skips null/destroyed elements before duplicate checks and returns null if no live target remains. |
| `bool AddTarget(Transform target, Handle handle)` | Add an object to an existing handle. Throws if `handle` is null/unmanaged; adding an ancestor replaces contained targets. |
| `void RemoveTarget(Transform target, Handle handle)` | Remove an object; validates handle/membership before mutation and destroys the handle when its last target leaves. A target owned by another handle throws `InvalidOperationException`. |
| `void RemoveHandle(Handle handle)` | Stop managing a handle and disable it; its GameObject is not destroyed. |
| `void DestroyAllHandles()` | Destroy every handle and clear all state. |
| `static void ChangeHandleType(Handle, HandleType)` | Set a handle's type. |
| `void ChangeHandleSpace(Handle, Space)` | Set a handle's space and re-center the ghost. |
| `void ChangeHandlePivot(TransformGroup, bool originToCenter)` | Toggle pivot vs. bounds-center origin. |
| `Camera MainCamera` | Camera used for raycasting (settable; falls back to `Camera.main`). |
| `IReadOnlyCollection<Transform> GetTargets(Handle)` | The live objects a handle manipulates. Prunes destroyed membership but defers cancel/recenter/destroy to the next manager `Update`. |
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

## Optional examples

Examples now live in the separate `com.orkunmanap.runtime-transform-handles.demos` package.
Core does not install or reference demos. Existing imported Assets copies are retained; consult the
repository's demos README before importing the relocated showcase, whose GUIDs remain unchanged.
Back up customized imports outside Assets to avoid duplicate GUIDs.

## Handle appearance and first-frame placement

The supplied `Runtime/Resources/TransformHandleManager.prefab` uses
`Runtime/Prefabs/PrecisionTransformHandle.prefab`. Precision has slim axes and rings, translucent
planes with a thin same-color rounded border and a neutral center. Axis, ring and tip picking volumes match Native. Precision's rounded
plane pads sit closer to the pivot (0.14 handle units), with 0.18-unit visible width and slightly
larger 0.20-unit aligned picking areas; Native retains its original 0.20 offset and plane size.
Plane placement respects the camera immediately on creation/rebuild, including local rotation.
The classic `NativeTransformHandle.prefab`, meshes, materials and shader remain available unchanged.
To retain the classic appearance, place a manager in your scene and assign NativeTransformHandle
in its **Transform Handle Prefab** Inspector field before creating handles. Existing scene managers
that already reference Native retain that appearance. Precision works without the optional Demos package.

Precision is a flat editor overlay: its shader uses `ZTest Always` and `ZWrite Off` in Built-in and URP.
It remains visible through scene geometry; it does not promise physical self-occlusion. Fixed material
queues order XY/YZ/XZ planes (3090–3092), rotation feedback (3093), dark backing (3100), Move/Rotate/Scale
XYZ (3120–3128), and center (3140). Hover material instances retain these queues.
The baked assets require no runtime generation. Maintainers can explicitly regenerate them with
`TransformHandles.Editor.PrecisionHandleAssetBuilder.Build`; it never runs automatically on import.

Factory calls return handles with their final root position, orientation and camera-dependent scale
already synchronized, so selection created in LateUpdate cannot render at the origin first. Rebuilding
modes/axes and recentering membership synchronize the same way. Existing sizing, including orthographic
camera behavior, is preserved. No extra manager is created if a camera is unavailable.

### Preserving hover colors

Enable **Preserve Handle Color On Hover** in a `TransformHandleSettings` asset to retain each component's original color and opacity while hovering. The read-only `PreserveHandleColorOnHover` property exposes this setting. It defaults to off; existing settings and managers without a settings asset continue using their highlight color. The optional Level Editor sample uses same-hue emphasis instead. Precision plane fills and borders remain in the plane's own color.

Enable **Emphasize Handle Color On Hover** (`EmphasizeHandleColorOnHover`) for visible feedback in each component's own hue: slightly darker RGB and stronger translucent fill. This takes priority over exact color preservation when both options are enabled. Both settings default to off, retaining the shared highlight color for existing consumers. `HandleBase.SetHighlightedColor()` derives its result from the default color and delegates to virtual `SetColor`; hover exit and cancellation restore the original RGBA.

Precision uses compact arrow tips and a thinner, softer contrast backing. Axis picking volumes remain unchanged for accessibility; Native assets retain their original appearance.

### Opt-in group transforms

`Handle.LocalSpaceReference` selects a member whose world orientation defines Local axes.
Assign it before `manager.ChangeHandleSpace(handle, Space.Self)` to synchronize the idle pivot.
World remains aligned with the scene. Null, destroyed or nonmember references preserve the legacy
orientation behavior. This changes axis orientation, not the common pivot location.

`Handle.UniformGroupScale = true` enables common-center proportional scaling for a managed
multi-target `HandleType.Scale` handle. With `Axes == HandleAxes.XYZ`, only the existing center
control is active. Partial axis masks disable all scale controls in this mode. Combined handle
types and single-target handles retain their previous scaling behavior. The option defaults to
false, so existing consumers retain additive per-object scaling.

The common center follows the existing pivot setting (the mean of object pivots by default).
Each drag snapshots the starting positions and scales; the center scale factor multiplies both
sizes and offsets from that center. Invalid/nonpositive/nonuniform factors and nonfinite results
are rejected before changing any member. Changing mode, axes, the option or group membership
ends an active uniform drag. Cancellation retains the last valid result; the optional Level Editor
additionally restores the whole drag if an object scale would leave its 0.25–4 bounds.

The Level Editor uses the last selected surviving prop as Local reference, allows group rotation,
and enables uniform group scaling. World/Local selection is retained across group Scale mode.
