$ErrorActionPreference='Stop'
$animationWorkspace='D:\Hack the Hill'
$animationSource=Join-Path $animationWorkspace 'Deliverables\TherapyGame'
$animationProject='D:\Unity\HTH3 Project\Assets\TherapyGame'
$animationBackup=Join-Path $animationWorkspace 'Deliverables\TherapyBackups\BeforeAnimatedTherapists-20260927.zip'

if(!(Test-Path -LiteralPath (Join-Path $animationProject 'Scenes\TherapyRoom.unity'))){throw 'The live TherapyRoom scene is missing.'}
if(!(Test-Path -LiteralPath (Join-Path $animationProject 'Characters\Refinement\RefinementCheck.txt'))){throw 'Install the corrected companion models before their animation packs.'}
if(Test-Path -LiteralPath $animationBackup){throw 'The animated-companion backup already exists; inspect the prior installation before retrying.'}
if(Test-Path -LiteralPath (Join-Path $animationProject 'TherapistAnimationRequest.txt')){throw 'A companion-animation request already exists; inspect its status before retrying.'}
if(Test-Path -LiteralPath (Join-Path $animationProject 'Editor\TherapyTherapistAnimationUpgrade.cs')){throw 'The companion-animation importer already exists in the live project.'}

$animationFiles=@(
 'Runtime\WellnessTherapist.cs',
 'Runtime\WellnessVoiceChat.cs',
 'Editor\TherapyTherapistAnimationUpgrade.cs',
 'Documentation\AnimatedTherapists.md',
 'Characters\Animation\Source\AnimationConversionCheck.json',
 'Characters\Animation\Source\julien_animations.json',
 'Characters\Animation\Source\camille_animations.json',
 'Characters\Animation\Source\therapist_julien_animated.glb',
 'Characters\Animation\Source\therapist_camille_animated.glb'
)
foreach($relative in $animationFiles){if(!(Test-Path -LiteralPath (Join-Path $animationSource $relative))){throw "Missing staged file: $relative"}}
if((Get-Content -LiteralPath (Join-Path $animationSource 'TherapistAnimationRequest.txt') -Raw).Trim() -ne 'upgrade-therapist-animations-once'){throw 'Staged request gate is invalid.'}

$animationBackupSources=@(
 (Join-Path $animationProject 'Scenes\TherapyRoom.unity'),
 (Join-Path $animationProject 'Runtime\WellnessTherapist.cs'),
 (Join-Path $animationProject 'Runtime\WellnessVoiceChat.cs')
)
Compress-Archive -LiteralPath $animationBackupSources -DestinationPath $animationBackup -CompressionLevel Fastest

foreach($relative in $animationFiles){
 $destination=Join-Path $animationProject $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $animationSource $relative) -Destination $destination -Force
 if((Get-FileHash -LiteralPath (Join-Path $animationSource $relative)).Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw "Hash mismatch after copy: $relative"}
}

# The request is copied last so Unity never runs against an incomplete source set.
Copy-Item -LiteralPath (Join-Path $animationSource 'TherapistAnimationRequest.txt') -Destination (Join-Path $animationProject 'TherapistAnimationRequest.txt')
Write-Output "Animated companion sources copied and verified; previous scene/runtime files backed up to $animationBackup."
Write-Output 'Waiting for Unity Assets refresh. No Play mode, microphone, remote agent session, bake, or graphics capture was started.'
