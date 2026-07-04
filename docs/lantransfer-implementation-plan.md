# LanTransfer Implementation Plan

## Target Repository

- May modify: this repository root.
- Skill instructions: `spec-driven-delivery` workflow, read-only reference.
- Read-only references: none

## Affected Modules

- `LanTransfer.sln`
- `src/LanTransfer.Core`
- `src/LanTransfer.Host`
- `src/LanTransfer.Host/wwwroot`
- `tests/LanTransfer.Tests`
- `README.md`
- `README.zh-CN.md`
- `.gitignore`
- `docs`
- `screenshots`

## Proposed Approach

Create the smallest useful vertical slice: a Core storage library, a console-style ASP.NET Core Host, a static chat-style UI, and unit tests. Keep Core independent from ASP.NET Core and keep the frontend framework-free.

## Engineering Guidelines

- Apply `templates/dotnet-engineering-guidelines.md` from the `spec-driven-delivery` skill.
- Project-specific decision: `LanTransfer.Host` uses `Microsoft.NET.Sdk` with `OutputType Exe` and `Microsoft.AspNetCore.App` to make the console Host boundary explicit while retaining Kestrel and Minimal APIs.
- Project-specific decision: no new runtime dependencies beyond ASP.NET Core shared framework and xUnit test packages.

## Requirement Traceability

| Requirement | Implementation Task | Verification |
|---|---|---|
| FR-1 | Update solution and project layout. | AC-1 |
| FR-2 | Configure Host as `lantransfer` executable. | AC-2 |
| FR-3 | Implement Core abstractions, options, models, and storage service. | AC-3 |
| FR-4 | Keep Core project dependency-free from ASP.NET Core. | AC-4 |
| FR-5 | Implement Minimal API routes and AccessToken validation. | AC-5 |
| FR-6 | Implement streamed temp-file upload and duplicate naming. | AC-6 |
| FR-7 | Replace static UI with chat-style layout. | AC-7 |
| FR-8 | Add `i18n.js`, `en.json`, and `zh-CN.json`. | AC-8 |
| FR-9 | Update README files and ignore local upload data. | AC-9 |
| FR-10 | Run build and tests. | AC-10 |

## Vertical Slice

1. Run `dotnet run --project src/LanTransfer.Host`.
2. Open the static page in a browser.
3. Upload a file through `POST /api/files/upload`.
4. See it appear in the file timeline and download it through `/api/files/{fileName}`.
5. Confirm `dotnet build` and `dotnet test` pass.

## File-level Plan

| File/Area | Change | Requirement |
|---|---|---|
| `LanTransfer.sln` | Reference Core, Host, and Tests only. | FR-1 |
| `src/LanTransfer.Core/LanTransfer.Core.csproj` | Add Core library project targeting `net10.0`. | FR-1, FR-4 |
| `src/LanTransfer.Core/Options/LanTransferOptions.cs` | Add required config shape. | FR-3 |
| `src/LanTransfer.Core/Models` | Add `FileItem`, `UploadResult`, and `ErrorResult`. | FR-3 |
| `src/LanTransfer.Core/Abstractions` | Add storage/inbox interfaces. | FR-3 |
| `src/LanTransfer.Core/Services` | Add local storage and inbox implementation. | FR-3, FR-6 |
| `src/LanTransfer.Host/LanTransfer.Host.csproj` | Configure console executable Host. | FR-2 |
| `src/LanTransfer.Host/Program.cs` | Add Minimal APIs, DI, Kestrel limits, static files, AccessToken. | FR-5, FR-6 |
| `src/LanTransfer.Host/appsettings.json` | Add `LanTransfer` config defaults. | FR-5 |
| `src/LanTransfer.Host/wwwroot` | Add responsive chat UI and i18n. | FR-7, FR-8 |
| `tests/LanTransfer.Tests` | Add Core-focused storage tests. | FR-3, FR-4, FR-10 |
| `README.md`, `README.zh-CN.md` | Update OSS documentation. | FR-9 |
| `.gitignore` | Ignore uploads and temporary upload files. | FR-9 |

## Task Plan

- [x] TASK-1: Scan current repository and old names.
- [x] TASK-2: Create Core, Host, and Tests structure.
- [x] TASK-3: Implement Core storage behavior.
- [x] TASK-4: Implement Host APIs and static hosting.
- [x] TASK-5: Implement chat-style static UI.
- [x] TASK-6: Implement lightweight frontend i18n.
- [x] TASK-7: Update README and `.gitignore`.
- [x] TASK-8: Add spec-driven delivery docs.
- [x] TASK-9: Run build and tests.

## Test/Build Plan

- Build: `dotnet build LanTransfer.sln`
- Tests: `dotnet test LanTransfer.sln`
- Manual verification: `dotnet run --project src/LanTransfer.Host`, then open `http://localhost:8765`.

## Rollback Considerations

- Reverting this change restores the old combined library/sample shape.
- Uploaded user files are outside source control under `uploads/` by default and should not be deleted by source rollback.

## Risks

- UI polish should still be reviewed in actual desktop and mobile browsers.
- Lightweight AccessToken is intended only for trusted LAN use.
