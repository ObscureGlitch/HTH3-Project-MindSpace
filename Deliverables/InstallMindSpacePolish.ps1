$ErrorActionPreference='Stop'
$polishStaging='D:\Hack the Hill\Deliverables\TherapyGame'
$polishProject='D:\Unity\HTH3 Project\Assets\TherapyGame'
$polishBackup='D:\Hack the Hill\Deliverables\TherapyBackups\BeforeMindSpacePolish-20260926.zip'
if((Get-Content -LiteralPath "$polishProject\QuietHudRequest.txt" -Raw).Trim() -ne 'installed-live-check-pending'){throw 'Verify the quiet HUD import before proceeding.'}
if(Test-Path -LiteralPath $polishBackup){throw 'Backup exists; inspect before retrying.'}
$polishExpected=@{
 'Runtime\WellnessHud.cs'='F6CEDDEED8C14214512D18D0B036F21911FACCF1B2213F57D844A858765A50F1'
 'Runtime\WellnessUiTheme.cs'='2B77B000C7766C54969AA52D3A40906EF6409C6966453B55FD6056447569F7D3'
 'Runtime\BackgroundMusicPlayer.cs'='6820DB00E365A6D9F6CCDEF1632CE28308D13084131B83CC2882AF13009626A3'
}
foreach($relative in $polishExpected.Keys){if((Get-FileHash -LiteralPath (Join-Path $polishProject $relative)).Hash -ne $polishExpected[$relative]){throw "Live source changed: $relative"}}
$polishNew=@('Editor\MindSpaceExteriorGeometry.cs','Editor\MindSpaceWallLighting.cs','Editor\TherapyMindSpacePolish.cs','Runtime\WellnessPlasterLighting.cs','Documentation\MindSpacePolish.md','MindSpacePolishRequest.txt')
foreach($relative in $polishNew){if(Test-Path -LiteralPath (Join-Path $polishProject $relative)){throw "New target exists: $relative"}}
$polishFiles=@($polishExpected.Keys)+$polishNew
foreach($relative in $polishFiles){if(!(Test-Path -LiteralPath (Join-Path $polishStaging $relative))){throw "Missing staged file: $relative"}}
$polishSources=@("$polishProject\Scenes\TherapyRoom.unity")+@($polishExpected.Keys|ForEach-Object{Join-Path $polishProject $_})
Compress-Archive -LiteralPath $polishSources -DestinationPath $polishBackup -CompressionLevel Fastest
foreach($relative in $polishFiles){
 if($relative -eq 'MindSpacePolishRequest.txt'){continue}
 Copy-Item -LiteralPath (Join-Path $polishStaging $relative) -Destination (Join-Path $polishProject $relative) -Force
 if((Get-FileHash -LiteralPath (Join-Path $polishStaging $relative)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $polishProject $relative)).Hash){throw "Hash mismatch: $relative"}
}
Copy-Item -LiteralPath "$polishStaging\MindSpacePolishRequest.txt" -Destination "$polishProject\MindSpacePolishRequest.txt"
Write-Output 'MindSpace facade and HUD update copied, hashes verified, previous scene/code backed up. Waiting for Assets > Refresh. No Play, bake or graphics session started.'
