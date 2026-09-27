$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'TherapyGame'
$before=Join-Path $PSScriptRoot 'PresageMinimalPatch/Before'
$target='D:\Unity\HTH3 Project\Assets\TherapyGame'
$files=@('Runtime/WellnessHud.cs','Runtime/WellnessPulseTrace.cs','Runtime/PresageUiRaster.cs','Documentation/PresageBiometricCoaching.md')
foreach($file in $files){
  $prior=Join-Path $before (Split-Path $file -Leaf)
  if((Get-FileHash -LiteralPath (Join-Path $target $file)).Hash -ne (Get-FileHash -LiteralPath $prior).Hash){throw "Concurrent changes need merging: $file"}
}
$backup=Join-Path $PSScriptRoot ('TherapyBackups/BeforePresageMinimal-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
Compress-Archive -LiteralPath @($files|ForEach-Object {Join-Path $target $_}) -DestinationPath $backup
foreach($file in $files){
  Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $target $file) -Force
  if((Get-FileHash -LiteralPath (Join-Path $source $file)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $file)).Hash){throw "Verification failed: $file"}
}
"Installed transparent heart sensor. Backup: $backup"
