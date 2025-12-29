# Runtime Transform Handles - Improvement Roadmap

This document outlines potential improvements and features for the Unity Runtime Transform Handles package.

## Quick Wins (High Impact, Low Effort)

### ~~Configurable Keyboard Shortcuts~~ (COMPLETED)
~~Move hardcoded W/E/R/A/X/Z keys to a ScriptableObject configuration, allowing users to customize shortcuts without modifying source code.~~

**Implementation:** `TransformHandleSettings` ScriptableObject with:
- Configurable key bindings for all shortcuts
- Enable/disable shortcuts toggle
- Default handle settings (type, space, axes)
- Highlight color configuration

Create via `Assets > Create > Transform Handles > Settings`

### ~~UnityEvents Option~~ (COMPLETED)
~~Add UnityEvent alternatives alongside existing C# events for designer-friendly integration in the Inspector.~~

**Implementation:** `HandleUnityEvent` class with serialized fields on `Handle`:
- `onInteractionStart`, `onInteraction`, `onInteractionEnd`, `onHandleDestroyed`
- Configure directly in Inspector, no code required

### ~~Touch Input Support~~ (COMPLETED)
~~Extend `InputWrapper` to handle touch and mobile input seamlessly.~~

**Implementation:** Enhanced `InputWrapper` with:
- Touch detection via `IsTouchSupported`, `TouchCount`, `TouchPosition`
- Unified `PointerPosition` for mouse/touch
- Mouse button methods automatically handle touch (button 0 = touch)
- Works with both Legacy Input and New Input System

### ~~Handle Customization~~ (COMPLETED)
~~Expose handle visual properties (colors, sizes, line widths) via public properties or ScriptableObject.~~

**Implementation:**
- `HandleAppearance` struct with axis colors and scale
- `TransformHandleSettings` extended with visual settings (X/Y/Z colors, global color, scale)
- `Handle.ScaleMultiplier` property for runtime size adjustment
- `Handle.ApplySettings()` to apply from ScriptableObject

### ~~XML Documentation~~ (COMPLETED)
~~Add XML documentation comments to all public API members for IntelliSense support.~~

**Implementation:** Full XML docs added to:
- All enums (`HandleType`, `HandleAxes`, `SnappingType`, `Origin`)
- `Ghost`, `TransformGroup` classes
- `Handle` appearance region
- `InputWrapper` touch methods

---

## Medium Effort Improvements

### Undo/Redo Integration
Integrate with Unity's Undo system or provide a custom history stack for transform operations.

**Benefits:** Essential for any editor-like application
**Considerations:** Need to decide between Unity's Undo (editor-only) vs custom solution (runtime)

### VR/XR Controller Support
Add XR Interaction Toolkit integration for VR/AR manipulation.

**Features:**
- Controller ray interaction
- Grab-based manipulation
- Haptic feedback

### Constraint System
Allow constraining transformations with min/max values, axis locks, and validation callbacks.

**API Example:**
```csharp
handle.constraints.minPosition = new Vector3(-10, 0, -10);
handle.constraints.maxPosition = new Vector3(10, 5, 10);
handle.constraints.lockRotationX = true;
handle.OnValidateTransform += (delta) => /* custom validation */;
```

### Object Pooling
Pool handle GameObjects instead of instantiate/destroy cycles.

**Benefits:** Reduced GC pressure, faster handle creation
**Implementation:** Internal pool managed by `TransformHandleManager`

### Unit Test Suite
Add Unity Test Framework tests for core functionality.

**Coverage Areas:**
- TransformGroup operations
- Snapping calculations
- Multi-object selection
- Input handling
- Event firing

### Snap Grid Visualization
Show a visual grid overlay when snapping is enabled during manipulation.

**Features:**
- Configurable grid appearance
- Axis-aligned grid planes
- Dynamic visibility based on handle type

---

## Major Features (High Effort)

### Custom Handle Types
Plugin architecture allowing users to create custom handle gizmos.

**Architecture:**
- `ICustomHandle` interface
- Registration system in manager
- Custom collider and visual support

### Physics-Aware Movement
Collision detection during drag operations with configurable responses.

**Options:**
- Stop at collision
- Slide along surfaces
- Callback for custom handling

### 2D/RectTransform Handles
Support for UI Canvas and 2D sprite manipulation.

**Features:**
- Anchor point manipulation
- Pivot editing
- Size delta controls
- 2D rotation (Z-axis only mode)

### DI-Friendly Architecture
Decouple from Singleton pattern for better testability and flexibility.

**Changes:**
- Interface extraction (`ITransformHandleManager`)
- Optional Singleton wrapper
- Constructor/property injection support

### World-Space Scale
Currently scale handles only work in local space. Add true world-space scaling.

**Challenges:**
- Non-uniform scale with rotation
- Shear prevention
- Multi-object world scale

---

## Stretch Goals

### Multi-User Collaboration
Network synchronization for collaborative editing scenarios.

### Animation Integration
Record transform changes as animation keyframes.

### Gizmo Presets
Saved configurations for different use cases (precision mode, fast mode, etc.).

### Localization
Localized UI strings for international users.

---

## Implementation Priority

```
Phase 1 (Quick Wins) ✓ COMPLETE
├── Configurable Shortcuts ✓
├── Handle Customization ✓
├── Touch Input Support ✓
├── UnityEvents Option ✓
└── XML Documentation ✓

Phase 2 (Core Improvements)
├── Constraint System
├── Undo/Redo Integration
├── Object Pooling
└── Unit Test Suite

Phase 3 (Platform Expansion)
├── VR/XR Support
├── 2D/RectTransform Handles
└── Snap Grid Visualization

Phase 4 (Architecture)
├── DI-Friendly Refactor
├── Custom Handle Types
└── Physics-Aware Movement
```

---

## Contributing

When implementing improvements:
1. Follow conventional commits for automatic versioning
2. Add/update XML documentation for public API changes
3. Include unit tests for new functionality
4. Update CLAUDE.md if architecture changes significantly
