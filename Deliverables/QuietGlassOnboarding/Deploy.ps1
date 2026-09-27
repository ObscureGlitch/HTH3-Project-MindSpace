$ErrorActionPreference='Stop'
$uiLive='D:\Unity\HTH3 Project\Assets\TherapyGame'
$uiStage=$PSScriptRoot
$uiExpected='FB50281068A2358F6FF6B2CD48D830A48D237F40C4FD512B9086431AB50D490D'
$uiMemory=Get-CimInstance Win32_OperatingSystem
if($uiMemory.FreeVirtualMemory -lt 1572864 -or $uiMemory.FreePhysicalMemory -lt 1048576){throw 'Need 1.5 GB committed and 1 GB physical headroom. Save and close Unity if necessary.'}
if((Get-FileHash -LiteralPath "$uiLive\Runtime\WellnessVoiceChat.cs").Hash -ne $uiExpected){throw 'Live voice script changed. Reconcile with the latest code before deployment.'}
$uiNewFiles=@('WellnessConsentChoices.cs','WellnessOnboardingTheme.cs','WellnessVoiceOnboarding.cs')
foreach($name in $uiNewFiles){if(Test-Path -LiteralPath "$uiLive\Runtime\$name"){throw "Already deployed or file conflict: $name"}}
if(Test-Path -LiteralPath "$uiLive\Documentation\QuietGlassOnboarding.md"){throw 'Onboarding documentation already exists; inspect before overwriting.'}
$uiChecks=@(& "$uiStage\Check.ps1")
$uiChecks | Write-Output
& 'C:\Users\obscu\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' "$uiStage\CheckTypography.mjs"
if($LASTEXITCODE -ne 0){throw 'Typography check failed.'}
if((Get-FileHash -LiteralPath "$uiLive\Runtime\WellnessVoiceChat.cs").Hash -ne $uiExpected){throw 'Live script changed during checks; nothing copied.'}
$uiBackup=Join-Path 'D:\Unity\HTH3 Project\TherapyBackups\QuietGlassOnboarding' (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $uiBackup -Force | Out-Null
Copy-Item -LiteralPath "$uiLive\Runtime\WellnessVoiceChat.cs", "$uiLive\Runtime\WellnessVoiceChat.cs.meta" -Destination $uiBackup
$uiChecks | Out-File -LiteralPath (Join-Path $uiBackup 'Checks.txt') -Encoding utf8
foreach($name in ($uiNewFiles+@('WellnessVoiceChat.cs'))){
 Copy-Item -LiteralPath "$uiStage\Runtime\$name" -Destination "$uiLive\Runtime\$name"
 if((Get-FileHash -LiteralPath "$uiStage\Runtime\$name").Hash -ne (Get-FileHash -LiteralPath "$uiLive\Runtime\$name").Hash){throw "Copy hash mismatch: $name"}
}
Copy-Item -LiteralPath "$uiStage\QuietGlassOnboarding.md" -Destination "$uiLive\Documentation\QuietGlassOnboarding.md"
"PASS: four checked runtime files and documentation copied. Backup: $uiBackup"
'No scene, shader, material, provider, camera gate, main menu or pause menu files replaced. Unity import and visual Play check remain pending.'
