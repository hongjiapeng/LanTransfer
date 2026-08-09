---
name: lantransfer-release
description: Publish LanTransfer versions through the repository release script. Use when Codex needs to recommend a release version, preview a release, create and push a v*.*.* tag, trigger GitHub Actions Releases, or explain/fix the LanTransfer release workflow that uses scripts/release.ps1.
---

# LanTransfer Release

## Overview

Use this skill to release LanTransfer from the repository root by calling `scripts/release.ps1`. The script is the source of truth for local release automation; do not reimplement tag creation, test execution, or pushing by hand unless the script is missing or broken.

## Version Choice

- Prefer SemVer tags in the form `vMAJOR.MINOR.PATCH`.
- Accept user input as either `0.1.0` or `v0.1.0`; the script normalizes it to `v0.1.0`.
- For the first public preview of LanTransfer, recommend `v0.1.0` unless existing tags or user intent suggest a different version.
- Use `v0.1.1` for bug-fix-only follow-ups, `v0.2.0` for meaningful feature additions before stability, and `v1.0.0` only when the project is ready to be presented as stable.
- Avoid four-part tags such as `v0.1.0.0` for GitHub Releases. Four-part versions are appropriate for Windows/.NET file versions, not release tags.

## Workflow

1. Confirm the repository context:
   - Run `git remote -v` if the target GitHub repository matters.
   - Run `git status --short --branch` and treat uncommitted changes as a release blocker unless the user explicitly wants to release from a dirty tree.
   - Check existing tags with `git tag --list "v*"` when choosing or validating a version.
2. If the user wants a preview, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release.ps1 0.1.0 -DryRun -AllowDirty -SkipTests
```

Use `-AllowDirty` only for dry runs while preparing local changes.

3. For a real release, first make sure all intended changes are committed and pushed. Then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\release.ps1 0.1.0
```

4. After the tag push succeeds, GitHub Actions creates the release assets from `.github/workflows/release.yml`. If the user asks to verify the remote release, inspect the GitHub Actions run or GitHub Releases page with the available GitHub/CLI tools.

## Guardrails

- Do not pass `-AllowDirty` for a real release unless the user explicitly requests it and understands the risk.
- Do not pass `-SkipTests` for a real release unless the user explicitly requests it.
- Do not manually create lightweight tags for normal releases; use the script so annotated tags are created consistently.
- If `scripts/release.ps1` is missing, stop and explain that the skill depends on that script instead of inventing a parallel process.
- If the local remote points at `PhoneControlKit` but the user expects `LanTransfer`, call out the repository mismatch before releasing.
