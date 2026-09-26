$ErrorActionPreference = 'Stop'
$source = 'D:\Hack the Hill\Deliverables\TherapyGame'
$target = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$expected = @{
    'WellnessSkyCycle.cs'='8D4F00A8E58E193DE1F837D081D9820202FFD11DBA634B5538119B2CC3875B14'
    'WellnessVoiceChat.cs'='7A6174B03E7C480060EA00605371F6B12231CA46202EC8DE2F1DB50DAFC53D29'
}
foreach ($file in $expected.Keys) {
    if ((Get-FileHash -LiteralPath "$target\Runtime\$file").Hash -ne $expected[$file]) { throw "Live $file changed; reconcile first." }
}
if (!(Test-Path -LiteralPath 'D:\Hack the Hill\Deliverables\TherapyBackups\BeforePhotographicSky-20260926.zip')) {throw 'Scene backup is missing.'}
$memory = Get-CimInstance Win32_OperatingSystem
if ($memory.FreeVirtualMemory -lt 1048576) {throw 'Import deferred: available committed memory is below 1 GB.'}
foreach ($relative in @(
    'Weather\Textures\PhotographicCloudAtlas.png',
    'Weather\Shaders\PhotographicCloud.shader',
    'Weather\Shaders\NaturalSky.shader',
    'Runtime\WellnessCloudDeck.cs',
    'Runtime\WellnessSkyCycle.cs',
    'Runtime\WellnessVoiceChat.cs',
    'Editor\TherapySkyRefinement.cs',
    'Documentation\PhotographicSky.md'
)) {
    $destination = Join-Path $target $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $destination -Force
}
Copy-Item -LiteralPath "$source\SkyRefinementRequest.txt" -Destination "$target\SkyRefinementRequest.txt"
Write-Output 'Photographic clouds/sky staged. No scene replacement, Play mode, microphone, GPU preview or bake requested.'
