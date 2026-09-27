$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'TherapyGame'
$before=Join-Path $PSScriptRoot 'PresageColourPatch/Before'
$target='D:\Unity\HTH3 Project\Assets\TherapyGame'
$bridge='D:\Unity\HTH3 Project\Assets\StreamingAssets\TherapyGame\PresageBridge'
$entries=@()
foreach($name in @('PresageBiometricProvider.cs','WellnessVoiceChat.cs','PresageColourCamera.cs','PresageColourPixels.cs')){
  $entries+=@{From=(Join-Path $source "Runtime/$name");To=(Join-Path $target "Runtime/$name");Before=(Join-Path $before "Runtime/$name")}
}
foreach($name in @('bridge.mjs','frames.mjs','README.md')){
  $entries+=@{From=(Join-Path $source "Integrations/PresageBridge/$name");To=(Join-Path $bridge $name);Before=(Join-Path $before "Bridge/$name")}
}
$entries+=@{From=(Join-Path $source 'Documentation/PresageBiometricCoaching.md');To=(Join-Path $target 'Documentation/PresageBiometricCoaching.md');Before=(Join-Path $before 'PresageBiometricCoaching.md')}
foreach($entry in $entries){
  if(!(Test-Path -LiteralPath $entry.From)){throw "Missing source: $($entry.From)"}
  if(Test-Path -LiteralPath $entry.Before){
    if((Get-FileHash -LiteralPath $entry.To).Hash -ne (Get-FileHash -LiteralPath $entry.Before).Hash){throw "Concurrent changes need merging: $($entry.To)"}
  }elseif(Test-Path -LiteralPath $entry.To){throw "New destination already exists: $($entry.To)"}
}
$backup=Join-Path $PSScriptRoot ('TherapyBackups/BeforePresageColour-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
Compress-Archive -LiteralPath @($entries | ForEach-Object {$_.To} | Where-Object {Test-Path -LiteralPath $_}) -DestinationPath $backup
foreach($entry in $entries){
  Copy-Item -LiteralPath $entry.From -Destination $entry.To -Force
  if((Get-FileHash -LiteralPath $entry.From).Hash -ne (Get-FileHash -LiteralPath $entry.To).Hash){throw "Verification failed: $($entry.To)"}
}
"Installed and hash-verified $($entries.Count) files. Backup: $backup"
