$ErrorActionPreference='Stop'
$quietStage='D:\Hack the Hill\Deliverables\TherapyGame'
$quietLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$quietBackup='D:\Hack the Hill\Deliverables\QuietGlassStaticTextBackups\'+(Get-Date -Format 'yyyyMMdd-HHmmss')
$expected=@{
 'Runtime\WellnessQuietGlassTheme.cs'='B7E4048AB9B17A04B3F868EFB540111D997C87BC425BC63EE239DA3F17108C69'
 'Runtime\WellnessQuietGlassMenu.cs'='21034A4951B04C668432B5A7446B71E82B8A4783FC51B5C713B45EB1AAC90629'
 'Runtime\WellnessUiTheme.cs'='D57F4D16C6286601D0607A5F907626B2C0BDBFA36159DF0118418FB3F20A910B'
 'Runtime\WellnessUiMotion.cs'='D08F3B46E54BDEA94FEF113A6E264341B8E780F73060077A2268A35228EFC7A8'
}
foreach($relative in $expected.Keys){if((Get-FileHash -LiteralPath "$quietLive\$relative").Hash -ne $expected[$relative]){throw "Live UI file changed since inspection: $relative"}}
$files=@('Runtime\WellnessUiText.cs','Runtime\WellnessUiMotion.cs','Runtime\WellnessQuietGlassTheme.cs','Runtime\WellnessQuietGlassMenu.cs','Runtime\WellnessUiTheme.cs','Editor\QuietGlassTextChecks.cs','Editor\QuietGlassPolishChecks.cs','Editor\TherapyQuietGlassPolish.cs','UI\QuietGlass\README.md','QuietGlassPolishRequest.txt')
New-Item -ItemType Directory -Path $quietBackup -Force | Out-Null
foreach($relative in $files){
 $source=Join-Path $quietStage $relative
 $target=Join-Path $quietLive $relative
 if(!(Test-Path -LiteralPath $source -PathType Leaf)){throw "Missing staged file: $relative"}
 if(Test-Path -LiteralPath $target){
  $copy=Join-Path $quietBackup $relative
  New-Item -ItemType Directory -Path (Split-Path $copy) -Force | Out-Null
  Copy-Item -LiteralPath $target -Destination $copy
 }
}
foreach($relative in $files){
 $source=Join-Path $quietStage $relative
 $target=Join-Path $quietLive $relative
 New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
 Copy-Item -LiteralPath $source -Destination $target -Force
 if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw "Deployment mismatch: $relative"}
 Write-Output "Verified: $relative"
}
Write-Output "Backup: $quietBackup"
Write-Output 'UI-only deployment complete; no scene or gameplay changes.'
