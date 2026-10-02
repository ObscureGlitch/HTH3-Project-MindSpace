$ErrorActionPreference='Stop'
$rareStage=$PSScriptRoot
$rareProject='D:\Unity\HTH3 Project'
$rareAssets=Join-Path $rareProject 'Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Close Unity before running batch validation.'}
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Insufficient Unity compilation memory.'}
$rareBaseline=Get-Content "$rareStage\Baseline.json" -Raw|ConvertFrom-Json
foreach($rareEntry in $rareBaseline.Files){if((Get-FileHash (Join-Path $rareAssets $rareEntry.File)).Hash -ne $rareEntry.SHA256){throw "Fishing source changed: $($rareEntry.File)"}}
if((Get-FileHash "$rareAssets\Runtime\KoiFishingLoot.cs").Hash -ne $rareBaseline.LootSHA256){throw 'Rarity data changed; review the updated tiers.'}
$rareBackup=Join-Path $rareProject ('TherapyBackups\RareFishVfx\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $rareBackup | Out-Null
foreach($rareEntry in $rareBaseline.Files){
    $rareDestination=Join-Path $rareBackup $rareEntry.File
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $rareDestination)|Out-Null
    Copy-Item -LiteralPath (Join-Path $rareAssets $rareEntry.File) -Destination $rareDestination
}
Copy-Item -LiteralPath "$rareAssets\Scenes\TherapyRoom.unity" -Destination "$rareBackup\TherapyRoom-unchanged.unity"
$rareProtected=[pscustomobject]@{SceneSHA256=(Get-FileHash "$rareAssets\Scenes\TherapyRoom.unity").Hash;LootSHA256=(Get-FileHash "$rareAssets\Runtime\KoiFishingLoot.cs").Hash;InventorySHA256=(Get-FileHash "$rareAssets\Runtime\KoiCatchInventory.cs").Hash;Backup=$rareBackup}
[IO.File]::WriteAllText("$rareStage\Checks\Protected.json",($rareProtected|ConvertTo-Json))
foreach($rareFile in Get-ChildItem "$rareStage\Payload" -File -Recurse){
    $rareRelative=$rareFile.FullName.Substring(("$rareStage\Payload\").Length)
    $rareDestination=Join-Path $rareAssets $rareRelative
    if($rareRelative -notin $rareBaseline.Files.File -and (Test-Path -LiteralPath $rareDestination)){throw "Unexpected existing file: $rareRelative"}
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $rareDestination)|Out-Null
    Copy-Item -LiteralPath $rareFile.FullName -Destination $rareDestination
}
$rareArgs=@('-batchmode','-projectPath',('"'+$rareProject+'"'),'-executeMethod','TherapyGame.Editor.TherapyRareFishVfxChecks.RunBatch','-logFile',('"'+$rareStage+'\Checks\Unity-Vfx.log"'))
$rareRun=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList $rareArgs -WindowStyle Hidden -PassThru
[pscustomobject]@{ProcessId=$rareRun.Id;Backup=$rareBackup}|ConvertTo-Json
