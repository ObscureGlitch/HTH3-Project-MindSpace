$ErrorActionPreference='Stop'
$photoStage='D:\Hack the Hill\Deliverables\PhotoCamera'
$photoProject='D:\Unity\HTH3 Project'
$photoAssets=Join-Path $photoProject 'Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Unity must be closed.'}
if(-not(Test-Path -LiteralPath "$photoStage\Checks\Compilation.txt")){throw 'Compile before deployment.'}
$photoBaseline=Get-Content -LiteralPath "$photoStage\Baseline.json" -Raw | ConvertFrom-Json
foreach($entry in $photoBaseline){if((Get-FileHash -LiteralPath (Join-Path $photoAssets $entry.File)).Hash -ne $entry.SHA256){throw "Live file changed: $($entry.File)"}}
$newFiles=@('Runtime\WellnessPhotoCamera.cs','Runtime\WellnessPhotoAlbum.cs','Editor\TherapyPhotoCameraSetup.cs')
foreach($file in $newFiles){if(Test-Path -LiteralPath (Join-Path $photoAssets $file)){throw "New file already exists: $file"}}
$photoBackup=Join-Path $photoProject ('TherapyBackups\PhotoCamera\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $photoBackup | Out-Null
foreach($entry in $photoBaseline){$dest=Join-Path $photoBackup $entry.File;New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest)|Out-Null;Copy-Item -LiteralPath (Join-Path $photoAssets $entry.File) -Destination $dest}
Copy-Item -LiteralPath "$photoAssets\Scenes\TherapyRoom.unity" -Destination "$photoBackup\TherapyRoom-before-photo-camera.unity"
$photoManifest=foreach($file in @($photoBaseline.File)+$newFiles){
    $source=Join-Path "$photoStage\Payload" $file;$target=Join-Path $photoAssets $file
    Copy-Item -LiteralPath $source -Destination $target
    $hash=(Get-FileHash -LiteralPath $source).Hash
    if((Get-FileHash -LiteralPath $target).Hash -ne $hash){throw "Copy mismatch: $file"}
    [pscustomobject]@{File=$file;SHA256=$hash}
}
$result=[pscustomobject]@{Backup=$photoBackup;Files=$photoManifest;Verification='Unity installation/checks pending';CopiedAt=(Get-Date -Format o)}
[IO.File]::WriteAllText("$photoStage\Deployment.json",($result|ConvertTo-Json -Depth 5))
$result|ConvertTo-Json -Depth 5
