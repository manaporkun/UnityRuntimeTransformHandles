# Unity test evidence

The Unity 6 test job runs input handlers 0 and 1 separately. The floor job uses 2021.3.45f1,
Test Framework 1.1.33, legacy input and no URP. Jobs pass
`-transformHandlesIsolatedTests` to permit the session fixture's clean untitled scene in an isolated
batch run. Dirty scenes are still rejected. Do not use this flag in a project with unsaved work.

`verify-unity-results.py` has three phases:

1. `prepare --project PATH --package PACKAGE --out OUTPUT --unity VERSION --input 0`
   records original settings, configures a stable full-reload baseline (enabled, flags None),
   then captures schema-v1 metadata and clears only that project's `Library/P0E-Evidence`. Run after
   restoring caches and selecting the input setting. Output must be fresh. For frozen packages
   outside a checkout, supply `--repository CHECKOUT` to identify the source commit too.
2. Run both test platforms with the isolated flag. Keep each platform's XML and log.
3. `collect --out OUTPUT --play-xml FILE --edit-xml FILE --play-log FILE --edit-log FILE`, then
   `verify --out OUTPUT`. Collect copies fresh session records and hashes all evidence. Verify
   requires the checked-in exact test lists, passing cases, expected logs, actual Unity version,
   input readback, unchanged package and restored reload settings. It also requires all eight
   session cases and their 24 complete cycles. Missing or stale evidence fails.

Run validator unit tests with `python3 -m unittest discover -s ci -p 'test_*.py'`.
Expected lists are reviewed source-controlled baselines, not generated from the run being accepted.
Update them deliberately when adding/removing package tests. XML `engine-version` identifies NUnit,
not Unity; Unity's startup log supplies the observed Editor version.

Evidence includes expected and observed metadata, XML/logs, transitions.tsv, settings.tsv and run ID.
Always-uploaded artifacts may contain partial diagnostics from failed jobs; upload success is not
acceptance. A skipped licensed job is not test evidence. The publish workflow retains its requirement
that the entire Unity 6 matrix succeeds; the floor workflow is separate, not a new publish dependency.
Local CLI success does not imply the exact code has passed remote CI.

## Local compatibility results

The versioned Core 4.0.0 / Demos 1.0.0 candidate passes on macOS ARM64 with Unity
2021.3.0f1 and 2021.3.45f1 Legacy, and Unity 6000.3.16f1 Legacy/Input System. Each configuration
passes Core 199 PlayMode / 74 EditMode tests and all 24 Editor session cycles. Combined samples
pass 116 Legacy / 119 Input System cases, with zero skips. Test Framework versions are 1.1.33
on Unity 2021 and 1.6.0 on Unity 6; uGUI is 1.0.0 / 2.0.0 respectively, and Input System 1.19.0
is installed only for its configuration. Additional minimum-Core projects without uGUI/Demos/Input
System/URP pass the same Core suites and session cycles on 2021.3.0f1 and Unity 6.

These are actual tarball consumers with resolved-package content verification, plus independent
sample imports, co-import, removal and local Git upgrade checks. The candidate source/archive hashes
and exact scope are recorded in `docs/DEVELOPMENT-HANDOFF.md`. A fresh floor consumer has no initial
ProjectSettings/EditorSettings assets; its actual generated input and fixture settings restoration
are checked, without claiming a raw before/after comparison where no before asset existed.

The prepare step changes only the disposable test project's reload baseline. Fixture restoration
is measured against that prepared baseline. Local callers that need to retain their initial
project settings must restore their own byte backups after collection; CI checkouts are disposable.
Remote CI success for the final release revision has not been established.

## Local WebGL check

Use an isolated full project copy with Web Build Support installed:

```sh
Unity -batchmode -buildTarget WebGL -projectPath PROJECT \
  -executeMethod BuildScript.BuildWebGLForPages -buildPath OUTPUT -quit -logFile LOG
```

Select WebGL when starting Unity. Setting only BuildPlayerOptions.target inside an already running
batch script can leave platform-specific Editor assemblies loaded for another target. Build success
is not browser success: serve the resulting output locally and verify startup, selection and all
three transform modes. Use a free local port and confirm the server started before navigating.

