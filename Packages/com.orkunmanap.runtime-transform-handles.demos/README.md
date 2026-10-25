# Runtime Transform Handles Demos

Optional examples for Runtime Transform Handles. Core does not depend on this package.
Import **Level Editor** for the Prop Workshop: select, move, rotate, scale and reset six props, with World/Local group movement and rotation, plus shared-center uniform group scaling. Import **Modular Building** for a wall-and-corner arrangement scene with a fixed 1-unit horizontal grid, 90-degree yaw, Reset and the same camera/handle-size controls. The three workspaces are separate imports through Package Manager > Samples. Import **[Demo Browser](Samples~/DemoBrowser/README.md)** as an optional fourth sample to navigate between included workspaces in one player. Returning to the gallery resets the departed workspace.
The workspaces share a white UI Toolkit header, scrollable inspector, Help panel and pixel-size control. Showcase-only tools live in foldouts. The showcase uses a pipeline-independent target material.

## Install

This work is not published yet. For local development install both package folders through explicit
file dependencies. For a future reviewed revision, add BOTH project manifest entries, replacing
<FULL_SHA> with the same immutable published repository commit:

```json
{
  "dependencies": {
    "com.orkunmanap.runtime-transform-handles": "https://github.com/manaporkun/UnityRuntimeTransformHandles.git?path=/Packages/com.orkunmanap.runtime-transform-handles#<FULL_SHA>",
    "com.orkunmanap.runtime-transform-handles.demos": "https://github.com/manaporkun/UnityRuntimeTransformHandles.git?path=/Packages/com.orkunmanap.runtime-transform-handles.demos#<FULL_SHA>"
  }
}
```

Unity cannot resolve a Git URL from another package's package.json. The two project entries are
intentional. The prototype's Core dependency version is the current checkout manifest, not a claim
that old published Core 3.3.0 has passed this revision's tests. Align and retest the release pair
before publishing. Core-only installs can continue using the existing Core #upm distribution.

Select **Runtime Transform Handles Demos** in Package Manager and import the desired sample.
Run **Tools > Transform Handles > Setup Layer** before Play. No URP or Input System dependency is
added by this package; UI dependencies belong to demos, not Core.
All four samples use built-in UI Toolkit with sample-local UXML/USS layouts and fonts.
They do not require TextMeshPro or an Essential Resources import. See its
[setup instructions](Samples~/ModularBuilding/README.md).

## Migrate an existing showcase

Core no longer includes a Samples tab entry. Existing imported scenes remain in Assets and are not
automatically edited or deleted. Before importing the moved showcase, back up customized content
OUTSIDE Assets (or export a .unitypackage), then explicitly remove the previous imported folder.
Its GUIDs are preserved, so keeping both copies would cause GUID and assembly collisions.
The previous path was Assets/Samples/Runtime Transform Handles/<version>/Runtime Transform Handles Demo.
The new path uses Runtime Transform Handles Demos as the package display name.
The build helper rejects duplicate GUIDs rather than overwriting a customized import.

Removing the demos package does not remove Assets copies. Keep Core and explicit project dependencies
on com.unity.ugui and com.unity.modules.imgui when retaining imports. Retained demo imports also need com.unity.modules.uielements for Toolkit navigation hit testing; Modular Building and Demo Browser render their controls with it. Input System remains optional. When using its backend, use 1.1.1 or later for these demos; the retained original showcase also calls the runtime UI default-actions API and is not compatible with Input System 1.0.
To remove examples entirely, explicitly remove their imported folders after backing up changes.
This sample relocation is a distribution breaking change; preserve old version pins when needed.
