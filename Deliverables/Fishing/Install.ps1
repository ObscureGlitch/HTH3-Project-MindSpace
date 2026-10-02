param([string]$ResumeBackup)
$ErrorActionPreference='Stop'
$fishingStage='D:\Hack the Hill\Deliverables\Fishing'
$fishingProject='D:\Unity\HTH3 Project'
$fishingAssets=Join-Path $fishingProject 'Assets\TherapyGame'
$fishingChecks=Join-Path $fishingStage 'Checks'
$fishingUnity='D:\Unity\6000.6.3f1\Editor\Unity.exe'
function AssertClosed {
    if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'The TherapyRoom Unity editor is open. Save and close it first.'}
    if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Less than 2.5 GB of free committed memory remains.'}
}
AssertClosed
$fishingSources=@(Get-ChildItem -LiteralPath "$fishingStage\Payload" -Filter '*.cs' -Recurse)
$fishingNew=@('Runtime\KoiFishingRound.cs','Runtime\KoiCatchInventory.cs','Runtime\KoiFishingViews.cs','Runtime\WellnessFishing.cs','Editor\TherapyFishingSetup.cs')
if($ResumeBackup){
    $fishingBackup=(Resolve-Path -LiteralPath $ResumeBackup).Path
    if(!$fishingBackup.StartsWith((Join-Path $fishingProject 'TherapyBackups\Fishing\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Resume backup is outside this feature backup folder.'}
    if(!(Test-Path -LiteralPath "$fishingBackup\TherapyRoom-before-fishing.unity")){throw 'Original scene backup missing.'}
    foreach($entry in (Get-Content -LiteralPath "$fishingStage\Baseline.json" -Raw | ConvertFrom-Json)){
        if((Get-FileHash -LiteralPath (Join-Path $fishingBackup $entry.File)).Hash -ne $entry.SHA256){throw "Original source backup mismatch: $($entry.File)."}
    }
    foreach($source in $fishingSources){
        $relative=$source.FullName.Substring(("$fishingStage\Payload\").Length)
        if((Get-FileHash -LiteralPath (Join-Path $fishingAssets $relative)).Hash -ne (Get-FileHash -LiteralPath $source.FullName).Hash){throw "Resumed source differs from staged update: $relative."}
    }
}else{
    foreach($entry in (Get-Content -LiteralPath "$fishingStage\Baseline.json" -Raw | ConvertFrom-Json)){
        if((Get-FileHash -LiteralPath (Join-Path $fishingAssets $entry.File) -Algorithm SHA256).Hash -ne $entry.SHA256){throw "Live source changed since staging: $($entry.File)."}
    }
    foreach($relative in $fishingNew){if(Test-Path -LiteralPath (Join-Path $fishingAssets $relative)){throw "Unexpected existing fishing file: $relative."}}
    $fishingBackup=Join-Path $fishingProject ('TherapyBackups\Fishing\'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-payload')
    New-Item -ItemType Directory -Path $fishingBackup -Force | Out-Null
}
$fishingProofFiles=@('Exterior\KoiPond\KoiLibrary.asset','Runtime\WellnessExplorer.cs','Runtime\WellnessKoiPond.cs','Runtime\WellnessMainMenu.cs','Runtime\WellnessSeasonCycle.cs','Runtime\TherapyGame.Runtime.asmdef')
$fishingProof=@()
foreach($relative in $fishingProofFiles){
    $path=Join-Path $fishingAssets $relative
    if(Test-Path -LiteralPath $path){$fishingProof+=@{File=$relative;SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}}
}
if(!$ResumeBackup){
Copy-Item -LiteralPath "$fishingAssets\Scenes\TherapyRoom.unity" -Destination "$fishingBackup\TherapyRoom-before-fishing.unity"
foreach($source in $fishingSources){
    $relative=$source.FullName.Substring(("$fishingStage\Payload\").Length)
    $live=Join-Path $fishingAssets $relative
    if(Test-Path -LiteralPath $live){
        $backup=Join-Path $fishingBackup $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
        Copy-Item -LiteralPath $live -Destination $backup
    }
}
foreach($source in $fishingSources){$relative=$source.FullName.Substring(("$fishingStage\Payload\").Length);Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $fishingAssets $relative)}
Copy-Item -LiteralPath "$fishingStage\README.md" -Destination "$fishingAssets\Documentation\Fishing.md"
}
function RunFishingCheck([string]$method,[string]$name,[bool]$graphics=$false){
    AssertClosed
    $log="$fishingChecks\$name.log"
    $arguments=@('-batchmode','-projectPath',('"'+$fishingProject+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"'))
    if(!$graphics){$arguments+=@('-nographics')}
    $process=Start-Process -FilePath $fishingUnity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    while(!$process.WaitForExit(1000)){}
    if($process.ExitCode -ne 0){
        if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log | Where-Object {$_ -match 'error CS\d|InvalidOperationException|Exception:|KOI_|FAILED|Script compilation errors'} | Select-Object -Last 18}
        throw "$name failed with exit code $($process.ExitCode). Backup: $fishingBackup"
    }
    Write-Output "PASS: $name"
}
RunFishingCheck 'TherapyGame.Editor.TherapyFishingSetup.InstallBatch' 'FishingInstall'
RunFishingCheck 'TherapyGame.Editor.TherapyFishingSetup.VerifyBatch' 'FishingReload'
RunFishingCheck 'TherapyGame.Editor.TherapyFishingSetup.PreviewBatch' 'FishingPreview' $true
foreach($relative in @('Documentation\FishingCheck.txt','Documentation\FishingReloadCheck.txt')){Copy-Item -LiteralPath (Join-Path $fishingAssets $relative) -Destination $fishingChecks}
foreach($entry in $fishingProof){if((Get-FileHash -LiteralPath (Join-Path $fishingAssets $entry.File) -Algorithm SHA256).Hash -ne $entry.SHA256){throw "Unrelated source or koi asset changed: $($entry.File)"}}
$deployed=@($fishingSources|ForEach-Object {
    $relative=$_.FullName.Substring(("$fishingStage\Payload\").Length)
    @{File=$relative;SHA256=(Get-FileHash -LiteralPath (Join-Path $fishingAssets $relative) -Algorithm SHA256).Hash}
})
@{Status='Installed and verified in Edit mode';Backup=$fishingBackup;Files=$deployed;Unchanged=$fishingProof;SceneSHA256=(Get-FileHash -LiteralPath "$fishingAssets\Scenes\TherapyRoom.unity" -Algorithm SHA256).Hash;Pending='Manual Play input/UI/lifecycle check'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$fishingChecks\Deployment.json"
Write-Output "PASS: fishing installed and fresh-reload verified. Backup: $fishingBackup"
