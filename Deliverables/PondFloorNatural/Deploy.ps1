param([switch]$PlayModeStopped)
$ErrorActionPreference = 'Stop'
if (-not $PlayModeStopped) { throw 'Confirm Play mode is stopped before import.' }
$stage = 'D:\Hack the Hill\Deliverables\PondFloorNatural'
$project = 'D:\Unity\HTH3 Project'
$assets = Join-Path $project 'Assets\TherapyGame'
$files = @('Editor\TherapyNaturalPondFloor.cs','Editor\PondFloorSource\NaturalPondFloor.json','Documentation\NaturalPondFloor.md')
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path "$stage\Payload" $file))) { throw "Missing prepared file: $file" }
    if (Test-Path -LiteralPath (Join-Path $assets $file)) { throw "Target already exists; inspect before replacement: $file" }
}
if ((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2300000) { throw 'Memory headroom is low; save and close Unity before import.' }
$backup = Join-Path $project ('TherapyBackups\KoiPond\NaturalFloorDeploy-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item -LiteralPath "$assets\Scenes\TherapyRoom.unity" -Destination "$backup\TherapyRoom-saved-before-import.unity"
$manifest = foreach ($file in $files) {
    $source = Join-Path "$stage\Payload" $file
    $target = Join-Path $assets $file
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    $actual = (Get-FileHash -LiteralPath $target).Hash
    if ($actual -ne (Get-FileHash -LiteralPath $source).Hash) { throw "Copy verification failed: $file" }
    [pscustomobject]@{File=$file; SHA256=$actual}
}
$result = [pscustomobject]@{Backup=$backup; Files=$manifest; SceneUpdate='Pending Therapy Game > Install Natural Pond Floor'; CopiedAt=(Get-Date -Format o)}
[IO.File]::WriteAllText("$stage\Deployment.json", ($result | ConvertTo-Json -Depth 5))
$result | ConvertTo-Json -Depth 5
