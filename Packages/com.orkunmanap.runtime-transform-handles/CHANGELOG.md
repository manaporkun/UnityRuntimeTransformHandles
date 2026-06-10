# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.4] - 2026-06-10

- Merge pull request #35 from manaporkun/ci/publish-push-rebase
- fix: match Unity Editor scale handle feel and gizmo visuals
- ci: recover release notes from the bump commit's changelog
- ci: package the exact bump commit, not main HEAD
- ci: make publish-recovery robust to new commits and API errors
- ci: complete an interrupted publish instead of skipping it
- ci: recompute release against latest main on every push attempt
- ci: don't waste the final push attempt on a rebase
- ci: compute release version against fresh origin/main
- ci: fail clean on version-bump rebase conflict
- ci: make version-bump push resilient to main advancing

## [Unreleased]

### Fixed
- Align axis and uniform scale input with Unity Editor handle math (`CalcLineTranslation`, `GetHandleSize`).
- Keep scale axis line and cube gizmo visuals in sync (tube mesh length vs cube rest distance).
- Reset scale axis gizmo visuals on drag end and re-initialize; guard absolute snap when start scale is zero.
- Guard scale handles when the interaction camera is missing.

### Added
- `HandleTransformUtility` runtime port of Unity Editor scale handle math.
- Edit Mode tests for handle transform utility and line/cube visual reach parity.

## [3.0.3] - 2026-06-01

- Merge pull request #34 from manaporkun/test/group-apply-paths
- Merge pull request #33 from manaporkun/fix/scaleglobal-framerate
- test: cover TransformGroup apply paths (positions + scales)
- Merge pull request #32 from manaporkun/ci/min-api-lint
- fix: make global (uniform) scale speed framerate-independent
- ci: add min-API lint guarding the Unity 2021.3 floor
- Merge pull request #30 from manaporkun/docs/handle-target-clarify
- Merge pull request #29 from manaporkun/ci/harden-publish
- docs: fix quick-start logging handle.target (the pivot, not the object)
- ci: harden publish (bump filter, concurrency, dependabot)

## [3.0.2] - 2026-06-01

- Merge pull request #28 from manaporkun/test/coverage-bounds-rotation
- test: cover GetBounds and world/self group rotation
- Merge pull request #27 from manaporkun/fix/changelog-output-newline
- ci: fix changelog GITHUB_OUTPUT heredoc missing trailing newline

## [3.0.1] - 2026-06-01

- Merge pull request #24 from manaporkun/fix/rotation-bounds-correctness
- Merge pull request #25 from manaporkun/fix/release-version-range
- ci: fix release version bump computed from entire history
- fix: correct world-space group rotation and multi-renderer bounds

## [3.0.0] - 2026-06-01

