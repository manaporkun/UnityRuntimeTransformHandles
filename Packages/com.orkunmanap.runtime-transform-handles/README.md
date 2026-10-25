# Runtime Transform Handles for Unity

Runtime object manipulation through visual transform handles (position, rotation, scale).
Supports multi-object selection, switchable pivot/center origin, world/local space, configurable
snapping, auto-scaling handles, and an event system — for modding tools, runtime editors, and
in-game object manipulation.

## Requirements

- Unity **2021.3** or higher.
- **Render pipeline:** Built-in and Universal Render Pipeline (URP) are supported — the handle
  shaders ship both a Built-in and a URP SubShader (auto-selected per active pipeline). URP is
  **not** a dependency: the URP SubShader is gated behind a `PackageRequirements` block and only
  compiles when `com.unity.render-pipelines.universal` 12.1.0+ is installed; Built-in projects
  render via the Built-in SubShader with no extra packages. HDRP is **not** supported out of the
  box (no HDRP SubShader/Shader Graph variant is provided).
- **Input:** Works with the legacy Input Manager out of the box. The New Input System is an
  optional dependency: when `com.unity.inputsystem` is present and enabled in Player Settings, the
  package uses it automatically (via the `TH_INPUTSYSTEM` define); otherwise it falls back to the
  legacy Input Manager with no extra setup.

## Validated compatibility

Development-revision macOS ARM64 runs pass all 198 PlayMode and 74 EditMode tests on Unity
2021.3.0f1 and 6000.3.16f1. Each run includes 24 real Editor Play Mode cycles across all four
domain/scene reload combinations and both manager creation paths. Unity 6 is tested separately
with legacy input and the Input System. The 2021.3.0 consumer uses Test Framework 1.1.33,
legacy input, no Input System package and no URP package. Imported optional samples pass
116 tests on Legacy and 119 on Unity 6 Input System. Unity 2021.3.45f1 is the remote CI floor;
its earlier 134/70 result does not validate this revision. These are measured configurations,
not a claim that every intermediate Editor or rendering configuration was tested. Versioned
release artifacts require fresh consumer validation before publication.

The combined gallery and standalone Lamp Assembly WebGL players passed local Chromium mouse
and Retina layout checks. Showcase targets use a sample-owned pipeline-independent shader.
Three inherited hidden URP shader diagnostics remain at log level; physical Mac trackpad
validation remains open. See the repository development handoff for the exact development results.

The repository CI checks exact test discovery, zero skips, session evidence and observed settings.
Local results do not establish remote CI success. See `ci/README.md` in the repository for the
isolated-run procedure and evidence requirements.

## Installation

Add the package from the Git URL in `Window > Package Manager > + > Add package from git URL`:

```
https://github.com/manaporkun/UnityRuntimeTransformHandles.git#upm
```

Or add it to `Packages/manifest.json`:

```json
{
    "dependencies": {
        "com.orkunmanap.runtime-transform-handles": "https://github.com/manaporkun/UnityRuntimeTransformHandles.git#upm"
    }
}
```

### Post-installation setup

Create the physics layer used for handle raycasting:

1. `Tools > Transform Handles > Setup Layer`
2. This creates the `TransformHandle` layer.

## Quick start

```csharp
using TransformHandles;
using UnityEngine;

public class SimpleExample : MonoBehaviour
{
    void CreateHandleForObject(Transform target)
    {
        Handle handle = TransformHandleManager.Instance.CreateHandle(target);
        // handle.Pivot is the manipulation pivot (the group's ghost); handle.Targets are the
        // actual objects being manipulated.
        handle.OnInteractionStartEvent += _ => Debug.Log("Started: " + target.name);
        handle.OnInteractionEndEvent   += _ => Debug.Log("Finished: " + target.name);
    }
}
```

### Lifecycle and ownership

The manager is a persistent singleton; put it on a root GameObject. It cleans up the handles and ghost
pivots it owns when it is destroyed, while your target objects keep their hierarchy and transforms.
Handles stay independent scene objects, so unloading a scene removes its handles.

`DestroyAllHandles()` clears every handle but keeps the manager usable; `RemoveHandle(handle)` detaches
one without destroying its GameObject. Ending a drag by removing/destroying the handle, unloading its
scene, or deactivating it fires `OnInteractionEndEvent` once, even without a mouse release. After a
manager is destroyed, create a new one with `TransformHandleManager.Instance` (a fresh manager starts
with no handles).

Targets destroyed independently are removed from the handle’s live membership. If others survive, the
handle and their TRS/hierarchy remain; if the last target is gone, the handle and ghost are torn down
and `OnHandleDestroyedEvent` fires once. `GetTargets` removes destroyed membership but defers any drag
cancel, pivot recenter, or empty-handle destruction to the next manager `Update`; a pending-loss handle
cannot start a new drag until then. Adding an ancestor replaces all its contained targets, and
`RemoveTarget` validates the handle/membership before changing ownership.

