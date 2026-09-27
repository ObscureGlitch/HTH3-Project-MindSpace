$ErrorActionPreference='Stop'
$gardenLifeStage='D:\Hack the Hill\Deliverables\TherapyGame'
$gardenLifeLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$gardenLifeMemory=Get-CimInstance Win32_OperatingSystem
if($gardenLifeMemory.FreeVirtualMemory -lt 1048576){throw 'Import deferred: available committed memory below 1 GB.'}
if(Test-Path -LiteralPath (Join-Path $gardenLifeLive 'GardenLifeRequest.txt')){throw 'Garden life installation already staged; inspect before repeating.'}
$gardenLifeBackup='D:\Hack the Hill\Deliverables\TherapyBackups\BeforeGardenLife-20260926.zip'
if(!(Test-Path -LiteralPath $gardenLifeBackup)){
    Compress-Archive -LiteralPath (Join-Path $gardenLifeLive 'Scenes\TherapyRoom.unity') -DestinationPath $gardenLifeBackup -CompressionLevel Fastest
}
$lifeSourceDirectory=Join-Path $gardenLifeStage 'Exterior\LivingGarden\Source'
New-Item -ItemType Directory -Path $lifeSourceDirectory -Force | Out-Null
Copy-Item -LiteralPath 'D:\Hack the Hill\Deliverables\OutdoorGarden\generated\GardenLife.json' -Destination (Join-Path $lifeSourceDirectory 'GardenLife.json') -Force
foreach($relative in @('Runtime\WellnessGardenLife.cs','Editor\TherapyGardenLifeSetup.cs','Exterior\LivingGarden\Shaders\GardenLife.shader','Exterior\LivingGarden\Source\GardenLife.json','Documentation\LivingGarden.md')){
    $destination=Join-Path $gardenLifeLive $relative
    if(Test-Path -LiteralPath $destination){throw "New asset already exists; reconcile first: $relative"}
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $gardenLifeStage $relative) -Destination $destination
}
Copy-Item -LiteralPath (Join-Path $gardenLifeStage 'GardenLifeRequest.txt') -Destination (Join-Path $gardenLifeLive 'GardenLifeRequest.txt')
Write-Output 'Garden life staged as new assets only. Existing scene/code/materials/settings not replaced; no Play or bake requested.'
