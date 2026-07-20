# Acceptance Checklist: Device connection, text transfer, and desktop startup

## Functional verification

- [x] More menu opens a localized Connect new device dialog.
- [x] Displayed URL, selected URL, copied URL, and QR source URL match.
- [x] Multiple usable IPv4 addresses can be selected.
- [x] Access token is safely included when configured.
- [x] Composer placeholder is “Enter a note or send a file” / “输入备注或发送文件”.
- [x] Enter sends text; Shift+Enter preserves a newline; add button uploads files.
- [x] Empty and oversized text are rejected; HTML-like text is rendered literally.
- [x] Text persists across refresh and appears in a second browser through polling.
- [x] Existing file upload/download APIs still work.

## UI verification

- [x] Dialog has label, close behavior, keyboard focus, QR alt text, URL selector, copy action, and help/error state.
- [x] Empty, loading, invalid input, unauthorized, network, and clipboard failure states are understandable.
- [x] At 1920x1080 the transfer surface fills the viewport height and the timeline alone scrolls.
- [x] At 390x844 header, timeline, modal, and composer remain usable with safe-area padding.
- [x] Desktop message/file content stays width-limited rather than stretching edge to edge.

## Desktop lifecycle verification

- [x] Host opens the local URL once after Kestrel starts.
- [x] `OpenBrowserOnStart=false` prevents browser launch.
- [ ] Windows publish shows no console and has tray Open/Exit actions. (Windowless process and tray creation were exercised; final menu click remains a manual OS check.)
- [x] Windows tray creates the LanTransfer blue monitor icon without falling back to the generic Windows application icon.
- [x] Tray/browser failures do not terminate Kestrel.
- [x] Linux/macOS builds do not load or call Windows native APIs.

## Error and security verification

- [x] Connect and message APIs preserve optional token authorization.
- [x] QR SVG is generated locally with no third-party network call.
- [x] Text is length-limited, trimmed, JSON serialized, and inserted with DOM text APIs.
- [x] Concurrent writes do not corrupt the message store.
- [x] Logs and responses do not expose file contents or unencoded secrets beyond the intentionally shareable connect URL.

## Build/test verification

- [x] `dotnet build LanTransfer.sln`
- [x] `dotnet test LanTransfer.sln --no-build`
- [x] Windows and Linux publish compile checks pass.
- [x] Root URL and all static assets work from built/published output.
- [x] No unrelated files, generated build output, or uploaded user data are tracked.

## Manual flow

1. Start with browser auto-open enabled; confirm one tab opens.
2. Open Connect new device, scan from a same-LAN phone, and load the page.
3. Send text in each direction and verify it appears without manually reloading.
4. Upload and download one image and one non-image file.
5. Resize to 1920x1080 and 390x844; verify only the timeline scrolls.
6. On Windows, close the browser, reopen from tray, then Exit and confirm the Host stops.
