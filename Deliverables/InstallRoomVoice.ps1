$ErrorActionPreference = 'Stop'
$stagedVoice = 'D:\Hack the Hill\Deliverables\TherapyGame'
$liveVoice = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$voiceBackup = 'D:\Hack the Hill\Deliverables\TherapyBackups\BeforeRoomVoice-20260926.zip'
if (!(Test-Path -LiteralPath "$liveVoice\Scenes\TherapyRoom.unity")) { throw 'TherapyRoom scene is missing.' }
if ((Get-Content -LiteralPath "$liveVoice\TherapistRefinementRequest.txt" -Raw).Trim() -ne 'installed-live-check-pending') { throw 'Verify the completed character repair before installing.' }
if (Test-Path -LiteralPath $voiceBackup) { throw 'Backup exists; inspect the prior installation before retrying.' }
$expectedVoiceHashes = @{
    'Runtime\WellnessVoiceChat.cs' = 'F9BC59E59AEF9A18513A8473F8297280B3A751E4B46B92D8D76ADB69B5A2920A'
    'Runtime\WellnessDoor.cs' = '2B47E7F7BBF848D72818F2FBE07DDBD934BF012591DAC9FC3FFC79499B095015'
}
foreach ($relative in $expectedVoiceHashes.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $liveVoice $relative)).Hash -ne $expectedVoiceHashes[$relative]) { throw "Existing source changed; inspect before replacing: $relative" }
}
$voiceFiles = @('Runtime\WellnessVoiceAccess.cs','Runtime\WellnessDoor.cs','Runtime\WellnessVoiceChat.cs','Runtime\WellnessTherapist.cs','Editor\TherapyRoomVoiceSetup.cs','Documentation\RoomVoice.md','Documentation\CharacterRepair.md')
foreach ($relative in $voiceFiles) { if (!(Test-Path -LiteralPath (Join-Path $stagedVoice $relative))) { throw "Staged file missing: $relative" } }
$backupSources = @("$liveVoice\Scenes\TherapyRoom.unity", "$liveVoice\Runtime\WellnessVoiceChat.cs", "$liveVoice\Runtime\WellnessDoor.cs", "$liveVoice\Runtime\WellnessTherapist.cs", "$liveVoice\Documentation\CharacterRepair.md")
Compress-Archive -LiteralPath $backupSources -DestinationPath $voiceBackup -CompressionLevel Fastest
foreach ($relative in $voiceFiles) { Copy-Item -LiteralPath (Join-Path $stagedVoice $relative) -Destination (Join-Path $liveVoice $relative) -Force }
# Last, arm the scoped, one-time editor configuration. It refuses Play mode/baking.
Copy-Item -LiteralPath "$stagedVoice\RoomVoiceRequest.txt" -Destination "$liveVoice\RoomVoiceRequest.txt"
Write-Output 'Room voice files staged in the live Unity project. Backup saved. No microphone, remote session, Play mode, rendering or bake started.'
