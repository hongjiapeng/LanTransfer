# LanTransfer Initialization Spec

## Assumptions

- Target repository: this repository root.
- Product, solution, executable, and root namespace names are standardized as `LanTransfer`.
- The first release slice is a browser-based LAN file transfer receiver, not a chat product.
- .NET 10 remains the target framework; do not downgrade if a local SDK is missing.
- The Host is a cross-platform console executable that runs ASP.NET Core Kestrel.

## Non-goals

- No `LanTransfer.Web`, `LanTransfer.Shared`, `LanTransfer.Desktop`, `LanTransfer.Cli`, `LanTransfer.Api`, or `LanTransfer.Infrastructure` projects.
- No chat message system, WebSocket chat, user login, friends list, database history, cloud sync, file preview editor, video player, or image editor.
- No React, Vue, Vite, or other frontend build system.
- No ASP.NET Core Identity, JWT, OAuth, or database-backed user system.
- No packaging, signing, installer, CI, or release automation changes.

## Feature Spec

## Problem

Users need a small cross-platform tool that can receive files from phones, tablets, and computers on the same local network through a browser. The current repository needed a clear `LanTransfer` structure, stable project boundaries, a safe storage core, HTTP APIs, a simple static UI, and basic tests.

## User Scenario

As a user on a trusted LAN, I want to run `lantransfer` on one device and open a web page from another device, so that I can upload and download files without installing a client app.

## Goals

- Initialize a clean `LanTransfer` solution with `LanTransfer.Core`, `LanTransfer.Host`, and `LanTransfer.Tests`.
- Keep Core free of ASP.NET Core and UI concerns.
- Provide a cross-platform console Host that starts Kestrel and serves APIs plus static files.
- Provide a chat-style file transfer UI using only HTML, CSS, and JavaScript.
- Add English and Simplified Chinese web UI localization.
- Add tests for filename safety, path traversal protection, listing, size limits, missing files, duplicate names, and Core dependency boundaries.

## Functional Requirements

| ID | Requirement | Rationale | Acceptance Criteria |
|---|---|---|---|
| FR-1 | Solution and projects are named `LanTransfer`, `LanTransfer.Core`, `LanTransfer.Host`, and `LanTransfer.Tests`. | Keeps repository identity consistent. | AC-1 |
| FR-2 | Host is a cross-platform console executable named `lantransfer`. | Users should run a CLI-style receiver. | AC-2 |
| FR-3 | Core implements storage, file listing, download metadata, filename safety, size limits, duplicate naming, and stable error codes. | Business logic should be reusable and testable. | AC-3 |
| FR-4 | Core does not reference ASP.NET Core or UI/i18n concepts. | Preserves layer boundaries. | AC-4 |
| FR-5 | Host exposes health, upload, list, and download Minimal APIs with optional AccessToken validation. | Provides the required web contract. | AC-5 |
| FR-6 | Uploads are streamed, size-limited, saved through temporary `.uploading` files, and do not overwrite existing files. | Prevents memory pressure and partial file pollution. | AC-6 |
| FR-7 | Static UI is a responsive chat-style file transfer surface. | Matches the product direction and screenshot. | AC-7 |
| FR-8 | Web UI supports `en` and `zh-CN` through JSON files and one `index.html`. | Keeps i18n lightweight and frontend-only. | AC-8 |
| FR-9 | README files document usage, configuration, APIs, build, roadmap, and license. | Makes the repository usable as an OSS project. | AC-9 |
| FR-10 | Build and tests pass on .NET 10. | Confirms the initialized project is healthy. | AC-10 |

## UX Behavior

- Desktop UI appears as a centered white transfer window with a subtle browser/app bar.
- Mobile UI fills the viewport like a chat page.
- Existing downloadable files appear as left-side file bubbles.
- Newly uploaded files appear as right-side sending/sent bubbles.
- Dragging files over the page gives a subtle highlight.
- Upload failures render inline/toast feedback using localized error messages.
- Empty state is text-only and understated.

## Technical Constraints

- Target repository: this repository root.
- Target framework: `net10.0`.
- Host SDK shape: `Microsoft.NET.Sdk` console executable with `Microsoft.AspNetCore.App` framework reference.
- Core dependency rule: no `Microsoft.AspNetCore.*`, no `HttpContext`, no `IFormFile`, no `IResult`, no `Results`, no Controllers, no Minimal API handlers.
- Frontend: static files only under `src/LanTransfer.Host/wwwroot`.
- Engineering guidelines: apply `templates/dotnet-engineering-guidelines.md` from the `spec-driven-delivery` skill.

## Edge Cases

- `../evil.txt`, `..\evil.txt`, URL-encoded traversal, and absolute paths are rejected.
- Empty, whitespace-only, or dot-only names are rejected for lookup and normalized safely for upload when appropriate.
- Invalid filename characters are replaced for upload filenames.
- Duplicate files become `name (1).ext`, `name (2).ext`, and so on.
- Files larger than `MaxFileSizeBytes` return `file_too_large`.
- Missing downloads return `file_not_found`.
- Unauthorized protected APIs return `unauthorized`.

## Acceptance Criteria

| ID | Criteria | Verification |
|---|---|---|
| AC-1 | Solution contains only `LanTransfer.Core`, `LanTransfer.Host`, and `LanTransfer.Tests` as active projects. | Inspect `LanTransfer.sln`. |
| AC-2 | `LanTransfer.Host` is an executable project with `AssemblyName` `lantransfer`. | Inspect `src/LanTransfer.Host/LanTransfer.Host.csproj`; run `dotnet run --project src/LanTransfer.Host`. |
| AC-3 | Core storage handles save, list, get, read, duplicate names, and stable error codes. | Unit tests in `LocalFileStorageTests`. |
| AC-4 | Core project file has no ASP.NET Core framework/package reference. | Unit test plus project inspection. |
| AC-5 | API endpoints exist at `/api/health`, `/api/files/upload`, `/api/files`, and `/api/files/{fileName}`. | Manual HTTP checks or browser use. |
| AC-6 | Uploads are streamed to a temp file, enforce max size, and clean temp files on failure. | Code review plus unit tests for size failures. |
| AC-7 | UI renders a chat-style file transfer timeline on desktop and mobile. | Manual browser check. |
| AC-8 | Language is selected by URL, localStorage, browser language, then `en`; HTML `lang` updates. | Manual browser check with `?lang=zh-CN`. |
| AC-9 | English and Chinese README files include required sections. | Inspect `README.md` and `README.zh-CN.md`. |
| AC-10 | `dotnet build LanTransfer.sln` and `dotnet test LanTransfer.sln` pass. | Run commands. |

## Risks and Review Notes

- Browser UI has not been automated with Playwright yet; perform manual desktop/mobile visual review.
- AccessToken is intentionally lightweight and not suitable for public internet exposure.
- Static image thumbnails load through download URLs; very large images may cost bandwidth in the browser timeline.
