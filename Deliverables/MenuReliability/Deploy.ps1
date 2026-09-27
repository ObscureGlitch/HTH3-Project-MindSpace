$ErrorActionPreference='Stop'
$fixStage=$PSScriptRoot
$fixLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$fixNames=@('WellnessMainMenu.cs','WellnessMainMenuFlow.cs','WellnessVoiceOnboarding.cs','WellnessOnboardingTheme.cs','WellnessVoiceChat.cs','WellnessQuietGlassTheme.cs')
$fixMemory=Get-CimInstance Win32_OperatingSystem
if($fixMemory.FreeVirtualMemory -lt 1572864 -or $fixMemory.FreePhysicalMemory -lt 1048576){throw 'Need 1.5 GB committed and 1 GB physical headroom for import. Save and close Unity if necessary.'}
function Assert-OriginalFiles {
 foreach($name in $fixNames){
  if((Get-FileHash -LiteralPath "$fixLive\Runtime\$name").Hash -ne (Get-FileHash -LiteralPath "$fixStage\Before\$name").Hash){throw "Live file changed since inspection: $name. Reconcile before copying."}
 }
}
Assert-OriginalFiles
if(Test-Path -LiteralPath "$fixLive\Documentation\MenuReliability.md"){throw 'Deployment documentation already exists; inspect before overwriting.'}
$fixChecks=@(& "$fixStage\Check.ps1")
$fixChecks | Write-Output
& 'C:\Users\obscu\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' "$fixStage\CheckTypography.mjs"
if($LASTEXITCODE -ne 0){throw 'Text-fit check failed.'}
Assert-OriginalFiles
$fixBackup=Join-Path 'D:\Unity\HTH3 Project\TherapyBackups\MenuReliability' (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $fixBackup -Force | Out-Null
foreach($name in $fixNames){Copy-Item -LiteralPath "$fixLive\Runtime\$name", "$fixLive\Runtime\$name.meta" -Destination $fixBackup}
$fixChecks | Out-File -LiteralPath (Join-Path $fixBackup 'Checks.txt') -Encoding utf8
foreach($name in $fixNames){
 Copy-Item -LiteralPath "$fixStage\Runtime\$name" -Destination "$fixLive\Runtime\$name"
 if((Get-FileHash -LiteralPath "$fixStage\Runtime\$name").Hash -ne (Get-FileHash -LiteralPath "$fixLive\Runtime\$name").Hash){throw "Copy hash mismatch: $name"}
}
Copy-Item -LiteralPath "$fixStage\MenuReliability.md" -Destination "$fixLive\Documentation\MenuReliability.md"
"PASS: six checked menu files copied. Backup: $fixBackup"
'No scene, project-wide play settings, shader, provider, camera gate, model or consent choices changed. Refresh Unity before the repeat-Play visual check.'
