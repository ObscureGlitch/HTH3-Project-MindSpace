$ErrorActionPreference='Stop'
$refStage='D:\Hack the Hill\Deliverables\FishingRefinement'
$refProject='D:\Unity\HTH3 Project'
$refAssets=Join-Path $refProject 'Assets\TherapyGame'
$refChecks=Join-Path $refStage 'Checks'
$refSaves='C:\Users\obscu\AppData\LocalLow\DefaultCompany\HTH3 Project\MindSpaceFishing'
function AssertClosed {
    if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'Save and close TherapyRoom before installing.'}
    if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Less than 2.5 GB of free committed memory.'}
}
function AssertSaves {
    $refSaveProof=Get-Content -LiteralPath "$refChecks\SaveHashes.json" -Raw | ConvertFrom-Json
    if(@(Get-ChildItem -LiteralPath $refSaves -File).Count -ne $refSaveProof.Count){throw 'Save file count changed. Re-audit before continuing.'}
    foreach($entry in $refSaveProof){if((Get-FileHash -LiteralPath (Join-Path $refSaves $entry.File)).Hash -ne $entry.SHA256){throw "Save changed: $($entry.File)"}}
}
AssertClosed
AssertSaves
$refBaseline=Get-Content -LiteralPath "$refStage\Baseline.json" -Raw | ConvertFrom-Json
foreach($entry in $refBaseline){if((Get-FileHash -LiteralPath (Join-Path $refAssets $entry.File)).Hash -ne $entry.SHA256){throw "Live source changed: $($entry.File)"}}
$refPayload=@(Get-ChildItem -LiteralPath "$refStage\Payload" -Recurse -File)
foreach($file in $refPayload){$relative=$file.FullName.Substring(("$refStage\Payload\").Length);if((Test-Path -LiteralPath (Join-Path $refAssets $relative)) -and $relative -notin $refBaseline.File){throw "Unexpected existing file: $relative"}}
$refProtected=@('Scenes\TherapyRoom.unity','Exterior\KoiPond\KoiLibrary.asset','Resources\PantheonKoiLibrary.asset','Runtime\KoiFishingLoot.cs','Runtime\KoiRarityVfx.cs','Runtime\WellnessKoiPond.cs','Runtime\WellnessPhotoCamera.cs','Runtime\WellnessVoiceChat.cs','Runtime\WellnessExplorer.cs','Runtime\WellnessMainMenu.cs')
$refProof=@($refProtected|ForEach-Object {@{File=$_;SHA256=(Get-FileHash -LiteralPath (Join-Path $refAssets $_)).Hash}})
$refBackup=Join-Path $refProject ('TherapyBackups\FishingRefinement\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $refBackup -Force | Out-Null
Copy-Item -LiteralPath "$refAssets\Scenes\TherapyRoom.unity" -Destination "$refBackup\TherapyRoom-before-refinement.unity"
Copy-Item -LiteralPath $refSaves -Destination "$refBackup\OriginalFishSaves" -Recurse
foreach($file in $refPayload){
    $relative=$file.FullName.Substring(("$refStage\Payload\").Length);$live=Join-Path $refAssets $relative
    if(Test-Path -LiteralPath $live){$old=Join-Path $refBackup $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $old) -Force | Out-Null;Copy-Item -LiteralPath $live -Destination $old}
}
foreach($file in $refPayload){$relative=$file.FullName.Substring(("$refStage\Payload\").Length);$live=Join-Path $refAssets $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $live) -Force | Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $live}
Copy-Item -LiteralPath "$refStage\README.md" -Destination "$refAssets\Documentation\FishingRefinement.md"
@{Backup=$refBackup;Protected=$refProof;Payload=@($refPayload|ForEach-Object{@{File=$_.FullName.Substring(("$refStage\Payload\").Length);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$refChecks\CopiedPayload.json"
Write-Output "Copied fishing refinement with original save backup: $refBackup"
