# Implementation Plan: Device connection, text transfer, and desktop startup

## Target repository

- May modify: this repository root.
- Read-only references: user-supplied product preview and real screenshot; `spec-driven-delivery` templates.

## Proposed approach

Extend the existing vertical slice rather than introducing a desktop shell: add Core-backed message persistence, small Host APIs for messages/connect URLs/QR SVG, update the static frontend, and runtime-gate a native Windows tray service. Keep Kestrel and the same release matrix.

## Engineering guidelines

- Apply `templates/dotnet-engineering-guidelines.md` from `spec-driven-delivery`.
- Add only QRCoder 1.8.0, scoped to the Host, for correct offline SVG QR generation.
- Keep Windows interop isolated and guarded by `OperatingSystem.IsWindows()`.

## Requirement traceability

| Requirement | Task | Verification |
|---|---|---|
| FR-11–FR-13 | TASK-2 connection URL and QR APIs; TASK-4 modal UI | AC-11–AC-14 |
| FR-14–FR-15 | TASK-1 message storage; TASK-3 message APIs; TASK-5 composer/timeline | AC-15–AC-17 |
| FR-16 | TASK-6 viewport layout | AC-18 |
| FR-17 | TASK-7 browser launcher | AC-19 |
| FR-18 | TASK-8 Windows output/tray | AC-20–AC-21 |
| FR-19 | TASK-9 tests/docs/smoke checks | AC-22 |

## File-level plan

| File/area | Change |
|---|---|
| `src/LanTransfer.Core/Models`, `Abstractions`, `Services` | Add text-message model, validation error, and atomic local JSON persistence. |
| `src/LanTransfer.Core/Options/LanTransferOptions.cs` | Add browser, tray, message-length, and refresh defaults. |
| `src/LanTransfer.Host/Program.cs` | Register services and map connect/message/QR APIs. |
| `src/LanTransfer.Host/Services` | Add LAN address resolution, browser launch, and Windows tray lifecycle. |
| `src/LanTransfer.Host/LanTransfer.Host.csproj` | Add QRCoder and Windows `WinExe` output conditions. |
| `src/LanTransfer.Host/wwwroot` | Add dialog/composer/message rendering/i18n and full-height CSS. |
| `tests/LanTransfer.Tests` | Add message storage/validation and connection URL tests. |
| `README*.md`, `docs/*` | Document behavior, config, APIs, risks, and verification. |

## Task plan

- [x] TASK-1: Implement and test local text-message persistence (FR-15).
- [x] TASK-2: Resolve distinct LAN URLs and generate token-aware connect data/QR SVG (FR-11–FR-13).
- [x] TASK-3: Add authorized `GET/POST /api/messages` endpoints and stable validation errors (FR-14–FR-15).
- [x] TASK-4: Add localized accessible connection dialog, address selector, and copy action (FR-11–FR-13).
- [x] TASK-5: Convert the file-only composer to text-or-file behavior and merge/poll the timeline (FR-14–FR-15).
- [x] TASK-6: Make desktop/tablet UI viewport-height with a width-limited inner column (FR-16).
- [x] TASK-7: Open localhost once through the OS default browser with a config opt-out (FR-17).
- [x] TASK-8: Add Windows-only native tray Open/Exit and windowed output without changing non-Windows runtime paths (FR-18).
- [x] TASK-9: Update docs; build, test, publish-smoke, API-smoke, and browser-check (FR-19).

## Test/build plan

- Build: `dotnet build LanTransfer.sln`
- Tests: `dotnet test LanTransfer.sln --no-build`
- Windows publish: `dotnet publish src/LanTransfer.Host/LanTransfer.Host.csproj -c Release -r win-x64 --self-contained false`
- Linux publish compile check: `dotnet publish src/LanTransfer.Host/LanTransfer.Host.csproj -c Release -r linux-x64 --self-contained false`
- Manual: start with `LanTransfer__OpenBrowserOnStart=false`, exercise connect/message/file APIs, then inspect 1920x1080 and 390x844 in a real browser.

## Rollback considerations

- Message state is isolated under `uploads/.lantransfer`; reverting code leaves it harmless and does not alter uploaded files.
- Browser/tray features have configuration switches and fail open: disabling them returns to console-host behavior.
- Existing API routes remain unchanged.
