# Feature Spec: LanTransfer device connection and text transfer

## Assumptions

- Target repository: this repository root; no other repository may be modified.
- The generated product preview is a visual reference, while the second supplied image is a real application screenshot.
- "Connect new device" means opening this Host's LAN web URL on another device. It does not establish a separate peer-to-peer tunnel or account session.
- The LAN is trusted. The existing optional access token remains the only access control and is included in generated links when configured.
- .NET 10 and the existing Core + Host + static frontend architecture remain in place.

## Non-goals

- No Internet relay, NAT traversal, Bluetooth discovery, cloud account, device approval workflow, expiring pairing code, TLS certificate automation, or end-to-end encryption.
- No WebSocket chat, typing indicators, read receipts, rich text, message editing, or message attachments beyond the existing file upload flow.
- No macOS/Linux tray integration and no platform-specific UI framework migration.
- No fake browser chrome from the product preview.

## Problem

The current application makes users manually discover and type the LAN URL, transfers only files, leaves unused vertical space on large displays, and behaves like a developer console program when launched. A normal user needs a discoverable connection flow, short text transfer, a viewport-filling UI, and desktop-friendly startup behavior.

## User scenarios

- As a desktop user, I can display a QR code for a reachable LAN URL so that a phone on the same LAN can scan and open LanTransfer.
- As either device, I can send a short plain-text note and see it in the same timeline as files.
- As a large-screen user, the transfer surface fills the available viewport height while retaining a readable maximum content width.
- As an ordinary user, launching the packaged program opens the site in my default browser without showing a Windows console; I can reopen or exit it from the Windows tray.

## Functional requirements

| ID | Requirement | Rationale | Acceptance criteria |
|---|---|---|---|
| FR-11 | The more menu exposes **Connect new device** and opens an accessible modal containing a scannable QR code and the encoded LAN URL. | Removes manual address entry. | AC-11, AC-12 |
| FR-12 | The Host enumerates usable IPv4 LAN URLs; the modal allows switching when more than one address exists. Configured access tokens are included in the URL. | Handles Wi-Fi/Ethernet/VPN ambiguity and protected deployments. | AC-13 |
| FR-13 | Users can copy the selected connection URL. QR generation is local and does not call an Internet service. | Keeps LAN/offline behavior and privacy. | AC-14 |
| FR-14 | The composer accepts plain text with the hint **Enter a note or send a file** / **输入备注或发送文件**. Enter sends, Shift+Enter inserts a newline, and empty/whitespace-only text is rejected. | Adds the requested lightweight note transfer. | AC-15 |
| FR-15 | Text messages are limited to 4,000 characters, stored locally, returned by authenticated APIs, and rendered safely as plain text in chronological order with files. | Prevents abuse and makes messages visible after reload/on another device. | AC-16, AC-17 |
| FR-16 | On desktop/tablet widths the outer surface uses the full viewport height with a centered, width-limited content column; on phones it remains a full-screen layout. | Uses large displays without stretching message content excessively. | AC-18 |
| FR-17 | After the server starts, it opens its localhost URL once in the default browser. This can be disabled by configuration. | Provides consumer-friendly startup. | AC-19 |
| FR-18 | Windows builds use a windowed executable, add a native LanTransfer tray icon with Open and Exit actions, and keep the server alive when the browser closes. macOS/Linux builds remain normal console hosts and do not load Windows APIs. | Hides developer-facing console while preserving cross-platform publication. | AC-20, AC-21 |
| FR-19 | Existing upload, download, localization, token authorization, storage safety, and release targets continue to work. | Prevents regressions. | AC-22 |

## UX behavior

- The connection dialog opens from the first menu action, traps focus through the native `dialog` element, closes via its close button or Escape, shows one QR at a time, and displays a network/help note.
- QR images are SVG for crisp display. The QR content and visible URL always match.
- The composer is a multiline text area. The add button still opens the file picker; the send button sends text when present and otherwise opens the file picker.
- Text uses `textContent`/plain JSON data only; HTML entered by a user is never interpreted.
- Timeline content is periodically refreshed so a second device sees new files and text without a full page reload. Locally sent item IDs are remembered for direction styling.
- Desktop layout has no fixed 760 px height cap. Header and composer remain fixed rows while only the timeline scrolls.
- Loading, empty, invalid-text, network-error, unauthorized, and copy-failure states use localized feedback.

## Technical constraints

