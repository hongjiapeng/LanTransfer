# Deletion feature test plan

## Planned implementation phases

### Phase 1: public contracts and file deletion behavior

Extend `tests/LanTransfer.Tests/LocalFileStorageTests.cs` after the production contracts and implementation are added.

| Acceptance item | Planned test | Assertions |
| --- | --- | --- |
| `IFileStorage.DeleteAsync(string)` returns `bool`. | `FileStorageContracts_ExposeDeleteAsyncReturningBooleanTask` | Reflection finds `DeleteAsync(string, CancellationToken)` on `IFileStorage` and its return type is `Task<bool>`. |
| `IFileInbox.DeleteAsync(string)` returns `bool`. | `FileStorageContracts_ExposeDeleteAsyncReturningBooleanTask` | The same signature and return type are present on `IFileInbox`. |
| `LocalFileStorage` moves the file into `<storage>/.lantransfer/trash`. | `DeleteAsync_ExistingFile_MovesItToTrashAndReturnsTrue` | Result is `true`; source path is absent; trash directory contains one file; its bytes equal the original payload. |
| An existing deleted file is unavailable through active storage APIs. | `DeleteAsync_ExistingFile_MovesItToTrashAndReturnsTrue` | `ListAsync` excludes the file, `GetAsync` returns null, and `OpenReadAsync` returns null. |
| A missing file returns `false` without disturbing existing files. | `DeleteAsync_MissingFile_ReturnsFalseAndPreservesExistingFiles` | Result is `false`; the pre-existing control file remains readable/listed with unchanged content; no trash entry is created for the missing name. |
| File deletion retains filename/path traversal protection. | `DeleteAsync_PathLikeName_RejectsWithoutMovingOutsideFile` (theory) | Traversal variants throw `FileStorageException` with `ErrorCodes.InvalidFileName`; an outside control file remains in place; trash remains empty. |
| Trash moves do not overwrite an earlier same-name deletion. | `DeleteAsync_ReusedFileName_PreservesBothTrashEntries` | Delete, upload the same name again, delete again; both calls return `true`, two trash entries exist, and both distinct payloads are preserved. |

### Phase 2: text-message deletion behavior

Extend `tests/LanTransfer.Tests/LocalTextMessageStoreTests.cs` after the production contract and implementation are added.

| Acceptance item | Planned test | Assertions |
| --- | --- | --- |
| `ITextMessageStore.DeleteAsync(Guid)` returns `bool`. | `TextMessageStoreContract_ExposesDeleteAsyncReturningBooleanTask` | Reflection finds `DeleteAsync(Guid, CancellationToken)` on `ITextMessageStore` and its return type is `Task<bool>`. |
| `LocalTextMessageStore` removes only the requested record. | `DeleteAsync_ExistingMessage_RemovesOnlyTargetAndReturnsTrue` | Add target plus two controls; result is `true`; the target ID is absent; both control IDs/text values remain; count is exactly two. |
| Deletion of an existing message is persisted. | `DeleteAsync_ExistingMessage_PersistsRemoval` | Delete one of two messages, dispose/reopen the store, and assert only the non-target record remains. |
| A missing message returns `false` and preserves every record. | `DeleteAsync_MissingMessage_ReturnsFalseAndPreservesAllRecords` | Result is `false`; IDs, text values, and record count match the pre-delete snapshot. |

### Phase 3: validation and quality review

1. Run the narrow test project: `dotnet test tests/LanTransfer.Tests/LanTransfer.Tests.csproj`.
2. Run a full non-incremental workspace build after all implementation phases.
3. Re-open both changed test files and map every acceptance item above to the exact test and its assertions.
4. Run the broad-workflow `test-gap-analysis` and `assertion-quality` reviews, fix verified gaps, and record findings in `.testagent/status.md`.
5. Re-run the narrow test command to a clean exit after all fixes.

## Expected source edits in a later turn

No source edits are authorized in the current planning-only turn. Later implementation is expected to touch only:

- `src/LanTransfer.Core/Abstractions/IFileStorage.cs`
- `src/LanTransfer.Core/Abstractions/IFileInbox.cs`
- `src/LanTransfer.Core/Abstractions/ITextMessageStore.cs`
- `src/LanTransfer.Core/Services/LocalFileStorage.cs`
- `src/LanTransfer.Core/Services/LocalTextMessageStore.cs`
- `tests/LanTransfer.Tests/LocalFileStorageTests.cs`
- `tests/LanTransfer.Tests/LocalTextMessageStoreTests.cs`

Any HTTP endpoint or UI tests are outside this bounded storage-layer plan unless the feature scope is expanded.

