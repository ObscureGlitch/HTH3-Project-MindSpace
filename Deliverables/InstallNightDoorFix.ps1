$ErrorActionPreference = 'Stop'
$sourceTherapy = 'D:\Hack the Hill\Deliverables\TherapyGame'
$targetTherapy = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$expectedNightFiles = @{
    'Runtime\WellnessAtmosphere.cs' = '914B8741FB692BE452F58C927E80EC2DE7BCDA6A34E6086F617DA284A416E134'
    'Runtime\WellnessInteraction.cs' = '50B7044FEE7BBDE244EC56323DF3E8800CEF7FFEBAF1CCA6606E10EDB02B98CF'
    'Runtime\WellnessExplorer.cs' = '433EA9B2A6F4C48A89E0EE2293FE39924B4E28F131CA43745C9E5323FA6F45EE'
    'Exterior\Shaders\QuietPond.shader' = '8AD869C14750B64CFCA4D4C693A5CE54CA19D77127194804465AD0AED2570CA5'
}
foreach ($relative in $expectedNightFiles.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $targetTherapy $relative)).Hash -ne $expectedNightFiles[$relative]) {
        throw "Live file changed; reconcile before import: $relative"
    }
}
$memoryBeforeNightFix = Get-CimInstance Win32_OperatingSystem
if ($memoryBeforeNightFix.FreeVirtualMemory -lt 1048576) { throw 'Import deferred: available committed memory below 1 GB.' }
$backupNightZip = 'D:\Hack the Hill\Deliverables\TherapyBackups\BeforeNightDoorFix-20260926.zip'
if (!(Test-Path -LiteralPath $backupNightZip)) {
    $backupNightFiles = @($expectedNightFiles.Keys | ForEach-Object { Join-Path $targetTherapy $_ })
    $backupNightFiles += Join-Path $targetTherapy 'Scenes\TherapyRoom.unity'
    Compress-Archive -LiteralPath $backupNightFiles -DestinationPath $backupNightZip -CompressionLevel Fastest
}
foreach ($relative in @(
    'Runtime\WellnessDoor.cs',
    'Runtime\WellnessAtmosphere.cs',
    'Exterior\Shaders\QuietPond.shader',
    'Runtime\WellnessInteraction.cs',
    'Runtime\WellnessExplorer.cs',
    'Editor\TherapyNightDoorFix.cs',
    'Documentation\NightAndDoorFix.md'
)) {
    Copy-Item -LiteralPath (Join-Path $sourceTherapy $relative) -Destination (Join-Path $targetTherapy $relative) -Force
}
# Gate last; no scene file is overwritten underneath the open Unity editor.
Copy-Item -LiteralPath (Join-Path $sourceTherapy 'NightDoorFixRequest.txt') -Destination (Join-Path $targetTherapy 'NightDoorFixRequest.txt')
Write-Output 'Night fog/pond/door files staged. Backup retained; no scene replacement, Play, microphone or bake requested.'
