$ErrorActionPreference='Stop'
$hoverLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$hoverStage='D:\Hack the Hill\Deliverables\TherapyGame'
$hoverOriginal=@{
 'Runtime\WellnessMainMenu.cs'='6D347B7B772C4D7D2283E44F10632AB5BB0878A4A1DE6D67B96CCF57B2415C3F'
 'Runtime\WellnessMainMenuFlow.cs'='F195597A6655B878020E2A5FDCD5931BA00764EBD8FC069B7CC80E92FB667862'
}
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Save the project and close Unity before this scoped update.'}
$hoverMemory=Get-CimInstance Win32_OperatingSystem
if($hoverMemory.FreeVirtualMemory -lt 1572864 -or $hoverMemory.FreePhysicalMemory -lt 1048576){throw 'Need 1.5 GB committed and 1 GB physical headroom before checks/deployment.'}
foreach($relative in $hoverOriginal.Keys){
 if((Get-FileHash -LiteralPath (Join-Path $hoverLive $relative)).Hash -ne $hoverOriginal[$relative]){throw "Live file changed since inspection: $relative. Reconcile before deploying."}
}
& 'D:\Hack the Hill\Deliverables\CheckMainMenu.ps1'
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity reopened during checks. Nothing copied.'}
$hoverFiles=@('Runtime\WellnessMainMenu.cs','Runtime\WellnessMainMenuFlow.cs','Documentation\NightSanctuaryMainMenu.md')
$hoverBackup=Join-Path 'D:\Unity\HTH3 Project\TherapyBackups\MainMenuHover' (Get-Date -Format 'yyyyMMdd-HHmmss')
foreach($relative in $hoverOriginal.Keys){
 if((Get-FileHash -LiteralPath (Join-Path $hoverLive $relative)).Hash -ne $hoverOriginal[$relative]){throw "Live file changed during checks: $relative."}
}
foreach($relative in $hoverFiles){
 $saved=Join-Path $hoverBackup $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $hoverLive $relative) -Destination $saved
}
foreach($relative in $hoverFiles){
 $source=Join-Path $hoverStage $relative
 $target=Join-Path $hoverLive $relative
 Copy-Item -LiteralPath $source -Destination $target
 if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw "Copy verification failed: $relative"}
}
'PASS: checked hover/pop update copied; two runtime scripts and their documentation only.'
"Backup: $hoverBackup"
'No scene, shader, material, consent setting, pause UI or camera configuration modified. Reopen Unity to import.'