The combined gallery and standalone Lamp Assembly candidate builds passed local Chromium
mouse interaction and Retina layout checks in Built-in/Legacy and URP 17.3.0/Input System hosts. Showcase targets now use their own pipeline-independent
shader. Three inherited hidden URP shader diagnostics remain at log level. The owner confirmed physical Mac trackpad
validation in the local player. Resolved package files match the versioned candidate hashes in both hosts.


## Optional demo checks

The stdlib boundary/release fixtures require Python 3.9 or later. Run
`python3 ci/verify-package-boundaries.py` in addition to the unit tests. It checks package direction,
version-only dependencies, isolated sample GUID references and absence of imported demo assets in Core.

Core evidence intentionally retains its exact suite lists. If a local consumer also imports sample
tests, add `-testFilter TransformHandles.Tests.` to the Core test commands, then run sample tests
separately with `-testPlatform playmode -testFilter TransformHandles.Samples.LevelEditor.Tests`.
Set up the TransformHandle layer before running the sample suite. Missing setup fails with an explicit
diagnostic; the tests do not silently modify the consumer's project layers.

`BuildScript.BuildLevelEditorWebGL` selects only the Level Editor sample. The existing
`BuildScript.BuildWebGLForPages` entry point still selects the original showcase. Neither overwrites
customized imports. The new scene uses its own opaque shader and current UI mode labels; relocating
the original showcase does not fix its pre-existing visual issues. Inspect all browser console text,
including log-level `ERROR: Shader` messages, before claiming a clean WebGL result.

Run the independently imported Modular Building suite with `-testPlatform playmode -testFilter TransformHandles.Samples.ModularBuilding.Tests`. Its WebGL entry point is `BuildScript.BuildModularBuildingWebGL`. Verify single-sample import and co-import with Level Editor; run the scenes separately. `node ci/test-level-editor-input.cjs` checks both sample-local wheel bridges and their co-existence.

On a clean CLI project run `BuildScript.PrepareModularBuildingWebGL` in a separate Unity invocation before `BuildScript.BuildModularBuildingWebGL`. A new sample assembly must compile and load into the Editor domain before player serialization. All build entry points reject an unloaded sample assembly rather than producing a player with missing scripts. Existing compiled imports can build directly.

Pages CI copies the original showcase sample to its manifest-defined Assets import path before launching Unity. This loads its assembly before the first build and preserves the original public demo selection; an unexpected existing destination fails instead of being overwritten.

## Versioned release candidates

See [release preparation](../docs/RELEASE-PREPARATION.md) for the Core 4.0.0 / Demos 1.0.0
pair, candidate commands, clean consumer matrix and publication order. The publish workflow now
prepares both `.tgz` packages before validation. Every licensed Core/Demos job applies those same
artifacts and verifies that package bytes remain unchanged. The separate compatibility-floor
workflow remains an additional required review check; it is not a substitute for local minimum-version
or versioned-artifact consumer tests.

Demos CI imports all five samples, runs only `TransformHandles.Samples.`, and checks the exact
`required-samples-input0.txt` or `required-samples-input1.txt` list with zero skips. These baselines
come from the existing reviewed 116/119-test development suites, not from the result being accepted.
`verify-sample-results.py` records import/candidate hashes before the run and verifies fresh XML,
actual Editor startup version, input setting and unchanged imported/package files afterward. Only
Unity-generated metadata for the five top-level import folders may be added; every distributed file
must remain byte-identical, and other additions fail.

GameCI test runners and the Pages builder pin CLI `v0.1.72` instead of resolving `latest` on each
runner. The upstream downloader's latest-release lookup can hit GitHub's unauthenticated shared-IP
rate limit before Unity starts. Both Core/Demos jobs use the same pinned action revision and CLI;
Unity editor versions remain explicit in test jobs and match each project's ProjectVersion.txt;
Pages uses auto detection. Test startup logs independently verify the observed editor version. CLI pinning does not relax any suite, artifact or publication gate.
