$ErrorActionPreference='Stop'
$castStage='D:\Hack the Hill\Deliverables\FishingPolish'
$castProject='D:\Unity\HTH3 Project'
$castAssets=Join-Path $castProject 'Assets\TherapyGame'
$castChecks=Join-Path $castStage 'Checks'
function AssertCastEditorClosed {
    if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'Save and close the TherapyRoom editor before updating.'}
    if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Less than 2.5 GB of free committed memory remains.'}
}
AssertCastEditorClosed
foreach($entry in (Get-Content -LiteralPath "$castStage\Baseline.json" -Raw | ConvertFrom-Json)){
    if((Get-FileHash -LiteralPath (Join-Path $castAssets $entry.File)).Hash -ne $entry.SHA256){throw "Live source changed since staging: $($entry.File)."}
}
foreach($relative in @('Runtime\KoiFishingLoot.cs','Runtime\KoiFishingCatchMotion.cs','Editor\TherapyFishingPolishChecks.cs')){
    if(Test-Path -LiteralPath (Join-Path $castAssets $relative)){throw "Unexpected existing update file: $relative."}
}
$castBackup=Join-Path $castProject ('TherapyBackups\FishingPolish\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $castBackup -Force | Out-Null
$castSources=@(Get-ChildItem -LiteralPath "$castStage\Payload" -Filter '*.cs' -Recurse)
$castProtected=@('Scenes\TherapyRoom.unity','Exterior\KoiPond\KoiLibrary.asset','Runtime\KoiFishingRound.cs','Runtime\WellnessKoiPond.cs','Runtime\WellnessPhotoCamera.cs','Runtime\WellnessVoiceChat.cs','Runtime\WellnessExplorer.cs','Runtime\WellnessMainMenu.cs')
$castProof=@($castProtected | ForEach-Object {@{File=$_;SHA256=(Get-FileHash -LiteralPath (Join-Path $castAssets $_)).Hash}})
Copy-Item -LiteralPath "$castAssets\Scenes\TherapyRoom.unity" -Destination "$castBackup\TherapyRoom-before-click-cast.unity"
foreach($source in $castSources){
    $relative=$source.FullName.Substring(("$castStage\Payload\").Length);$live=Join-Path $castAssets $relative
    if(Test-Path -LiteralPath $live){$backup=Join-Path $castBackup $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null;Copy-Item -LiteralPath $live -Destination $backup}
}
Copy-Item -LiteralPath "$castAssets\Documentation\Fishing.md" -Destination "$castBackup\Fishing-before-click-cast.md"
foreach($source in $castSources){$relative=$source.FullName.Substring(("$castStage\Payload\").Length);Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $castAssets $relative)}
Copy-Item -LiteralPath "$castStage\README.md" -Destination "$castAssets\Documentation\Fishing.md"
@{Backup=$castBackup;Unchanged=$castProof;Files=@($castSources | ForEach-Object {$relative=$_.FullName.Substring(("$castStage\Payload\").Length);@{File=$relative;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$castChecks\CopiedPayload.json"
function RunCastCheck([string]$method,[string]$name,[bool]$graphics=$false){
    AssertCastEditorClosed
    $log="$castChecks\$name.log"
    $arguments=@('-batchmode','-projectPath',('"'+$castProject+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"'))
    if(!$graphics){$arguments+='-nographics'}
    $process=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    while(!$process.WaitForExit(1000)){}
    if($process.ExitCode -ne 0){
        if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log | Where-Object {$_ -match 'error CS\d|InvalidOperationException|Exception:|KOI_|FAILED|Script compilation errors'} | Select-Object -Last 16}
        throw "$name failed. Recoverable backup: $castBackup"
    }
    Write-Output "PASS: $name"
}
RunCastCheck 'TherapyGame.Editor.TherapyFishingPolishChecks.VerifyBatch' 'FishingPolishVerify'
RunCastCheck 'TherapyGame.Editor.TherapyFishingPolishChecks.PreviewBatch' 'FishingPolishPreview' $true
foreach($entry in $castProof){if((Get-FileHash -LiteralPath (Join-Path $castAssets $entry.File)).Hash -ne $entry.SHA256){throw "Protected scene/asset/source changed: $($entry.File)."}}
foreach($relative in @('Documentation\FishingPolishCheck.txt','Documentation\FishingPolishRenderCheck.txt','Documentation\FishingReloadCheck.txt')){Copy-Item -LiteralPath (Join-Path $castAssets $relative) -Destination $castChecks}
$castEvidence=Get-Content -LiteralPath "$castChecks\CopiedPayload.json" -Raw | ConvertFrom-Json
$castEvidence | Add-Member -NotePropertyName Status -NotePropertyValue 'Installed; Edit-mode checks and keyframe rendering passed'
$castEvidence | Add-Member -NotePropertyName Pending -NotePropertyValue 'Manual Play input and UI check'
$castEvidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$castChecks\Deployment.json"
Write-Output "PASS: fishing polish installed; scene, koi library, pond, photo, voice and player sources unchanged. Backup: $castBackup"
