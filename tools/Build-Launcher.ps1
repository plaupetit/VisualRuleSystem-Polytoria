param(
    [switch]$SkipCopyToRoot
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$launcherProject = Join-Path $projectRoot "src\Vrs.Launcher\Vrs.Launcher.csproj"
$launcherPublish = Join-Path $projectRoot "publish\launcher"
$rootExe = Join-Path $projectRoot "VisualRuleSystem.exe"
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$tempObj = Join-Path $env:TEMP "vrs-launcher-obj-$stamp"
$tempBin = Join-Path $env:TEMP "vrs-launcher-bin-$stamp"

Push-Location $projectRoot
try {
    if (Test-Path $launcherPublish) {
        Remove-Item -LiteralPath $launcherPublish -Recurse -Force
    }

    dotnet publish $launcherProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:UseAppHost=true `
        -p:BaseIntermediateOutputPath="$tempObj\" `
        -p:BaseOutputPath="$tempBin\" `
        -o $launcherPublish

    if ($LASTEXITCODE -ne 0) {
        throw "Launcher publish failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipCopyToRoot) {
        $publishedExe = Join-Path $launcherPublish "VisualRuleSystem.exe"
        if (-not (Test-Path $publishedExe)) {
            throw "Launcher publish did not produce $publishedExe."
        }

        Copy-Item -LiteralPath $publishedExe -Destination $rootExe -Force
        Write-Host "Launcher ready: $rootExe"

        if (Test-Path $launcherPublish) {
            Remove-Item -LiteralPath $launcherPublish -Recurse -Force
        }
    }
}
finally {
    if (Test-Path $tempObj) {
        Remove-Item -LiteralPath $tempObj -Recurse -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $tempBin) {
        Remove-Item -LiteralPath $tempBin -Recurse -Force -ErrorAction SilentlyContinue
    }

    Pop-Location
}
