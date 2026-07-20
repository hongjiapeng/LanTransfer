# Coding Agent Prompt: LanTransfer connection and text transfer

Implement `docs/lantransfer-spec.md` in this repository root. This repository is the only writable repository; the user images and skill templates are read-only references.

Allowed modifications are `src/LanTransfer.Core`, `src/LanTransfer.Host`, `tests/LanTransfer.Tests`, `docs`, `README.md`, and `README.zh-CN.md`. Do not perform unrelated refactors, change target framework, add projects/frontend tooling, change signing/release automation, delete user data, or alter existing file API contracts. QRCoder 1.8.0 is the only approved new package.

Implement the smallest vertical slice in this order:

1. Core message model/store with 4,000-character validation and atomic local persistence.
2. Authorized message APIs plus LAN connect URL and local SVG QR APIs.
3. Accessible localized connection dialog and a text/file composer with safe timeline rendering/polling.
4. Viewport-height responsive layout with width-limited content.
5. Configurable default-browser launch and runtime-gated Windows native tray/Open/Exit behavior.
6. Unit tests, README/config updates, build/test, Windows/Linux publish checks, API smoke tests, and desktop/mobile browser checks.

Apply the .NET guidelines referenced by the spec. Keep Windows native calls behind `OperatingSystem.IsWindows()` and make failures non-fatal. Preserve access-token behavior and use plain DOM text APIs for user content.

Required checks:

- `dotnet build LanTransfer.sln`
- `dotnet test LanTransfer.sln --no-build`
- Publish checks for `win-x64` and `linux-x64`
- Built-output checks for `/`, static assets, connect API, messages, file upload/download
- Browser checks at 1920x1080 and 390x844

Final response: files changed; requirement/acceptance coverage; exact verification results; remaining risks or platform checks.
