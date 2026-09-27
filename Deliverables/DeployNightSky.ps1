$ErrorActionPreference='Stop'
$nightLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$nightStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$nightExpected=@{
 'Runtime\WellnessSkyCycle.cs'='F163B45784066C146C196D621B6FCA2760B46FAB3BC7C7BAFACE1E870D8AE843'
 'Runtime\WellnessVoiceChat.cs'='59516B2970CE42F759553715EA3B1CAF1702CD2AB754AB5404D9EDDED61C709E'
 'Weather\Shaders\WellnessSkySampling.hlsl'='3966D61F78E223D651663A13A576E50625E474A65F5D146F2EBE515323E85DAF'
 'Weather\Shaders\NaturalSky.shader'='B50C6443AD95785F43EA133B5052582CB74D0D2EDAC3E3E36BBE681FA586805C'
 'Exterior\Shaders\QuietPond.shader'='7BA51C6E6CDEE8ED281CCA8169810A552495B1B160863B6BC4836467AAD0F849'
}
foreach($relative in $nightExpected.Keys) {
 if((Get-FileHash -LiteralPath (Join-Path $nightLive $relative)).Hash -ne $nightExpected[$relative]){throw "Live file changed during work; merge before deployment: $relative"}
}
$nightFiles=@(
 'Runtime\WellnessSkyCycle.cs','Runtime\WellnessVoiceChat.cs',
 'Runtime\WellnessNightSkyEvents.cs','Runtime\WellnessNightSkyEvents.cs.meta',
 'Weather\Shaders\WellnessNightSky.hlsl','Weather\Shaders\WellnessNightSky.hlsl.meta',
 'Weather\Shaders\WellnessSkySampling.hlsl','Weather\Shaders\NaturalSky.shader','Exterior\Shaders\QuietPond.shader',
 'Editor\NightSkyEventChecks.cs','Editor\NightSkyEventChecks.cs.meta',
 'Editor\TherapyNightSkyUpgrade.cs','Editor\TherapyNightSkyUpgrade.cs.meta',
 'NightSkyRequest.txt.meta','NightSkyRequest.txt'
)
foreach($relative in $nightFiles) {
 if(!(Test-Path -LiteralPath (Join-Path $nightStaged $relative))){throw "Missing staged file: $relative"}
 if(!$nightExpected.ContainsKey($relative) -and (Test-Path -LiteralPath (Join-Path $nightLive $relative))){throw "New target already exists; inspect before replacing: $relative"}
}
$nightBackup='D:\Hack the Hill\Deliverables\TherapyBackups\NightSky-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-files'
New-Item -ItemType Directory -Path $nightBackup | Out-Null
foreach($relative in @($nightExpected.Keys)+@('Scenes\TherapyRoom.unity')) {
 $destination=Join-Path $nightBackup $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $nightLive $relative) -Destination $destination
 if((Get-FileHash -LiteralPath (Join-Path $nightLive $relative)).Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw "Backup verification failed: $relative"}
}
foreach($relative in $nightFiles) {
 Copy-Item -LiteralPath (Join-Path $nightStaged $relative) -Destination (Join-Path $nightLive $relative)
 if((Get-FileHash -LiteralPath (Join-Path $nightStaged $relative)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $nightLive $relative)).Hash){throw "Deployment verification failed: $relative"}
}
Write-Output "PASS: copied and hash-verified $($nightFiles.Count) scoped files; unchanged originals backed up at $nightBackup"
