# Demo Browser

An optional UI Toolkit gallery for opening the four demo workspaces in one player. Import **Demo Browser** from the Demos package's Samples tab, then import the demos you want to include. Core-only and individual demo imports do not depend on this sample.

## Run in the Editor

1. Add `DemoBrowser.unity` and your imported demo scenes to the enabled scenes in Build Settings (Build Profiles in Unity 6).
2. Open `DemoBrowser.unity` and enter Play Mode.
3. Open a workspace. Use **Demos** in the header to return to the gallery. Returning discards that workspace's changes.

The gallery shows only included scenes named `ModularBuilding`, `LevelEditor`, `RuntimeTransformHandlesDemo`, or `ProductConfigurator`. It resolves their full build paths, supports custom import folders, and rejects duplicate filenames rather than opening an arbitrary match. Keep these scene filenames when relocating samples. With no demo scenes included, it shows setup guidance.

The Browser owns a persistent gallery document and EventSystem. Each workspace declares the same `demo-gallery` header button; the Browser binds that slot and detects rebuilt UI trees after controller re-enable. Standalone samples hide the slot. Demo scenes load individually; the gallery scene separates successive workspaces so the previous workspace's handles and manager finish their cleanup. Navigation claims its mouse press before the handle manager processes input, including when the showcase's UI test toggle is off. A navigation press cancels an active handle drag. It does not change the underlying standalone demo controls.

## Build all demos from this repository

Use **Tools > Build > Prepare Demo Browser**, wait for compilation, then **Tools > Build > Demo Browser WebGL**. Existing sample imports are never overwritten; update them explicitly if you want newer package contents.

For CLI builds, use two Unity invocations:

```sh
Unity -batchmode -quit -buildTarget WebGL -projectPath PROJECT \
  -executeMethod BuildScript.PrepareDemoBrowserWebGL -logFile prepare.log
Unity -batchmode -quit -buildTarget WebGL -projectPath PROJECT \
  -executeMethod BuildScript.BuildDemoBrowserWebGL \
  -buildPath Build/DemoBrowserWebGL -logFile build.log
```

`BuildScript` is a repository host utility, not a package dependency. Consumer projects can build normally with the Browser scene first. Existing individual-demo build methods and the public Pages build remain unchanged.

## Tests and layout

The PlayMode suite resolves absent and ambiguous scenes, verifies gallery entries, and exercises repeated workspace transitions when all demos are imported. The test prebuild step temporarily sets the required build scenes and restores the previous list afterward. Browser-only imports can run the gallery tests; the integration test requires all four workspaces.

Edit `UI/DemoBrowser.uxml` and `UI/DemoBrowser.uss` in UI Builder. Panel settings and the licensed font are local to this sample. Unity 2021.3.0f1 or later, uGUI and UI Toolkit are required; Input System is optional (1.1.1 or later when enabled).

Lamp Assembly appears as the fourth workspace when its scene is included in Build Settings. Its sample is optional for consumer galleries; the repository combined-build helper imports all four workspaces. Four cards wrap within the scrollable gallery at narrow widths.
