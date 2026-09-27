$ErrorActionPreference='Stop'
$summonStage=$PSScriptRoot
$summonLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$summonNames=@('WellnessNightSkyEvents.cs','WellnessSkyCycle.cs','WellnessQuietGlassMenu.cs','WellnessVoiceChat.cs')
$summonMemory=Get-CimInstance Win32_OperatingSystem
if($summonMemory.FreeVirtualMemory -lt 1572864 -or $summonMemory.FreePhysicalMemory -lt 1048576){throw 'Need 1.5 GB committed and 1 GB physical headroom for import. Save and close Unity if necessary.'}
function Assert-OriginalFiles {
 foreach($name in $summonNames){
  if((Get-FileHash -LiteralPath "$summonLive\Runtime\$name").Hash -ne (Get-FileHash -LiteralPath "$summonStage\Before\$name").Hash){throw "Live file changed since staging: $name. Reconcile before copying."}
 }
}
Assert-OriginalFiles
if(Test-Path -LiteralPath "$summonLive\Documentation\AuroraSummon.md"){throw 'Already deployed; inspect before overwriting.'}
$summonChecks=@(& "$summonStage\Check.ps1")
$summonChecks | Write-Output
& 'C:\Users\obscu\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' "$summonStage\CheckTypography.mjs"
if($LASTEXITCODE -ne 0){throw 'Text-fit check failed.'}
Assert-OriginalFiles
$summonBackup=Join-Path 'D:\Unity\HTH3 Project\TherapyBackups\AuroraSummon' (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $summonBackup -Force | Out-Null
foreach($name in $summonNames){Copy-Item -LiteralPath "$summonLive\Runtime\$name", "$summonLive\Runtime\$name.meta" -Destination $summonBackup}
$summonChecks | Out-File -LiteralPath (Join-Path $summonBackup 'Checks.txt') -Encoding utf8
foreach($name in $summonNames){
 Copy-Item -LiteralPath "$summonStage\Runtime\$name" -Destination "$summonLive\Runtime\$name"
 if((Get-FileHash -LiteralPath "$summonStage\Runtime\$name").Hash -ne (Get-FileHash -LiteralPath "$summonLive\Runtime\$name").Hash){throw "Copy hash mismatch: $name"}
}
Copy-Item -LiteralPath "$summonStage\AuroraSummon.md" -Destination "$summonLive\Documentation\AuroraSummon.md"
"PASS: four checked scripts copied. Backup: $summonBackup"
'No scene, material, shader, cloud family, main-menu defaults, provider or consent choices changed by deployment.'
