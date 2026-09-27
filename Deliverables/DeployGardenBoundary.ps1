$ErrorActionPreference='Stop'
$boundaryStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$boundaryLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$boundaryMemory=Get-CimInstance Win32_OperatingSystem
if($boundaryMemory.FreeVirtualMemory -lt 1835008 -or $boundaryMemory.FreePhysicalMemory -lt 1572864){throw 'Insufficient memory headroom. Close unused apps before deploying the scoped boundary update.'}
$boundaryInclude='Weather\Shaders\WellnessSkySampling.hlsl'
if((Get-FileHash -LiteralPath "$boundaryLive\$boundaryInclude").Hash -ne '99C3BF09470C732D463A2BF9E45385EBF96C050A0467C4085B60F329913204C1'){throw 'Live sky include changed. Reconcile it before deployment.'}
$boundaryFiles=@('Editor\GardenHorizonGeometry.cs','Editor\TherapyGardenBoundaryUpgrade.cs','Weather\Shaders\WellnessDistantLandscape.hlsl',$boundaryInclude,'Documentation\GardenBoundary.md','GardenBoundaryRequest.txt')
foreach($relative in $boundaryFiles){
 if(!(Test-Path -LiteralPath "$boundaryStaged\$relative")){throw "Missing staged file: $relative"}
 if($relative -ne $boundaryInclude -and (Test-Path -LiteralPath "$boundaryLive\$relative")){throw "Unexpected existing live file: $relative"}
}
$boundaryBackup='D:\Unity\HTH3 Project\TherapyBackups\GardenBoundary\Files-'+(Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $boundaryBackup -Force | Out-Null
Copy-Item -LiteralPath "$boundaryLive\$boundaryInclude" -Destination "$boundaryBackup\WellnessSkySampling.hlsl"
Copy-Item -LiteralPath "$boundaryLive\Scenes\TherapyRoom.unity" -Destination "$boundaryBackup\TherapyRoom-on-disk.unity"
foreach($relative in $boundaryFiles){
 $destination=Join-Path $boundaryLive $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
 Copy-Item -LiteralPath "$boundaryStaged\$relative" -Destination $destination
 if((Get-FileHash -LiteralPath "$boundaryStaged\$relative").Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw "Copy verification failed: $relative"}
 Write-Output "Verified scoped copy: $relative"
}
Write-Output "Backup: $boundaryBackup"
Write-Output 'Keep Play stopped. Assets > Refresh will run the guarded one-time boundary installer.'