- Target framework: `net10.0`.
- Preserve `LanTransfer.Core`, `LanTransfer.Host`, `LanTransfer.Tests`; do not add a desktop project or frontend build tool.
- Text persistence lives under a hidden state subdirectory of the configured storage root so it does not appear in the file list.
- With no explicit storage configuration, user data lives under the current user's local application-data directory rather than the executable/package directory, so package upgrades and uninstall do not remove received files.
- Use `QRCoder` 1.8.0 for standards-compliant, local SVG QR generation. It is MIT licensed, has no non-framework runtime dependencies, and supports .NET 10.
- Windows tray support uses runtime-gated Win32 interop in the Host. No Windows-only assembly may be loaded on macOS/Linux.
- The tray artwork is drawn deterministically in memory as a transparent blue rounded tile with a white monitor glyph, with the Windows application icon as a non-fatal fallback.
- Apply `templates/dotnet-engineering-guidelines.md` from the `spec-driven-delivery` skill.

## Edge cases

- No usable LAN IPv4 address: show localhost as a diagnostic fallback and explain that another device cannot use it.
- Multiple adapters: list distinct addresses and let the user choose; do not silently claim which is reachable.
- Access token: URL query is encoded; API authorization remains unchanged.
- Text consisting only of whitespace is rejected; leading/trailing whitespace is trimmed; line breaks within the message are preserved.
- Concurrent message writes are serialized and persisted atomically; a missing message file produces an empty list.
- Browser launch failure or missing desktop environment is logged but does not stop the server.
- Windows tray creation failure is logged and does not stop the Host.

## Acceptance criteria

| ID | Observable result | Verification |
|---|---|---|
| AC-11 | Connect new device opens a modal from the more menu on desktop and phone widths. | Browser UI check. |
| AC-12 | A phone can scan the QR and open the exact displayed URL. | Scan test on a same-LAN phone or decode the SVG in a QR test tool. |
| AC-13 | `/api/connect` returns distinct LAN URLs and includes an encoded token when configured. | Automated/API tests and manual multi-adapter check. |
| AC-14 | Copy link copies the selected URL; QR is served locally as SVG. | Browser clipboard check and network inspection. |
| AC-15 | Placeholder text matches the requested copy; Enter sends and Shift+Enter adds a newline. | Desktop/mobile keyboard check. |
| AC-16 | Empty and >4,000-character messages return stable validation errors; HTML-like input renders literally. | Unit/API/UI checks. |
| AC-17 | A sent message remains after reload and appears on another browser during polling. | Two-browser manual check. |
| AC-18 | At 1920x1080 and taller viewports the surface fills the viewport vertically, while message content remains width-limited; 390x844 remains usable. | Screenshot/layout check. |
| AC-19 | Starting the Host opens one default-browser tab; `OpenBrowserOnStart=false` disables it. | Launch twice with both settings. |
| AC-20 | Windows publish has no console window and exposes tray Open/Exit actions. | Run the `win-x64` publish artifact. |
| AC-21 | Linux/macOS publish remains an executable console Host and never enters Win32 tray code. | Publish matrix/build inspection; smoke test where available. |
| AC-22 | `dotnet build LanTransfer.sln` and `dotnet test LanTransfer.sln` pass; upload/download still work. | Automated build/test plus smoke test. |

## Risks and review notes

- A QR URL proves reachability only if firewall and Wi-Fi client isolation allow the port; the modal must state this rather than representing the QR as a pairing guarantee.
- Multi-adapter enumeration cannot know which subnet the phone uses, so address selection is explicit.
- Hiding the Windows console removes an easy diagnostic surface; startup failures must still be observable through process exit/logging, with future file logging left out of this slice.
- The QRCoder package is the only new dependency and is confined to the Host.

## CLI and Agent integration (planned follow-up)

The browser remains the primary human-facing interface. A separate console CLI is planned as an automation interface for scripts, CI jobs, and coding Agents. The CLI treats LanTransfer as a local-network file and text transport rather than introducing a second transfer protocol.

### Goals

- Allow a script or Agent to send files and text without opening a browser or requiring interactive input.
- Make service status and reachable connection URLs available in both human-readable and machine-readable formats.
- Support build, test, backup, and report workflows that need to transfer artifacts to another LAN device.
- Keep the existing browser, Host, HTTP API, token authorization, and storage behavior compatible.

### Initial command scope

The first CLI slice should cover the following commands:

