<#
.SYNOPSIS
    Generates SoundBoard/VersionInfo.xml (the in-app update manifest) for a release tag.

.DESCRIPTION
    Single source of truth is the git tag (vX.Y.Z). This script derives everything the
    manifest needs from it:

      <Version>        X.Y.Z.0 (the same four-part version stamped into the assembly)
      <ReleaseDate>    today (UTC) unless -ReleaseDate is given
      <DownloadLink> / <Downloads><Link>
                       https://github.com/<Repository>/releases/download/<tag>/SoundBoard.exe
      <FileHash algorithm="SHA256">
                       SHA-256 of the built SoundBoard.exe (-ExePath). The updater
                       (Bluegrams AppHelpers UpdateCheckerBase.VerifyHash) refuses the
                       download if it does not match. The algorithm attribute is required:
                       the library defaults to MD5 when it is absent.
      <VersionNotes>   the "## [X.Y.Z]" section of CHANGELOG.md, flattened to " - item" lines

    It also writes the same changelog section as Markdown (-ReleaseNotesPath) for use as
    the GitHub Release body.

    Usage (locally, to preview):
      .\scripts\New-VersionInfo.ps1 -Tag v1.10.3 -ExePath SoundBoard\bin\Release\SoundBoard.exe -Repository micahmo/SoundBoard -OutputPath out.xml
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Tag,
    [Parameter(Mandatory)] [string] $ExePath,
    [string] $Repository = 'micahmo/SoundBoard',   # owner/name; must match SoundBoardRepository in Version.targets
    [string] $ChangelogPath = (Join-Path $PSScriptRoot '..\CHANGELOG.md'),
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\SoundBoard\VersionInfo.xml'),
    [string] $ReleaseNotesPath,
    [string] $ReleaseDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd'),
    [switch] $AllowMissingNotes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve paths against the PowerShell location, not the process cwd, and allow the target
# file (or a bare file name) to not exist yet.
function Resolve-FullPath([string] $Path) {
    return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}
$ExePath = Resolve-FullPath $ExePath
$ChangelogPath = Resolve-FullPath $ChangelogPath
$OutputPath = Resolve-FullPath $OutputPath
if ($ReleaseNotesPath) { $ReleaseNotesPath = Resolve-FullPath $ReleaseNotesPath }

if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    throw "Repository '$Repository' must be of the form owner/name"
}

# --- Parse the tag -------------------------------------------------------------------
if ($Tag -notmatch '^v(?<core>\d+\.\d+(?:\.\d+)?)(?<pre>[-.][0-9A-Za-z.-]+)?$') {
    throw "Tag '$Tag' is not of the form vX.Y[.Z][-prerelease]"
}
$core = $Matches.core
if ($core.Split('.').Count -eq 2) { $core = "$core.0" }
$version = "$core.0"
$isPrerelease = $Matches.ContainsKey('pre')

# --- Hash the executable -------------------------------------------------------------
if (-not (Test-Path -PathType Leaf $ExePath)) { throw "Executable not found: $ExePath" }
$hash = (Get-FileHash -Algorithm SHA256 -Path $ExePath).Hash.ToUpperInvariant()
$fileName = [System.IO.Path]::GetFileName($ExePath)
$link = "https://github.com/$Repository/releases/download/$Tag/$fileName"

# --- Pull the release notes from CHANGELOG.md --------------------------------------------
# Section header forms accepted: "## [1.10.3] - 2026-08-21", "## [1.10.3]", "## 1.10.3".
$notesMarkdown = ''
if (Test-Path -PathType Leaf $ChangelogPath) {
    $lines = Get-Content -Path $ChangelogPath -Encoding UTF8
    $wanted = $core
    $inSection = $false
    $section = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        # The trailing "[1.2.3]: https://..." link-reference block ends the last section.
        if ($inSection -and $line -match '^\[[^\]]+\]:\s*\S') { break }
        if ($line -match '^##\s+\[?(?<v>[^\]\s]+)\]?') {
            if ($inSection) { break }
            $v = $Matches.v
            if ($v -eq $wanted -or $v -eq $Tag.TrimStart('v') -or $v -eq $version) { $inSection = $true }
            continue
        }
        if ($inSection) { $section.Add($line) }
    }
    $notesMarkdown = (($section -join "`n").Trim())
}
if (-not $notesMarkdown) {
    $msg = "No '## [$core]' section found in $ChangelogPath. Rename the '## [Unreleased]' heading to '## [$core] - $ReleaseDate' before tagging."
    if ($AllowMissingNotes) { Write-Warning $msg } else { throw $msg }
}

# VersionNotes is a plain-text element; flatten Markdown: drop "### Added"-style headings
# and turn "- item" bullets into the " - item" form the existing manifests used.
$notesLines = New-Object System.Collections.Generic.List[string]
foreach ($line in ($notesMarkdown -split "`n")) {
    if ($line -match '^\s*#' -or $line.Trim() -eq '') { continue }
    if ($line -match '^\s*[-*]\s+(?<text>.*)$') {
        $notesLines.Add(" - " + $Matches.text.Trim())
    } elseif ($notesLines.Count -gt 0) {
        # Wrapped continuation of the previous bullet.
        $notesLines[$notesLines.Count - 1] += ' ' + $line.Trim()
    } else {
        $notesLines.Add($line.Trim())
    }
}
$notesText = $notesLines -join "`n"

# --- Emit the manifest -----------------------------------------------------------------
$esc = { param($s) [System.Security.SecurityElement]::Escape($s) }

$xml = @"
<?xml version="1.0" encoding="utf-8" ?>
<AppUpdate xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
           xsi:noNamespaceSchemaLocation="https://raw.githubusercontent.com/$Repository/master/SoundBoard/AppUpdate.xsd">
  <!-- GENERATED by scripts/New-VersionInfo.ps1 from tag $Tag - do not edit by hand. -->
  <Version>$version</Version>
  <ReleaseDate>$ReleaseDate</ReleaseDate>
  <!-- Default download -->
  <DownloadLink>$(& $esc $link)</DownloadLink>
  <DownloadFileName>$fileName</DownloadFileName>
  <!-- All download options -->
  <Downloads>
    <Download key="portable">
      <Link>$(& $esc $link)</Link>
      <FileName>$fileName</FileName>
      <FileHash algorithm="SHA256">$hash</FileHash>
    </Download>
  </Downloads>
  <!-- Release notes -->
  <VersionNotes>$(& $esc $notesText)</VersionNotes>
  <ReleaseNotes></ReleaseNotes>
</AppUpdate>
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutputPath, ($xml -replace "`r?`n", "`n"), $utf8NoBom)

if ($ReleaseNotesPath) {
    [System.IO.File]::WriteAllText($ReleaseNotesPath, $notesMarkdown + "`n", $utf8NoBom)
}

# Machine-readable summary for the workflow (and humans).
[pscustomobject]@{
    Tag          = $Tag
    Version      = $version
    IsPrerelease = $isPrerelease
    Sha256       = $hash
    DownloadLink = $link
    Output       = $OutputPath
}
