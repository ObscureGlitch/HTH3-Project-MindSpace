$ErrorActionPreference='Stop'
$cloudStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$cloudLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Save and close Unity before this scoped cloud import.'}
$cloudExpected='D8E269EDCAA1904E1AC72808150AE412314591CE0B04369A68D2B0634D3C95DA'
if((Get-FileHash -LiteralPath "$cloudLive\Runtime\WellnessCloudDeck.cs").Hash -ne $cloudExpected){throw 'Live cloud deck changed; reconcile it before deployment.'}
$cloudUnchanged=@{
 'Weather\Shaders\PhotographicCloud.shader'='00CE2783268DB67DE3A84A226F67CC5E80E53F0D4148646CB971B5F389459F36'
 'Exterior\Shaders\QuietPond.shader'='1A5A0103901EF32420E0E55899932641562928355511606BFAC5E235FC8A3B70'
}
foreach($relative in $cloudUnchanged.Keys){if((Get-FileHash -LiteralPath "$cloudLive\$relative").Hash -ne $cloudUnchanged[$relative]){throw "Shader changed; review first: $relative"}}
$cloudNew=@('Runtime\WellnessCloudSequence.cs','Editor\CloudSequenceChecks.cs','Editor\TherapyCloudTypesUpgrade.cs','Weather\Textures\CloudTypes\PuffyCumulusAtlas.png','Weather\Textures\CloudTypes\AltocumulusAtlas.png','Documentation\CloudTypes.md','CloudTypesRequest.txt')
foreach($relative in $cloudNew){if(Test-Path -LiteralPath "$cloudLive\$relative"){throw "Unexpected existing live file: $relative"}}
$cloudFiles=@('Runtime\WellnessCloudDeck.cs')+$cloudNew
foreach($relative in $cloudFiles){if(!(Test-Path -LiteralPath "$cloudStaged\$relative")){throw "Missing staged file: $relative"}}
foreach($kind in @('Runtime','Editor')){
 $binary=Get-Item -LiteralPath "D:\Hack the Hill\Deliverables\CloudTypesCodeCheck\TherapyGame.$kind.dll"
 foreach($relative in $cloudFiles | Where-Object {$_ -like "$kind\*.cs"}){
  if((Get-Item -LiteralPath "$cloudStaged\$relative").LastWriteTimeUtc -gt $binary.LastWriteTimeUtc){throw "Compile again after editing $relative"}
 }
}
$cloudBackup='D:\Unity\HTH3 Project\TherapyBackups\CloudTypesDeployment\'+(Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $cloudBackup -Force | Out-Null
Copy-Item -LiteralPath "$cloudLive\Scenes\TherapyRoom.unity" -Destination "$cloudBackup\TherapyRoom-before-import.unity"
Copy-Item -LiteralPath "$cloudLive\Runtime\WellnessCloudDeck.cs" -Destination "$cloudBackup\WellnessCloudDeck.cs"
foreach($relative in $cloudFiles){
 $target=Join-Path $cloudLive $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
 Copy-Item -LiteralPath "$cloudStaged\$relative" -Destination $target
 if((Get-FileHash -LiteralPath "$cloudStaged\$relative").Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw "Copy verification failed: $relative"}
 Write-Output "Verified scoped copy: $relative"
}
foreach($relative in $cloudUnchanged.Keys){if((Get-FileHash -LiteralPath "$cloudLive\$relative").Hash -ne $cloudUnchanged[$relative]){throw "Unexpected shader change: $relative"}}
Write-Output "Backup: $cloudBackup"
Write-Output 'Ready to reopen TherapyRoom with Play stopped. No shaders changed; the one-time installer only configures the two new atlases and their scene references.'
