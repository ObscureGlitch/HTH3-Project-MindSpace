$ErrorActionPreference='Stop'
$therapistStage='D:\Hack the Hill\Deliverables\TherapyGame'
$therapistLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$therapistMemory=Get-CimInstance Win32_OperatingSystem
if($therapistMemory.FreeVirtualMemory -lt 1572864){throw 'Import deferred: available committed memory below 1.5 GB.'}
if(Test-Path -LiteralPath (Join-Path $therapistLive 'TherapistRequest.txt')){throw 'Therapist installation already staged; inspect before repeating.'}
$therapistExpected=@{
    'Runtime\WellnessVoiceChat.cs'='D5E831624D364D1C1EF1720410BBD0F2749DF22506B9DEFB095709804C79899C'
    'Runtime\WellnessSeat.cs'='AEF508F1D0A862083B400BBD3092B84EC3824F51E194359F27A1FBD7A8167C82'
    'Runtime\WellnessInteraction.cs'='AD35DA7E9C760D668134E8BA9C954720B99C95FE3D02290EF5C934F58EF3952A'
}
foreach($relative in $therapistExpected.Keys){
    if((Get-FileHash -LiteralPath (Join-Path $therapistLive $relative)).Hash -ne $therapistExpected[$relative]){throw "Live source changed; reconcile before replacing: $relative"}
}
$therapistNew=@('Runtime\WellnessTherapist.cs','Editor\TherapyTherapistSetup.cs','Documentation\SeatedCompanions.md')
foreach($relative in $therapistNew){if(Test-Path -LiteralPath (Join-Path $therapistLive $relative)){throw "New file already exists: $relative"}}
if(Test-Path -LiteralPath (Join-Path $therapistLive 'Characters')){throw 'Characters folder already exists; inspect it before importing.'}
$therapistBackup='D:\Hack the Hill\Deliverables\TherapyBackups\BeforeTherapists-20260926.zip'
if(Test-Path -LiteralPath $therapistBackup){throw 'Backup already exists; inspect prior attempt.'}
$therapistBackupPaths=@((Join-Path $therapistLive 'Scenes\TherapyRoom.unity'),(Join-Path $therapistLive 'Integrations\Settings\VoiceCompanions.asset'))
$therapistBackupPaths+=@($therapistExpected.Keys|ForEach-Object{Join-Path $therapistLive $_})
Compress-Archive -LiteralPath $therapistBackupPaths -DestinationPath $therapistBackup -CompressionLevel Fastest
foreach($relative in @($therapistExpected.Keys)+$therapistNew){
    $destination=Join-Path $therapistLive $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $therapistStage $relative) -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $therapistStage 'Characters') -Destination (Join-Path $therapistLive 'Characters') -Recurse
# Marker last: import cannot run until all source files are present and compiled.
Copy-Item -LiteralPath (Join-Path $therapistStage 'TherapistRequest.txt') -Destination (Join-Path $therapistLive 'TherapistRequest.txt')
Write-Output 'Seated companions staged. Scene and existing agent IDs not overwritten. One-use importer awaits Unity refresh; no Play, microphone, session, or bake requested.'
