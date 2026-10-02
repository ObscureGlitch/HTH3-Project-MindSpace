$ErrorActionPreference='Stop'
$seasonStage='D:\Hack the Hill\Deliverables\Seasons'
$seasonProject='D:\Unity\HTH3 Project'
$seasonAssets=Join-Path $seasonProject 'Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Unity must be closed.'}
if(-not(Test-Path -LiteralPath "$seasonStage\Checks\Compilation.txt")){throw 'Compile before deployment.'}
$baseline=Get-Content -LiteralPath "$seasonStage\Baseline.json" -Raw | ConvertFrom-Json
foreach($entry in $baseline){if((Get-FileHash -LiteralPath (Join-Path $seasonAssets $entry.File)).Hash -ne $entry.SHA256){throw "Live file changed: $($entry.File)"}}
$newFiles=@('Runtime\WellnessSeasonCycle.cs','Editor\TherapySeasonSetup.cs')
foreach($file in $newFiles){if(Test-Path -LiteralPath (Join-Path $seasonAssets $file)){throw "New file already exists: $file"}}
if(Test-Path -LiteralPath "$seasonAssets\Exterior\Seasons"){throw 'Seasons assets already exist; inspect before changing.'}
$backup=Join-Path $seasonProject ('TherapyBackups\Seasons\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
foreach($entry in $baseline){$dest=Join-Path $backup $entry.File;New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest)|Out-Null;Copy-Item -LiteralPath (Join-Path $seasonAssets $entry.File) -Destination $dest}
Copy-Item -LiteralPath "$seasonAssets\Scenes\TherapyRoom.unity" -Destination "$backup\TherapyRoom-before-seasons.unity"
$manifest=foreach($file in @($baseline.File)+$newFiles){
    $source=Join-Path "$seasonStage\Payload" $file;$target=Join-Path $seasonAssets $file
    Copy-Item -LiteralPath $source -Destination $target
    $hash=(Get-FileHash -LiteralPath $source).Hash
    if((Get-FileHash -LiteralPath $target).Hash -ne $hash){throw "Copy mismatch: $file"}
    [pscustomobject]@{File=$file;SHA256=$hash}
}
$result=[pscustomobject]@{Backup=$backup;Files=$manifest;Verification='Unity installation and tests pending';CopiedAt=(Get-Date -Format o)}
[IO.File]::WriteAllText("$seasonStage\Deployment.json",($result|ConvertTo-Json -Depth 5))
$result|ConvertTo-Json -Depth 5
