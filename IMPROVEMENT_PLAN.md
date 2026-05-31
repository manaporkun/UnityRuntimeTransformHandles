# Industry-Standard Improvement Plan

Checklist to bring **com.orkunmanap.runtime-transform-handles** up to industry-standard UPM quality.
Derived from a 6-dimension audit (manifest, asmdef/deps, package files, testing/CI, API/namespacing, versioning/compat) with adversarial verification of every finding.

**Legend:** `P0` blocks/violates spec · `P1` significant gap · `P2` polish · 🔴 **source-breaking (needs 2.0.0)**

Package root referenced below as `PKG = Packages/com.orkunmanap.runtime-transform-handles/`.

---

## P0 — Must fix first

### [x] P0-1 · Declare or conditionalize the Input System dependency
**Problem:** Runtime asmdef hard-references `Unity.InputSystem` (GUID `75469ad4d38634e559750d17036d5f7c`) while `package.json` `dependencies` is `{}` and `versionDefines` is `[]`. Works today only because this project pins `com.unity.inputsystem`.
**Impact (verified):** On an Input-System-less install Unity drops the unresolved reference + logs a warning; the `#else` legacy branch still compiles. Not a hard compile-fail, but a UPM-spec violation + warning noise on every clean install.

Pick **one**:
- [ ] **(A) Mandatory** — add to `package.json`: `"dependencies": { "com.unity.inputsystem": "1.7.0" }`. Keep the GUID ref. Simplest.
- [x] **(B) Optional (recommended)** — add a `versionDefines` entry on the runtime asmdef mapping `com.unity.inputsystem` → a **package-private** symbol `TH_INPUTSYSTEM` (NOT `ENABLE_INPUT_SYSTEM` — that's a built-in driven by Player Settings). Update `Runtime/Scripts/Utils/InputWrapper.cs` guards to `#if ENABLE_INPUT_SYSTEM && TH_INPUTSYSTEM`. **DONE:** versionDefines entry added (`com.unity.inputsystem` ≥1.0.0 → `TH_INPUTSYSTEM`); all 9 guards in `InputWrapper.cs` updated.

**Files:** `PKG/Runtime/com.orkunmanap.runtime-transform-handles.asmdef`, `PKG/package.json`, `PKG/Runtime/Scripts/Utils/InputWrapper.cs`
**Breaking:** No.
**Done when:** Fresh project WITHOUT Input System imports the package with no warnings/errors and legacy input works.

### [x] P0-2 · Fix the false minimum Unity version
**Problem:** `"unity": "2019.4"` cannot compile. Two independent blockers:
- `PreserveScaleOnScreenExtension.cs:13` uses `GeometryUtility.CalculateFrustumPlanes(camera, Plane[])` — non-alloc overload **added 2020.1**.
- Core uses **C# 9** `or` patterns / switch expressions (`HandleAxesExtensions`, `HandleBase`) — needs **2021.2+**.

- [x] Set `"unity": "2021.3"` in `package.json` (2022.3 also fine).
- [x] Optionally add `"unityRelease": "0f1"`.
- [ ] Do not re-advertise a lower floor without a CI matrix proving it.

**Files:** `PKG/package.json`
**Breaking:** No (metadata; tightens the contract).
**Done when:** Declared floor matches a version the package actually compiles on.

---

## P1 — Significant adoption / quality gaps

### [x] P1-1 · URP/HDRP shader support (handles render magenta under SRP)
**Problem:** `HandleShader.shader` + `Origin.shader` are Built-in-only (`CGPROGRAM`, `UnityCG.cginc`, no `RenderPipeline` tag). Demo runs URP 17.3 → gizmos go magenta with no install-time warning.

- [x] Add a URP SubShader (`Tags{ "RenderPipeline"="UniversalPipeline" }`, HLSL, `Core.hlsl`) to `HandleShader.shader` so the correct SubShader auto-selects.
- [x] Same for `Origin.shader`.
- [ ] HDRP: ship an Unlit Shader Graph variant OR document as unsupported. **→ documenting as unsupported in PKG/README (PR5).**
- [ ] Verify affected materials: `Red/Green/Blue/Cube.mat` (→ HandleShader), `Orange.mat` (→ Origin). `Object.mat`/`Plane.mat` use built-in Standard — cover if shipped as samples. **→ needs Unity render verify.**

**Files:** `PKG/Runtime/Shader/*.shader`, `PKG/Runtime/Materials/*.mat`
**Breaking:** No.
**Done when:** Handles render correctly in a clean URP project.

### [x] P1-2 · Reliability: manager self-destructs + map corruption
**Problem (verified):** `TransformHandleManager.cs:426` calls `Destroy(gameObject)` from `GetHandle`, which runs every frame via `Update`, whenever `mainCamera` and `Camera.main` are both null — a `DontDestroyOnLoad` singleton kills itself on a recoverable transient (scene load, camera swap).

- [x] Replace `Destroy(gameObject)` with: log a warning (throttled), guard, early-return so the manager survives. Public settable `Camera` (`mainCamera`) already exists.
- [x] `DestroyAllHandles` (≈309) iterates `_handleGroupMap.Keys` while `DestroyImmediate`→`RemoveHandle` mutates the dict mid-loop → throws `InvalidOperationException`. Snapshot into `List<Handle>`, then `Clear()` the three maps + set `_handleActive = false`.
- [x] `CreateHandleFromList` (+ `CreateHandle`) bail path calls full teardown (`RemoveHandle` → maps + ghost cleanup, then `DestroyHandle`); `RemoveHandle` not-found is now an idempotent silent no-op.
- [x] Error policy applied in the 2.0.0 batch (PR8): `CreateHandle`/`CreateHandleFromList`/`AddTarget`/`RemoveTarget`/`RemoveHandle`/`ChangeHandleType`/`ChangeHandleSpace`/`ChangeHandlePivot` now `throw ArgumentNullException` on null args (and `ArgumentException`/`InvalidOperationException` on empty-list / unmanaged-handle); benign state conditions ("already has a handle", not-found) stay `LogWarning`/silent; logs prefixed `"TransformHandles:"`.

**Files:** `PKG/Runtime/Scripts/TransformHandleManager.cs`
**Breaking:** No (the throw-on-contract part is behavioral — note in CHANGELOG / bundle with 2.0.0).
**Done when:** Manager survives a null-camera frame; `DestroyAllHandles` cleans up without throwing.

### [x] 🔴 P1-3 · Namespace + scope the global-namespace types
**Problem:** `Singleton<T>`, `ApplicationQuitManager` (both in `Singleton.cs`), and `PreserveScaleOnScreenExtension` live in the **global namespace**. With `autoReferenced: true`, any consumer with their own global `Singleton<T>` hits `CS0436`/`CS0104` with no opt-out. `rootNamespace` does NOT fix this — it only seeds new-file templates.

- [x] Wrap `Runtime/Scripts/Utils/Singleton.cs` (both types) in `namespace TransformHandles.Utils { ... }`.
- [x] Wrap `Runtime/Scripts/Utils/PreserveScaleOnScreenExtension.cs` in `namespace TransformHandles.Utils { ... }`.
- [~] Downgrade to `internal`: `ApplicationQuitManager` ✅, `PreserveScaleOnScreenExtension` ✅. **`Singleton<T>` MUST stay `public`** — the public `TransformHandleManager` derives from it and a public type cannot inherit an internal base (CS0060). Namespacing alone resolves the CS0436/CS0104 collision the item targets.
- [x] Add `using TransformHandles.Utils;` to `Handle.cs` (calls `PreserveScaleOnScreen`).

**Files:** `PKG/Runtime/Scripts/Utils/Singleton.cs`, `PKG/Runtime/Scripts/Utils/PreserveScaleOnScreenExtension.cs`, `PKG/Runtime/Scripts/Handle.cs`
**Breaking:** 🔴 YES — public-type move = semver major.
**Done when:** No package types in the global namespace. **Verified via Unity MCP: `Singleton` resolves under `TransformHandles.Utils`, no longer in the global namespace.**

### [x] P1-4 · Ship the demo as a registered Sample
**Problem:** Demo lives in `Assets/Scripts` (outside the package); no `Samples~/` + `samples[]`. UPM consumers get no example.

- [x] Created `PKG/Samples~/Demo/`; moved the full-example demo scene (`New Scene.unity` → `RuntimeTransformHandlesDemo.unity`) + `HandleDemo.cs` (which contains `DemoTarget` and spawns its own targets — self-contained).
- [x] Added sample asmdef `TransformHandles.Samples.Demo` referencing the runtime asmdef by name `com.orkunmanap.runtime-transform-handles` (`autoReferenced: false`).
- [x] Registered `samples[]` in `package.json`.
- [~] Added `using TransformHandles.Utils;` to `HandleDemo` (binds the package `InputWrapper`). **Did NOT delete `Assets/Scripts/InputWrapper.cs` nor move `ObjSelector.cs`/`CameraMovement.cs`** — the **WebGL build scene `SampleScene.unity` (the only build-enabled scene) depends on all three**; removing them would break the WebGL deploy. They are a separate legacy demo from the `demo/full-example` HUD shipped here.

**Files:** `PKG/Samples~/Demo/*`, `PKG/package.json`
**Breaking:** No.
**Done when:** "Import Sample" in Package Manager drops a working demo scene. **Verified via Unity MCP: staged in a non-tilde `Samples/` folder, the `TransformHandles.Samples.Demo` assembly compiled and `HandleDemo` resolved against the package `InputWrapper`; then renamed to `Samples~` (confirmed hidden from the AssetDatabase).**

> **Note:** `RuntimeTransformHandlesDemo.unity` carries one pre-existing stray missing-script component (GUID `a79441f3…`, an orphaned `DemoTarget` placement). Harmless — `HandleDemo` spawns its own `DemoTarget` targets at runtime — but a future cleanup pass could strip that dead component.

### [x] P1-5 · Add automated tests
**Problem:** Zero tests anywhere.

- [x] `PKG/Tests/Editor/TransformHandles.Tests.Editor.asmdef` (`includePlatforms: ["Editor"]`).
- [x] `PKG/Tests/Runtime/TransformHandles.Tests.Runtime.asmdef`.
- [x] Both reference the runtime asmdef GUID + TestRunner + `nunit.framework.dll`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.
- [x] EditMode targets: `HandleAxesExtensions.HasAxis/HasBothAxes/IsMultiAxis` (all 7 axes), `MathUtils.ClosestPointOnRay` (incl. parallel + anti-parallel degenerate), `MeshUtils.CreateArc`/`RebuildArc` invariants, `SnapUtils.Snap`.
- [x] Extracted duplicated `Mathf.Round(v/snap)*snap` (in `PositionAxis`, `PositionPlane`, `ScaleAxis`, `RotationAxis`) into testable `SnapUtils.Snap(value, increment)`.
- [x] PlayMode: `TransformGroup` average pos + parent/child rejection (and parent-replaces-child), `Singleton<T>` lifecycle + duplicate self-destruct.

**Files:** `PKG/Tests/**`, `PKG/Runtime/Scripts/Utils/SnapUtils.cs` (new)
**Breaking:** No (`SnapUtils` is additive).
**Done when:** EditMode + PlayMode suites green in the Test Runner. **Compilation verified via Unity MCP (all 4 assemblies load, isCompiling=False); every non-trivial test expectation re-verified against the live runtime (intersect=5, parallel=0, arc 10 vtx/24 tris, parent/child swap, avg=(2,0,0)).**

### [x] P1-6 · Gate the release pipeline on build/test
**Problem:** `publish-upm.yml` force-pushes `upm`, tags, and creates a Release with no compile/test step. A broken release hits pinned consumers instantly and can't be cleanly retracted.

- [x] ~~compile gate~~ → went straight to the test gate (P1-5 already landed).
- [x] `game-ci/unity-test-runner@v4` (`unityVersion: 6000.3.16f1`, `testMode: all`, Library cache, license secrets reused from `webgl-pages.yml`).
- [x] `publish` job has `needs: test` + `if: push/dispatch only`; workflow now also runs on `pull_request`.

**Files:** `.github/workflows/publish-upm.yml`
**Breaking:** No (CI only).
**Done when:** Publish cannot run unless build/tests pass.

---

## P2 — Polish / standards / future-proofing

### [~] P2-1 · Ship in-package docs + populate manifest URLs
**Problem:** Repo-root `README.md`/`LICENSE` never ship — CI copies only the package folder to `upm`.

- [x] Add `PKG/LICENSE.md` (copy root MIT text; Unity convention uses `.md`).
- [x] Add `PKG/README.md` (copy/trim root README); optionally `PKG/Documentation~/index.md`. **Both added.**
- [x] Add `PKG/CHANGELOG.md` (keepachangelog format; package is v1.20.0).
- [x] Add `PKG/Third Party Notices.md` (note no bundled third-party code + the optional Input System dep; attribute any borrowed meshes/shaders).
- [x] `package.json`: add `documentationUrl`, `changelogUrl` (only after CHANGELOG exists), `licensesUrl`, `unityRelease`.
- [x] Tighten the ~90-word marketing `description` to 1–2 technical sentences.
- [x] Replace thin keywords with focused terms (`gizmo`, `transform-handles`, `runtime-editor`, `object-manipulation`, `modding`, `input-system`); drop bare `"Unity"`.
- [x] Extend `publish-upm.yml` to prepend the generated changelog into in-package `CHANGELOG.md` on bump. **Done in PR7 (awk-prepend step before the version-bump commit).**

**Breaking:** No.

### [x] P2-2 · Add UPM package validation to CI
- [x] Added a license-free `validate` job to `publish-upm.yml` that lints the manifest (required fields, semver, `name`/`unity` form), required in-package docs, `samples[]` paths, and every asmdef's JSON. `publish` now `needs: [validate, test]`. Runs on every PR. **Verified the rules pass against the current package locally; workflow YAML lint-clean.**
- [ ] (Optional future) swap in Unity's full Package Validation Suite via the editor — heavier (needs license); the structural lint enforces the floor for now.

**Breaking:** No.

### [~] P2-3 · Harden conventional-commit auto-versioning
**Problem (`publish-upm.yml`):** hand-rolled regex can ship breaking changes as patches.

- [x] Read full messages (`git log --format=%B`) so `BREAKING CHANGE:` body footers aren't missed (was `%s`).
- [x] Drop non-standard bare aliases `^break`/`^feature`/`^bugfix` (mis-bump risk).
- [x] Remove the catch-all "any package-dir change → patch" fallback.
- [ ] Best: adopt `release-please` or `semantic-release`. **→ optional future migration; the hardened hand-rolled logic is the interim.**

**Breaking:** No.

### [x] 🔴 P2-4 · Reduce accidental public surface (fold into 2.0.0)
- [x] Marked `internal`: `Ghost` interaction callbacks (`OnInteractionStart/OnInteraction/UpdateGhostTransform/ResetGhostTransform/Terminate`), `TransformGroup.UpdatePositions/UpdateRotations/UpdateScales/UpdateBounds` and the exposed `RenderersMap/BoundsMap/Transforms`.
- [x] Added `[assembly: InternalsVisibleTo]` for `TransformHandles.Editor`, `TransformHandles.Tests.Editor`, `TransformHandles.Tests.Runtime` (`Runtime/Scripts/AssemblyInfo.cs`).
- [x] (`RaycastHitDistanceComparer` already `internal`.)

**Breaking:** 🔴 YES — narrowing visibility.

### [~] 🔴 P2-5 · Mutable fields → properties with controlled setters (fold into 2.0.0)
- [x] `Handle.type/axes` → properties whose setters rebuild children (`Clear()/CreateHandles()`); `space` → property applying the Scale→Self clamp. `ChangeHandleType/ChangeAxes/ChangeHandleSpace` kept as `[Obsolete]` forwarders. **Implementation choice:** kept the lowercase public *names* as properties (backing fields `_type/_axes/_space/...` + `[FormerlySerializedAs]`) so prefab serialization and consumer field-reads survive; the real source-break is the read-only `target`/`handleCamera` + `[Obsolete]` markers.
- [x] Snap members (`snappingType/positionSnap/rotationSnap/scaleSnap`) → properties (backing fields + `FormerlySerializedAs`).
- [x] `target`/`handleCamera` are now read-only (`{ get; private set; }`, set inside `Enable`/`OnEnable`).
- [~] `TransformGroup.IsOriginOnCenter` — left as a public field; `ChangeHandlePivot` already routes through the manager (which re-applies the ghost transform), so a property setter buys nothing here. Low value; skipped.

**Breaking:** 🔴 YES (read-only `target`/`handleCamera`). **Verified via Unity MCP: `Handle.type` is a property with a working setter, `Handle.target` has no public setter, and the prefab's serialized `axes`/`type` survived the field→property migration via `FormerlySerializedAs`.**

### [ ] P2-6 · asmdef confirmations (no action unless noted)
- [ ] Keep `autoReferenced: true` — fine **once** P1-3 namespacing lands (it only amplifies collisions while global types exist).
- [ ] Keep GUID-form references (best practice); do NOT regress to name-based when doing P0-1.
- [ ] `allowUnsafeCode: false`, `includePlatforms` (`[]` runtime / `["Editor"]` editor), editor→runtime GUID ref are all correct — no change.

---

## Suggested execution order (PRs)

- [x] **PR 1** — P0-1 Input System dependency *(unblocks non-IS consumers; ship first)*
- [x] **PR 2** — P0-2 Unity version floor (+ `unityRelease`) *(one-line truth fix)*
- [x] **PR 3** — P1-1 URP shader SubShaders *(restores core function for majority pipeline)*
- [x] **PR 4** — P1-2 camera self-destruct + map bugs *(non-breaking parts now; defer throw-on-contract to 2.0.0)*
- [x] **PR 5** — P2-1 in-package docs/license/changelog + manifest fields + metadata *(big low-risk trust win; unblocks P2-2)*
- [x] **PR 6** — P1-4 Samples~ + P1-5 Tests/ — **both done** (P1-4 ships the HandleDemo full-example; build-scene scripts left in place — see P1-4 note).
- [x] **PR 7** — P1-6 CI test gate + P2-3 versioning hardening (P2-2 validation still optional/future)
- [x] **PR 8** — **2.0.0 MAJOR:** P1-3 namespacing + P2-4 internal scoping + P2-5 fields→properties + throw-on-contract from P1-2. Done as one breaking batch (bump to 2.0.0 happens on the next release commit). **P2-2 UPM validation suite remains the only optional/future item.**

---

## Notes / honesty caveats

- **P0-1 severity:** a missing asmdef reference in modern Unity **warns and drops the reference** (the `#else` legacy branch still compiles) — not a hard compile-fail. Severity is driven by the UPM-spec violation + warning noise, not a broken build. Fix is identical regardless.
- **P1-2** (camera self-destruct) is **pre-existing** — not introduced by the `demo/full-example` branch (that branch only refactored the surrounding try/catch).
- Items marked 🔴 are **source-breaking** — batch them into a single 2.0.0 so consumers absorb the break once.
