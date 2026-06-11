# Third Party Notices

This package contains third-party software components governed by the license(s) indicated below.

## Runtime Transform Handle

This package is derived from [Runtime Transform Handle](https://github.com/pshtif/RuntimeTransformHandle)
by Peter @sHTiF Stefcek. The code has since been substantially modified and extended, but the
package originates from that project and portions of the original implementation remain.

- **Author:** Peter @sHTiF Stefcek
- **Source:** https://github.com/pshtif/RuntimeTransformHandle
- **License:** MIT

As required by the MIT license, the original copyright and permission notice is reproduced in
full below:

```text
MIT License

Copyright (c) 2021 Peter @sHTiF Stefcek

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Optional dependencies (not redistributed)

- **com.unity.render-pipelines.universal** (Universal Render Pipeline) — Optional. The handle
  shaders include a URP SubShader gated behind a `PackageRequirements` block, so it is compiled
  only when URP (12.1.0 or newer) is installed in the consuming project; Built-in projects need
  no extra packages. URP is distributed by Unity Technologies under the Unity Companion License /
  Unity Package Distribution Terms and is **not** redistributed with this package.

- **com.unity.inputsystem** (Unity Input System) — Used only when present in the project and
  enabled in Player Settings. The package compiles against Unity's legacy Input Manager when the
  Input System package is absent. The Input System package is distributed by Unity Technologies
  under the Unity Companion License / Unity Package Distribution Terms and is **not** redistributed
  with this package.

## Bundled assets

All meshes (`cone`, `torus`, `tube`), materials, prefabs, and shaders shipped in this package are
distributed under the package's MIT license (see `LICENSE.md`). Portions derive from the Runtime
Transform Handle project credited above (also MIT).
