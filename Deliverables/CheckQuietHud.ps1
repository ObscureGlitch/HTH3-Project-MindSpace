$ErrorActionPreference = 'Stop'
Add-Type -Path @(
 'D:\Hack the Hill\Deliverables\TherapyGame\Runtime\WellnessCaptions.cs',
 'D:\Hack the Hill\Deliverables\TherapyGame\Runtime\WellnessPlaylist.cs',
 'D:\Hack the Hill\Deliverables\TherapyGame\Editor\QuietHudStateChecks.cs'
)
$quietChecks = [TherapyGame.Editor.QuietHudStateChecks]::Run()
Write-Output "PASS: $quietChecks pure caption/playlist assertions. No Unity, graphics, microphone or network session."
