# Core and optional Demos release preparation

The owner approved the implementation plan for **Core 4.0.0 / Demos 1.0.0**. Demos 1.0.0 requires
Core 4.0.0, including `RotationAxis.DragAngleLimits`. Published Core 3.3.x is not sufficient.
Moving the original sample out of Core is a distribution breaking change; the release PR must use
`feat!` or another appropriate breaking Conventional Commit title and explain migration.
Implementation approval does not authorize merging, publishing, deploying or Asset Store submission.

## Prepare once, validate the resulting files

The source manifests remain the development pair until release preparation. Preserve the latest
main Core version during integration; do not pre-bump it to 4.0.0 and let automation bump it again.
`ci/release-candidate.py` retains the existing Core-scoped Conventional Commit rules and calculates
the major bump from the fixed source commit. It prepares Core's final manifest/changelog, promotes
Demos' initial preview to 1.0.0, finalizes its changelog and sets its Core dependency to 4.0.0.
It packs both packages as UPM `.tgz` files and records every package file hash, archive hash, source
commit, package source and release notes hash in `candidate.json`.

Use Python 3.9+, Git and a fresh output directory. A local preview performs no GitHub writes:

```sh
python3 ci/release-candidate.py prepare --out /tmp/rth-candidate
python3 ci/release-candidate.py unpack --out /tmp/rth-candidate --root /tmp/rth-unpacked
```

Preparation archives tracked files from HEAD. Commit or create a disposable committed snapshot of
intended changes first; uncommitted package changes do not belong to that candidate. Local previews
are marked non-publishable. CI on main additionally queries GitHub release state with read-only
credentials to detect interrupted releases. API errors fail rather than masquerading as missing releases.

CI uploads the candidate once. Validation, Core tests and Demos tests download and apply those same
artifacts. `apply` requires the checkout's HEAD to match the recorded source; `verify` rejects missing,
changed or extra package files. Tests never recalculate versions or fetch a newer source revision.
For an interrupted publication, the candidate contains the previous bump commit's package files and
notes, even if newer commits exist; those recovered artifacts must pass the current gates again.

Future stable Demos dependency changes require an explicit new Demos version in the source manifest
and matching changelog. The script rejects silently changing an existing stable Demos version when
Core advances. Demos-only changes do not manufacture a Core release: CI still validates them and
their source distribution, but the Core publisher reports that no Core release was triggered. A
Demos-only release must explicitly update its own version/changelog and use its validated source SHA.

## Clean consumer acceptance

Install the candidate Core `.tgz` in a new disposable Unity project through Package Manager's
**Add package from tarball**. The project must not reference this repository's development folders.
Verify resolution, compilation and basic handle creation before installing Demos. Record the resolved
package versions and compare PackageCache bytes with the candidate's file hashes. Unity 6 UPM
may add `_fingerprint` to its cached package.json; compare every other manifest field exactly and
all remaining files byte-for-byte. Do not treat other cache differences as acceptable.

Then add the candidate Demos `.tgz` explicitly; its Core dependency must resolve to the installed
candidate Core, without relying on a registry containing an older Core package. Import each sample
independently through Package Manager and run its tests. Co-import all five and test the gallery,
workspace transitions and standalone behavior. Test removing Demos both with imported Assets retained
(and explicit UI dependencies retained) and with imports removed. Core must remain usable.

For upgrade validation, start with the old showcase import from Core 3.3.1. Back it up outside Assets,
remove its old import, upgrade Core, install Demos and import the moved showcase. Check GUID preservation
and absence of duplicate GUIDs/assemblies. Repeat cleanup for the old Product Configurator import before
Lamp Assembly; the internal ProductConfigurator identities are intentionally retained.

| Editor | Input | Required evidence |
| --- | --- | --- |
| 2021.3.0f1 | Legacy; no Input System or URP | Core 199 PlayMode / 74 EditMode, 24 session cycles; samples 116 |
| 2021.3.45f1 | Legacy; no Input System or URP | Same suites; also the configured remote compatibility floor |
| 6000.3.16f1 | Legacy | Same Core suites/session cycles; samples 116 |
| 6000.3.16f1 | Input System | Same Core suites/session cycles; samples 119 |

Follow `ci/README.md` for prepare/run/collect/verify; a Unity process exit alone is insufficient.
Independent sample tests and WebGL mouse/Retina checks must use the candidate artifacts. Each future
release candidate also requires an owner-performed physical Mac trackpad smoke test. The owner
confirmed that check for the current candidate in the local player. Earlier development results
do not satisfy these versioned-artifact gates.

## Publish only what passed

Publication requires successful package validation and both Core and Demos Unity matrices. Skipped
licensed jobs cannot publish. Review the separate compatibility-floor result before owner approval.
The publish job verifies candidate hashes and requires remote main still to equal the candidate source.
It commits only the already-tested package files and performs a normal push. A concurrent main change
rejects the push; the workflow must validate a new candidate instead of rebasing or recomputing versions.

1. Publish the verified Core tree to the existing `upm` branch and `v4.0.0` tag. Compare its Git tree
   with the tested source's Core subtree; an existing tag must match rather than being silently reused.
2. Only after Core is available, advertise Demos 1.0.0 using the final monorepo release-source SHA
   recorded in the release notes. Both project manifest Git dependencies use that same SHA with their
   respective `?path=/Packages/...` paths. A Core-only tag cannot resolve the Demos path.
3. Check both installation URLs from a fresh consumer. No sibling Git dependency is implicitly resolved
   from a package manifest; both project entries remain explicit.

Do not dispatch the workflow, push main, merge or publish as part of local validation. Prepare the
reviewed PR, confirm actual remote checks for its final revision and obtain owner approval for the
next external action. Asset Store packaging, listing, media, license review and submission approval
remain separate work in the development handoff.
