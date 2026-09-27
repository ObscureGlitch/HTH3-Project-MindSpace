$ErrorActionPreference = 'Stop'
$sourceRoot = 'D:\Hack the Hill'
$stageRoot = 'D:\Hack the Hill\Deliverables\TherapyGame'
$copies = @(
    @('Assets\_Project\Art\Models\Wellness', 'Models'),
    @('Assets\_Project\Art\Materials\Wellness', 'Materials'),
    @('Assets\_Project\Art\Textures\Wellness', 'Textures'),
    @('Assets\_Project\Prefabs\Environment\Wellness', 'Prefabs'),
    @('Assets\_Project\Config\Wellness', 'Settings'),
    @('Assets\_Project\Resources', 'Resources')
)
foreach ($pair in $copies) {
    $targetPath = Join-Path $stageRoot $pair[1]
    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $sourceRoot $pair[0]) | Copy-Item -Destination $targetPath -Recurse -Force
}
New-Item -ItemType Directory -Path (Join-Path $stageRoot 'Scenes') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Assets\_Project\Scenes') | Where-Object { $_.Name -like 'TherapyRoom*' } | Copy-Item -Destination (Join-Path $stageRoot 'Scenes') -Recurse -Force
$runtimeFiles = @(
    'Scripts\Player\WellnessExplorer.cs',
    'Scripts\Input\WellnessRoomInput.cs',
    'Scripts\Interaction\WellnessInteraction.cs',
    'Scripts\Environment\WellnessBreathingOrb.cs',
    'Scripts\Environment\WellnessScenePipeline.cs',
    'Scripts\Audio\BackgroundMusicPlayer.cs',
    'Scripts\Audio\OutdoorNatureAmbience.cs'
)
foreach ($relative in $runtimeFiles) {
    $path = Join-Path $sourceRoot ('Assets\_Project\' + $relative)
    Copy-Item -LiteralPath $path -Destination (Join-Path $stageRoot 'Runtime') -Force
    Copy-Item -LiteralPath ($path + '.meta') -Destination (Join-Path $stageRoot 'Runtime') -Force
}
New-Item -ItemType Directory -Path (Join-Path $stageRoot 'Documentation') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Documentation\Wellness') | Where-Object { $_.Extension -in @('.png', '.md', '.txt') -and $_.Name -ne 'PlayCheckFailed.txt' } | Copy-Item -Destination (Join-Path $stageRoot 'Documentation') -Force
Get-ChildItem -Recurse -File -LiteralPath $stageRoot | Measure-Object -Property Length -Sum
