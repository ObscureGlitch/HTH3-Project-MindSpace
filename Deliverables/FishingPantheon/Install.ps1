$ErrorActionPreference='Stop'
$stage='D:\Hack the Hill\Deliverables\FishingPantheon'
$project='D:\Unity\HTH3 Project'
$assets=Join-Path $project 'Assets\TherapyGame'
$checks=Join-Path $stage 'Checks'
function AssertEditorClosed {
    if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'Save and close TherapyRoom before installing.'}
    if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Less than 2.5 GB of free committed memory.'}
}
AssertEditorClosed
$baseline=Get-Content -LiteralPath "$stage\Baseline.json" -Raw | ConvertFrom-Json
foreach($entry in $baseline){if((Get-FileHash -LiteralPath (Join-Path $assets $entry.File)).Hash -ne $entry.SHA256){throw "Live source changed: $($entry.File)"}}
$payload=@(Get-ChildItem -LiteralPath "$stage\Payload" -Recurse -File)
foreach($file in $payload){$relative=$file.FullName.Substring(("$stage\Payload\").Length);if((Test-Path -LiteralPath (Join-Path $assets $relative)) -and $relative -notin $baseline.File){throw "Unexpected existing file: $relative"}}
if(Test-Path -LiteralPath "$assets\Resources\PantheonKoiLibrary.asset"){throw 'Pantheon catalog already exists. Inspect before reinstalling.'}
$protected=@('Scenes\TherapyRoom.unity','Exterior\KoiPond\KoiLibrary.asset','Runtime\WellnessKoiPond.cs','Runtime\WellnessPhotoCamera.cs','Runtime\WellnessVoiceChat.cs','Runtime\WellnessExplorer.cs','Runtime\WellnessMainMenu.cs')
$proof=@($protected|ForEach-Object {@{File=$_;SHA256=(Get-FileHash -LiteralPath (Join-Path $assets $_)).Hash}})
$backup=Join-Path $project ('TherapyBackups\FishingPantheon\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
Copy-Item -LiteralPath "$assets\Scenes\TherapyRoom.unity" -Destination "$backup\TherapyRoom-before-pantheon.unity"
foreach($file in $payload){
    $relative=$file.FullName.Substring(("$stage\Payload\").Length);$live=Join-Path $assets $relative
    if(Test-Path -LiteralPath $live){$old=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $old) -Force | Out-Null;Copy-Item -LiteralPath $live -Destination $old}
}
Copy-Item -LiteralPath "$assets\Documentation\Fishing.md" -Destination "$backup\Fishing-before-pantheon.md"
foreach($file in $payload){$relative=$file.FullName.Substring(("$stage\Payload\").Length);$live=Join-Path $assets $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $live) -Force | Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $live}
Copy-Item -LiteralPath "$stage\README.md" -Destination "$assets\Documentation\Fishing.md"
@{Backup=$backup;Protected=$proof;Payload=@($payload|ForEach-Object{@{File=$_.FullName.Substring(("$stage\Payload\").Length);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$checks\CopiedPayload.json"
function RunCheck([string]$method,[string]$name,[bool]$graphics=$false){
    AssertEditorClosed
    $arguments=@('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-logFile',('"'+$checks+'\'+$name+'.log"'))
    if(!$graphics){$arguments+='-nographics'}
    $process=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    while(!$process.WaitForExit(1000)){}
    if($process.ExitCode -ne 0){Get-Content -LiteralPath "$checks\$name.log"|Where-Object{$_ -match 'error CS\d|InvalidOperationException|NullReferenceException|KOI_|Script compilation errors'}|Select-Object -Last 12;throw "$name failed. Backup: $backup"}
    Write-Output "PASS: $name"
}
RunCheck 'TherapyGame.Editor.TherapyPantheonSetup.InstallBatch' 'PantheonInstall'
RunCheck 'TherapyGame.Editor.TherapyPantheonSetup.VerifyBatch' 'PantheonReload'
RunCheck 'TherapyGame.Editor.TherapyFishingSetup.VerifyBatch' 'FishingRegression'
RunCheck 'TherapyGame.Editor.TherapyPantheonRenderChecks.RunBatch' 'PantheonRender' $true
foreach($entry in $proof){if((Get-FileHash -LiteralPath (Join-Path $assets $entry.File)).Hash -ne $entry.SHA256){throw "Protected file changed: $($entry.File)"}}
Copy-Item -LiteralPath "$assets\Documentation\FishingPantheonCheck.txt","$assets\Documentation\FishingReloadCheck.txt" -Destination $checks
@{Status='Installed; Edit-mode tests, fresh reload and actual Unity fish renders passed';Backup=$backup;ProtectedFilesUnchanged=$true;Pending='Hands-on Play input and UI readability, sustained FPS measurement'}|ConvertTo-Json|Set-Content -LiteralPath "$checks\Deployment.json"
Write-Output "Installed koi pantheon. Scene and original pond assets unchanged. Backup: $backup"