Disabling a handle component or GameObject suspends it reversibly: ownership stays, and re-enabling
restores the pivot, axes and camera. Component `enabled=true` from an end callback is immediate; a
GameObject reopen must be requested by the callback and applied with `SetActive(true)` on the following
frame because Unity rejects immediate reactivation during `OnDisable`. Public `Handle.Disable()` is an
ownership-retaining local suspension; `RemoveHandle` is the explicit detach. Manager component/GameObject
disable retains ownership and pauses input; re-enable resumes the same handles. A missing/destroyed
camera pauses safely and recovers when a camera is assigned; replacing `MainCamera` cancels an active
drag once.

Querying `Targets` or enabling a handle is passive: it never creates a manager. A standalone or
detached handle reports no targets. With no live manager, re-enabling assigns no camera and performs
no restore. An existing manager may supply its camera, but never auto-registers the handle. During application quit those passive paths stay
empty and skip camera assignment/restore, so shutdown does not instantiate anything. The explicit
`TransformHandleManager.Instance` factory and `Ghost.Initialize` are unchanged: `Instance` still
creates or finds a manager, is null during quit, and can create a fresh manager after teardown.

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

The Editor session tests cover three consecutive Play/Stop cycles with domain and scene reload
both enabled, either one disabled, and both disabled. They cover a manager authored in the scene and
one created during Play Mode, checking manager readiness, handle ownership, scene-change cancellation
and cleanup between sessions. The session-entry quit reset runs naturally; tests do not reset package
state between cycles. Local validation uses Unity 6000.3.16f1 with both input backends. Interaction
checks use the manager's test seam, so this does not establish mouse/touch-device recovery, private
event subscription counts, player shutdown behavior or compatibility with every Editor version.

A throwing destruction subscriber does not bypass handle cleanup: the manager releases its target
registration and ghost pivot, so surviving targets can be registered again. Event delivery still
stops at the first exception; a failing C# destruction subscriber prevents the following UnityEvent
from running. If a destruction callback also destroys the manager, manager cleanup avoids destroying
the same handle twice while still releasing its other owned objects. Exceptions remain visible in
Unity's log. This does not guarantee recovery from arbitrary overrides that throw during cleanup.

### Multiple objects

```csharp
var targets = new List<Transform> { obj1, obj2, obj3 };
Handle handle = TransformHandleManager.Instance.CreateHandleFromList(targets);
TransformHandleManager.Instance.AddTarget(newTarget, handle);
TransformHandleManager.Instance.RemoveTarget(targetToRemove, handle);
```

### Changing handle properties

```csharp
TransformHandleManager.ChangeHandleType(handle, HandleType.Rotation);
TransformHandleManager.Instance.ChangeHandleSpace(handle, Space.World);

handle.PositionSnap = new Vector3(0.5f, 0.5f, 0.5f);
handle.RotationSnap = 15f;
handle.ScaleSnap = new Vector3(0.1f, 0.1f, 0.1f);
```

### Settings asset (optional)

Create via `Assets > Create > Transform Handles > Settings` to customize shortcuts, default handle
type/space/axes, and highlight color, then assign it:

```csharp
TransformHandleManager.Instance.Settings = mySettings;
```

### Blocking interaction over UI (opt-in)

Optionally, a handle interaction can be prevented from starting while the pointer is over a uGUI
element, so clicking a button or panel above the scene does not begin a drag (an interaction
already in progress is never interrupted; touch uses the active finger). It is **off by default**
so it never silently changes input behavior. Needs an `EventSystem` and the uGUI package; projects
without uGUI are unaffected. Enable it at runtime or in the Inspector:

```csharp
TransformHandleManager.Instance.BlockWhenPointerOverUI = true;
```

For a non-uGUI UI stack, subclass and override `IsPointerOverUI()`.

## Default keyboard shortcuts

| Key | Action |
|-----|--------|
| W | Position mode |
| E | Rotation mode |
| R | Scale mode |
| A | All modes (Position + Rotation + Scale) |
| X | Toggle World/Local space |
| Z | Toggle Pivot/Center origin |

## Samples

Install **Runtime Transform Handles Demos** separately, then import an example from that package's
Samples tab. Core has no samples or dependency on demos; see Optional examples below.

## Upgrading

- **From 1.x:** 2.0.0 is a breaking release. See `Documentation~/migration-1.x-to-2.0.md` for the
  (small) steps.
- **From 2.x:** no code changes required — 3.0.0 shipped no breaking changes (the major bump was a
  release-automation artifact, corrected in 3.0.1).

## License

MIT — see `LICENSE.md`.

Created and maintained by [Orkun Manap](https://manap.dev). Based on
[Runtime Transform Handle](https://github.com/pshtif/RuntimeTransformHandle) by Peter Stefcek and
contributors (MIT) — see `Third Party Notices.md`.

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
