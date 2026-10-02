$ErrorActionPreference = 'Stop'
$performanceStaged = 'D:\Hack the Hill\Deliverables\TherapyGame'
$performanceLive = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$performanceBackup = Join-Path 'D:\Hack the Hill\Deliverables\TherapyBackups' ('Performance-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$performanceFiles = @(
    'Runtime\WellnessGardenPerformance.cs',
    'Runtime\PondWaveField.cs',
    'Runtime\WellnessPondWater.cs',
    'Runtime\WellnessHud.cs',
    'Runtime\WellnessUiTheme.cs',
    'Runtime\TherapyGame.Runtime.asmdef',
    'Documentation\PondWater.md',
    'Documentation\Performance.md'
)
$performanceHashes = @{}
foreach ($relative in $performanceFiles) {
    $source = Join-Path $performanceStaged $relative
    if (!(Test-Path -LiteralPath $source)) { throw "Missing staged source: $relative" }
    $live = Join-Path $performanceLive $relative
    if (Test-Path -LiteralPath $live) {
        $performanceHashes[$relative] = (Get-FileHash -LiteralPath $live).Hash
        $backup = Join-Path $performanceBackup $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
        Copy-Item -LiteralPath $live -Destination $backup
    }
}
$performanceHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $performanceBackup 'OriginalHashes.json')
foreach ($relative in $performanceHashes.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $performanceLive $relative)).Hash -ne $performanceHashes[$relative]) {
        throw "Live file changed during backup: $relative"
    }
}
foreach ($relative in $performanceFiles) {
    $target = Join-Path $performanceLive $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $performanceStaged $relative) -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath (Join-Path $performanceStaged $relative)).Hash) {
        throw "Copy verification failed: $relative"
    }
}
Write-Output "Installed and hash-verified $($performanceFiles.Count) files. Backup: $performanceBackup"
