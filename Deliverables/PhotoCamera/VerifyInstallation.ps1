$ErrorActionPreference='Stop'
$photoStage='D:\Hack the Hill\Deliverables\PhotoCamera'
$photoAssets='D:\Unity\HTH3 Project\Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Wait for headless Unity to exit.'}
$photoDeployment=Get-Content -LiteralPath "$photoStage\Deployment.json" -Raw | ConvertFrom-Json
foreach($entry in $photoDeployment.Files){
    $source=Join-Path "$photoStage\Payload" $entry.File;$target=Join-Path $photoAssets $entry.File
    $hash=(Get-FileHash -LiteralPath $source).Hash
    if((Get-FileHash -LiteralPath $target).Hash -ne $hash){throw "Installed code mismatch: $($entry.File)"}
    $entry.SHA256=$hash
}
$photoReport=Get-Content -LiteralPath "$photoAssets\Documentation\PhotoCameraCheck.txt" -Raw
if(-not $photoReport.StartsWith('PASS:')){throw $photoReport}
$photoMeta=Get-Content -LiteralPath "$photoAssets\Runtime\WellnessPhotoCamera.cs.meta" -Raw
$photoGuid=([regex]::Match($photoMeta,'guid: ([a-f0-9]+)')).Groups[1].Value
$photoScene=Get-Content -LiteralPath "$photoAssets\Scenes\TherapyRoom.unity" -Raw
if(-not $photoScene.Contains($photoGuid)){throw 'Photo camera is not saved in the scene.'}
$photoDeployment.Verification='PASS: Unity import, scene references, PNG/JPEG/metadata save and reload, exact PNG colors, optical zoom and capture sizing. GPU/visual/input Play-mode check pending.'
[IO.File]::WriteAllText("$photoStage\Deployment.json",($photoDeployment|ConvertTo-Json -Depth 5))
Copy-Item -LiteralPath "$photoAssets\Documentation\PhotoCameraCheck.txt" -Destination "$photoStage\Checks\PhotoCameraCheck.txt"
Write-Output $photoReport
Write-Output 'PASS: final installed code matches compiled payload; camera component saved in TherapyRoom.'
