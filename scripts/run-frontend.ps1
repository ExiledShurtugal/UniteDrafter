param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\UniteDrafter.Frontend"

$frontendProcesses = Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq "UniteDrafter.Frontend.exe" -or
        ($_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*UniteDrafter.Frontend*")
    }

foreach ($process in $frontendProcesses) {
    Write-Host "Stopping existing frontend process $($process.ProcessId)..."
    Stop-Process -Id $process.ProcessId -Force
}

$arguments = @("run", "--project", $projectPath)
if ($NoBuild) {
    $arguments += "--no-build"
}

Write-Host "Starting frontend from $projectPath"
& dotnet @arguments
