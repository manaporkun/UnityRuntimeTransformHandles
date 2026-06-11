# Contributing

Thanks for contributing to Runtime Transform Handles! This document explains how the repository
is organized, how releases work, and what a pull request needs to pass.

## Repository layout

- **The package** lives at `Packages/com.orkunmanap.runtime-transform-handles/` — runtime code,
  editor code, tests, sample, and docs. This is what consumers install.
- **The repository root** is a Unity development project (currently tracking a recent Unity
  release) that embeds the package for development, the demo scene, and the WebGL showcase.
- **`ci/floor-2021/`** is a minimal Unity 2021.3 project used only by CI to verify the declared
  minimum Unity version, the legacy Input Manager path, and URP-free installs. Don't open it
  with a newer editor.

## Branch model

- **`main`** — trunk. All work lands here through pull requests.
- **`upm`** — generated release branch containing only the package contents. It is force-pushed
  by the publish automation on every release. **Never commit to it directly**; consumers install
  from it (`#upm` / version tags).
- `develop` is historical and unused.

## Conventional Commits are required

Release automation derives everything from commit messages on `main`
(`.github/workflows/publish-upm.yml`):

| Commit | Effect on next release |
|---|---|
| `feat: ...` | minor version bump |
| `fix: ...`, `perf: ...`, `refactor: ...` | patch bump |
| `feat!: ...` or a `BREAKING CHANGE:` footer | major bump |
| `docs:`, `chore:`, `ci:`, `test:`, `build:`, `style:` | patch bump only if shipping code (`.cs` outside `Tests/`/`Samples~/`, or `package.json`) changed; otherwise no release |

Since PRs are squash-merged, **the PR title must be a Conventional Commit** — it becomes the
commit on `main`.

## Changelog

Add consumer-facing changes to the `[Unreleased]` section of
`Packages/com.orkunmanap.runtime-transform-handles/CHANGELOG.md` (Keep a Changelog format:
`### Added/Changed/Deprecated/Removed/Fixed/Security`). On release, the automation moves that
content into the new version's section and uses it as the GitHub release notes. If
`[Unreleased]` is empty, grouped commit subjects are used as a fallback — curated entries are
always better.

## How releases happen

On every push to `main`, the publish workflow:

1. Validates the package manifest and structure (no Unity license needed).
2. Runs EditMode + PlayMode tests on the current Unity version, plus the 2021.3 floor job.
3. Computes the version bump from the commits since the last release, updates `package.json`
   and `CHANGELOG.md`, and pushes a `chore: bump version to X.Y.Z [skip ci]` commit.
4. Splits the package directory onto the `upm` branch, tags `vX.Y.Z`, and creates a GitHub
   Release.

There is nothing to do manually — merging to `main` releases.

## Pull request checklist

- PR title is a Conventional Commit (it becomes the squash commit).
- `[Unreleased]` changelog entry added for consumer-facing changes.
- Tests pass locally: Unity Test Runner (`Window > General > Test Runner`), EditMode and
  PlayMode. New behavior comes with tests.
- Code compiles on Unity 2021.3 (the package floor): no APIs newer than 2021.3 unguarded —
  CI's min-api lint and the 2021.3 floor job both enforce this.
- Public API is PascalCase and XML-documented; existing camelCase members are deprecated
  shims — don't add new ones.
- No new hard dependencies in `package.json` without discussion (URP is intentionally
  optional via shader `PackageRequirements`).

## CI jobs at a glance

| Workflow | What it gates |
|---|---|
| `publish-upm.yml` | manifest validation, EditMode+PlayMode tests on current Unity, release automation |
| `compat-floor.yml` | package tests on Unity 2021.3 without Input System or URP |
| `min-api-lint.yml` | static scan for unguarded post-2021.3 APIs, `.meta` BOM check |
| `webgl-pages.yml` | WebGL demo build + GitHub Pages deploy |

## Optional: smarter merges for Unity YAML files

Scene/prefab merge conflicts resolve better with Unity's Smart Merge. Opt in locally:

```ini
# .git/config
[merge "unityyaml"]
    name = Unity SmartMerge
    driver = '<path to UnityYAMLMerge>' merge -p %O %B %A %A
```

and add `*.unity merge=unityyaml`, `*.prefab merge=unityyaml`, `*.asset merge=unityyaml` to
`.git/info/attributes` (kept out of the shared `.gitattributes` so contributors without the
tool keep normal text merges).
