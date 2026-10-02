$ErrorActionPreference='Stop'
$stage='D:\Hack the Hill\Deliverables\FishingPantheon'
$project='D:\Unity\HTH3 Project'
$assets=Join-Path $project 'Assets\TherapyGame'
$checks=Join-Path $stage 'Checks'
function AssertEditorClosed {
    if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'Close TherapyRoom before checks.'}
    if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Less than 2.5 GB of committed headroom.'}
}
AssertEditorClosed
$evidence=Get-Content -LiteralPath "$checks\CopiedPayload.json" -Raw|ConvertFrom-Json
foreach($entry in $evidence.Payload){if((Get-FileHash -LiteralPath (Join-Path $assets $entry.File)).Hash -ne $entry.SHA256){throw "Live file changed since copying: $($entry.File)"}}
foreach($entry in $evidence.Protected){if((Get-FileHash -LiteralPath (Join-Path $assets $entry.File)).Hash -ne $entry.SHA256){throw "Protected file changed: $($entry.File)"}}
foreach($entry in $evidence.Payload){
    $source=Join-Path "$stage\Payload" $entry.File;$hash=(Get-FileHash -LiteralPath $source).Hash
    if($hash -ne $entry.SHA256){Copy-Item -LiteralPath $source -Destination (Join-Path $assets $entry.File);$entry.SHA256=$hash}
}
$evidence|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$checks\CopiedPayload.json"
function RunCheck([string]$method,[string]$name,[bool]$graphics=$false){
    AssertEditorClosed
    $arguments=@('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-logFile',('"'+$checks+'\'+$name+'.log"'))
    if(!$graphics){$arguments+='-nographics'}
    $process=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    while(!$process.WaitForExit(1000)){}
    if($process.ExitCode -ne 0){Get-Content -LiteralPath "$checks\$name.log"|Where-Object{$_ -match 'error CS\d|InvalidOperationException|NullReferenceException|KOI_|Script compilation errors'}|Select-Object -Last 12;throw "$name failed. Backup: $($evidence.Backup)"}
    Write-Output "PASS: $name"
}
RunCheck 'TherapyGame.Editor.TherapyPantheonSetup.InstallBatch' 'PantheonInstall'
RunCheck 'TherapyGame.Editor.TherapyPantheonSetup.VerifyBatch' 'PantheonReload'
RunCheck 'TherapyGame.Editor.TherapyFishingSetup.VerifyBatch' 'FishingRegression'
RunCheck 'TherapyGame.Editor.TherapyPantheonRenderChecks.RunBatch' 'PantheonRender' $true
foreach($entry in $evidence.Protected){if((Get-FileHash -LiteralPath (Join-Path $assets $entry.File)).Hash -ne $entry.SHA256){throw "Protected file changed: $($entry.File)"}}
Copy-Item -LiteralPath "$assets\Documentation\FishingPantheonCheck.txt","$assets\Documentation\FishingReloadCheck.txt" -Destination $checks
@{Status='Installed; Edit-mode tests, fresh reload and actual Unity fish renders passed';Backup=$evidence.Backup;ProtectedFilesUnchanged=$true;Pending='Hands-on Play input and UI readability, sustained FPS measurement'}|ConvertTo-Json|Set-Content -LiteralPath "$checks\Deployment.json"
Write-Output "Installed koi pantheon. Scene and original pond assets unchanged. Backup: $($evidence.Backup)"
