# LanTransfer Acceptance Checklist

## Functional Verification

- [x] Project structure contains `src/LanTransfer.Core`, `src/LanTransfer.Host`, and `tests/LanTransfer.Tests`.
- [x] Host is a console executable project named `lantransfer`.
- [x] Core can save files without loading the entire file into memory.
- [x] Uploads use `.uploading` temp files and avoid overwriting existing files.
- [x] Duplicate names use `photo (1).jpg` style.
- [x] File list returns file name, size, last modified time, and download URL.
- [x] Missing files produce `file_not_found`.
- [x] Invalid filenames and traversal attempts produce `invalid_file_name`.
- [x] Oversized uploads produce `file_too_large`.
- [x] AccessToken is optional and enforced on upload/list/download when configured.

## UI Verification

- [x] One `index.html` is used for both English and Simplified Chinese.
- [x] Desktop layout uses a centered transfer window.
- [x] Mobile layout uses a full-screen chat-style surface.
- [x] Existing files render on the left as downloadable file cards.
- [x] Uploading files render on the right with progress.
- [x] Upload success renders `Sent` / `已发送`.
- [x] Upload failure renders localized inline/toast feedback.
- [x] Empty state is text-only and understated.
- [ ] Manual visual review on real phone viewport.

## Error Handling Verification

- [x] `../evil.txt` is rejected.
- [x] `..\evil.txt` is rejected.
- [x] Absolute paths are rejected.
- [x] URL-encoded traversal is rejected.
- [x] Failed upload temp files are cleaned best-effort.
- [x] Backend responses use stable error codes.
- [x] Frontend maps backend error codes to localized messages.

## Build/Test Verification

- [x] Build passes: `dotnet build LanTransfer.sln`
- [x] Tests pass: `dotnet test LanTransfer.sln`
- [x] No `bin`, `obj`, `uploads`, or `.uploading` files are tracked.
- [x] No old forbidden names remain in source text.

## Regression Checks

- [x] README links between English and Chinese docs are present.
- [x] `appsettings.json` contains required `LanTransfer` options.
- [x] Core project does not reference ASP.NET Core.
- [x] Host serves static files from `wwwroot`.

## Manual Verification Steps

1. Run `dotnet run --project src/LanTransfer.Host`.
2. Open `http://localhost:8765`.
3. Upload a small file and confirm it appears as a sent bubble.
4. Refresh and confirm it appears as a downloadable received bubble.
5. Open `http://localhost:8765?lang=zh-CN` and confirm Chinese UI text.
