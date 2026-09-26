$ErrorActionPreference = 'Stop'
$staging = 'D:\Hack the Hill\Deliverables\TherapyGame'
$destination = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$expected = @{
    'WellnessRain.cs'='EAACD9A308AFED696FDFE5AA083EC9DAA96B65307D92265DDC7C5A7252133569'
    'WellnessVoiceChat.cs'='48546A07766012DC013F9A5ABB90416A3585FCCB03DCFA879493A3316E3A17F2'
}
foreach ($name in $expected.Keys) {
    if ((Get-FileHash -LiteralPath "$destination\Runtime\$name").Hash -ne $expected[$name]) {
        throw "The live $name changed since inspection. Reconcile before copying."
    }
}
if (!(Test-Path -LiteralPath 'D:\Hack the Hill\Deliverables\TherapyBackups\BeforeWeatherCycle-20260926.zip')) { throw 'Backup is missing.' }
foreach ($relative in @(
    'Weather\Shaders\QuietSky.shader',
    'Weather\Shaders\SoftCloud.shader',
    'Runtime\WellnessSkyCycle.cs',
    'Runtime\WellnessRain.cs',
    'Runtime\WellnessVoiceChat.cs',
    'Editor\TherapyWeatherSetup.cs',
    'Documentation\WeatherCycle.md'
)) {
    $target = Join-Path $destination $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $staging $relative) -Destination $target -Force
}
# Marker goes last; the scene is updated by the editor, never replaced from disk.
Copy-Item -LiteralPath "$staging\WeatherCycleRequest.txt" -Destination "$destination\WeatherCycleRequest.txt"
Write-Output 'Weather scripts and shaders copied; one-use installer ready. No Unity launch, Play mode, microphone, render or bake was requested.'
