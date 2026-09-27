$ErrorActionPreference = 'Stop'
$bunnyStage = 'D:\Hack the Hill\Deliverables\TherapyGame'
$bunnyLive = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$bunnyScene = Join-Path $bunnyLive 'Scenes\TherapyRoom.unity'
$bunnyRequest = Join-Path $bunnyLive 'BunnyUpgradeRequest.txt'

if (!(Test-Path -LiteralPath $bunnyScene)) { throw 'The live TherapyRoom scene was not found.' }
if (!(Test-Path -LiteralPath (Join-Path $bunnyLive 'Exterior\LivingGarden\SquirrelUpgradeCheck.txt'))) { throw 'Install and verify the animated squirrel garden first.' }
if (Test-Path -LiteralPath $bunnyRequest) { throw 'A bunny upgrade request already exists; inspect its status before repeating.' }

$bunnyBackup = 'D:\Hack the Hill\Deliverables\TherapyBackups\BeforeAnimatedBunnies-20260926.zip'
if (!(Test-Path -LiteralPath $bunnyBackup)) {
    Compress-Archive -LiteralPath $bunnyScene -DestinationPath $bunnyBackup -CompressionLevel Fastest
}

foreach ($relative in @(
    'Runtime\WellnessGardenLife.cs',
    'Editor\TherapyBunnyUpgrade.cs',
    'Exterior\LivingGarden\Source\GardenLife.json',
    'Exterior\LivingGarden\Source\Bunny.json',
    'Exterior\LivingGarden\Source\BunnyConversionCheck.json',
    'Exterior\LivingGarden\Source\tiny-bunny-animated.glb',
    'Documentation\LivingGarden.md'
)) {
    $source = Join-Path $bunnyStage $relative
    $destination = Join-Path $bunnyLive $relative
    if (!(Test-Path -LiteralPath $source)) { throw "Missing staged bunny file: $relative" }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

# Written last so Unity cannot run the one-shot upgrade before all source data lands.
Copy-Item -LiteralPath (Join-Path $bunnyStage 'BunnyUpgradeRequest.txt') -Destination $bunnyRequest
Write-Output 'Animated bunny model, clips and one-shot scene upgrade staged. Unity will preserve the current open scene in a second backup before editing it.'
