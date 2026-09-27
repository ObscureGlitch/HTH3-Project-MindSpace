$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'TherapyGame'
$target = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$hud = Join-Path $target 'Runtime\WellnessHud.cs'
$before = Join-Path $PSScriptRoot 'PresageVisualPatch\WellnessHud.before.cs'
if ((Get-FileHash -LiteralPath $hud).Hash -ne (Get-FileHash -LiteralPath $before).Hash) {
    throw 'The live HUD changed after the patch was prepared. Merge those changes before installing.'
}
$files = [ordered]@{
    'Runtime\WellnessHud.cs' = (Join-Path $PSScriptRoot 'PresageVisualPatch\WellnessHud.cs')
    'Runtime\PresagePositionOverlay.cs' = (Join-Path $source 'Runtime\PresagePositionOverlay.cs')
    'Runtime\PresageUiRaster.cs' = (Join-Path $source 'Runtime\PresageUiRaster.cs')
    'Runtime\WellnessPulseTrace.cs' = (Join-Path $source 'Runtime\WellnessPulseTrace.cs')
    'Documentation\PresageBiometricCoaching.md' = (Join-Path $source 'Documentation\PresageBiometricCoaching.md')
}
foreach ($path in $files.Values) { if (!(Test-Path -LiteralPath $path)) { throw "Missing source: $path" } }
$backup = Join-Path $PSScriptRoot ('TherapyBackups\BeforePresageVisuals-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip')
$existing = @($files.Keys | ForEach-Object { Join-Path $target $_ } | Where-Object { Test-Path -LiteralPath $_ })
Compress-Archive -LiteralPath $existing -DestinationPath $backup
foreach ($relative in $files.Keys) {
    $destination = Join-Path $target $relative
    Copy-Item -LiteralPath $files[$relative] -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $files[$relative]).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
        throw "Installed file verification failed: $relative"
    }
}
"Installed and hash-verified $($files.Count) UI/documentation files."
"Backup: $backup"
'No camera, biometric session or Play mode was started.'
