$ErrorActionPreference='Stop'
$fxStage='D:\Hack the Hill\Deliverables\KoiSignatureVfx'
$fxProject='D:\Unity\HTH3 Project'
$fxAssets=Join-Path $fxProject 'Assets\TherapyGame'
$fxChecks=Join-Path $fxStage 'Checks'
$fxSaves='C:\Users\obscu\AppData\LocalLow\DefaultCompany\HTH3 Project\MindSpaceFishing'
if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'Save and close TherapyRoom before installing.'}
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Less than 2.5 GB of free committed memory.'}
$fxBaseline=Get-Content -LiteralPath "$fxStage\Baseline.json" -Raw|ConvertFrom-Json
foreach($entry in $fxBaseline){if((Get-FileHash -LiteralPath (Join-Path $fxAssets $entry.File)).Hash -ne $entry.SHA256){throw "Live source changed: $($entry.File)"}}
$fxPayload=@(Get-ChildItem -LiteralPath "$fxStage\Payload" -Recurse -File)
foreach($file in $fxPayload){$relative=$file.FullName.Substring(("$fxStage\Payload\").Length);if((Test-Path -LiteralPath (Join-Path $fxAssets $relative)) -and $relative -notin $fxBaseline.File){throw "Unexpected existing file: $relative"}}
$fxProtected=@('Scenes\TherapyRoom.unity','Exterior\KoiPond\KoiLibrary.asset','Resources\PantheonKoiLibrary.asset','Resources\KoiRarityAura.shader','Resources\KoiPantheonSurface.shader','Runtime\KoiFishingLoot.cs','Runtime\KoiCatchInventory.cs','Runtime\WellnessFishing.cs','Runtime\WellnessFishingHud.cs','Runtime\KoiCatchSplash.cs','Runtime\WellnessKoiPond.cs','Runtime\WellnessPhotoCamera.cs','Runtime\WellnessVoiceChat.cs','Runtime\WellnessExplorer.cs','Runtime\WellnessMainMenu.cs')
$fxProof=@($fxProtected|ForEach-Object{@{File=$_;SHA256=(Get-FileHash -LiteralPath (Join-Path $fxAssets $_)).Hash}})
$fxSaveProof=@(Get-ChildItem -LiteralPath $fxSaves -File|ForEach-Object{@{File=$_.Name;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$fxSaveProof|ConvertTo-Json|Set-Content -LiteralPath "$fxChecks\SaveHashes.json"
$fxBackup=Join-Path $fxProject ('TherapyBackups\KoiSignatureVfx\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $fxBackup -Force|Out-Null
Copy-Item -LiteralPath "$fxAssets\Scenes\TherapyRoom.unity" -Destination "$fxBackup\TherapyRoom-before-VFX.unity"
Copy-Item -LiteralPath $fxSaves -Destination "$fxBackup\OriginalFishSaves" -Recurse
foreach($file in $fxPayload){$relative=$file.FullName.Substring(("$fxStage\Payload\").Length);$live=Join-Path $fxAssets $relative;if(Test-Path -LiteralPath $live){$old=Join-Path $fxBackup $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $old) -Force|Out-Null;Copy-Item -LiteralPath $live -Destination $old}}
foreach($file in $fxPayload){$relative=$file.FullName.Substring(("$fxStage\Payload\").Length);$live=Join-Path $fxAssets $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $live) -Force|Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $live}
Copy-Item -LiteralPath "$fxStage\README.md" -Destination "$fxAssets\Documentation\KoiSignatureVfx.md"
@{Backup=$fxBackup;Protected=$fxProof;Payload=@($fxPayload|ForEach-Object{@{File=$_.FullName.Substring(("$fxStage\Payload\").Length);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$fxChecks\CopiedPayload.json"
Write-Output "Installed signature VFX with backup: $fxBackup"
