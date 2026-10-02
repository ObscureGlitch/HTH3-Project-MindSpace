$ErrorActionPreference='Stop'
$refStage='D:\Hack the Hill\Deliverables\FishingRefinement'
$refProject='D:\Unity\HTH3 Project'
$refAssets=Join-Path $refProject 'Assets\TherapyGame'
$refChecks=Join-Path $refStage 'Checks'
$refCopied=Get-Content -LiteralPath "$refChecks\CopiedPayload.json" -Raw|ConvertFrom-Json
function RunCheck([string]$method,[string]$name,[bool]$graphics=$false){
    if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'TherapyRoom editor still running.'}
    if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Less than 2.5 GB of free committed memory.'}
    $refArgs=@('-batchmode','-projectPath',('"'+$refProject+'"'),'-executeMethod',$method,'-logFile',('"'+$refChecks+'\'+$name+'.log"'))
    if(!$graphics){$refArgs+='-nographics'}
    $refProcess=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList $refArgs -WindowStyle Hidden -PassThru
    while(!$refProcess.WaitForExit(1000)){}
    if($refProcess.ExitCode -ne 0){Get-Content -LiteralPath "$refChecks\$name.log"|Where-Object{$_ -match 'error CS\d|InvalidOperationException|NullReferenceException|KOI_|Script compilation errors'}|Select-Object -Last 12;throw "$name failed. Backup: $($refCopied.Backup)"}
    Write-Output "PASS: $name"
}
RunCheck 'TherapyGame.Editor.TherapyFishingRefinementChecks.VerifyBatch' 'RefinementValidation'
RunCheck 'TherapyGame.Editor.TherapyPantheonSetup.VerifyBatch' 'FreshPantheonRegression'
RunCheck 'TherapyGame.Editor.TherapyFishingSetup.VerifyBatch' 'FishingRegression'
RunCheck 'TherapyGame.Editor.TherapyFishingRefinementRenders.RunBatch' 'RefinementRender' $true
foreach($entry in $refCopied.Protected){if((Get-FileHash -LiteralPath (Join-Path $refAssets $entry.File)).Hash -ne $entry.SHA256){throw "Protected file changed: $($entry.File)"}}
$refSaves='C:\Users\obscu\AppData\LocalLow\DefaultCompany\HTH3 Project\MindSpaceFishing'
$refSaveProof=Get-Content -LiteralPath "$refChecks\SaveHashes.json" -Raw|ConvertFrom-Json
if(@(Get-ChildItem -LiteralPath $refSaves -File).Count -ne $refSaveProof.Count){throw 'Real save file count changed.'}
foreach($entry in $refSaveProof){if((Get-FileHash -LiteralPath (Join-Path $refSaves $entry.File)).Hash -ne $entry.SHA256){throw "Real save modified: $($entry.File)"}}
@{Status='Installed and Edit-mode verified';Backup=$refCopied.Backup;NineOriginalCatchFilesUnchanged=$true;ProtectedFilesUnchanged=$true;Pending='Hands-on Play input feel and audio'}|ConvertTo-Json|Set-Content -LiteralPath "$refChecks\Deployment.json"
Write-Output 'PASS: installed repair, regressions and offscreen renders; nine original catch files unchanged.'
