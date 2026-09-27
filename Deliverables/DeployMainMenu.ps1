$ErrorActionPreference='Stop'
$menuStage='D:\Hack the Hill\Deliverables\TherapyGame'
$menuProject='D:\Unity\HTH3 Project'
$menuLive=Join-Path $menuProject 'Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Save TherapyRoom and close Unity before deployment.'}
$menuMemory=Get-CimInstance Win32_OperatingSystem
if($menuMemory.FreeVirtualMemory -lt 1572864 -or $menuMemory.FreePhysicalMemory -lt 1048576){throw 'Insufficient memory for the upcoming import.'}
$menuFiles=@('Runtime\WellnessMainMenuFlow.cs','Runtime\WellnessMainMenu.cs','Editor\TherapyMainMenuSetup.cs','Documentation\NightSanctuaryMainMenu.md','MainMenuRequest.txt')
foreach($menuFile in $menuFiles){
 if(!(Test-Path -LiteralPath (Join-Path $menuStage $menuFile))){throw "Missing staged file: $menuFile"}
 if(Test-Path -LiteralPath (Join-Path $menuLive $menuFile)){throw "Existing live file must be inspected before overwrite: $menuFile"}
}
& 'D:\Hack the Hill\Deliverables\CheckMainMenu.ps1'
if(!$?){throw 'Main-menu checks failed.'}
$menuBackup=Join-Path $menuProject ('TherapyBackups\MainMenuDeployment\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $menuBackup | Out-Null
Copy-Item -LiteralPath (Join-Path $menuProject 'Assets\TherapyGame\Scenes\TherapyRoom.unity') -Destination (Join-Path $menuBackup 'TherapyRoom-before-import.unity')
Copy-Item -LiteralPath (Join-Path $menuProject 'ProjectSettings\EditorBuildSettings.asset') -Destination (Join-Path $menuBackup 'EditorBuildSettings.asset')
# Request is last, so a partial copy cannot trigger an incomplete installation.
foreach($menuFile in $menuFiles){
 Copy-Item -LiteralPath (Join-Path $menuStage $menuFile) -Destination (Join-Path $menuLive $menuFile)
 if((Get-FileHash -LiteralPath (Join-Path $menuStage $menuFile)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $menuLive $menuFile)).Hash){throw "Copy mismatch: $menuFile"}
}
'PASS: five scoped new main-menu files copied; no existing runtime, shader, material or scene overwritten.'
'Backup: '+$menuBackup
'Reopen Unity with TherapyRoom and Play stopped, then inspect Assets/TherapyGame/UI/MainMenuCheck.txt.'
