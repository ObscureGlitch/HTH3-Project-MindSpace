$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'TherapyGame'
$before=Join-Path $PSScriptRoot 'PresageStabilityPatch/Before'
$target='D:/Unity/HTH3 Project/Assets/TherapyGame'
$bridge='D:/Unity/HTH3 Project/Assets/StreamingAssets/TherapyGame/PresageBridge'
$files=@(
 @{Source="$source/Runtime/PresageBiometricProvider.cs";Before="$before/Runtime/PresageBiometricProvider.cs";Target="$target/Runtime/PresageBiometricProvider.cs"},
 @{Source="$source/Integrations/PresageBridge/bridge.mjs";Before="$before/Bridge/bridge.mjs";Target="$bridge/bridge.mjs"},
 @{Source="$source/Integrations/PresageBridge/frames.mjs";Before="$before/Bridge/frames.mjs";Target="$bridge/frames.mjs"},
 @{Source="$source/Documentation/PresageBiometricCoaching.md";Before="$before/PresageBiometricCoaching.md";Target="$target/Documentation/PresageBiometricCoaching.md"}
)
foreach($file in $files){
 if((Get-FileHash -LiteralPath $file.Target).Hash -ne (Get-FileHash -LiteralPath $file.Before).Hash){throw "Concurrent changes need merging: $($file.Target)"}
 if(!(Test-Path -LiteralPath $file.Source)){throw "Missing source: $($file.Source)"}
}
$backup=Join-Path $PSScriptRoot ('TherapyBackups/BeforePresageStability-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
Compress-Archive -LiteralPath @($files|ForEach-Object {$_.Target}) -DestinationPath $backup
foreach($file in $files){
 Copy-Item -LiteralPath $file.Source -Destination $file.Target -Force
 if((Get-FileHash -LiteralPath $file.Target).Hash -ne (Get-FileHash -LiteralPath $file.Source).Hash){throw "Verification failed: $($file.Target)"}
}
"Installed and hash-verified bounded Presage frame recovery. Backup: $backup"
