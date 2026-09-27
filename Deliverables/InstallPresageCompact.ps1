$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'TherapyGame'
$patch=Join-Path $PSScriptRoot 'PresageCompactPatch'
$target='D:\Unity\HTH3 Project\Assets\TherapyGame'
$bridge='D:\Unity\HTH3 Project\Assets\StreamingAssets\TherapyGame\PresageBridge'
$entries=@()
foreach($name in @('WellnessHud.cs','WellnessVoiceChat.cs','PresageBiometricProvider.cs','PresageCameraCheck.cs','PresageUiRaster.cs')){
    $from=Join-Path $source "Runtime\$name"
    if($name -eq 'WellnessHud.cs'){$from=Join-Path $patch $name}
    $entries+=@{From=$from;To=(Join-Path $target "Runtime\$name");Before=(Join-Path $patch "Before\Runtime\$name")}
}
foreach($name in @('bridge.mjs','signals.mjs')){
    $entries+=@{From=(Join-Path $source "Integrations\PresageBridge\$name");To=(Join-Path $bridge $name);Before=(Join-Path $patch "Before\Bridge\$name")}
}
$doc='Documentation\PresageBiometricCoaching.md'
$entries+=@{From=(Join-Path $source $doc);To=(Join-Path $target $doc);Before=(Join-Path $patch "Before\$doc")}
foreach($entry in $entries){
    if(!(Test-Path -LiteralPath $entry.From)){throw "Missing source: $($entry.From)"}
    if((Get-FileHash -LiteralPath $entry.To).Hash -ne (Get-FileHash -LiteralPath $entry.Before).Hash){
        throw "Live file changed while preparing this patch; merge before installing: $($entry.To)"
    }
}
$backup=Join-Path $PSScriptRoot ('TherapyBackups\BeforePresageCompact-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
Compress-Archive -LiteralPath @($entries | ForEach-Object {$_.To}) -DestinationPath $backup
foreach($entry in $entries){
    Copy-Item -LiteralPath $entry.From -Destination $entry.To -Force
    if((Get-FileHash -LiteralPath $entry.From).Hash -ne (Get-FileHash -LiteralPath $entry.To).Hash){throw "Verification failed: $($entry.To)"}
}
"Installed and hash-verified $($entries.Count) files."
"Backup: $backup"
'No camera, microphone or Play mode was started.'