```text
lantransfer start
lantransfer status
lantransfer url
lantransfer send file <path>
lantransfer send text <text>
lantransfer files list
lantransfer files download <fileName>
```

Representative automation examples:

```bash
lantransfer url --json
lantransfer send file ./build.zip --to http://192.168.1.20:8765 --json
echo "Build completed" | lantransfer send text --to http://192.168.1.20:8765
```

The CLI should support an explicit target URL, an optional access token, multiple-file input where practical, and text from standard input. File deletion, service stop, device aliases, directory watching, and device discovery are deferred until the first slice proves useful.

### Functional requirements

| ID | Requirement | Rationale | Acceptance criteria |
|---|---|---|---|
| FR-20 | Provide a non-interactive console CLI for starting and inspecting the service, obtaining connection URLs, sending files and text, and basic received-file access. | Enables scripts, CI, and Agent workflows without browser automation. | AC-23, AC-24 |
| FR-21 | CLI transfer commands use the existing authenticated HTTP APIs and never access the Host's local storage files directly. | Avoids a second storage protocol and prevents cross-process file-write races. | AC-25 |
| FR-22 | CLI commands support human-readable output by default and stable `--json` output for automation. Successful operations and failures must have stable exit codes. | Lets Agents and shell scripts reliably inspect results. | AC-26 |
| FR-23 | CLI commands must be non-interactive by default, must not open a browser, and must support bounded request timeouts. | Makes behavior safe for unattended execution. | AC-27 |
| FR-24 | Access tokens may be supplied through configuration or an environment variable as well as an explicit option; normal output must not print the token. | Supports protected Hosts without encouraging secret leakage through logs. | AC-28 |
| FR-25 | Windows GUI/Host publishing remains a windowed executable, while the CLI is a console entry point. | Preserves the current tray experience while providing visible CLI output. | AC-29 |

### Agent-facing contract

- Standard output is reserved for command results; diagnostics and progress belong on standard error.
- JSON output should contain a stable success/error shape, including the existing `errorCode` where the HTTP API provides one.
- Exit code `0` means success; usage, authorization, connection, and transfer failures must be distinguishable and documented.
- The CLI must not require a confirmation prompt for ordinary transfers. Commands that may later delete data or stop a service should require an explicit opt-in flag.
- Target URLs must be explicit or come from an unambiguous configured default; the CLI must not silently choose an arbitrary remote device.

### Technical direction

- Add a console `LanTransfer.Cli` project alongside `LanTransfer.Core` and `LanTransfer.Host` when implementation begins.
- Reuse a small HTTP client/protocol layer rather than duplicating upload, message, authorization, and error parsing logic in each command.
- Keep the current Host as the service and browser/tray process. A CLI `start` command may launch or configure that Host, but lifecycle control should not require weakening the Windows single-instance behavior.
- Do not make the CLI depend on browser automation, a frontend build tool, or platform-specific tray APIs.

### Additional acceptance criteria

| ID | Observable result | Verification |
|---|---|---|
| AC-23 | `start`, `status`, `url`, `send file`, and `send text` work without opening a browser; `start` can launch or configure the Host for unattended use. | CLI integration smoke tests. |
| AC-24 | A file, text from an argument, and text from standard input can be transferred to a selected target URL. | CLI/API integration tests. |
| AC-25 | CLI transfers go through the existing HTTP endpoints and preserve token authorization and stable API errors. | Authenticated and unauthorized integration tests. |
| AC-26 | Human output is readable; `--json` output is valid, stable JSON and successful/failing commands return documented exit codes. | Golden-output and process exit-code tests. |
| AC-27 | CLI execution is non-interactive, does not launch a browser, and fails within the configured timeout when the target is unavailable. | Unattended process test. |
| AC-28 | Tokens supplied through configuration or environment variables are accepted and are absent from normal output and error messages. | Secret-redaction test. |
| AC-29 | Windows Host remains windowless with tray behavior, while the published CLI writes usable output to a console. | Windows publish smoke test. |

### Follow-up risks

- Passing tokens directly on a command line can expose them through shell history or process inspection; environment/configuration input should be preferred and documented.
- A CLI cannot infer which LAN URL is reachable from the target device, so target selection remains explicit and should reuse the existing connection URL data.
- File upload and text send are currently one-way operations from the CLI perspective; a useful `pull` or remote-to-local workflow may require additional API semantics later.
