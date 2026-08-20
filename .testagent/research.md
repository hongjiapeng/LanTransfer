# Deletion feature test research

## Scope classification

Broad scope: the feature changes three public abstractions and two persistence implementations, with tests extending two existing test files. Per the code-testing-agent workflow, this research and the implementation plan are required before source or test edits.

This turn is planning-only. No production or test source is to be changed yet.

## Acceptance checklist

1. `IFileStorage.DeleteAsync(string)` returns `bool` (as `Task<bool>` following the repository's asynchronous API convention).
2. `IFileInbox.DeleteAsync(string)` returns `bool` (as `Task<bool>` following the repository's asynchronous API convention).
3. `LocalFileStorage` moves a deleted file into `<storage>/.lantransfer/trash` rather than permanently deleting it.
4. Deleting an existing file returns `true` and makes it unavailable through the active storage listing and lookup APIs.
5. Deleting a missing file returns `false` and does not disturb existing files.
6. File deletion retains the existing filename/path-traversal protections.
7. `ITextMessageStore.DeleteAsync(Guid)` returns `bool` (as `Task<bool>` following the repository's asynchronous API convention).
8. `LocalTextMessageStore` removes only the record whose ID was requested.
9. Deleting an existing text message returns `true` and persists the remaining records.
10. Deleting a missing text message returns `false` and preserves all records.

## Bounded target inventory

| Role | Path | Relevant surface |
| --- | --- | --- |
| File contract | `src/LanTransfer.Core/Abstractions/IFileStorage.cs` | Existing async save/list/get/open API; planned `DeleteAsync` contract. |
| Inbox contract | `src/LanTransfer.Core/Abstractions/IFileInbox.cs` | Mirrors the file-storage contract; planned `DeleteAsync` contract. |
| Text contract | `src/LanTransfer.Core/Abstractions/ITextMessageStore.cs` | Existing async add/list API; planned `DeleteAsync` contract. |
| File implementation | `src/LanTransfer.Core/Services/LocalFileStorage.cs` | Resolves safe filenames inside the storage root; planned move into `.lantransfer/trash`. |
| Text implementation | `src/LanTransfer.Core/Services/LocalTextMessageStore.cs` | Serializes access with `SemaphoreSlim` and atomically rewrites `.lantransfer/messages.json`. |
| File tests | `tests/LanTransfer.Tests/LocalFileStorageTests.cs` | Existing temp-directory fixture and filesystem assertions. |
| Text tests | `tests/LanTransfer.Tests/LocalTextMessageStoreTests.cs` | Existing temp-directory fixture, persisted-message assertions, and disposal cleanup. |

## Existing conventions

- Tests are located in `tests/LanTransfer.Tests` and use namespace `LanTransfer.Tests`.
- Framework: xUnit (`[Fact]`, `[Theory]`, `[InlineData]`) with `Assert.*` assertions.
- Naming convention: `Method_Condition_ExpectedResult`.
- Fixtures create a unique directory below `Path.GetTempPath()` and remove it in `Dispose`.
- Tests exercise local persistence directly without network or external dependencies.
- Async tests return `Task`; production async methods accept an optional `CancellationToken`.
- Assertions check both primary results and secondary filesystem/persistence state.

## .NET platform detection

- SDK version: `10.0.203`.
- Project system: SDK-style (`<Project Sdk="Microsoft.NET.Sdk">`).
- Target framework: `net10.0`.
- Test framework: xUnit 2.9.3 with `xunit.runner.visualstudio` 3.1.1.
- `dotnet test` mode: VSTest (SDK 10, no root `global.json` selecting the MTP runner).
- Executed test platform: VSTest (`Microsoft.NET.Test.Sdk` is present; no MTP runner or bridge setting is configured).
- Detection-file audit: no `global.json`, `packages.config`, `Directory.Build.props`, or `Directory.Packages.props` was found; `tests/LanTransfer.Tests/LanTransfer.Tests.csproj` contains the decisive settings.

## Static source-to-test pairing

Previously observed Find-UntestedSources result (consumed without rerunning):

- 19 source files
- 3 test files
- 12 untested source files
- 7 paired source files
- `LocalFileStorage` is paired with `tests/LanTransfer.Tests/LocalFileStorageTests.cs`.
- `LocalTextMessageStore` is paired with `tests/LanTransfer.Tests/LocalTextMessageStoreTests.cs`.

This is a static symbol-pairing heuristic, not evidence of line or branch coverage. It establishes the existing test locations but does not prove deletion behavior is covered.

## Behavioral and risk notes

- File deletion must use the same safe lookup path validation as `GetAsync`/`OpenReadAsync`, so traversal input cannot move an arbitrary file into trash.
- The trash directory is nested under `.lantransfer`; the current top-level file listing already enumerates only the storage root, so trashed files should disappear from `ListAsync`.
- A move preserves bytes and avoids permanent deletion. The test should verify both source absence and destination content, not only the returned boolean.
- A trash filename collision policy is not specified. The implementation must preserve both files rather than overwrite or throw; the test plan treats collision-safe trash naming as required data-loss protection.
- Text deletion must execute under the existing semaphore and use the existing atomic write path. Tests should observe the store via `ListAsync` and a newly opened store to prove persistence.
- A missing ID/file is an expected no-op represented by `false`, not an exception.

