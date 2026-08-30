[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ArtifactDirectory,

    [Parameter(Mandatory)]
    [string]$ReleaseTag,

    [Parameter(Mandatory)]
    [string]$Repository,

    [Parameter(Mandatory)]
    [string]$OutputManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$expectedArtifacts = [ordered]@{
    'LetterboxdSync.zip' = 'Jellyfin.Plugin.LetterboxdSync.dll'
    'LetterboxdRatings.zip' = 'Jellyfin.Plugin.LetterboxdRatings.dll'
    'LetterboxdWatchedSync.zip' = 'Jellyfin.Plugin.LetterboxdWatchedSync.dll'
}

function Get-ValidatedArtifactChecksum {
    param([string]$Name, [string]$ExpectedEntry)

    $path = Join-Path $ArtifactDirectory $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing release artifact: $Name"
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') })
        if ($entries.Count -ne 1 -or $entries[0].FullName -ne $ExpectedEntry) {
            $actual = $entries.FullName -join ', '
            throw "Unexpected contents in $Name. Expected only $ExpectedEntry; found $actual"
        }
    }
    finally {
        $archive.Dispose()
    }

    return (Get-FileHash -LiteralPath $path -Algorithm MD5).Hash.ToLowerInvariant()
}

$checksums = @{}
foreach ($artifact in $expectedArtifacts.GetEnumerator()) {
    $checksums[$artifact.Key] = Get-ValidatedArtifactChecksum -Name $artifact.Key -ExpectedEntry $artifact.Value
}

$sourceManifestPath = Join-Path $PSScriptRoot '..' 'manifest.json'
$sourceManifest = Get-Content -LiteralPath $sourceManifestPath -Raw | ConvertFrom-Json -AsHashtable
$artifactByPlugin = @{
    'Letterboxd Watchlist Sync' = 'LetterboxdSync.zip'
    'Letterboxd Ratings' = 'LetterboxdRatings.zip'
    'Letterboxd Watched Sync' = 'LetterboxdWatchedSync.zip'
}
$projectByPlugin = @{
    'Letterboxd Watchlist Sync' = Join-Path $PSScriptRoot '..' 'LetterboxdSync' 'LetterboxdSync.csproj'
    'Letterboxd Ratings' = Join-Path $PSScriptRoot '..' 'LetterboxdRatings' 'LetterboxdRatings.csproj'
    'Letterboxd Watched Sync' = Join-Path $PSScriptRoot '..' 'LetterboxdWatchedSync' 'LetterboxdWatchedSync.csproj'
}

function Get-PluginVersion {
    param([string]$PluginName)

    $projectPath = $projectByPlugin[$PluginName]
    if (-not $projectPath -or -not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Missing project file for $PluginName."
    }

    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $version = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Missing <Version> in $projectPath."
    }

    return [string]$version
}

$releaseManifest = foreach ($plugin in $sourceManifest) {
    $artifact = $artifactByPlugin[$plugin.name]
    if (-not $artifact) {
        throw "No release artifact mapping for catalog plugin '$($plugin.name)'."
    }

    # A release manifest only advertises artifacts this run produced. This avoids
    # retaining historical entries that point at mutable main-branch binaries.
    $latest = $plugin.versions | Select-Object -First 1
    $version = Get-PluginVersion -PluginName $plugin.name
    [ordered]@{
        category = $plugin.category
        guid = $plugin.guid
        name = $plugin.name
        description = $plugin.description
        owner = $plugin.owner
        overview = $plugin.overview
        versions = @([ordered]@{
            version = $version
            targetAbi = $latest.targetAbi
            sourceUrl = "https://github.com/$Repository/releases/download/$ReleaseTag/$artifact"
            checksum = $checksums[$artifact]
            changelog = $latest.changelog
            timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')
        })
    }
}

$outputDirectory = Split-Path -Parent $OutputManifest
if ($outputDirectory) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}
$releaseManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputManifest -Encoding utf8NoBOM

Write-Host "Validated $($checksums.Count) release ZIPs and wrote $OutputManifest."
Write-Host 'Promote this generated manifest to the repository root in a reviewed follow-up commit after the GitHub Release is published.'
