$ErrorActionPreference='Stop'
$refineStage='D:\Hack the Hill\Deliverables\TherapyGame'
$refineLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 1572864){throw 'Available memory below 1.5 GB; defer import.'}
if(Test-Path -LiteralPath (Join-Path $refineLive 'TherapistRefinementRequest.txt')){throw 'Refinement is already staged; inspect before retrying.'}
$refineExpected=@{
 'Runtime\WellnessTherapist.cs'='473B31386820FEF533AA176D5E744804B66F1F36C42259D5C64DB25E15AF7431'
 'Runtime\TherapyGame.Runtime.asmdef'='B66C70C7E93C3512E8998B77737FA17E2CE4F9ED883EFEDBF1E71676C5FC23F9'
 'Editor\TherapyGame.Editor.asmdef'='CB37FE1370B7AF1F7DE4E7E4C2C95B85A5865DDF7F4E5E4D03828659E86FB236'
}
foreach($p in $refineExpected.Keys){if((Get-FileHash -LiteralPath (Join-Path $refineLive $p)).Hash -ne $refineExpected[$p]){throw "Existing file changed: $p"}}
$refineNew=@('Runtime\WellnessSpeechAnalysis.cs','Editor\TherapyTherapistRefinement.cs','Documentation\CharacterRepair.md')
foreach($p in $refineNew){if(Test-Path -LiteralPath (Join-Path $refineLive $p)){throw "New file already exists: $p"}}
if((Test-Path -LiteralPath (Join-Path $refineLive 'Characters\Refinement')) -or (Test-Path -LiteralPath (Join-Path $refineLive 'Runtime\ThirdParty\uLipSync'))){throw 'New asset folders already exist; inspect before retrying.'}
$refineBackup='D:\Hack the Hill\Deliverables\TherapyBackups\BeforeCharacterRepair-20260926.zip'
if(Test-Path -LiteralPath $refineBackup){throw 'Backup already exists; inspect prior attempt.'}
$refineFiles=@((Join-Path $refineLive 'Scenes\TherapyRoom.unity'),(Join-Path $refineLive 'Characters'))
$refineFiles+=@($refineExpected.Keys|ForEach-Object{Join-Path $refineLive $_})
Compress-Archive -LiteralPath $refineFiles -DestinationPath $refineBackup -CompressionLevel Fastest
foreach($p in @($refineExpected.Keys)+$refineNew){
 $target=Join-Path $refineLive $p
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $refineStage $p) -Destination $target -Force
}
Copy-Item -LiteralPath (Join-Path $refineStage 'Characters\Refinement') -Destination (Join-Path $refineLive 'Characters\Refinement') -Recurse
New-Item -ItemType Directory -Path (Join-Path $refineLive 'Runtime\ThirdParty') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $refineStage 'Runtime\ThirdParty\uLipSync') -Destination (Join-Path $refineLive 'Runtime\ThirdParty\uLipSync') -Recurse
Copy-Item -LiteralPath (Join-Path $refineStage 'TherapistRefinementRequest.txt') -Destination (Join-Path $refineLive 'TherapistRefinementRequest.txt')
Write-Output 'Character repair staged with scene/model/code backup. No microphone, session, Play mode, render, bake, package-manager changes or original model deletion.'
