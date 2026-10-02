$ErrorActionPreference='Stop'
$seasonStage='D:\Hack the Hill\Deliverables\Seasons'
$seasonProject='D:\Unity\HTH3 Project'
$seasonAssets=Join-Path $seasonProject 'Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Save the scene and close Unity first.'}
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Not enough committed memory for the repair check.'}
$seasonManifest=Get-Content -LiteralPath "$seasonStage\Deployment.json" -Raw|ConvertFrom-Json
$seasonSource='Editor\TherapySeasonSetup.cs'
$seasonExpected=($seasonManifest.Files|Where-Object File -eq $seasonSource).SHA256
$seasonLive=Join-Path $seasonAssets $seasonSource
if((Get-FileHash -LiteralPath $seasonLive).Hash -ne $seasonExpected){throw 'The live season installer changed; compare before copying.'}
$seasonBackup=Join-Path $seasonProject ('TherapyBackups\WinterBranchAttachments\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $seasonBackup|Out-Null
Copy-Item -LiteralPath $seasonLive -Destination "$seasonBackup\TherapySeasonSetup-before-repair.cs"
Copy-Item -LiteralPath "$seasonAssets\Exterior\Seasons\BareWinterBranches.asset" -Destination "$seasonBackup\BareWinterBranches-before-repair.asset"
Copy-Item -LiteralPath "$seasonAssets\Scenes\TherapyRoom.unity" -Destination "$seasonBackup\TherapyRoom-before-repair.unity"
$seasonSceneHash=(Get-FileHash -LiteralPath "$seasonAssets\Scenes\TherapyRoom.unity").Hash
Copy-Item -LiteralPath "$seasonStage\Payload\$seasonSource" -Destination $seasonLive
$seasonHash=(Get-FileHash -LiteralPath $seasonLive).Hash
if($seasonHash -ne (Get-FileHash -LiteralPath "$seasonStage\Payload\$seasonSource").Hash){throw 'Installer copy mismatch.'}
foreach($method in @('RepairBranchesBatch','VerifyBranchesBatch')){
    $seasonLog=Join-Path $seasonStage "Checks\Unity-$method.log"
    $seasonRun=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList @('-batchmode','-nographics','-projectPath',('"'+$seasonProject+'"'),'-executeMethod',('TherapyGame.Editor.TherapySeasonSetup.'+$method),'-logFile',('"'+$seasonLog+'"')) -WindowStyle Hidden -PassThru
    $seasonRun.WaitForExit()
    if($seasonRun.ExitCode -ne 0){
        $report=if($method -eq 'RepairBranchesBatch'){'WinterBranchesCheck.txt'}else{'WinterBranchesReloadCheck.txt'}
        Get-Content -LiteralPath "$seasonAssets\Documentation\$report"
        throw "$method failed; backup retained at $seasonBackup"
    }
}
if((Get-FileHash -LiteralPath "$seasonAssets\Scenes\TherapyRoom.unity").Hash -ne $seasonSceneHash){throw 'Scene changed unexpectedly during the mesh-only repair.'}
foreach($report in @('WinterBranchesCheck.txt','WinterBranchesReloadCheck.txt')){
    if((Get-Content -LiteralPath "$seasonAssets\Documentation\$report" -Raw) -notmatch '^PASS:'){throw "Failed report: $report"}
    Copy-Item -LiteralPath "$seasonAssets\Documentation\$report" -Destination "$seasonStage\Checks\$report"
    Get-Content -LiteralPath "$seasonStage\Checks\$report"
}
($seasonManifest.Files|Where-Object File -eq $seasonSource).SHA256=$seasonHash
$seasonManifest.Verification='PASS: seasons and trunk-anchored winter mesh; compilation, saved-mesh reload, complete trunk/branch/twig attachments, palette restore and unchanged scene hash. Play visual check pending.'
$seasonManifest.VerifiedAt=Get-Date -Format o
$seasonManifest|Add-Member -NotePropertyName WinterBranchBackup -NotePropertyValue $seasonBackup -Force
[IO.File]::WriteAllText("$seasonStage\Deployment.json",($seasonManifest|ConvertTo-Json -Depth 5))
$seasonResult=[pscustomobject]@{Status='PASS';Backup=$seasonBackup;SceneUnchanged=$true;MeshSHA256=(Get-FileHash -LiteralPath "$seasonAssets\Exterior\Seasons\BareWinterBranches.asset").Hash;EditorSHA256=$seasonHash;Pending='Play-mode visual check'}
[IO.File]::WriteAllText("$seasonStage\Checks\WinterBranchRepair.json",($seasonResult|ConvertTo-Json -Depth 4))
$seasonResult|ConvertTo-Json -Depth 4
