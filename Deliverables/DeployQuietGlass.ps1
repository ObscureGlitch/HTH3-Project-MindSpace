$ErrorActionPreference='Stop'
$quietStage='D:\Hack the Hill\Deliverables\TherapyGame'
$quietLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$quietBackup='D:\Hack the Hill\Deliverables\QuietGlassBackups\'+(Get-Date -Format 'yyyyMMdd-HHmmss')
$expected=@{
 'Runtime\WellnessVoiceChat.cs'='63251F7FA2CB57A07D4403FB70F32B99D31D5E9962269F103FDFA3B8AD34A3E2'
 'Runtime\OutdoorNatureAmbience.cs'='8E94F6CE432C9F25AF8C56C4EA906C9F65279762CD811C51A0A66855491D6D2E'
 'Runtime\WellnessRain.cs'='25F142BA9777F04256249C3B4E0EF737BD994459FF61A32992FBF7C3E7E37651'
}
foreach($relative in $expected.Keys){if((Get-FileHash -LiteralPath "$quietLive\$relative").Hash -ne $expected[$relative]){throw "Live file changed since inspection: $relative"}}
$files=@('Runtime\WellnessVoiceChat.cs','Runtime\OutdoorNatureAmbience.cs','Runtime\WellnessRain.cs','Runtime\WellnessQuietGlassLayout.cs','Runtime\WellnessQuietGlassTheme.cs','Runtime\WellnessQuietGlassMenu.cs','Editor\QuietGlassLayoutChecks.cs','Editor\TherapyQuietGlassUpgrade.cs','UI\QuietGlass\Fonts\SourceSerif4-Regular.otf','UI\QuietGlass\Fonts\SourceSerif-LICENSE.md','UI\QuietGlass\Fonts\Carlito-Regular.ttf','UI\QuietGlass\Fonts\Carlito-OFL.txt','UI\QuietGlass\README.md','QuietGlassRequest.txt')
New-Item -ItemType Directory -Path $quietBackup -Force | Out-Null
Copy-Item -LiteralPath "$quietLive\Scenes\TherapyRoom.unity" -Destination "$quietBackup\TherapyRoom_BeforeQuietGlass.unity"
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
Write-Output 'Scoped import ready. Keep Play stopped; use Assets > Refresh.'
