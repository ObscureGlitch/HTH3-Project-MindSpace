$ErrorActionPreference='Stop'
$winterStage='D:\Hack the Hill\Deliverables\Seasons'
$winterProject='D:\Unity\HTH3 Project'
$winterAssets=Join-Path $winterProject 'Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Save the scene and close Unity first.'}
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Not enough committed memory for Unity validation.'}
$winterBaseline=Get-Content -LiteralPath "$winterStage\WinterProportionsBaseline.json" -Raw | ConvertFrom-Json
$winterNew='Editor\WinterTrunkBatch.cs'
foreach($winterFile in $winterBaseline.Files){
    if((Get-FileHash -LiteralPath (Join-Path $winterAssets $winterFile.File)).Hash -ne $winterFile.SHA256){throw "Live source changed: $($winterFile.File)"}
}
if(Test-Path -LiteralPath (Join-Path $winterAssets $winterNew)){throw 'New trunk helper already exists; compare before copying.'}
$winterScene="$winterAssets\Scenes\TherapyRoom.unity"
$winterMesh="$winterAssets\Exterior\Seasons\BareWinterBranches.asset"
$winterSourceTrunks="$winterAssets\Exterior\Meshes\Tree_trunks__bark.asset"
if((Get-FileHash -LiteralPath $winterScene).Hash -ne $winterBaseline.SceneSHA256){throw 'Scene changed since preparation.'}
if((Get-FileHash -LiteralPath $winterMesh).Hash -ne $winterBaseline.MeshSHA256){throw 'Winter mesh changed since preparation.'}
if((Get-FileHash -LiteralPath $winterSourceTrunks).Hash -ne $winterBaseline.SourceTrunkSHA256){throw 'Source trunks changed since preparation.'}
$winterBackup=Join-Path $winterProject ('TherapyBackups\WinterTreeProportions\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $winterBackup | Out-Null
foreach($winterFile in $winterBaseline.Files){
    $winterDestination=Join-Path $winterBackup $winterFile.File
    New-Item -ItemType Directory -Path (Split-Path -Parent $winterDestination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $winterAssets $winterFile.File) -Destination $winterDestination
}
Copy-Item -LiteralPath $winterScene -Destination "$winterBackup\TherapyRoom-before.unity"
Copy-Item -LiteralPath $winterMesh -Destination "$winterBackup\BareWinterBranches-before.asset"
Copy-Item -LiteralPath $winterSourceTrunks -Destination "$winterBackup\OriginalTrunks-unchanged.asset"
$winterFiles=@($winterBaseline.Files.File)+$winterNew
$winterCopied=@()
foreach($winterFile in $winterFiles){
    Copy-Item -LiteralPath "$winterStage\Payload\$winterFile" -Destination (Join-Path $winterAssets $winterFile)
    $winterHash=(Get-FileHash -LiteralPath (Join-Path $winterAssets $winterFile)).Hash
    if($winterHash -ne (Get-FileHash -LiteralPath "$winterStage\Payload\$winterFile").Hash){throw "Copy mismatch: $winterFile"}
    $winterCopied+=[pscustomobject]@{File=$winterFile;SHA256=$winterHash}
}
Write-Output "Copied four checked editor scripts. Backup: $winterBackup"
foreach($winterMethod in @('RepairBranchesBatch','VerifyBranchesBatch','PreviewBranchesBatch')){
    if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Another Unity editor opened before validation.'}
    $winterLog="$winterStage\Checks\Unity-Proportions-$winterMethod.log"
    $winterArguments=@('-batchmode','-projectPath',('"'+$winterProject+'"'),'-executeMethod',('TherapyGame.Editor.TherapySeasonSetup.'+$winterMethod),'-logFile',('"'+$winterLog+'"'))
    if($winterMethod -ne 'PreviewBranchesBatch'){$winterArguments+= '-nographics'}
    Write-Output "Starting $winterMethod in Edit mode."
    $winterRun=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList $winterArguments -WindowStyle Hidden -PassThru
    $winterRun.WaitForExit()
    if($winterRun.ExitCode -ne 0){
        if($winterMethod -ne 'PreviewBranchesBatch'){
            $winterReport=if($winterMethod -eq 'RepairBranchesBatch'){'WinterBranchesCheck.txt'}else{'WinterBranchesReloadCheck.txt'}
            if(Test-Path -LiteralPath "$winterAssets\Documentation\$winterReport"){Get-Content -LiteralPath "$winterAssets\Documentation\$winterReport"}
        }
        Select-String -LiteralPath $winterLog -Pattern 'error CS\d+|WINTER_BRANCHES|InvalidOperationException|Aborting batchmode|NotSupportedException' | ForEach-Object {$_.Line}
        throw "$winterMethod failed. Backup retained at $winterBackup."
    }
}
if((Get-FileHash -LiteralPath $winterSourceTrunks).Hash -ne $winterBaseline.SourceTrunkSHA256){throw 'Original trunk source changed unexpectedly.'}
# Only the dormant-renderer reference array needs a scene change. Everything
# outside that array must remain byte-for-byte identical after normalization.
$winterBefore=Get-Content -LiteralPath "$winterBackup\TherapyRoom-before.unity" -Raw
$winterAfter=Get-Content -LiteralPath $winterScene -Raw
$winterDormantPattern='(?m)^  winterDormant:(?: \[\])?\r?\n(?:  - [^\r\n]*\r?\n)*'
if([regex]::Replace($winterBefore,$winterDormantPattern,'') -ne [regex]::Replace($winterAfter,$winterDormantPattern,'')){throw 'Scene changed outside the winterDormant renderer references; compare before accepting.'}
foreach($winterReport in @('WinterBranchesCheck.txt','WinterBranchesReloadCheck.txt')){
    $winterText=Get-Content -LiteralPath "$winterAssets\Documentation\$winterReport" -Raw
    if($winterText -notmatch '^PASS:'){throw "Failed report: $winterReport"}
    Copy-Item -LiteralPath "$winterAssets\Documentation\$winterReport" -Destination "$winterStage\Checks\$winterReport"
    Write-Output $winterText
}
$winterManifest=Get-Content -LiteralPath "$winterStage\Deployment.json" -Raw | ConvertFrom-Json
foreach($winterFile in $winterCopied){
    $winterEntry=$winterManifest.Files | Where-Object File -eq $winterFile.File
    if($winterEntry){$winterEntry.SHA256=$winterFile.SHA256}else{$winterManifest.Files=@($winterManifest.Files)+$winterFile}
}
$winterManifest.Verification='PASS: lower Winter scaffold branches, short exposed trunks, connected ground-rooted trees, exact evergreen-trunk preservation, saved-mesh reload, seasonal visibility restore, unchanged source trunks/colliders and only winterDormant scene references changed. Edit-mode GPU preview rendered; inspection and Play test pending.'
$winterManifest.VerifiedAt=Get-Date -Format o
$winterManifest | Add-Member -NotePropertyName WinterTreeProportionsBackup -NotePropertyValue $winterBackup -Force
[IO.File]::WriteAllText("$winterStage\Deployment.json",($winterManifest | ConvertTo-Json -Depth 6))
$winterResult=[pscustomobject]@{Status='PASS';Backup=$winterBackup;SourceTrunksUnchanged=$true;OnlyDormantReferencesChanged=$true;SceneSHA256=(Get-FileHash -LiteralPath $winterScene).Hash;MeshSHA256=(Get-FileHash -LiteralPath $winterMesh).Hash;Files=$winterCopied;Preview="$winterStage\Checks\WinterTreeProportions.png";Verification=$winterManifest.Verification;VerifiedAt=$winterManifest.VerifiedAt}
[IO.File]::WriteAllText("$winterStage\Checks\WinterTreeProportionsDeployment.json",($winterResult | ConvertTo-Json -Depth 6))
$winterResult | ConvertTo-Json -Depth 6
