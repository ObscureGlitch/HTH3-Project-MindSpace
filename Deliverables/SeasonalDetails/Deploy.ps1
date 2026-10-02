$ErrorActionPreference='Stop'
$detailStage=$PSScriptRoot
$detailProject='D:\Unity\HTH3 Project'
$detailAssets=Join-Path $detailProject 'Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Unity is open. Close it before a batch install.'}
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Insufficient memory for Unity validation.'}
$detailBaseline=Get-Content -LiteralPath "$detailStage\Baseline.json" -Raw|ConvertFrom-Json
foreach($detailEntry in $detailBaseline.Files){if((Get-FileHash -LiteralPath (Join-Path $detailAssets $detailEntry.File)).Hash -ne $detailEntry.SHA256){throw "Source changed since preparation: $($detailEntry.File)"}}
if((Get-FileHash -LiteralPath "$detailAssets\Scenes\TherapyRoom.unity").Hash -ne $detailBaseline.SceneSHA256){throw 'Scene changed since preparation.'}
$detailBackup=Join-Path $detailProject ('TherapyBackups\SeasonalDetails\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $detailBackup | Out-Null
foreach($detailEntry in $detailBaseline.Files){
    $detailDestination=Join-Path $detailBackup $detailEntry.File
    New-Item -ItemType Directory -Path (Split-Path -Parent $detailDestination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $detailAssets $detailEntry.File) -Destination $detailDestination
}
Copy-Item -LiteralPath "$detailAssets\Scenes\TherapyRoom.unity" -Destination "$detailBackup\TherapyRoom-before.unity"
[IO.File]::WriteAllText("$detailStage\Checks\Backup.txt",$detailBackup)
foreach($detailFile in Get-ChildItem -LiteralPath "$detailStage\Payload" -File -Recurse){
    $detailRelative=$detailFile.FullName.Substring(("$detailStage\Payload\").Length)
    $detailDestination=Join-Path $detailAssets $detailRelative
    if(-not ($detailBaseline.Files.File -contains $detailRelative) -and (Test-Path -LiteralPath $detailDestination)){throw "Unexpected existing file: $detailRelative"}
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $detailDestination) | Out-Null
    Copy-Item -LiteralPath $detailFile.FullName -Destination $detailDestination
}
$detailLog="$detailStage\Checks\Unity-Install.log"
$detailArguments=@('-batchmode','-nographics','-projectPath',('"'+$detailProject+'"'),'-executeMethod','TherapyGame.Editor.SeasonalDetailsSetup.InstallBatch','-logFile',('"'+$detailLog+'"'))
$detailRun=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList $detailArguments -WindowStyle Hidden -PassThru
[pscustomobject]@{ProcessId=$detailRun.Id;Backup=$detailBackup;Log=$detailLog}|ConvertTo-Json
