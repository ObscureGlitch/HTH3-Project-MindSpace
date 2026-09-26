$ErrorActionPreference = 'Stop'
$project = 'D:\Unity\HTH3 Project'
$staging = 'D:\Hack the Hill\Deliverables\TherapyGame'
$destination = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$sdkSource = 'D:\Hack the Hill\Deliverables\UnityPackages\io.elevenlabs.agents'
$sdkTemp = 'D:\Unity\HTH3 Project\Temp\TherapyVoiceRainPackageStaging'
$sdkTarget = 'D:\Unity\HTH3 Project\Packages\io.elevenlabs.agents'
$backup = 'D:\Hack the Hill\Deliverables\TherapyBackups\BeforeVoiceAndRain-20260926.zip'
if (!(Test-Path -LiteralPath $backup)) { throw 'Pre-integration backup missing.' }
if ((Get-FileHash -LiteralPath "$destination\Runtime\WellnessExplorer.cs" -Algorithm SHA256).Hash -ne '905CDDCCB1375D1D3892EE94856A857E40D6BA0AFBA979410594D897E45AAB25') {
    throw 'The live player has changed since it was inspected. Reconcile before installing.'
}
if ((Get-FileHash -LiteralPath "$staging\Integrations\Audio\GardenRain.mp3" -Algorithm SHA256).Hash -ne 'CA25C3C85F4FD56ABF29EC7A59FC75D243BFBE12160F4F0870FC4F18C5E2C63E') { throw 'Unexpected rain audio content.' }
foreach ($path in @($sdkTemp, $sdkTarget)) {
    $absolute = [IO.Path]::GetFullPath($path)
    if (!$absolute.StartsWith($project + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Package move would leave the selected Unity project.' }
    if (Test-Path -LiteralPath $absolute) { throw "Package destination already exists; do not overwrite: $absolute" }
}
Copy-Item -LiteralPath $sdkSource -Destination $sdkTemp -Recurse
# Both absolute paths were validated above, and the destination does not exist.
Move-Item -LiteralPath $sdkTemp -Destination $sdkTarget
foreach ($relative in @(
    'Runtime\WellnessVoiceSettings.cs',
    'Runtime\WellnessRain.cs',
    'Runtime\WellnessVoiceChat.cs',
    'Runtime\WellnessExplorer.cs',
    'Runtime\TherapyGame.Runtime.asmdef',
    'Editor\TherapyVoiceRainSetup.cs',
    'Integrations\Audio\GardenRain.mp3',
    'Documentation\VoiceAndRain.md'
)) {
    $target = Join-Path $destination $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $staging $relative) -Destination $target -Force
}
# Publish the one-use marker last, only after every required file is present.
Copy-Item -LiteralPath "$staging\VoiceRainRequest.txt" -Destination "$destination\VoiceRainRequest.txt"
Write-Output 'Voice/rain package and scoped therapy files copied. No scene replacement, package manifest rewrite, Play mode, bake, microphone or remote session was started.'
