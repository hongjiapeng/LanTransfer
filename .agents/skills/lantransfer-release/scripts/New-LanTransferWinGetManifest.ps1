[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [string] $InstallerUrl,

    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string] $InstallerSha256,

    [string] $ReleaseDate = (Get-Date).ToString('yyyy-MM-dd'),

    [switch] $Validate,

    [switch] $Offline,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-YamlString {
    param([Parameter(Mandatory = $true)][string] $Value)

    $escaped = $Value.Replace("'", "''")
    return "'$escaped'"
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string[]] $Lines
    )

    $content = ($Lines -join [Environment]::NewLine) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($Path, $content, [System.Text.UTF8Encoding]::new($false))
}

$packageIdentifier = 'JiaPeng.LanTransfer'
$manifestVersion = '1.12.0'
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

if ([string]::IsNullOrWhiteSpace($InstallerUrl)) {
    $InstallerUrl = "https://github.com/hongjiapeng/LanTransfer/releases/download/v$Version/LanTransfer-$Version-win-x64-Setup.exe"
}

$uri = $null
if (-not [System.Uri]::TryCreate($InstallerUrl, [System.UriKind]::Absolute, [ref] $uri) -or $uri.Scheme -ne 'https') {
    throw "InstallerUrl must be an absolute HTTPS URL: $InstallerUrl"
}

if ([string]::IsNullOrWhiteSpace($InstallerSha256)) {
    if ($Offline) {
        throw 'Offline mode requires InstallerSha256.'
    }

    $temporaryFile = [System.IO.Path]::GetTempFileName()
    try {
        Write-Host "Downloading installer to calculate SHA256: $InstallerUrl"
        Invoke-WebRequest -Uri $InstallerUrl -OutFile $temporaryFile -UseBasicParsing
        $InstallerSha256 = (Get-FileHash -LiteralPath $temporaryFile -Algorithm SHA256).Hash
    }
    finally {
        if (Test-Path -LiteralPath $temporaryFile) {
            Remove-Item -LiteralPath $temporaryFile -Force
        }
    }
}

$InstallerSha256 = $InstallerSha256.Trim().ToUpperInvariant()
if ($InstallerSha256 -notmatch '^[0-9A-F]{64}$') {
    throw 'InstallerSha256 must be a 64-character hexadecimal SHA256 value.'
}

$parsedReleaseDate = [datetime]::MinValue
if (-not [datetime]::TryParseExact(
        $ReleaseDate,
        'yyyy-MM-dd',
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::None,
        [ref] $parsedReleaseDate)) {
    throw "ReleaseDate must use yyyy-MM-dd: $ReleaseDate"
}

if (-not (Test-Path -LiteralPath $resolvedOutputDirectory)) {
    New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null
}

