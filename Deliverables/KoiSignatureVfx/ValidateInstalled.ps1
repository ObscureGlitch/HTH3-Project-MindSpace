$ErrorActionPreference='Stop'
$fxStage='D:\Hack the Hill\Deliverables\KoiSignatureVfx'
$fxProject='D:\Unity\HTH3 Project'
$fxAssets=Join-Path $fxProject 'Assets\TherapyGame'
$fxChecks=Join-Path $fxStage 'Checks'
$fxCopied=Get-Content -LiteralPath "$fxChecks\CopiedPayload.json" -Raw|ConvertFrom-Json
function RunCheck([string]$method,[string]$name,[bool]$graphics=$false){
    if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'TherapyRoom editor still running.'}
    if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Less than 2.5 GB of free committed memory.'}
    $fxArgs=@('-batchmode','-projectPath',('"'+$fxProject+'"'),'-executeMethod',$method,'-logFile',('"'+$fxChecks+'\'+$name+'.log"'))
    if(!$graphics){$fxArgs+='-nographics'}
    $fxProcess=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList $fxArgs -WindowStyle Hidden -PassThru
    while(!$fxProcess.WaitForExit(1000)){}
    if($fxProcess.ExitCode -ne 0){Get-Content -LiteralPath "$fxChecks\$name.log"|Where-Object{$_ -match 'error CS\d|InvalidOperationException|NullReferenceException|KOI_|Script compilation errors|Shader error'}|Select-Object -Last 12;throw "$name failed. Backup: $($fxCopied.Backup)"}
    Write-Output "PASS: $name"
}
RunCheck 'TherapyGame.Editor.TherapyKoiSignatureChecks.VerifyBatch' 'SignatureValidation'
RunCheck 'TherapyGame.Editor.TherapyPantheonSetup.VerifyBatch' 'FreshReload'
RunCheck 'TherapyGame.Editor.TherapyFishingRefinementChecks.VerifyBatch' 'InventoryRegression'
RunCheck 'TherapyGame.Editor.TherapyKoiSignatureRenders.RunBatch' 'SignatureRender' $true
foreach($entry in $fxCopied.Protected){if((Get-FileHash -LiteralPath (Join-Path $fxAssets $entry.File)).Hash -ne $entry.SHA256){throw "Protected file changed: $($entry.File)"}}
$fxSaves='C:\Users\obscu\AppData\LocalLow\DefaultCompany\HTH3 Project\MindSpaceFishing'
$fxSaveProof=Get-Content -LiteralPath "$fxChecks\SaveHashes.json" -Raw|ConvertFrom-Json
if(@(Get-ChildItem -LiteralPath $fxSaves -File).Count -ne $fxSaveProof.Count){throw 'Real save count changed.'}
foreach($entry in $fxSaveProof){if((Get-FileHash -LiteralPath (Join-Path $fxSaves $entry.File)).Hash -ne $entry.SHA256){throw "Real save modified: $($entry.File)"}}
foreach($entry in $fxCopied.Payload){if((Get-FileHash -LiteralPath (Join-Path $fxAssets $entry.File)).Hash -ne $entry.SHA256){throw "Installed source mismatch: $($entry.File)"}}
@{Status='Installed; Edit-mode validation and actual Unity model/catch renders passed';Backup=$fxCopied.Backup;OriginalSavesUnchanged=$true;ProtectedFilesUnchanged=$true;Pending='Visual review, hands-on motion feel and sustained FPS'}|ConvertTo-Json|Set-Content -LiteralPath "$fxChecks\Deployment.json"
Write-Output 'PASS: installed species VFX; lower tiers excluded, saves/models/odds unchanged.'
