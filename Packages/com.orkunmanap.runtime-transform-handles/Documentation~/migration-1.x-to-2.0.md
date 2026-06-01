# Migrating from 1.x to 2.0

2.0.0 is a **source-breaking** release. Most projects need only a couple of small edits.
Work through the items below; each shows the old code and the 2.0 replacement.

## 0. Requirements changed

- **Minimum Unity is now 2021.3** (was an incorrect `2019.4` — the code never compiled on 2019.4).
  Projects below 2021.3 must stay on 1.x.
- **URP is now a package dependency** (`com.unity.render-pipelines.universal`). The handle shaders
  ship a URP SubShader whose include must always resolve. Built-in projects are unaffected at
  render time — the Built-in SubShader is still used; URP being *installed* does not make it the
  *active* pipeline. HDRP is not supported out of the box.

## 1. `using` for the utility types (only if you referenced them)

`Singleton<T>`, `ApplicationQuitManager`, and `PreserveScaleOnScreen` moved out of the **global
namespace** into `TransformHandles.Utils` (this is what fixes the `CS0436`/`CS0104` collisions with
your own `Singleton<T>`). `ApplicationQuitManager` and `PreserveScaleOnScreen` are now `internal`.

```csharp
// before — relied on the package's global Singleton<T>
public class MyManager : Singleton<MyManager> { }

// after
using TransformHandles.Utils;
public class MyManager : Singleton<MyManager> { }
```

If you had your *own* global `Singleton<T>` that collided with the package's, **you can delete the
workaround** — the collision is gone.

## 2. `Change*` methods → property assignment

`Handle.ChangeHandleType`, `ChangeHandleSpace`, and `ChangeAxes` are now `[Obsolete]` (they still
work, with a warning). Assign the properties directly — their setters do the right thing
(`type`/`axes` rebuild the child handles, `space` applies the Scale-is-always-Self clamp):

```csharp
// before
handle.ChangeHandleType(HandleType.Rotation);
handle.ChangeHandleSpace(Space.World);
handle.ChangeAxes(HandleAxes.XY);

// after
handle.type  = HandleType.Rotation;
handle.space = Space.World;
handle.axes  = HandleAxes.XY;
```

`TransformHandleManager.ChangeHandleType(handle, type)` and `ChangeHandleSpace(handle, space)` are
unchanged and now route through the setters internally.

## 3. `target` and `handleCamera` are read-only

`Handle.target` and `Handle.handleCamera` are now read-only properties (set by the package when the
handle is enabled). Reading them is unchanged; **assigning** them no longer compiles.

```csharp
// before (no longer compiles)
handle.target = myTransform;

// after — create the handle for the target instead
var handle = TransformHandleManager.Instance.CreateHandle(myTransform);
// add/remove more targets:
TransformHandleManager.Instance.AddTarget(other, handle);
TransformHandleManager.Instance.RemoveTarget(other, handle);
```

## 4. Public methods now throw on contract violations

`CreateHandle`, `CreateHandleFromList`, `AddTarget`, `RemoveTarget`, `RemoveHandle`,
`ChangeHandleType`, `ChangeHandleSpace`, and `ChangeHandlePivot` now throw
`ArgumentNullException` (and `ArgumentException`/`InvalidOperationException`) on null/empty/unmanaged
arguments, instead of logging an error and returning `null`/`false`. Benign state conditions
("target already has a handle", target not found) still log a warning and no-op.

```csharp
// before — returned null on a null target
var handle = TransformHandleManager.Instance.CreateHandle(maybeNull);
if (handle == null) { ... }

// after — guard before calling
if (maybeNull == null) return;
var handle = TransformHandleManager.Instance.CreateHandle(maybeNull);
```

## 5. Reduced public surface (`internal`)

`Ghost`'s interaction callbacks (`OnInteractionStart`, `OnInteraction`, `UpdateGhostTransform`,
`ResetGhostTransform`, `Terminate`) and `TransformGroup`'s `UpdatePositions`/`UpdateRotations`/
`UpdateScales`/`UpdateBounds` plus the `Transforms`/`RenderersMap`/`BoundsMap` collections are now
`internal`. These were internal-use plumbing driven by the manager; if you called them directly,
drive interaction through the public `Handle`/`TransformHandleManager` API and the `Handle` events
(`OnInteractionStartEvent`, `OnInteractionEvent`, `OnInteractionEndEvent`) instead.

## Not breaking, but worth knowing

- `type`, `axes`, `space`, `snappingType`, `positionSnap`, `rotationSnap`, `scaleSnap` are now
  properties (same names — your reads/writes still compile). Writing `type` or `axes` now rebuilds
  the child handles immediately; previously a raw field write left stale children.
- Prefab/inspector values for those fields are preserved across the upgrade (`FormerlySerializedAs`).
