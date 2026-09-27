$ErrorActionPreference = 'Stop'
$quietStaging = 'D:\Hack the Hill\Deliverables\TherapyGame'
$quietProject = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$quietBackup = 'D:\Hack the Hill\Deliverables\TherapyBackups\BeforeQuietHud-20260926.zip'
if ((Get-Content -LiteralPath "$quietProject\HudRequest.txt" -Raw).Trim() -ne 'installed-live-check-pending') { throw 'Verify the previous reference HUD installation first.' }
if (Test-Path -LiteralPath $quietBackup) { throw 'Quiet HUD backup already exists. Inspect before retrying.' }
$quietExpected = @{
 'Runtime\WellnessHud.cs'='9188C3260F923966308E5DE0B268124445800AFD51DEDFFCA14A233B7D4E5B37'
 'Runtime\WellnessCaptions.cs'='13E00778230F8386AF596BC4E6391BB76DDB6967825131CCA0F677724D4CF586'
 'Runtime\BackgroundMusicPlayer.cs'='89DBB31A40F04CA88ECAEADA4423C2A010ACC2D91CC459FCB67131037B6AA32E'
 'Runtime\WellnessVoiceChat.cs'='284588F7550FF95E288B2C8ACF0342B3B73D8401F4240A2DFB17E24DDFBB740B'
 'Runtime\WellnessUiTheme.cs'='2CCE813F932E43BA40E095B533C34D55506800957824691BA86C47E86EDFB542'
}
foreach ($relative in $quietExpected.Keys) {
 if ((Get-FileHash -LiteralPath (Join-Path $quietProject $relative)).Hash -ne $quietExpected[$relative]) { throw "Existing file changed; inspect before overwriting: $relative" }
}
$quietNew = @('Runtime\WellnessPlaylist.cs','Editor\QuietHudStateChecks.cs','Editor\TherapyQuietHudSetup.cs','Documentation\QuietHud.md','QuietHudRequest.txt')
foreach ($relative in $quietNew) {
 if (Test-Path -LiteralPath (Join-Path $quietProject $relative)) { throw "New target already exists; inspect first: $relative" }
}
$quietFiles = @($quietExpected.Keys) + $quietNew
foreach ($relative in $quietFiles) { if (!(Test-Path -LiteralPath (Join-Path $quietStaging $relative))) { throw "Missing staged file: $relative" } }
$quietSources = @("$quietProject\Scenes\TherapyRoom.unity") + @($quietExpected.Keys | ForEach-Object { Join-Path $quietProject $_ })
Compress-Archive -LiteralPath $quietSources -DestinationPath $quietBackup -CompressionLevel Fastest
foreach ($relative in $quietFiles) {
 if ($relative -eq 'QuietHudRequest.txt') { continue }
 Copy-Item -LiteralPath (Join-Path $quietStaging $relative) -Destination (Join-Path $quietProject $relative) -Force
 if ((Get-FileHash -LiteralPath (Join-Path $quietStaging $relative)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $quietProject $relative)).Hash) { throw "Copy verification failed: $relative" }
}
# Arm last, after all source files are complete. No old installer marker is rearmed.
Copy-Item -LiteralPath "$quietStaging\QuietHudRequest.txt" -Destination "$quietProject\QuietHudRequest.txt"
Write-Output 'Quiet HUD copied and all source hashes verified. Scene and previous code backed up. Awaiting Assets > Refresh; no Play, render, mic or network session started.'
