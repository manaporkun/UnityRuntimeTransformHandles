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

Freshness compares artifact timestamps with the newly created run-ID file's filesystem
timestamp, keeping Linux timestamp precision consistent. The run ID must still match;
artifacts older than that preparation marker fail collection.

Run validator unit tests with `python3 -m unittest discover -s ci -p 'test_*.py'`.
Expected lists are reviewed source-controlled baselines, not generated from the run being accepted.
Update them deliberately when adding/removing/renaming package tests. Core's expected exception
messages (including duplicates), session cases and four ordered phase names live in
`required-unity-evidence.json`. Its `require_quit_on_exit` flag requires a naturally true quit flag
for no-domain-reload cases; false retains the existing full-reload behavior without adding an exit
quit assertion. The current verifier enforces three complete cycles per case and rejects empty,
duplicate or malformed expectations. Update this data deliberately when those observations change.
XML `engine-version` identifies NUnit,
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

The candidate includes `required/` with the four exact test-name files and
`required-unity-evidence.json`, copied byte-for-byte from `package_source` and hashed in
`candidate.json`. That source is HEAD for normal preparation and the interrupted bump commit for
recovery. Both publish-workflow verifiers use `--required-dir artifacts/candidate/required`, so
later test additions, removals, renames or changed exception/session expectations cannot invalidate
an older candidate's inventory. Current freshness, hash, no-skip and observed-settings checks still
apply. Candidate validation rejects missing, changed or extra inventory files. Keep this directory
with the candidate archives when validating a local candidate and pass its path with `--required-dir`
to both `verify-unity-results.py verify` and `verify-sample-results.py verify`. Their default remains
the current `ci/` directory for ordinary local and compatibility-floor runs. Historical releases
missing these files stop; see the release preparation document for independently verified manual
completion. They never fall back to current expectations.

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

## Clean consumer journeys

`publish-upm.yml` runs eight fresh consumer jobs after the existing Core/Demos suites.
Tarball jobs run the full lifecycle in 2021.3.0f1 Legacy/Built-in and 6000.3.16f1
Legacy/Built-in, Input System/Built-in and Input System/URP 17.3.0. Git runs a full
journey in Unity 6 Input System/Built-in and installation/build/player probes in the
other three profiles. Every full lifecycle stage therefore runs in each profile;
this does not claim a full lifecycle in every transport/profile combination.

The fixture and exact standalone test inventories live in `ci/consumer/` and are
frozen from `package_source` into each candidate. The older Core-only v3.3.1 baseline
is frozen separately, with its ref, full commit, Git tree and archive/file hashes.
Never replace missing historical fixture/baseline bytes with current files. To
change the baseline or inventories, review and commit the contract/data deliberately.
Browser-only discovery is exactly three applicable cases; gallery-dependent cases
belong to the co-import run. Unexpected skips always fail.

`editor-evidence.json` freezes the four ordered batch-mode dialog cancellation
messages from successful public `SetupLayer` calls. Their full text and public API
stack are required in setup and independent-import phases; layer allocation,
idempotence and preserved user layers are checked separately. Unknown assertions,
errors, exceptions, extra or missing dialogs fail. Player errors remain disallowed.

For a local candidate, begin with a fresh output and work directory:

```sh
python3 ci/release-candidate.py prepare --root . --out artifacts/candidate
python3 ci/consumer-project.py prepare --candidate artifacts/candidate \
  --work artifacts/consumer-work --profile 2021-legacy --transport tarball
python3 ci/consumer-project.py before --work artifacts/consumer-work --stage core-resolve
```

`before` selects the reviewed project, Editor method or test filter and records the
pending phase. The composite action in `.github/actions/consumer-editor/action.yml`
runs that invocation with the pinned GameCI CLI/image, then calls `after`. `after`
verifies actual UPM/cache bytes, test discovery and real player evidence before
allowing the next phase. `required_stages()` in `consumer-project.py` owns order and
scope; a report cannot choose a smaller journey. Use the workflow's sequence rather
than skipping to a build. No Library or lock is restored into a fresh consumer.

