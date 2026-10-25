# Shared demo presentation

`DemoTheme.uss` is the canonical style source for the five optional samples. It defines the workspace header, scrollable inspector, typography, tools, Help and input states. Scenario UXML and small scenario-specific styles stay in their own samples. Workspace classes are scoped under `.demo-shell`; the gallery uses `.demo-gallery`.

After changing the canonical theme, run:

```sh
python3 ci/sync-demo-ui.py
python3 ci/sync-demo-ui.py --check
```

The script updates package-source stylesheet copies only. It never overwrites consumer imports, modifies GUIDs, or creates a runtime dependency between samples. Each imported sample retains its own theme and licensed font. The drift check runs in Min-API Lint CI.

Each workspace UXML declares a `Button` named `demo-gallery`, with class `demo-gallery-link`, hidden by default. Demo Browser adds `available` and binds its callback when that scene is active. It detaches and rebinds if a controller re-enables and UIDocument reconstructs its tree. Keep that slot in the header when editing a layout.
