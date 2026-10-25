# Product Configurator

A small desktop collection: arrange a monitor, desk lamp and accessory tray, then choose White, Graphite or Sand finishes. This independently imported sample uses the public Core API and the shared white UI Toolkit presentation.

Install Core and the optional Demos package, import **Product Configurator** from Package Manager, run **Tools > Transform Handles > Setup Layer**, and open `ProductConfigurator.unity`. Unity 2021.3.0f1 or later is supported. No URP, Input System or other sample import is required. With the new input backend, Input System 1.1.1 or later must be installed. Import each sample only once.

## Controls and placement

Click a product to select it; Shift-click toggles selection membership. Child colliders resolve to their registered product root. Click empty space to clear selection. Left-drag orbits the camera, right-drag pans, and scroll zooms. UI and handle gestures own their entire press, so dragging outside a control does not start navigation.

Move (`W`) exposes X/Z axes and the horizontal plane. Rotate (`E`) exposes only Y. Product dimensions are fixed: Scale and snapping are intentionally absent. World uses the desk axes; Local follows the selected product, or the last selected product for a group. Both spaces share the vertical rotation axis. Groups rotate around the mean of their root pivots.

The whole product must fit on the fixed horizontal desk, including overhanging screen/shade geometry. Bounds are conservative: each product's child mesh bounds are combined in root-local space once, then transformed for each preview. During a drag, invalid placement is described in the inspector. Normal release commits only if every selected product fits; otherwise the entire selection returns to its drag-start poses. Products can overlap; there is no collision or stacking solver.

Controller disable/destruction, focus loss, manager cancellation, navigation and selected-target loss cancel an unfinished drag and restore surviving products, even if the preview was valid. Re-enabling retains the committed layout and finishes. Reset restores initial poses, White finishes, World/Move tools and the initial camera; it preserves the 64–512 pixel handle-size preference. Reset does not recreate products destroyed by external code.

The inspector contains transform tools, World/Local space, finish choices, handle size and placement feedback. A mixed finish selection is labeled Mixed; choosing a finish applies it to every selected product. Only designated body renderers change: display, diffuser, lining and accessories keep their authored materials. Finish, size, tool and Reset commands are blocked during a handle drag. Enter or focus loss commits the size field; Escape cancels text editing, closes Help, or cancels/clears selection in that order.

## Scene and ownership

`ProductConfiguratorDemo` follows the Modular Building controller lifecycle. Its serialized nested product records contain a root Transform, display name and finish Renderer array. Roots are distinct, unparented, unit-scaled, yaw-only and at the configured desk height. The desk rectangle is defined by `deskCenter` and `deskSize`; keep the authored desktop geometry aligned with those fields. Products and geometry are static during a session apart from supported root movement/rotation. Runtime geometry resizing, arbitrary tilted surfaces and skinned products are outside this sample's contract. Invalid initial configuration fails explicitly.

The supplied beveled meshes and materials belong to this sample. Its shader works in Built-in and URP without a pipeline package dependency. The controller owns only its created handles, cloned finish materials, UI panel settings and any manager/EventSystem it created. Disabling restores borrowed manager settings and original finish materials; re-enabling attaches owned materials with the current choices. Other controllers' handles are never cleared. Simultaneous active demo controllers in additive scenes are not supported.

UI assets, fonts, shader, settings and input bridge are sample-local, with no references to another sample. `UI/DemoTheme.uss` is generated from `ci/demo-ui/DemoTheme.uss`; synchronize package sources with `python3 ci/sync-demo-ui.py`. This never overwrites consumer imports. Demo Browser binds the named `demo-gallery` header slot when installed; standalone play hides it.

## Build and verification

`BuildScript` is a repository host utility and is not included in the installed package. Consumer projects can add `ProductConfigurator.unity` to Build Settings and build normally. In the repository host, run `BuildScript.PrepareProductConfiguratorWebGL`, let the imported assembly compile, then run `BuildScript.BuildProductConfiguratorWebGL` in a separate Editor invocation with `-buildTarget WebGL`. The repository's combined Browser build includes this scene. The public Pages build still targets the original showcase.

Run imported PlayMode tests with `-testFilter TransformHandles.Samples.ProductConfigurator.Tests`. Coverage includes composite selection, actual Core group translation/rotation, rotated bounds, atomic rollback, manager-driven cancellation, target loss, material ownership, Reset, text editing and camera/UI isolation. The supplied scene is loaded separately to validate its actual product geometry. Run the Browser suite with all four workspaces imported for navigation and header-rebinding coverage.

This demo does not add a catalog, object spawning, save/load, export, Undo/Redo, arbitrary surface placement or mobile controls. It demonstrates constrained transform handles and product finishes rather than a complete commerce application.
