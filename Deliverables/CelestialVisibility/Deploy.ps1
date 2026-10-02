$ErrorActionPreference = 'Stop'
$stage = 'D:\Hack the Hill\Deliverables\CelestialVisibility'
$project = 'D:\Unity\HTH3 Project'
$assets = Join-Path $project 'Assets\TherapyGame'
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'Close Unity before importing shader changes.' }
if ((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 3000000) { throw 'Insufficient memory headroom for sky shader import.' }
if (-not (Test-Path -LiteralPath "$stage\Checks\CompilationChecks.txt")) { throw 'Run code and shader checks first.' }
$baseline = Get-Content -LiteralPath "$stage\Baseline.json" -Raw | ConvertFrom-Json
foreach ($entry in $baseline) {
    if ((Get-FileHash -LiteralPath (Join-Path $assets $entry.File)).Hash -ne $entry.SHA256) { throw "Live file changed: $($entry.File). Rebase before copying." }
    if (-not (Test-Path -LiteralPath (Join-Path "$stage\Payload" $entry.File))) { throw "Missing payload: $($entry.File)" }
}
$backup = Join-Path $project ('TherapyBackups\CelestialVisibility\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
foreach ($entry in $baseline) {
    $destination = Join-Path $backup $entry.File
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath (Join-Path $assets $entry.File) -Destination $destination
}
Copy-Item -LiteralPath "$assets\Scenes\TherapyRoom.unity" -Destination "$backup\TherapyRoom-saved-before-import.unity"
$manifest = foreach ($entry in $baseline) {
    $source = Join-Path "$stage\Payload" $entry.File
    $target = Join-Path $assets $entry.File
    Copy-Item -LiteralPath $source -Destination $target
    $expected = (Get-FileHash -LiteralPath $source).Hash
    $actual = (Get-FileHash -LiteralPath $target).Hash
    if ($actual -ne $expected) { throw "Copy verification failed: $($entry.File)" }
    [pscustomobject]@{File=$entry.File; SHA256=$actual}
}
$result = [pscustomobject]@{Backup=$backup; Files=$manifest; Verification='Pending Therapy Game > Verify Varied Night Sky'; CopiedAt=(Get-Date -Format o)}
[IO.File]::WriteAllText("$stage\Deployment.json", ($result | ConvertTo-Json -Depth 5))
$result | ConvertTo-Json -Depth 5
