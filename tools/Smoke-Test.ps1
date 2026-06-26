param(
    [switch]$SkipLaunch
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $projectRoot "VisualRuleSystem.Polytoria.slnx"

Push-Location $projectRoot
try {
    dotnet build $solutionPath --no-restore -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed with exit code $LASTEXITCODE."
    }

    dotnet test $solutionPath --no-build -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Solution tests failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipLaunch) {
        & (Join-Path $projectRoot "tools\Build-Launcher.ps1")
        if ($LASTEXITCODE -ne 0) {
            throw "Launcher build failed with exit code $LASTEXITCODE."
        }

        $launcherPath = Join-Path $projectRoot "VisualRuleSystem.exe"

        # The launcher is the user-facing entrypoint. It publishes the latest
        # app into publish/current and then exits after starting Vrs.App.dll.
        $launcher = Start-Process -FilePath $launcherPath `
            -WorkingDirectory $projectRoot `
            -PassThru

        $currentRuntime = Join-Path $projectRoot "publish\current"
        $appDll = Join-Path $currentRuntime "Vrs.App.dll"
        $appPidFile = Join-Path $currentRuntime "vrs-app.pid"
        $legacyExe = Join-Path $projectRoot "publish\win-x64\Vrs.App.exe"

        # Publishing after source changes can take longer than the old fixed
        # sleep. Wait for the launcher to finish, then poll the app process.
        if (-not $launcher.WaitForExit(180000)) {
            Stop-Process -Id $launcher.Id -Force
            throw "VisualRuleSystem launcher did not exit within 180 seconds."
        }

        if (-not (Test-Path $appDll)) {
            throw "VisualRuleSystem launcher did not publish publish/current/Vrs.App.dll."
        }

        if (Test-Path $legacyExe) {
            throw "Legacy publish/win-x64/Vrs.App.exe still exists."
        }

        if (-not (Test-Path $appPidFile)) {
            throw "VisualRuleSystem launcher did not write publish/current/vrs-app.pid."
        }

        $appProcess = $null
        $appProcessId = 0
        $deadline = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $deadline) {
            $rawPid = Get-Content -Raw -Path $appPidFile
            if ([int]::TryParse($rawPid.Trim(), [ref]$appProcessId)) {
                $appProcess = Get-Process -Id $appProcessId -ErrorAction SilentlyContinue
                if ($appProcess -and $appProcess.ProcessName -eq "dotnet") {
                    break
                }
            }

            Start-Sleep -Milliseconds 500
        }

        if (-not $appProcess -or $appProcess.ProcessName -ne "dotnet") {
            throw "VisualRuleSystem launcher did not start the published app dotnet process."
        }

        Stop-Process -Id $appProcessId -Force

        Write-Host "Smoke launcher passed. Vrs.App dotnet PID $appProcessId was stopped."
    }
}
finally {
    Pop-Location
}
