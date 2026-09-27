$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'TherapyGame'
$before=Join-Path $PSScriptRoot 'PresagePipeFix/Before'
$target='D:\Unity\HTH3 Project\Assets\TherapyGame'
$files=@('Runtime/PresageColourCamera.cs','Runtime/PresagePrivatePipe.cs','Documentation/PresageBiometricCoaching.md')
foreach($file in $files){
  $prior=Join-Path $before (Split-Path $file -Leaf)
  $destination=Join-Path $target $file
  if(Test-Path -LiteralPath $prior){
    if((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $prior).Hash){throw "Concurrent changes need merging: $file"}
  }elseif(Test-Path -LiteralPath $destination){throw "New destination already exists: $file"}
  if(!(Test-Path -LiteralPath (Join-Path $source $file))){throw "Missing source: $file"}
}
$backup=Join-Path $PSScriptRoot ('TherapyBackups/BeforePresagePipeFix-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
Compress-Archive -LiteralPath @($files|ForEach-Object {Join-Path $target $_}|Where-Object {Test-Path -LiteralPath $_}) -DestinationPath $backup
foreach($file in $files){
  Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $target $file) -Force
  if((Get-FileHash -LiteralPath (Join-Path $source $file)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $file)).Hash){throw "Verification failed: $file"}
}
"Installed and hash-verified Unity-compatible private camera connection. Backup: $backup"