$releaseNotesUrl = "https://github.com/hongjiapeng/LanTransfer/releases/tag/v$Version"
$files = [ordered]@{
    "$packageIdentifier.yaml" = @(
        '# yaml-language-server: $schema=https://aka.ms/winget-manifest.version.1.12.0.schema.json',
        '',
        "PackageIdentifier: $(ConvertTo-YamlString $packageIdentifier)",
        "PackageVersion: $(ConvertTo-YamlString $Version)",
        'DefaultLocale: en-US',
        'ManifestType: version',
        "ManifestVersion: $(ConvertTo-YamlString $manifestVersion)"
    )
    "$packageIdentifier.installer.yaml" = @(
        '# yaml-language-server: $schema=https://aka.ms/winget-manifest.installer.1.12.0.schema.json',
        '',
        "PackageIdentifier: $(ConvertTo-YamlString $packageIdentifier)",
        "PackageVersion: $(ConvertTo-YamlString $Version)",
        "InstallerType: $(ConvertTo-YamlString 'inno')",
        'InstallModes:',
        "- $(ConvertTo-YamlString 'silent')",
        "- $(ConvertTo-YamlString 'silentWithProgress')",
        'InstallerSwitches:',
        "  Silent: $(ConvertTo-YamlString '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART')",
        "  SilentWithProgress: $(ConvertTo-YamlString '/SILENT /SUPPRESSMSGBOXES /NORESTART')",
        "ReleaseDate: $(ConvertTo-YamlString $ReleaseDate)",
        'Installers:',
        '- Architecture: x64',
        "  InstallerUrl: $(ConvertTo-YamlString $InstallerUrl)",
        "  InstallerSha256: $InstallerSha256",
        'ManifestType: installer',
        "ManifestVersion: $(ConvertTo-YamlString $manifestVersion)"
    )
    "$packageIdentifier.locale.en-US.yaml" = @(
        '# yaml-language-server: $schema=https://aka.ms/winget-manifest.defaultLocale.1.12.0.schema.json',
        '',
        "PackageIdentifier: $(ConvertTo-YamlString $packageIdentifier)",
        "PackageVersion: $(ConvertTo-YamlString $Version)",
        "PackageLocale: $(ConvertTo-YamlString 'en-US')",
        "Publisher: $(ConvertTo-YamlString 'JiaPeng')",
        "PublisherUrl: $(ConvertTo-YamlString 'https://github.com/hongjiapeng')",
        "PublisherSupportUrl: $(ConvertTo-YamlString 'https://github.com/hongjiapeng/LanTransfer/issues')",
        "PackageName: $(ConvertTo-YamlString 'LanTransfer')",
        "PackageUrl: $(ConvertTo-YamlString 'https://github.com/hongjiapeng/LanTransfer')",
        "License: $(ConvertTo-YamlString 'MIT')",
        "LicenseUrl: $(ConvertTo-YamlString 'https://github.com/hongjiapeng/LanTransfer/blob/main/LICENSE')",
        "ShortDescription: $(ConvertTo-YamlString 'Transfer files and text between devices on the same local network through a browser.')",
        "Description: $(ConvertTo-YamlString 'LanTransfer is a cross-platform local-network file and text transfer tool with a browser-based interface, QR-code connection, and no cloud dependency.')",
        "Moniker: $(ConvertTo-YamlString 'lantransfer')",
        'Tags:',
        "- $(ConvertTo-YamlString 'file-transfer')",
        "- $(ConvertTo-YamlString 'lan')",
        "- $(ConvertTo-YamlString 'local-network')",
        "- $(ConvertTo-YamlString 'qr-code')",
        "- $(ConvertTo-YamlString 'text-transfer')",
        "ReleaseNotesUrl: $(ConvertTo-YamlString $releaseNotesUrl)",
        'ManifestType: defaultLocale',
        "ManifestVersion: $(ConvertTo-YamlString $manifestVersion)"
    )
    "$packageIdentifier.locale.zh-CN.yaml" = @(
        '# yaml-language-server: $schema=https://aka.ms/winget-manifest.locale.1.12.0.schema.json',
        '',
        "PackageIdentifier: $(ConvertTo-YamlString $packageIdentifier)",
        "PackageVersion: $(ConvertTo-YamlString $Version)",
        "PackageLocale: $(ConvertTo-YamlString 'zh-CN')",
        "Publisher: $(ConvertTo-YamlString 'JiaPeng')",
        "PublisherUrl: $(ConvertTo-YamlString 'https://github.com/hongjiapeng')",
        "PublisherSupportUrl: $(ConvertTo-YamlString 'https://github.com/hongjiapeng/LanTransfer/issues')",
        "PackageName: $(ConvertTo-YamlString 'LanTransfer')",
        "PackageUrl: $(ConvertTo-YamlString 'https://github.com/hongjiapeng/LanTransfer')",
        "License: $(ConvertTo-YamlString 'MIT')",
        "LicenseUrl: $(ConvertTo-YamlString 'https://github.com/hongjiapeng/LanTransfer/blob/main/LICENSE')",
        "ShortDescription: $(ConvertTo-YamlString '通过浏览器在同一局域网的设备之间传输文件和文字。')",
        "Description: $(ConvertTo-YamlString 'LanTransfer 是一个跨平台局域网文件和文字传输工具，提供浏览器界面、二维码连接，并且不依赖云服务。')",
        'Tags:',
        "- $(ConvertTo-YamlString '二维码')",
        "- $(ConvertTo-YamlString '局域网')",
        "- $(ConvertTo-YamlString '文件传输')",
        "- $(ConvertTo-YamlString '文字传输')",
        "ReleaseNotesUrl: $(ConvertTo-YamlString $releaseNotesUrl)",
        'ManifestType: locale',
        "ManifestVersion: $(ConvertTo-YamlString $manifestVersion)"
    )
}

foreach ($fileName in $files.Keys) {
    $targetPath = Join-Path $resolvedOutputDirectory $fileName
    if ((Test-Path -LiteralPath $targetPath) -and -not $Force) {
        throw "Refusing to overwrite '$targetPath'. Use -Force after verifying the target directory."
    }
}

foreach ($fileName in $files.Keys) {
    Write-Utf8File -Path (Join-Path $resolvedOutputDirectory $fileName) -Lines $files[$fileName]
}

if ($Validate) {
    $wingetCommand = Get-Command winget -ErrorAction SilentlyContinue
    if ($null -eq $wingetCommand) {
        throw 'winget was not found. Install or repair Windows App Installer before validation.'
    }

    & $wingetCommand.Source validate --manifest $resolvedOutputDirectory --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw "winget validate failed with exit code $LASTEXITCODE."
    }
}

[pscustomobject]@{
    PackageIdentifier = $packageIdentifier
    PackageVersion = $Version
    OutputDirectory = $resolvedOutputDirectory
    InstallerUrl = $InstallerUrl
    InstallerSha256 = $InstallerSha256
    Validated = [bool] $Validate
} | ConvertTo-Json -Depth 4
