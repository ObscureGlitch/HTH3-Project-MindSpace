$ErrorActionPreference='Stop'
$wallArtStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$wallArtLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$wallArtScene='Scenes\TherapyRoom.unity'
$wallArtExpectedScene='7634522BD0B06CB2E3CAFD3DB55A92450E356BB956126C17D2C56C8C425CCB67'
$wallArtFiles=@(
 'WallArt.meta',
 'WallArt\Textures.meta',
 'WallArt\Textures\QuietOrbit.png','WallArt\Textures\QuietOrbit.png.meta',
 'WallArt\Textures\StillWater.png','WallArt\Textures\StillWater.png.meta',
 'WallArt\Textures\BalancedStones.png','WallArt\Textures\BalancedStones.png.meta',
 'WallArtVariationRequest.txt','WallArtVariationRequest.txt.meta',
 'Editor\TherapyWallArtVariationUpgrade.cs','Editor\TherapyWallArtVariationUpgrade.cs.meta',
 'Documentation\WallArtVariation.md','Documentation\WallArtVariation.md.meta'
)
if((Get-FileHash -LiteralPath (Join-Path $wallArtLive $wallArtScene)).Hash -ne $wallArtExpectedScene){throw 'Live TherapyRoom changed; reconcile it before wall-art deployment.'}
foreach($relative in $wallArtFiles){
 if(!(Test-Path -LiteralPath (Join-Path $wallArtStaged $relative))){throw "Missing staged wall-art file: $relative"}
 if(Test-Path -LiteralPath (Join-Path $wallArtLive $relative)){throw "Unexpected existing live wall-art file: $relative"}
}
$wallArtBackup='D:\Hack the Hill\Deliverables\TherapyBackups\WallArtVariation-'+(Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $wallArtBackup | Out-Null
Copy-Item -LiteralPath (Join-Path $wallArtLive $wallArtScene) -Destination (Join-Path $wallArtBackup 'TherapyRoom.unity')
foreach($relative in $wallArtFiles){
 $target=Join-Path $wallArtLive $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $wallArtStaged $relative) -Destination $target
 if((Get-FileHash -LiteralPath (Join-Path $wallArtStaged $relative)).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw "Copy verification failed: $relative"}
 Write-Output "Verified scoped copy: $relative"
}
Write-Output "Backup: $wallArtBackup"
Write-Output 'Ready for Unity asset refresh; the guarded installer keeps one original and replaces three framed motifs.'
