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
        // Note: handle.Target is the internal manipulation pivot (the group's ghost), not your
        // object. Capture your own `target` reference for anything object-specific.
        handle.OnInteractionStartEvent += _ => Debug.Log("Started: " + target.name);
        handle.OnInteractionEndEvent   += _ => Debug.Log("Finished: " + target.name);
    }
}
```

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

Import the **Runtime Transform Handles Demo** from the package's Samples tab in the Package Manager
for a selection + multi-object manipulation example scene.

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
