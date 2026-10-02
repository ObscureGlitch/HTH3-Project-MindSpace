param([switch]$PlayModeStopped)
$ErrorActionPreference = 'Stop'
if (-not $PlayModeStopped) { throw 'Confirm Play mode is stopped before importing.' }
$stage = 'D:\Hack the Hill\Deliverables\KoiSchoolExpansion'
$project = 'D:\Unity\HTH3 Project'
$assets = Join-Path $project 'Assets\TherapyGame'
$baseline = Get-Content -LiteralPath "$stage\Baseline.json" -Raw | ConvertFrom-Json
foreach ($entry in $baseline) {
    if ((Get-FileHash -LiteralPath (Join-Path $assets $entry.File)).Hash -ne $entry.SHA256) { throw "Live file changed: $($entry.File). Rebase before copying." }
}
$newFile = 'Runtime\KoiSchoolLayout.cs'
if (Test-Path -LiteralPath (Join-Path $assets $newFile)) { throw 'Population helper already exists. Inspect before replacing.' }
$files = @($baseline.File) + @($newFile)
foreach ($file in $files) { if (-not (Test-Path -LiteralPath (Join-Path "$stage\Payload" $file))) { throw "Missing payload: $file" } }
$memory = Get-CimInstance Win32_OperatingSystem
if ($memory.FreeVirtualMemory -lt 2300000) { throw 'Memory headroom is low. Save and close Unity before importing.' }
$backup = Join-Path $project ('TherapyBackups\KoiPond\SchoolDeploy-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
foreach ($entry in $baseline) {
    $dest = Join-Path $backup $entry.File
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
    Copy-Item -LiteralPath (Join-Path $assets $entry.File) -Destination $dest
}
Copy-Item -LiteralPath "$assets\Scenes\TherapyRoom.unity" -Destination "$backup\TherapyRoom-saved-before-import.unity"
$manifest = foreach ($file in $files) {
    $source = Join-Path "$stage\Payload" $file
    $target = Join-Path $assets $file
    Copy-Item -LiteralPath $source -Destination $target
    $expected = (Get-FileHash -LiteralPath $source).Hash
    $actual = (Get-FileHash -LiteralPath $target).Hash
    if ($actual -ne $expected) { throw "Copy verification failed: $file" }
    [pscustomobject]@{File=$file; SHA256=$actual}
}
$result = [pscustomobject]@{Backup=$backup; Files=$manifest; SceneUpdate='Pending Therapy Game > Update Koi School'; CopiedAt=(Get-Date -Format o)}
[IO.File]::WriteAllText("$stage\Deployment.json", ($result | ConvertTo-Json -Depth 5))
$result | ConvertTo-Json -Depth 5