- Merge pull request #23 from manaporkun/develop
- chore: remove planning markdown (CLAUDE, IMPROVEMENT_PLAN, ROADMAP)
- refactor: consolidate WebGL demo onto the package sample
- chore: bump version to 2.0.0 [skip ci]
- Merge pull request #22 from manaporkun/develop
- fix: address PR #22 review feedback
- fix: assign position-plane quad materials by normal axis
- fix: color position-plane gizmos by normal axis for Unity parity
- Merge pull request #20 from manaporkun/docs/2.0-migration-readme
- docs: add 1.x->2.0 migration guide, API reference, sample README; sync root README
- Merge pull request #17 from manaporkun/fix/change-axes-mask-10
- fix: enable each position plane on the axis pair it constrains + harden scale-delta
- Merge pull request #19 from manaporkun/feat/upm-2.0-industry-standard
- fix: declare URP dependency so handle shaders resolve without URP installed (PR #19 review)
- ci: grant checks/pull-requests write so test-runner can post results
- docs: mark P2-2 validation gate done in improvement plan
- ci: add UPM package validation gate (P2-2)
- fix: capture position-axis drag vector in handle-local space
- fix: absolute position snap only snaps the dragged axis
- feat!: bring UPM package to industry standard (2.0.0)
- chore: upgrade to Unity 6000.3.16f1 and add demo scene
- refactor: tidy handle component internals
- fix: stop long HandleType names clipping in demo HUD
- fix: make HandleDemo HUD legible over bright scenes
- fix: auto-load configured manager prefab from Resources
- fix: use FindAnyObjectByType in HandleDemo to drop CS0618 warning
- docs: add self-contained HandleDemo exercising the full public API
- chore: bump version to 1.20.0 [skip ci]
- Merge pull request #14 from manaporkun/fix/change-axes-mask-10
- perf: cache handle materials once so re-runnable Initialize doesn't leak
- fix: make handle Initialize re-runnable so ChangeAxes applies the mask
- chore: bump version to 1.19.0 [skip ci]
- docs: update README project description and credits
- chore: bump version to 1.18.0 [skip ci]
- ci: fix WebGL Pages build running out of disk space
- chore: bump version to 1.17.0 [skip ci]
- chore: update README, clean up tracked files, improve .gitignore
- chore: bump version to 1.16.0 [skip ci]
- Merge pull request #9 from manaporkun/fix/code-analysis-improvements
- fix: use localScale consistently in Ghost scale delta calculations
- fix: add warning log and update docs for null target in AddTransform
- fix: address Copilot review feedback
- fix: reduce GC pressure and improve null safety across handle system
- chore: bump version to 1.15.0 [skip ci]
- ci: avoid writing .nojekyll into Unity build output
- chore: bump version to 1.14.0 [skip ci]
- ci: resolve WebGL output path for pages artifact
- chore: bump version to 1.13.0 [skip ci]
- ci: fix Unity activation env handling for GameCI
- chore: bump version to 1.12.0 [skip ci]
- Force license-file activation by clearing serial and credentials
- chore: bump version to 1.11.0 [skip ci]
- Use UNITY_LICENSE-only activation in CI
- chore: bump version to 1.10.0 [skip ci]
- Pass Unity credentials to WebGL build workflow
- chore: bump version to 1.9.0 [skip ci]
- Simplify README browser demo section
- chore: bump version to 1.8.0 [skip ci]
- Add WebGL showcase site and Pages deployment pipeline
- chore: bump version to 1.7.0 [skip ci]
- fix: clean up _transformHashSet in RemoveHandle to allow handle re-creation
- feat: implement Phase 1 quick wins for improved usability
- chore: bump version to 1.6.0 [skip ci]
- refactor(package): Rename to FQDN format
- chore: bump version to 1.5.0 [skip ci]
- chore: Update project development settings
- chore: bump version to 1.4.0 [skip ci]
- chore: update project to Unity 6 and New Input System
- chore: bump version to 1.3.0 [skip ci]
- docs: update README with dynamic badges and conventional commits
- chore: bump version to 1.2.0 [skip ci]
- fix: resolve FindObjectOfType deprecation warning
- chore: bump version to 1.1.0 [skip ci]
- fix(ci): add write permissions to workflow
- docs: update README and add CLAUDE.md
- ci: add GitHub Action for automatic UPM publishing
- feat: add New Input System support and runtime layer configuration
- Merge remote-tracking branch 'origin/copilot/fix-import-package-errors'
- chore: add missing meta files
- Merge remote-tracking branch 'origin/claude/what-do-you-01DYQNLqJJ8VTkw74CexH3tJ'
- Improve README with comprehensive documentation
- Merge branch 'feature/code_overhaul'
- Improve code quality across the codebase
- Refactor: Extract TransformGroup and consolidate collider controllers
- Merge branch 'hotfix/2-error-when-creating-handle-in-transfor'
- Create a ghost object if prefab is empty
- Delete unused Axis script, don't update ghost transform on interaction end, public properties for groups
- Small change
- Every handle has its own interaction events
- Fix the RemoveHandle function called twice bug
- deactivate position plane when it cannot be seen
- Deactivate position handle when it cannot be seen
- New test scene, scene changer, handle OnDestroy, ActiveSceneChanged, singleton update
- Prevent possible errors when camera got destroyed
- Update README.md
- Update README.md
- Delete .idea/.idea.UnityRuntimeTransformHandles/.idea directory
- Delete .github/workflows directory
- Update main.yml
- Delete license.yml
- Update main.yml
- Update main.yml
- Update license.yml
- Create license.yml
- Update main.yml
- Create .github/workflows/main.yml
- Delete unity-package.yml
- Update unity-package.yml
- Create unity-package.yml
- Make it a Unity package
- Add shortcut texts to the scene
- Make materials more transparent, origin indicator update, handle prefab update, etc.
- Cache HSV values and change the script name
- Update README.md typo
- Update README.md
- default pixel size change
- Fixes and project settings
- Auto scale extension
- Remove accidentally pushed build
- Small change
- AutoScale fix
- Camera zoom, and other small changes
- Code refactor and folders
- URP, torus collider mesh change, scene update, etc
- Some improvements, folder change, etc
- Small fix
- Handle shader and bug fix
- Camera movement, events, bug fixes
- A lot of fixes
- Handle prefab
- Remove com.shtif.runtimetransformhandle
- New scripts
- Peter @sHTiF Stefcek's Runtime Transform Handles added as a package
- Unity project init
- Initial commit

## [2.0.0] - 2026-06-01

- Merge pull request #22 from manaporkun/develop
- fix: address PR #22 review feedback
- fix: assign position-plane quad materials by normal axis
- fix: color position-plane gizmos by normal axis for Unity parity
- Merge pull request #20 from manaporkun/docs/2.0-migration-readme
- docs: add 1.x->2.0 migration guide, API reference, sample README; sync root README
- Merge pull request #17 from manaporkun/fix/change-axes-mask-10
- fix: enable each position plane on the axis pair it constrains + harden scale-delta
- Merge pull request #19 from manaporkun/feat/upm-2.0-industry-standard
- fix: declare URP dependency so handle shaders resolve without URP installed (PR #19 review)
- ci: grant checks/pull-requests write so test-runner can post results
- docs: mark P2-2 validation gate done in improvement plan
- ci: add UPM package validation gate (P2-2)
- fix: capture position-axis drag vector in handle-local space
- fix: absolute position snap only snaps the dragged axis
- feat!: bring UPM package to industry standard (2.0.0)
- chore: upgrade to Unity 6000.3.16f1 and add demo scene
- refactor: tidy handle component internals
- fix: stop long HandleType names clipping in demo HUD
- fix: make HandleDemo HUD legible over bright scenes
- fix: auto-load configured manager prefab from Resources
- fix: use FindAnyObjectByType in HandleDemo to drop CS0618 warning
- docs: add self-contained HandleDemo exercising the full public API
- chore: bump version to 1.20.0 [skip ci]
- Merge pull request #14 from manaporkun/fix/change-axes-mask-10
- perf: cache handle materials once so re-runnable Initialize doesn't leak
- fix: make handle Initialize re-runnable so ChangeAxes applies the mask
- chore: bump version to 1.19.0 [skip ci]
- docs: update README project description and credits
- chore: bump version to 1.18.0 [skip ci]
- ci: fix WebGL Pages build running out of disk space
- chore: bump version to 1.17.0 [skip ci]
- chore: update README, clean up tracked files, improve .gitignore
- chore: bump version to 1.16.0 [skip ci]
- Merge pull request #9 from manaporkun/fix/code-analysis-improvements
- fix: use localScale consistently in Ghost scale delta calculations
- fix: add warning log and update docs for null target in AddTransform
- fix: address Copilot review feedback
- fix: reduce GC pressure and improve null safety across handle system
- chore: bump version to 1.15.0 [skip ci]
- ci: avoid writing .nojekyll into Unity build output
- chore: bump version to 1.14.0 [skip ci]
- ci: resolve WebGL output path for pages artifact
- chore: bump version to 1.13.0 [skip ci]
- ci: fix Unity activation env handling for GameCI
- chore: bump version to 1.12.0 [skip ci]
- Force license-file activation by clearing serial and credentials
- chore: bump version to 1.11.0 [skip ci]
- Use UNITY_LICENSE-only activation in CI
- chore: bump version to 1.10.0 [skip ci]
- Pass Unity credentials to WebGL build workflow
- chore: bump version to 1.9.0 [skip ci]
- Simplify README browser demo section
- chore: bump version to 1.8.0 [skip ci]
- Add WebGL showcase site and Pages deployment pipeline
- chore: bump version to 1.7.0 [skip ci]
- fix: clean up _transformHashSet in RemoveHandle to allow handle re-creation
- feat: implement Phase 1 quick wins for improved usability
- chore: bump version to 1.6.0 [skip ci]
- refactor(package): Rename to FQDN format
- chore: bump version to 1.5.0 [skip ci]
- chore: Update project development settings
- chore: bump version to 1.4.0 [skip ci]
- chore: update project to Unity 6 and New Input System
- chore: bump version to 1.3.0 [skip ci]
- docs: update README with dynamic badges and conventional commits
- chore: bump version to 1.2.0 [skip ci]
- fix: resolve FindObjectOfType deprecation warning
- chore: bump version to 1.1.0 [skip ci]
- fix(ci): add write permissions to workflow
- docs: update README and add CLAUDE.md
- ci: add GitHub Action for automatic UPM publishing
- feat: add New Input System support and runtime layer configuration
- Merge remote-tracking branch 'origin/copilot/fix-import-package-errors'
- chore: add missing meta files
- Merge remote-tracking branch 'origin/claude/what-do-you-01DYQNLqJJ8VTkw74CexH3tJ'
- Improve README with comprehensive documentation
- Merge branch 'feature/code_overhaul'
- Improve code quality across the codebase
- Refactor: Extract TransformGroup and consolidate collider controllers
- Merge branch 'hotfix/2-error-when-creating-handle-in-transfor'
- Create a ghost object if prefab is empty
- Delete unused Axis script, don't update ghost transform on interaction end, public properties for groups
- Small change
- Every handle has its own interaction events
- Fix the RemoveHandle function called twice bug
- deactivate position plane when it cannot be seen
- Deactivate position handle when it cannot be seen
- New test scene, scene changer, handle OnDestroy, ActiveSceneChanged, singleton update
- Prevent possible errors when camera got destroyed
- Update README.md
- Update README.md
- Delete .idea/.idea.UnityRuntimeTransformHandles/.idea directory
- Delete .github/workflows directory
- Update main.yml
- Delete license.yml
- Update main.yml
- Update main.yml
- Update license.yml
- Create license.yml
- Update main.yml
- Create .github/workflows/main.yml
- Delete unity-package.yml
- Update unity-package.yml
- Create unity-package.yml
- Make it a Unity package
- Add shortcut texts to the scene
- Make materials more transparent, origin indicator update, handle prefab update, etc.
- Cache HSV values and change the script name
- Update README.md typo
- Update README.md
- default pixel size change
- Fixes and project settings
- Auto scale extension
- Remove accidentally pushed build
- Small change
- AutoScale fix
- Camera zoom, and other small changes
- Code refactor and folders
- URP, torus collider mesh change, scene update, etc
- Some improvements, folder change, etc
- Small fix
- Handle shader and bug fix
- Camera movement, events, bug fixes
- A lot of fixes
- Handle prefab
- Remove com.shtif.runtimetransformhandle
- New scripts
- Peter @sHTiF Stefcek's Runtime Transform Handles added as a package
- Unity project init
- Initial commit

## [Unreleased]

This release contains **source-breaking** changes and is intended to ship as **2.0.0**.
Migration steps: see `Documentation~/migration-1.x-to-2.0.md`.

### Breaking
- **Namespaces:** `Singleton<T>`, `ApplicationQuitManager`, and `PreserveScaleOnScreenExtension`
  moved out of the global namespace into `TransformHandles.Utils`. `ApplicationQuitManager` and
  `PreserveScaleOnScreenExtension` are now `internal`.
- **Read-only fields:** `Handle.target` and `Handle.handleCamera` are now read-only properties
  (set by the package when the handle is enabled); external code can no longer assign them.
- **Reduced public surface:** `Ghost` interaction callbacks
  (`OnInteractionStart`/`OnInteraction`/`UpdateGhostTransform`/`ResetGhostTransform`/`Terminate`)
  and `TransformGroup.UpdatePositions`/`UpdateRotations`/`UpdateScales`/`UpdateBounds` plus the
  `RenderersMap`/`BoundsMap`/`Transforms` collections are now `internal`.
- **Contract exceptions:** public manager methods now throw `ArgumentNullException` (and
  `ArgumentException`/`InvalidOperationException`) on contract violations instead of logging and
  returning; benign state conditions still log a warning.

### Deprecated
- `Handle.ChangeHandleType`, `ChangeHandleSpace`, and `ChangeAxes` are `[Obsolete]` forwarders;
  assign the `type`, `space`, and `axes` properties directly (their setters rebuild the child
  handles / apply the Scale→Self clamp).

### Added
- URP (Universal Render Pipeline) SubShader for `HandleShader` and `Origin` shaders so
  handles render correctly under URP instead of falling back to the magenta error shader.
- `com.unity.render-pipelines.universal` as a dependency so the URP SubShader's `Core.hlsl`
  include always resolves (a Built-in-only project without URP would otherwise hit a
  missing-include shader error). Built-in rendering is unaffected.
- `versionDefines` entry mapping `com.unity.inputsystem` to the package-private `TH_INPUTSYSTEM`
  symbol, making the New Input System an optional dependency.
- In-package `README.md`, `LICENSE.md`, `CHANGELOG.md`, and `Third Party Notices.md`.
- A registered UPM Sample (`Samples~/Demo`) with the full-featured runtime demo scene.
- EditMode + PlayMode test suites under `Tests/`, and a `SnapUtils.Snap` helper.

### Changed
- Declared minimum Unity version raised to **2021.3** to match the C# 9 language features and
  APIs the package actually uses (the previous `2019.4` floor could not compile).
- Input System guards in `InputWrapper` now require both `ENABLE_INPUT_SYSTEM` (Player Settings)
  and `TH_INPUTSYSTEM` (package present), so a project without the Input System package compiles
  cleanly on the legacy backend with no dropped-reference warnings.

### Fixed
- `TransformHandleManager` no longer destroys itself when no main camera is present for a frame
  (e.g. during scene loads or camera swaps). It logs a single warning, pauses raycasting, and
  recovers automatically once a camera is available.
- `DestroyAllHandles` no longer throws `InvalidOperationException` from mutating the handle map
  while iterating it; it now snapshots the keys and clears all internal maps.
- Aborted handle creation (`CreateHandle` / `CreateHandleFromList`) now fully tears down the
  partially-built handle and its ghost instead of leaking orphaned map entries.

## [1.20.0]

- Baseline release. See the
  [GitHub releases](https://github.com/manaporkun/UnityRuntimeTransformHandles/releases) for the
  history prior to this changelog.

[Unreleased]: https://github.com/manaporkun/UnityRuntimeTransformHandles/compare/v1.20.0...HEAD
[1.20.0]: https://github.com/manaporkun/UnityRuntimeTransformHandles/releases/tag/v1.20.0
