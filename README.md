# Runtime Transform Handles for Unity

![Icon](https://i.imgur.com/NRdmzlQ.png)

[![Unity 2019.4+](https://img.shields.io/badge/Unity-2019.4%2B-blue.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Latest Release](https://img.shields.io/github/v/release/manaporkun/UnityRuntimeTransformHandles?include_prereleases)](https://github.com/manaporkun/UnityRuntimeTransformHandles/releases)
[![OpenUPM](https://img.shields.io/badge/OpenUPM-compatible-blue.svg)](https://openupm.com/)

## About

This project is based on [RuntimeTransformHandle](https://github.com/pshtif/RuntimeTransformHandle) by pshtif.

Unity Runtime Transform Handles is a powerful tool that allows developers to transform objects at runtime using a set of intuitive and professional gizmos. Ideal for building modding tools, runtime editors, or games that require object manipulation, this plugin adds multiple object selection, changeable origin points, better auto scale, multiple handles, and other features to the base project.

### Features

- Position, Rotation, and Scale handles
- Multiple object selection and manipulation
- Changeable origin points (pivot/center)
- World and Local coordinate space support
- Configurable snapping for precise transformations
- Auto-scaling handles based on camera distance
- Customizable keyboard shortcuts
- Event system for handle interactions

### Video Demo

[![Video](https://i.imgur.com/OSXsYXA.png)](https://www.youtube.com/watch?v=-6tpim397F0)

## Requirements

- Unity **2019.4** or higher
- Works with both **Legacy Input Manager** and **New Input System**

## Installation

### Option 1: Package Manager (Recommended)

1. Open the Package Manager from `Window > Package Manager`
2. Click the `"+" button > Add package from git URL`
3. Enter the following URL:
   ```
   https://github.com/manaporkun/UnityRuntimeTransformHandles.git#upm
   ```

### Option 2: Manual Installation

Open `Packages/manifest.json` and add the following to the dependencies block:

```json
{
    "dependencies": {
        "com.orkunmanap.runtime-transform-handles": "https://github.com/manaporkun/UnityRuntimeTransformHandles.git#upm"
    }
}
```

### Option 3: Specific Version

To install a specific version, append the version tag:

```json
{
    "dependencies": {
        "com.orkunmanap.runtime-transform-handles": "https://github.com/manaporkun/UnityRuntimeTransformHandles.git#v1.0.0"
    }
}
```

### Post-Installation Setup

After installing the package, set up the required layer:
1. Go to `Tools > Transform Handles > Setup Layer`
2. This creates the "TransformHandle" layer needed for handle raycasting

## Quick Start

### Basic Usage

```csharp
using TransformHandles;
using UnityEngine;

public class SimpleExample : MonoBehaviour
{
    private TransformHandleManager _manager;

    void Start()
    {
        _manager = TransformHandleManager.Instance;
    }

    void CreateHandleForObject(Transform target)
    {
        // Create a handle for a single object
        Handle handle = _manager.CreateHandle(target);

        // Subscribe to events
        handle.OnInteractionStartEvent += OnHandleStart;
        handle.OnInteractionEndEvent += OnHandleEnd;
    }

    void OnHandleStart(Handle handle)
    {
        Debug.Log("Started manipulating: " + handle.target.name);
    }

    void OnHandleEnd(Handle handle)
    {
        Debug.Log("Finished manipulating: " + handle.target.name);
    }
}
```

### Multiple Object Selection

```csharp
// Create a handle for multiple objects
List<Transform> targets = new List<Transform> { obj1, obj2, obj3 };
Handle handle = _manager.CreateHandleFromList(targets);

// Add more targets to an existing handle
_manager.AddTarget(newTarget, handle);

// Remove a target from a handle
_manager.RemoveTarget(targetToRemove, handle);
```

### Changing Handle Properties

```csharp
// Change handle type
TransformHandleManager.ChangeHandleType(handle, HandleType.Position);
TransformHandleManager.ChangeHandleType(handle, HandleType.Rotation);
TransformHandleManager.ChangeHandleType(handle, HandleType.Scale);
TransformHandleManager.ChangeHandleType(handle, HandleType.All);

// Change coordinate space
_manager.ChangeHandleSpace(handle, Space.World);
_manager.ChangeHandleSpace(handle, Space.Self);

// Configure snapping
handle.positionSnap = new Vector3(0.5f, 0.5f, 0.5f);
handle.rotationSnap = 15f;
handle.scaleSnap = new Vector3(0.1f, 0.1f, 0.1f);
```

## Default Keyboard Shortcuts

| Key | Action |
|-----|--------|
| W | Position mode |
| E | Rotation mode |
| R | Scale mode |
| A | All modes (Position + Rotation + Scale) |
| X | Toggle World/Local space |
| Z | Toggle Pivot/Center origin |

## Main Components

### TransformHandleManager

Central manager for all transform handles in the scene. Handles creation, destruction, and interaction with transform handles.

**Key Methods:**
- `CreateHandle(Transform target)` - Creates a handle for a single object
- `CreateHandleFromList(List<Transform> targets)` - Creates a handle for multiple objects
- `AddTarget(Transform target, Handle handle)` - Adds an object to an existing handle
- `RemoveTarget(Transform target, Handle handle)` - Removes an object from a handle
- `RemoveHandle(Handle handle)` - Removes a handle
- `DestroyAllHandles()` - Destroys all handles
- `ChangeHandleType(Handle handle, HandleType type)` - Changes the handle type
- `ChangeHandleSpace(Handle handle, Space space)` - Changes the coordinate space

### Handle

Main handle component that manages transform manipulation through position, rotation, and scale handles.

**Properties:**
- `target` - The transform being manipulated
- `type` - Current handle type (Position, Rotation, Scale, or combinations)
- `space` - Coordinate space (Self or World)
- `axes` - Active axes (X, Y, Z, or combinations)
- `positionSnap`, `rotationSnap`, `scaleSnap` - Snapping values
- `autoScale` - Enable/disable auto-scaling based on camera distance

**Events:**
- `OnInteractionStartEvent` - Fired when interaction begins
- `OnInteractionEvent` - Fired during interaction
- `OnInteractionEndEvent` - Fired when interaction ends
- `OnHandleDestroyedEvent` - Fired when handle is destroyed

### Ghost

Represents an empty transform object that serves as the pivot point for handle manipulation. The Ghost transform is instantiated when a handle is created and updates its position, rotation, and scale based on user input.

### TransformGroup

Groups and transforms multiple Unity Transform objects together. Contains methods to add/remove transforms and update the group's position, rotation, and scale collectively.

## Handle Types

```csharp
public enum HandleType
{
    Position,
    Rotation,
    Scale,
    PositionRotation,
    PositionScale,
    RotationScale,
    All
}
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes using [Conventional Commits](https://www.conventionalcommits.org/):
   - `feat: add new feature` (triggers minor version bump)
   - `fix: resolve bug` (triggers patch version bump)
   - `feat!: breaking change` (triggers major version bump)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

Releases are automated via GitHub Actions based on commit messages.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Credits

- Original project by [pshtif](https://github.com/pshtif/RuntimeTransformHandle)
- Extended and maintained by [Orkun Manap](https://manap.dev)

## Support

If you encounter any issues or have questions, please [open an issue](https://github.com/manaporkun/UnityRuntimeTransformHandles/issues) on GitHub.
