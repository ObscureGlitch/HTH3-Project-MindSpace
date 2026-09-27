$ErrorActionPreference = 'Stop'
$hudStaging = 'D:\Hack the Hill\Deliverables\TherapyGame'
$hudProject = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$hudBackup = 'D:\Hack the Hill\Deliverables\TherapyBackups\BeforeReferenceHud-20260926.zip'
if ((Get-Content -LiteralPath "$hudProject\RoomVoiceRequest.txt" -Raw).Trim() -ne 'installed-live-check-pending') { throw 'Verify the completed room voice update before installing the HUD.' }
if (Test-Path -LiteralPath $hudBackup) { throw 'HUD backup already exists. Inspect before retrying.' }
$hudExpected = @{
 'Runtime\WellnessVoiceChat.cs'='9484E96906F4775882D0E205CC44BBC4B719724EC7D9BE2D9C743410599B8D62'
 'Runtime\WellnessExplorer.cs'='7B39EDBE500196223CBB45BF0DE173061FDDDA3653486E31625980C70E6ED38E'
 'Runtime\BackgroundMusicPlayer.cs'='4AB6E6921653EED01C9AA7008DB3C76D3DA2E9148DC40A00100698F3E583E3F9'
 'Runtime\WellnessInteraction.cs'='5074F414E7116B0056BA113B31F73DC3EB2E4416B3F69C6F68F58641D723CC3A'
}
foreach ($relative in $hudExpected.Keys) {
 if ((Get-FileHash -LiteralPath (Join-Path $hudProject $relative)).Hash -ne $hudExpected[$relative]) { throw "Existing source changed; inspect before overwriting: $relative" }
}
$hudFiles = @('Runtime\WellnessCaptions.cs','Runtime\WellnessUiTheme.cs','Runtime\WellnessHud.cs','Runtime\WellnessVoiceChat.cs','Runtime\WellnessExplorer.cs','Runtime\WellnessInteraction.cs','Runtime\BackgroundMusicPlayer.cs','Editor\TherapyHudSetup.cs','Documentation\ReferenceHud.md')
foreach ($relative in $hudFiles) { if (!(Test-Path -LiteralPath (Join-Path $hudStaging $relative))) { throw "Missing staged HUD file: $relative" } }
$hudSources = @("$hudProject\Scenes\TherapyRoom.unity") + @($hudExpected.Keys | ForEach-Object { Join-Path $hudProject $_ })
Compress-Archive -LiteralPath $hudSources -DestinationPath $hudBackup -CompressionLevel Fastest
foreach ($relative in $hudFiles) { Copy-Item -LiteralPath (Join-Path $hudStaging $relative) -Destination (Join-Path $hudProject $relative) -Force }
Copy-Item -LiteralPath "$hudStaging\HudRequest.txt" -Destination "$hudProject\HudRequest.txt"
Write-Output 'Reference HUD staged in TherapyGame with scene/code backup. No game, rendering, microphone or service session started.'
