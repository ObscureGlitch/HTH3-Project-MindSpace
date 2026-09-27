$ErrorActionPreference='Stop'
$varietyStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$varietyLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$varietyExpected=@{
 'Runtime\WellnessNightSkyEvents.cs'='623B7A522F66097999EEB7D3A6CA4370D2DF72C4934BAD0FAFB45B095C585F65'
 'Runtime\WellnessSkyCycle.cs'='8D46074ACF17ED4ADF8CA9AC05E7CF5BBB1B99AB43B1FD72C304C121A9D16940'
 'Weather\Shaders\WellnessNightSky.hlsl'='A81916F7E1D5227A776BA89764E6CFFD754DF8C855A3C1979ADFFD651F21D79F'
 'Weather\Shaders\WellnessSkySampling.hlsl'='899E3634601B37F4A1E76032E10A1339957A0999DED8EA0414E1633909A6394C'
 'Weather\Shaders\NaturalSky.shader'='BD2A914F78619B0B03D3B5A00C8999DA379CBFE2BB28AFD51DC7D353D08404C7'
 'Exterior\Shaders\QuietPond.shader'='767F8297E72A7C5F6AE298144AA708015B9A3BF23211A9C4BC7E42BFB926E525'
 'Editor\NightSkyEventChecks.cs'='AE324C5EFE65A4E17A128D406ECC45E2034D10BBFE9FF6B433D86BEACF8BB14C'
}
$varietyNew=@('Weather\Shaders\WellnessMoon.hlsl','Editor\TherapyNightSkyVarietyUpgrade.cs','Documentation\NightSkyVariety.md','NightSkyVarietyRequest.txt')
$varietyMemory=Get-CimInstance Win32_OperatingSystem
if($varietyMemory.FreeVirtualMemory -lt 2097152 -or $varietyMemory.FreePhysicalMemory -lt 1572864){throw 'Need at least 2 GB available committed memory and 1.5 GB physical before this shader import.'}
foreach($relative in $varietyExpected.Keys){
 if((Get-FileHash -LiteralPath "$varietyLive\$relative").Hash -ne $varietyExpected[$relative]){throw "Live file changed; reconcile first: $relative"}
}
foreach($relative in $varietyNew){if(Test-Path -LiteralPath "$varietyLive\$relative"){throw "Unexpected existing live file: $relative"}}
$varietyBackup='D:\Unity\HTH3 Project\TherapyBackups\NightSkyVariety\'+(Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $varietyBackup -Force | Out-Null
Copy-Item -LiteralPath "$varietyLive\Scenes\TherapyRoom.unity" -Destination "$varietyBackup\TherapyRoom-on-disk.unity"
foreach($relative in $varietyExpected.Keys){
 $target=Join-Path $varietyBackup $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
 Copy-Item -LiteralPath "$varietyLive\$relative" -Destination $target
}
$varietyFiles=@('Weather\Shaders\WellnessMoon.hlsl')+@($varietyExpected.Keys)+@('Editor\TherapyNightSkyVarietyUpgrade.cs','Documentation\NightSkyVariety.md','NightSkyVarietyRequest.txt')
foreach($relative in $varietyFiles){
 $target=Join-Path $varietyLive $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
 Copy-Item -LiteralPath "$varietyStaged\$relative" -Destination $target
 if((Get-FileHash -LiteralPath "$varietyStaged\$relative").Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw "Copy verification failed: $relative"}
 Write-Output "Verified scoped copy: $relative"
}
Write-Output "Backup: $varietyBackup"
Write-Output 'Ready for Assets > Refresh with Play stopped. Verification is guarded and does not render, bake or enter Play.'
