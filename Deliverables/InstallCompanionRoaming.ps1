$ErrorActionPreference='Stop'
$roamingStage='D:\Hack the Hill\Deliverables\TherapyGame'
$roamingLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$roamingBackup='D:\Hack the Hill\Deliverables\TherapyBackups\BeforeCompanionRoaming-20260927.zip'
$expected=@{
 'Runtime\WellnessTherapist.cs'='D83593F5F0CC102C59C5F91A2A9D37B468317BE76D39A76AB434542645FB025E'
 'Runtime\WellnessVoiceChat.cs'='91D0DCA277DCCAD16919A7498C4ABFF10129A4D26F0CD82719CDF522FAAD6E25'
 'Runtime\WellnessDoor.cs'='5D496CF3ECBA48AD51B1DDF176569343F38FE903D44AEE60B24FF346FED3A0F1'
}
foreach($relative in $expected.Keys){if((Get-FileHash -LiteralPath (Join-Path $roamingLive $relative)).Hash -ne $expected[$relative]){throw "Live source changed since inspection: $relative"}}
if(Test-Path -LiteralPath $roamingBackup){throw 'Backup already exists; inspect installation before retrying.'}
if(Test-Path -LiteralPath "$roamingLive\CompanionRoamingRequest.txt"){throw 'Roaming install gate already exists.'}
$files=@($expected.Keys)+@('Runtime\WellnessCompanionMovement.cs','Runtime\WellnessCompanionNavigation.cs','Runtime\WellnessCompanionRoomPolicy.cs','Editor\TherapyCompanionRoamingUpgrade.cs','Editor\CompanionRoamingValidation.cs','Documentation\RoamingCompanions.md')
foreach($relative in $files){if(!(Test-Path -LiteralPath (Join-Path $roamingStage $relative))){throw "Missing staged source: $relative"}}
$backupSources=@("$roamingLive\Scenes\TherapyRoom.unity")+@($expected.Keys|ForEach-Object{Join-Path $roamingLive $_})
Compress-Archive -LiteralPath $backupSources -DestinationPath $roamingBackup -CompressionLevel Fastest
foreach($relative in $files){
 Copy-Item -LiteralPath (Join-Path $roamingStage $relative) -Destination (Join-Path $roamingLive $relative) -Force
 if((Get-FileHash -LiteralPath (Join-Path $roamingStage $relative)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $roamingLive $relative)).Hash){throw "Copy mismatch: $relative"}
}
Copy-Item -LiteralPath "$roamingStage\CompanionRoamingRequest.txt" -Destination "$roamingLive\CompanionRoamingRequest.txt"
Write-Output "Roaming companions staged; scene and runtime sources backed up to $roamingBackup. Unity will perform a bounded navigation bake and connectivity checks before saving the scene."
