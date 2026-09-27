$ErrorActionPreference='Stop'
$cloudControlStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$cloudControlLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$cloudControlExpected=@{
 'Runtime\WellnessCloudDeck.cs'='7953EA291AD6E0E57193FFB4878FCE83BFDE660EF602C0F8B6A1525231FD828D'
 'Runtime\WellnessCloudSequence.cs'='F081ABE51B8FFC74764613998848FD1A3BE29F0E70A6335A2FDE2B287FF4B8D7'
 'Runtime\WellnessHud.cs'='F033CF440DD58AC092E7E9DBA6F25E5EA0FEDBAC24E254309D673F9368A36BD8'
 'Runtime\WellnessQuietGlassMenu.cs'='76920AB5412013ED160A194A27093A4FA156C5B72A08CF9A0B29D84E3432C571'
 'Editor\CloudSequenceChecks.cs'='A1892A7D4FD87E9B74BF5E282FCE13C6C8B0EEEEC7604AB56F177A4C4621339B'
 'Editor\TherapyCloudTypesUpgrade.cs'='6DEAB4F8BF38212DAB09AAFA247999154249C20DFBE3C038D685C32AE4408ACC'
 'Documentation\CloudTypes.md'='DEB75A488B4A8A450B3EE08C84B6E7C7BE56804646EBC7CE32A1FDC2091ABCEC'
 'CloudTypesRequest.txt'='4B023FFD4D6B67ED3EC42FA3AB3AA284CFB20BB52A34C3F1D047BB05653E0432'
}
$cloudControlMemory=Get-CimInstance Win32_OperatingSystem
if($cloudControlMemory.FreeVirtualMemory -lt 2097152 -or $cloudControlMemory.FreePhysicalMemory -lt 1572864){throw 'Need at least 2 GB available committed memory and 1.5 GB physical before the scoped import.'}
$cloudControlFiles=@('Runtime\WellnessCloudSequence.cs','Runtime\WellnessCloudDeck.cs','Runtime\WellnessHud.cs','Runtime\WellnessQuietGlassMenu.cs','Editor\CloudSequenceChecks.cs','Editor\TherapyCloudTypesUpgrade.cs','Documentation\CloudTypes.md','CloudTypesRequest.txt')
foreach($relative in $cloudControlFiles){
 if(!(Test-Path -LiteralPath "$cloudControlStaged\$relative")){throw "Missing staged file: $relative"}
 if((Get-FileHash -LiteralPath "$cloudControlLive\$relative").Hash -ne $cloudControlExpected[$relative]){throw "Live file changed; reconcile before copying: $relative"}
 if($relative -match '^(Runtime|Editor)\\.*\.cs$'){
  $binary=Get-Item -LiteralPath ("D:\Hack the Hill\Deliverables\CloudTypesCodeCheck\TherapyGame."+$Matches[1]+'.dll')
  if((Get-Item -LiteralPath "$cloudControlStaged\$relative").LastWriteTimeUtc -gt $binary.LastWriteTimeUtc){throw "Compile again after editing $relative"}
 }
}
$cloudProtected=@('Weather\Shaders\PhotographicCloud.shader','Weather\Shaders\NaturalSky.shader','Exterior\Shaders\QuietPond.shader','Weather\Textures\CloudTypes\PuffyCumulusAtlas.png','Weather\Textures\CloudTypes\AltocumulusAtlas.png')
$cloudProtectedHashes=@{}
foreach($relative in $cloudProtected){$cloudProtectedHashes[$relative]=(Get-FileHash -LiteralPath "$cloudControlLive\$relative").Hash}
$cloudControlBackup='D:\Unity\HTH3 Project\TherapyBackups\CloudControls\'+(Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $cloudControlBackup -Force | Out-Null
Copy-Item -LiteralPath "$cloudControlLive\Scenes\TherapyRoom.unity" -Destination "$cloudControlBackup\TherapyRoom-on-disk.unity"
foreach($relative in $cloudControlFiles){
 $target=Join-Path $cloudControlBackup $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
 Copy-Item -LiteralPath "$cloudControlLive\$relative" -Destination $target
}
foreach($relative in $cloudControlFiles){
 Copy-Item -LiteralPath "$cloudControlStaged\$relative" -Destination "$cloudControlLive\$relative"
 if((Get-FileHash -LiteralPath "$cloudControlStaged\$relative").Hash -ne (Get-FileHash -LiteralPath "$cloudControlLive\$relative").Hash){throw "Copy verification failed: $relative"}
 Write-Output "Verified scoped copy: $relative"
}
foreach($relative in $cloudProtected){if((Get-FileHash -LiteralPath "$cloudControlLive\$relative").Hash -ne $cloudProtectedHashes[$relative]){throw "Unexpected asset change: $relative"}}
Write-Output "Backup: $cloudControlBackup"
Write-Output 'No voice/companion source, image assets or shaders copied. Ready for Assets > Refresh with Play stopped; the one-time cloud installer is armed.'
