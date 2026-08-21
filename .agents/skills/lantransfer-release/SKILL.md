---
name: lantransfer-release
description: Publish LanTransfer GitHub Releases and WinGet package updates. Use when Codex needs to recommend a release version, preview a release, create and push a v*.*.* tag, trigger GitHub Actions assets, submit or update the JiaPeng.LanTransfer WinGet manifest, or explain/fix the LanTransfer release workflow.
---

# LanTransfer Release

## Overview

Use this skill to release LanTransfer from the repository root by calling `scripts/release.ps1`. The script is the source of truth for local GitHub Release automation; do not reimplement tag creation, test execution, or pushing by hand unless the script is missing or broken.

For WinGet submission or updates, read [references/winget-publish.md](references/winget-publish.md) and follow it after the GitHub Release assets are available.

GitHub Release publication and WinGet catalog publication are separate state transitions. Establish the current state of both before choosing a version or creating a tag, manifest, branch, or PR.

## Release State Preflight

1. Inspect local and remote release state before interpreting a generic request such as "publish":
   - `git status --short --branch`
   - `git tag --list "v*" --sort=-version:refname`
   - `gh release list --repo hongjiapeng/LanTransfer --limit 10`
2. When WinGet is in scope, also run the fixed-identity checks in [references/winget-publish.md](references/winget-publish.md) before choosing the target version.
3. If the latest GitHub version is already present in WinGet and the user did not name a newer version, report that it is already live and ask for the intended next SemVer. Do not resubmit the same version and do not invent a second package identifier.
4. Use precise state language:
   - A pushed tag is not yet a completed GitHub Release.
   - An open WinGet PR is submitted, not published.
   - A version is live in WinGet only after the PR is merged, source propagation completes, and `winget show --id JiaPeng.LanTransfer --exact --versions` lists it.

## Version Choice

- Prefer SemVer tags in the form `vMAJOR.MINOR.PATCH`.
- Accept user input as either `0.1.0` or `v0.1.0`; the script normalizes it to `v0.1.0`.
- For the first public preview of LanTransfer, recommend `v0.1.0` unless existing tags or user intent suggest a different version.
- Use `v0.1.1` for bug-fix-only follow-ups, `v0.2.0` for meaningful feature additions before stability, and `v1.0.0` only when the project is ready to be presented as stable.
- Avoid four-part tags such as `v0.1.0.0` for GitHub Releases. Four-part versions are appropriate for Windows/.NET file versions, not release tags.

## GitHub Release Workflow

1. Confirm the repository context:
   - Run `git remote -v` if the target GitHub repository matters.
   - Run `git status --short --branch` and treat uncommitted changes as a release blocker unless the user explicitly wants to release from a dirty tree.
   - Check existing tags with `git tag --list "v*"` when choosing or validating a version.
   - Inspect `git log --graph --oneline --decorate --all` and verify the release commit is a descendant of the previous release tag. If `main` contains the previous version bump while `dev` contains the new features, merge the intended feature branch into `main` before tagging; do not create a divergent release history.
   - Keep the default version metadata aligned with the new release in both project files, the Inno Setup default, the local installer script, and README build examples. The workflow's `-p:Version` override does not replace source-level version hygiene.
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

5. If the user also requests WinGet, continue with [references/winget-publish.md](references/winget-publish.md) only after the exact versioned Windows asset exists and its SHA256 can be verified.

## Guardrails

- Do not pass `-AllowDirty` for a real release unless the user explicitly requests it and understands the risk.
- Do not pass `-SkipTests` for a real release unless the user explicitly requests it.
- Do not manually create lightweight tags for normal releases; use the script so annotated tags are created consistently.
- If `scripts/release.ps1` is missing, stop and explain that the skill depends on that script instead of inventing a parallel process.
- If the local remote points at `PhoneControlKit` but the user expects `LanTransfer`, call out the repository mismatch before releasing.
- Keep the LanTransfer WinGet package identifier stable; do not create a second identifier for a version update.
- Treat an existing GitHub Release, accepted WinGet version, or open PR for the target version as a hard duplicate-submission stop.
- Never sign the Microsoft CLA or make ownership declarations for the user; stop and ask the user to complete those human gates personally.
