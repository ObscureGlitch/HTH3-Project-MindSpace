param([switch]$InstallDependencies)
$ErrorActionPreference = 'Stop'
$workspace = 'D:\Hack the Hill'
$source = Join-Path $workspace 'Deliverables\TherapyGame'
$project = 'D:\Unity\HTH3 Project'
$target = Join-Path $project 'Assets\TherapyGame'
$streamingTarget = Join-Path $project 'Assets\StreamingAssets\TherapyGame\PresageBridge'
$backup = Join-Path $workspace 'Deliverables\TherapyBackups\BeforePresageCoaching-20260927.zip'

if (!(Test-Path -LiteralPath (Join-Path $target 'Scenes\TherapyRoom.unity'))) { throw 'The live TherapyRoom scene is missing.' }
if (!(Test-Path -LiteralPath (Join-Path $target 'Runtime\WellnessVoiceChat.cs'))) { throw 'Install the ElevenLabs voice integration first.' }
if (Test-Path -LiteralPath $backup) { throw 'The Presage backup already exists; inspect the prior installation before retrying.' }
if (Test-Path -LiteralPath $streamingTarget) { throw "Presage bridge destination already exists; inspect it before replacing: $streamingTarget" }

$packageManager = $null
if ($InstallDependencies) {
    $packageManager = Get-Command pnpm -ErrorAction SilentlyContinue
    if (!$packageManager) { $packageManager = Get-Command npm -ErrorAction SilentlyContinue }
    if (!$packageManager) { throw 'Node package manager not found. Install Node.js 20+ with npm, then retry.' }
}

$files = @(
    'Runtime\PresageBiometricProvider.cs',
    'Runtime\PresageColourCamera.cs',
    'Runtime\PresagePrivatePipe.cs',
    'Runtime\PresageColourPixels.cs',
    'Runtime\PresageCameraCheck.cs',
    'Runtime\PresagePositionOverlay.cs',
    'Runtime\PresageUiRaster.cs',
    'Runtime\WellnessPulseTrace.cs',
    'Runtime\PresageBiometricCoach.cs',
    'Runtime\WellnessVoiceChat.cs',
    'Runtime\WellnessHud.cs',
    'Editor\TherapyPresageSetup.cs',
    'Documentation\PresageBiometricCoaching.md'
)
foreach ($relative in $files) {
    if (!(Test-Path -LiteralPath (Join-Path $source $relative))) { throw "Staged file missing: $relative" }
}

$backupSources = @(
    (Join-Path $target 'Scenes\TherapyRoom.unity'),
    (Join-Path $target 'Runtime\WellnessVoiceChat.cs'),
    (Join-Path $target 'Runtime\WellnessHud.cs')
)
Compress-Archive -LiteralPath $backupSources -DestinationPath $backup -CompressionLevel Fastest

foreach ($relative in $files) {
    $destination = Join-Path $target $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $destination -Force
}

New-Item -ItemType Directory -Path $streamingTarget -Force | Out-Null
foreach ($name in @('bridge.mjs','preview.mjs','signals.mjs','frames.mjs','package.json','pnpm-lock.yaml','pnpm-workspace.yaml','README.md')) {
    Copy-Item -LiteralPath (Join-Path $source "Integrations\PresageBridge\$name") -Destination (Join-Path $streamingTarget $name)
}

if ($InstallDependencies) {
    Push-Location $streamingTarget
    try {
        if ($packageManager.Name -eq 'pnpm.cmd' -or $packageManager.Name -eq 'pnpm') { & $packageManager.Source install --frozen-lockfile --prod }
        else { & $packageManager.Source install --omit=dev }
        if ($LASTEXITCODE -ne 0) { throw 'Presage bridge dependency installation failed.' }
    }
    finally { Pop-Location }
}

Copy-Item -LiteralPath (Join-Path $source 'PresageRequest.txt') -Destination (Join-Path $target 'PresageRequest.txt') -Force
Write-Output 'Presage biometric coaching staged. No camera, microphone, provider session, Play mode, rendering, or bake was started.'
if (!$InstallDependencies) { Write-Output "Run a production dependency install in '$streamingTarget' before Play mode." }
