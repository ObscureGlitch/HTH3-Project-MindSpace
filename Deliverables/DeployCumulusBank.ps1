$ErrorActionPreference='Stop'
$bankStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$bankLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$bankExpected=@{
 'Runtime\WellnessCloudDeck.cs'='DFBBA65EA8890B64C352024175EAC003AF90C09E8FDC6E7FA97144B3FFC3785F'
 'Exterior\Shaders\QuietPond.shader'='1A5A0103901EF32420E0E55899932641562928355511606BFAC5E235FC8A3B70'
 'Weather\Shaders\PhotographicCloud.shader'='00CE2783268DB67DE3A84A226F67CC5E80E53F0D4148646CB971B5F389459F36'
}
$bankNew=@('Runtime\WellnessCumulusLife.cs','Editor\CumulusBankChecks.cs','Editor\TherapyCumulusBankUpgrade.cs','Weather\Textures\CloudTypes\CumulusBank.png','Weather\Shaders\WellnessCloudOcclusion.hlsl','Documentation\CumulusBank.md','CumulusBankRequest.txt')
$bankMemory=Get-CimInstance Win32_OperatingSystem
if($bankMemory.FreeVirtualMemory -lt 2621440 -or $bankMemory.FreePhysicalMemory -lt 1835008){throw 'Need 2.5 GB available committed memory and 1.75 GB physical before the limited shader/texture import.'}
foreach($relative in $bankExpected.Keys){if((Get-FileHash -LiteralPath "$bankLive\$relative").Hash -ne $bankExpected[$relative]){throw "Live file changed; reconcile: $relative"}}
foreach($relative in $bankNew){if(Test-Path -LiteralPath "$bankLive\$relative"){throw "Unexpected existing live file: $relative"}}
$bankFiles=@('Weather\Shaders\WellnessCloudOcclusion.hlsl','Runtime\WellnessCumulusLife.cs','Runtime\WellnessCloudDeck.cs','Weather\Textures\CloudTypes\CumulusBank.png','Weather\Shaders\PhotographicCloud.shader','Exterior\Shaders\QuietPond.shader','Editor\CumulusBankChecks.cs','Editor\TherapyCumulusBankUpgrade.cs','Documentation\CumulusBank.md','CumulusBankRequest.txt')
foreach($relative in $bankFiles){
 if(!(Test-Path -LiteralPath "$bankStaged\$relative")){throw "Missing staged file: $relative"}
 if($relative -match '^(Runtime|Editor)\\.*\.cs$'){
  $binary=Get-Item -LiteralPath ("D:\Hack the Hill\Deliverables\CumulusBankCodeCheck\TherapyGame."+$Matches[1]+'.dll')
  if((Get-Item -LiteralPath "$bankStaged\$relative").LastWriteTimeUtc -gt $binary.LastWriteTimeUtc){throw "Compile again: $relative"}
 }
}
$bankBackup='D:\Unity\HTH3 Project\TherapyBackups\CumulusBankDeployment\'+(Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $bankBackup -Force | Out-Null
Copy-Item -LiteralPath "$bankLive\Scenes\TherapyRoom.unity" -Destination "$bankBackup\TherapyRoom-before-import.unity"
foreach($relative in $bankExpected.Keys){
 $target=Join-Path $bankBackup $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
 Copy-Item -LiteralPath "$bankLive\$relative" -Destination $target
}
foreach($relative in $bankFiles){
 $target=Join-Path $bankLive $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
 Copy-Item -LiteralPath "$bankStaged\$relative" -Destination $target
 if((Get-FileHash -LiteralPath "$bankStaged\$relative").Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw "Copy verification failed: $relative"}
 Write-Output "Verified scoped copy: $relative"
}
Write-Output "Backup: $bankBackup"
Write-Output 'Ready for Assets > Refresh with Play stopped. Only cloud/pond shaders, cloud runtime and the new bank assets/installer are in scope.'
