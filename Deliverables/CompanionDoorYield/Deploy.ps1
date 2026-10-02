$ErrorActionPreference='Stop'
$doorStage='D:\Hack the Hill\Deliverables\CompanionDoorYield'
$doorProject='D:\Unity\HTH3 Project'
$doorAssets=Join-Path $doorProject 'Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Unity must be closed for this import.'}
if(-not(Test-Path -LiteralPath "$doorStage\Checks\Compilation.txt")){throw 'Compile before deploying.'}
$doorBaseline=Get-Content -LiteralPath "$doorStage\Baseline.json" -Raw | ConvertFrom-Json
foreach($entry in $doorBaseline){if((Get-FileHash -LiteralPath (Join-Path $doorAssets $entry.File)).Hash -ne $entry.SHA256){throw "Live file changed: $($entry.File)"}}
$doorFiles=@($doorBaseline.File)+@('Editor\TherapyDoorwayClearanceChecks.cs','DoorwayClearanceRequest.txt')
foreach($file in @('Editor\TherapyDoorwayClearanceChecks.cs','DoorwayClearanceRequest.txt')){if(Test-Path -LiteralPath (Join-Path $doorAssets $file)){throw "New file already exists: $file"}}
$doorBackup=Join-Path $doorProject ('TherapyBackups\CompanionDoorYield\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $doorBackup | Out-Null
foreach($entry in $doorBaseline){$dest=Join-Path $doorBackup $entry.File;New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest)|Out-Null;Copy-Item -LiteralPath (Join-Path $doorAssets $entry.File) -Destination $dest}
Copy-Item -LiteralPath "$doorAssets\Scenes\TherapyRoom.unity" -Destination "$doorBackup\TherapyRoom-saved-before-import.unity"
$doorManifest=foreach($file in $doorFiles){
    $source=Join-Path "$doorStage\Payload" $file;$target=Join-Path $doorAssets $file
    Copy-Item -LiteralPath $source -Destination $target
    $hash=(Get-FileHash -LiteralPath $source).Hash
    if((Get-FileHash -LiteralPath $target).Hash -ne $hash){throw "Copy mismatch: $file"}
    [pscustomobject]@{File=$file;SHA256=$hash}
}
$result=[pscustomobject]@{Backup=$doorBackup;Files=$doorManifest;Verification='Queued once in Edit mode when TherapyRoom opens';CopiedAt=(Get-Date -Format o)}
[IO.File]::WriteAllText("$doorStage\Deployment.json",($result|ConvertTo-Json -Depth 5))
$result|ConvertTo-Json -Depth 5