The pinned image's license setup needs the default container user. The composite
transfers only the generated workspace under `artifacts/` to that user before each
invocation and returns ownership to the runner afterward, including failed phases.
This also keeps temporary Git repository ownership consistent with UPM. The guarded
helper never follows symlinks or changes ownership of the checkout or outside paths.
The 2021.3.0f1 profile uses the same checksum-pinned CLI directly so it can explicitly
select assigned named-user activation with `-serial` and no key, using the existing
account credentials. The older automatic Personal route reaches a legacy endpoint
that timed out in CI; a Mac-bound ULF also fails on the runner. Activation, the phase
and license return remain in one CLI/container lifecycle. Other profiles retain the
pinned action wrappers. The minimum Editor and all acceptance gates stay the same.
This minimum-version activation still requires a real successful seat grant and
return; a selected command or activation banner alone is insufficient evidence.
The `Minimum consumer activation and return` prerequisite runs one fresh Core
resolution before the eight matrix rows. It permits one activation attempt and
requires the real bootstrap, UPM observation and explicit successful license return.
Missing or failed return confirmation stops the matrix. Each minimum-Editor phase
also records a sanitized `license.json` receipt bound to its run and stage; raw
authentication output is not uploaded. This prerequisite adds one Editor phase
outside the full/probe journey counts. Test phases also reject late Editor errors,
exceptions and assertions, including failures after XML results have been written.
The pinned builder also requires its standard completion marker for custom Editor
methods. The bootstrap emits it after persisting the phase observation. It is an
adapter acknowledgement, not native build evidence: the host verifier still requires
the raw reports, and native stages require a successful BuildReport, executable,
unique build identity and driven player results. Native build counts come from those
records. Process failures and managed exceptions still fail the phase.

The local `run-editor` command accepts `--unity`, `--project`, `--method` and
`--timeout` for individual Editor smoke checks. `prepare --build-target StandaloneOSX`
can produce a local macOS counterpart. Full acceptance uses native Linux Mono
players on Ubuntu with Xvfb, xdotool and Mesa; macOS compilation or a player process
exit is not Linux interaction evidence.

After all phases, run the current aggregate verifier:

```sh
python3 ci/verify-consumer-results.py --work artifacts/consumer-work \
  --candidate artifacts/candidate --profile 2021-legacy --transport tarball
```

Each phase's evidence directory contains `record.json`, `editor.log` and
`resolution.json` with the raw observed package/assembly list and manifest/lock
snapshots. Non-test phases add `editor.json`; tests add their mode's XML/log. Import
phases retain the public Sample API inventory. Builds add `build.json` and a `player/`
directory containing native startup log, `input.json`, `ready.json`/PNG and every
stage/nonce JSON/PNG pair. The first Core build also retains `negative/` from the
same binary with interaction input withheld. A fresh build identity embedded in a
Resource and the complete player-file digest prevent evidence reuse across builds.
The exact output patterns are frozen in `consumer/contract.json`.

Acceptance checks actual focused-window input, rendered targets and handles, hover
pixels, numeric snapped transforms, public events and cleanup, effective saved
settings/prefab/material values, sample UI clicks and gallery transitions.
Browser-only builds must render the empty-gallery text inside its observed label bounds;
a label string paired with a blank frame is rejected. Existing
customized imports must survive another public-import attempt; a deliberate duplicate
GUID probe must be rejected. PackageCache bytes stay unchanged. Demos removal retains
explicit UI dependencies and imported Assets. Later removals happen only in controlled
copies with named dependency-bearing assets backed up outside Assets. Unrelated user
content and GUIDs must survive. Final uninstall checks raw manifest, lock, package
list and loaded assemblies after reopening.

The workflow permits two consumer jobs at once and gives each job 180 minutes, with
20-minute phase deadlines and shorter player/checkpoint deadlines. The current full
sequence uses 37 Editor phases and 13 native builds; a probe uses four and two. Narrow
UTF tests and distinct retained Core/gallery players account for these counts. Evidence
and external backups are always uploaded, including partial failures. After each
independent sample its Library/build output is removed only after hashed evidence is
exported. `summary.json` records observed Editor process/build/player counts and elapsed
time. Artifact upload alone never means acceptance; all eight jobs must succeed before
the publisher can run. Keep the PR draft until final-revision CI and independent review
are complete.

Run `python3 -m unittest discover -s ci -p 'test_*.py'` for stdlib regression tests.
These test evidence rejection and orchestration policy; they do not replace licensed
Editor/player results. The generic showcase now omits the optional URP camera
component, preserving its base Camera and GUIDs. Both Built-in and active URP must
render its actual target/handle pixels with zero missing scripts.
