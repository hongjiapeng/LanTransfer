# LanTransfer WinGet Publishing

Use this reference only when the user requests WinGet publication or an update in addition to the GitHub Release.

## Fixed package identity

- Repository: `microsoft/winget-pkgs`
- User fork: `hongjiapeng/winget-pkgs`
- Package identifier: `JiaPeng.LanTransfer`
- Manifest path: `manifests/j/JiaPeng/LanTransfer/<version>/`
- Current installer choice: Inno Setup (`installerType: inno`)
- GitHub asset pattern: `LanTransfer-<version>-win-x64-Setup.exe`

The identifier is already accepted by WinGet. Preserve its casing and do not create a new identifier for later versions.

## Preconditions

1. Complete the GitHub Release first using `scripts/release.ps1`.
2. Wait until the Windows installer asset is present on the GitHub Release page.
3. Download the exact asset URL, calculate SHA256, and inspect its Windows product version.
4. Confirm the release is public, immutable, licensed under MIT, and linked over HTTPS.
5. Check the existing package and open `winget-pkgs` PRs for conflicts before creating a new version directory.

For release version `<version>`, the expected asset is:

```text
https://github.com/hongjiapeng/LanTransfer/releases/download/v<version>/LanTransfer-<version>-win-x64-Setup.exe
```

Do not hard-code a future hash. Recalculate it from the final asset every release.

## Manifest shape

Generate four files with the bundled `scripts/New-LanTransferWinGetManifest.ps1` script. This keeps the LanTransfer workflow self-contained; do not require the separate `winget-package-publisher` skill to be installed.

```text
JiaPeng.LanTransfer.yaml
JiaPeng.LanTransfer.installer.yaml
JiaPeng.LanTransfer.locale.en-US.yaml
JiaPeng.LanTransfer.locale.zh-CN.yaml
```

Use these stable metadata values unless the product metadata intentionally changes:

- Publisher: `JiaPeng`
- Package name: `LanTransfer`
- License: `MIT`
- Package URL: `https://github.com/hongjiapeng/LanTransfer`
- Support URL: `https://github.com/hongjiapeng/LanTransfer/issues`
- Moniker: `lantransfer`
- Locales: `en-US` and `zh-CN`

For each release, set `PackageVersion` to the SemVer version without the leading `v`, update `ReleaseNotesUrl`, `ReleaseDate`, installer URL, and installer SHA256. Use the Inno Setup installer rather than the portable ZIP unless the user explicitly requests a different WinGet installation mode.

Use the verified Inno Setup switches:

```yaml
InstallerType: inno
InstallModes:
- silent
- silentWithProgress
InstallerSwitches:
  Silent: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
  SilentWithProgress: /SILENT /SUPPRESSMSGBOXES /NORESTART
```

Do not add `Scope`, `Commands`, `NestedInstallerType`, or `ArchiveBinariesDependOnPath` to the Inno installer manifest; those fields belong to the portable ZIP variant.

## Generate and validate from an isolated clone

Do not modify the LanTransfer source repository with `winget-pkgs` files. Use a separate sparse clone or worktree based on the latest upstream `master`:

```powershell
$wingetRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('winget-pkgs-lantransfer-' + [guid]::NewGuid().ToString('N'))
git clone --filter=blob:none --sparse https://github.com/microsoft/winget-pkgs.git $wingetRoot
git -C $wingetRoot remote add user git@github.com:hongjiapeng/winget-pkgs.git
git -C $wingetRoot fetch origin master
git -C $wingetRoot switch -c codex/winget-jiaPeng-lantransfer-<version> origin/master
git -C $wingetRoot sparse-checkout set --no-cone 'manifests/j/JiaPeng/LanTransfer'
```

Supply the already verified installer hash and run the bundled generator in offline mode:

```powershell
$output = Join-Path $wingetRoot 'manifests\j\JiaPeng\LanTransfer\<version>'
& '<lantransfer-release-skill>\scripts\New-LanTransferWinGetManifest.ps1' `
  -Version '<version>' `
  -OutputDirectory $output `
  -ReleaseDate '<yyyy-MM-dd>' `
  -InstallerSha256 '<sha256>' `
  -Offline `
  -Validate
```

Treat every validation warning as actionable. Inspect the generated YAML and run a whitespace check before staging:

```powershell
git -C $wingetRoot diff --check
git -C $wingetRoot add --sparse manifests/j/JiaPeng/LanTransfer/<version>
git -C $wingetRoot diff --cached --check
```

## Commit, PR, and human gates

Create one focused commit containing only the new version directory, then push the branch to the user's fork:

```powershell
git -C $wingetRoot commit -m "Update JiaPeng.LanTransfer to <version>"
git -C $wingetRoot push -u user codex/winget-jiaPeng-lantransfer-<version>
gh pr create `
  --repo microsoft/winget-pkgs `
  --head 'hongjiapeng:codex/winget-jiaPeng-lantransfer-<version>' `
  --base master `
  --title 'Update: JiaPeng.LanTransfer to <version>'
```

The PR body should include the asset URL, SHA256, `winget validate` result, and any local installation-test limitation. Monitor GitHub checks and the linked WinGet validation pipeline separately. The PR is not published to the WinGet source until Microsoft validation and review complete.

Never accept the CLA, answer ownership declarations, solve a CAPTCHA, or approve an elevation prompt for the user. Ask the user to complete those steps personally when requested by GitHub or Microsoft.

## Installer test limitation

Prefer a clean user context, Windows Sandbox, or disposable VM for a full silent install/upgrade/uninstall test. LanTransfer's installer detects an already running LanTransfer instance; do not force-close the user's existing app. If that instance prevents a local test, report the limitation and rely on asset inspection, `winget validate`, and successful GitHub Actions output until the user can close the app or a clean test environment is available.

## After merge

After the PR is merged and the package source has propagated, verify:

```powershell
winget show --id JiaPeng.LanTransfer --exact
winget install --id JiaPeng.LanTransfer --exact
```

Do not claim the WinGet update is live while the PR is only open or while source propagation is pending.
