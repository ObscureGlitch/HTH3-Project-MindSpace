$ErrorActionPreference = 'Stop'
$squirrelStage = 'D:\Hack the Hill\Deliverables\TherapyGame'
$squirrelLive = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$squirrelScene = Join-Path $squirrelLive 'Scenes\TherapyRoom.unity'
$squirrelRequest = Join-Path $squirrelLive 'SquirrelUpgradeRequest.txt'

if (!(Test-Path -LiteralPath $squirrelScene)) { throw 'The live TherapyRoom scene was not found.' }
if (!(Test-Path -LiteralPath (Join-Path $squirrelLive 'Exterior\LivingGarden\GardenLifeCheck.txt'))) { throw 'Install and verify the living garden first.' }
if (Test-Path -LiteralPath $squirrelRequest) { throw 'A squirrel upgrade request already exists; inspect its status before repeating.' }

$squirrelBackup = 'D:\Hack the Hill\Deliverables\TherapyBackups\BeforeAnimatedSquirrels-20260926.zip'
if (!(Test-Path -LiteralPath $squirrelBackup)) {
    Compress-Archive -LiteralPath $squirrelScene -DestinationPath $squirrelBackup -CompressionLevel Fastest
}

foreach ($relative in @(
    'Runtime\WellnessGardenLife.cs',
    'Editor\TherapySquirrelUpgrade.cs',
    'Exterior\LivingGarden\Source\GardenLife.json',
    'Exterior\LivingGarden\Source\Squirrel.json',
    'Exterior\LivingGarden\Source\SquirrelConversionCheck.json',
    'Exterior\LivingGarden\Source\tiny-squirrel-animated.glb',
    'Documentation\LivingGarden.md'
)) {
    $source = Join-Path $squirrelStage $relative
    $destination = Join-Path $squirrelLive $relative
    if (!(Test-Path -LiteralPath $source)) { throw "Missing staged squirrel file: $relative" }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

# Written last so Unity cannot run the one-shot upgrade before all source data lands.
Copy-Item -LiteralPath (Join-Path $squirrelStage 'SquirrelUpgradeRequest.txt') -Destination $squirrelRequest
Write-Output 'Animated squirrel model, clips and one-shot scene upgrade staged. Unity will preserve the current open scene in a second backup before editing it.'
