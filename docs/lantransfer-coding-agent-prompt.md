# Coding Agent Prompt: LanTransfer Initialization

You are implementing a scoped change from an approved spec. First inspect the target repository, confirm affected files, then implement the smallest safe vertical slice that satisfies the acceptance criteria.

## Repositories

- Target repository, may modify: this repository root.
- Skill instructions, read-only unless explicitly stated: `spec-driven-delivery` workflow.
- Reference repositories, read-only: none

## Allowed Modifications

- `LanTransfer.sln`
- `src/LanTransfer.Core`
- `src/LanTransfer.Host`
- `tests/LanTransfer.Tests`
- `docs`
- `screenshots`
- `README.md`
- `README.zh-CN.md`
- `.gitignore`
- `LICENSE`

## Disallowed Modifications

- Do not modify unrelated repositories.
- Do not add `LanTransfer.Web`, `LanTransfer.Shared`, `LanTransfer.Desktop`, `LanTransfer.Cli`, `LanTransfer.Api`, or `LanTransfer.Infrastructure`.
- Do not add React, Vue, Vite, or a frontend build system.
- Do not add chat backend, WebSockets, user login, friends, database history, cloud sync, preview editors, or media editors.
- Do not add ASP.NET Core Identity, JWT, OAuth, or a database.
- Do not change CI, packaging, signing, or release automation.
- Do not downgrade from `net10.0`.
- Do not add unnecessary third-party runtime packages.

## Requirement

Initialize and refactor the repository into a clean `LanTransfer` cross-platform LAN file transfer tool with Core, Host, Tests, static chat-style UI, frontend i18n, documentation, and verification.

## Spec Summary

- Problem: the repository needs a clear open-source .NET structure for a browser-based LAN file receiver.
- Goals: safe file storage, Minimal API Host, responsive static UI, lightweight i18n, README docs, and tests.
- Non-goals: chat system, user system, WebSocket, database, separate frontend project, desktop app, CLI split, packaging, or cloud sync.
- Assumptions: Host is a console executable that runs ASP.NET Core Kestrel; Core remains free of ASP.NET Core.
- Risks: UI needs manual visual QA; AccessToken is lightweight and only appropriate for trusted LANs.
- Engineering guidelines: apply `templates/dotnet-engineering-guidelines.md`.

## Implementation Plan

1. Update solution to include `LanTransfer.Core`, `LanTransfer.Host`, and `LanTransfer.Tests`.
2. Implement Core options, models, interfaces, storage service, duplicate naming, path safety, size checks, and error codes.
3. Implement Host as `Microsoft.NET.Sdk` executable with `Microsoft.AspNetCore.App`, Minimal APIs, DI, Kestrel/form limits, static files, and AccessToken.
4. Implement `wwwroot` static chat-style UI with `app.js`, `i18n.js`, `en.json`, and `zh-CN.json`.
5. Update README files, `.gitignore`, and docs.
6. Add xUnit tests for Core safety and storage behavior.
7. Run `dotnet build LanTransfer.sln` and `dotnet test LanTransfer.sln`.

## Acceptance Criteria

- Solution and project names match `LanTransfer`.
- Host executable name is `lantransfer`.
- Core has no ASP.NET Core dependency.
- Upload/list/download APIs work and return stable error codes.
- File paths cannot escape `StorageDirectory`.
- Duplicate names are readable and non-destructive.
- UI is responsive and chat-style without a frontend framework.
- i18n uses one HTML file and language JSON.
- README docs contain required English and Chinese sections.
- Build and tests pass.

## Build/Test Commands

- Build: `dotnet build LanTransfer.sln`
- Test: `dotnet test LanTransfer.sln`
- Manual: `dotnet run --project src/LanTransfer.Host`

## Required Agent Behavior

- Keep changes scoped to the target repository.
- Preserve `net10.0`.
- Keep Core independent from ASP.NET Core.
- Run listed checks when possible.
- If a check cannot run, state exactly why.
- Stop and ask before schema migrations, new dependencies, public API breaks, installer/signing changes, or destructive data changes.

## Final Summary Format

Return:

1. Files changed
2. Requirement coverage
3. Verification run and results
4. Risks or follow-up work
