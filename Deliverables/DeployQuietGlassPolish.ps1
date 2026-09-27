$ErrorActionPreference='Stop'
$quietStage='D:\Hack the Hill\Deliverables\TherapyGame'
$quietLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$quietBackup='D:\Hack the Hill\Deliverables\QuietGlassPolishBackups\'+(Get-Date -Format 'yyyyMMdd-HHmmss')
$expected=@{
 'Runtime\WellnessQuietGlassTheme.cs'='B2E0099ED325A21B0A887C28906104AF8E0B107359CDD154750F4FD08A08B10D'
 'Runtime\WellnessQuietGlassMenu.cs'='AE7EE49885CC94E7117670B32E1CBD3B4DABC62009D2F74D294105A619C7A0F2'
 'Runtime\WellnessUiTheme.cs'='5EAE887745ADF19AA5934ECD7353E811442D75CA24B5611925C6E509083C2FE5'
}
foreach($relative in $expected.Keys){if((Get-FileHash -LiteralPath "$quietLive\$relative").Hash -ne $expected[$relative]){throw "Live UI file changed since inspection: $relative"}}
$files=@('Runtime\WellnessUiMotion.cs','Runtime\WellnessUiPrimitives.cs','Runtime\WellnessQuietGlassTheme.cs','Runtime\WellnessQuietGlassMenu.cs','Runtime\WellnessUiTheme.cs','Editor\QuietGlassPolishChecks.cs','Editor\TherapyQuietGlassPolish.cs','UI\QuietGlass\README.md','QuietGlassPolishRequest.txt')
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
Write-Output 'Only UI drawing files and verification support were copied. No scene, voice, weather behavior, lighting or project settings were overwritten.'
