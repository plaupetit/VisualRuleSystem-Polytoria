[CmdletBinding()]
param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$addonRoot = Join-Path $projectRoot "addons\visual-programming-bridge"
$sourcePath = Join-Path $addonRoot "src\visual_programming_bridge.server.luau"
$packagePath = Join-Path $addonRoot "package\VisualProgrammingBridge.ptaddon"
$scriptEntryName = "scripts/visual_programming_bridge.server.luau"
$addonMetaEntryName = "addonmeta.json"
$addonDisplayName = "[VRS]Visual Programming Bridge"

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Addon source was not found: $sourcePath"
}

if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "Addon package template was not found: $packagePath"
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Read-ZipTextEntry {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive]$Zip,
        [Parameter(Mandatory = $true)]
        [string]$EntryName
    )

    $entry = $Zip.GetEntry($EntryName)
    if ($null -eq $entry) {
        return $null
    }

    $reader = [System.IO.StreamReader]::new($entry.Open())
    try {
        return $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}

function Write-ZipTextEntry {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive]$Zip,
        [Parameter(Mandatory = $true)]
        [string]$EntryName,
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $existing = $Zip.GetEntry($EntryName)
    if ($null -ne $existing) {
        $existing.Delete()
    }

    $entry = $Zip.CreateEntry($EntryName, [System.IO.Compression.CompressionLevel]::Optimal)
    $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.Encoding]::UTF8)
    try {
        $writer.Write($Text)
    }
    finally {
        $writer.Dispose()
    }
}

$stream = [System.IO.File]::Open($packagePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite)
$zip = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    $metaText = Read-ZipTextEntry -Zip $zip -EntryName $addonMetaEntryName
    if ([string]::IsNullOrWhiteSpace($metaText)) {
        throw "Package metadata entry is missing or empty: $addonMetaEntryName"
    }

    $meta = $metaText | ConvertFrom-Json
    $nextMeta = [ordered]@{
        Name = $addonDisplayName
        Description = [string]$meta.Description
        Author = [string]$meta.Author
        Version = [string]$meta.Version
    }
    $nextMetaText = ($nextMeta | ConvertTo-Json -Depth 4)
    Write-ZipTextEntry -Zip $zip -EntryName $addonMetaEntryName -Text ($nextMetaText + "`n")

    $existingScript = $zip.GetEntry($scriptEntryName)
    if ($null -ne $existingScript) {
        $existingScript.Delete()
    }

    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip,
        $sourcePath,
        $scriptEntryName,
        [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}
finally {
    $zip.Dispose()
    $stream.Dispose()
}

Write-Host "Addon package rebuilt: $packagePath"

if ($Install) {
    $installPath = Join-Path $env:APPDATA "PolytoriaClient\creator\addons\VisualProgrammingBridge.ptaddon"
    $installDir = Split-Path -Parent $installPath
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    if (Test-Path -LiteralPath $installPath) {
        $stamp = Get-Date -Format "yyyyMMddHHmmss"
        Copy-Item -LiteralPath $installPath -Destination ($installPath + ".pre-vrs-build-$stamp.bak") -Force
    }

    Copy-Item -LiteralPath $packagePath -Destination $installPath -Force
    Write-Host "Addon package installed: $installPath"
    Write-Host "Restart Polytoria Creator to reload the addon."
}
