# Compatibility floor project (CI only)

Minimal Unity **2021.3 LTS** project used exclusively by the
`.github/workflows/compat-floor.yml` CI job. It consumes the package from this repository via a
local `file:` dependency and runs the package's EditMode + PlayMode test suites
(`testables` in `Packages/manifest.json`).

It deliberately differs from the development project at the repository root:

- **Unity 2021.3** — verifies the `"unity": "2021.3"` floor declared in `package.json` with a
  real editor instead of a lint heuristic (the dev project tracks current Unity and cannot open
  in 2021.3).
- **No `com.unity.inputsystem`** — `TH_INPUTSYSTEM` stays undefined, so the legacy Input Manager
  `#else` branches of `Runtime/Scripts/Utils/InputWrapper.cs` are compiled (the dev project and
  the Unity 6 test job always compile the New Input System path).
- **No render-pipeline packages** — proves the package installs and compiles without URP
  (the handle shaders fall back to their Built-in SubShader).

Do not open this project with a newer editor — Unity would upgrade `ProjectVersion.txt` and
defeat the purpose of the job.
