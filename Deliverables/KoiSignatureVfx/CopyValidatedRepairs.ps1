$ErrorActionPreference='Stop'
$fxStage='D:\Hack the Hill\Deliverables\KoiSignatureVfx'
$fxAssets='D:\Unity\HTH3 Project\Assets\TherapyGame'
if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'Save and close TherapyRoom before copying.'}
$fxCopied=Get-Content -LiteralPath "$fxStage\Checks\CopiedPayload.json" -Raw|ConvertFrom-Json
foreach($entry in $fxCopied.Payload){if((Get-FileHash -LiteralPath (Join-Path $fxAssets $entry.File)).Hash -ne $entry.SHA256){throw "Installed file changed: $($entry.File)"}}
foreach($file in Get-ChildItem -LiteralPath "$fxStage\Payload" -Recurse -File){$relative=$file.FullName.Substring(("$fxStage\Payload\").Length);Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $fxAssets $relative)}
$fxCopied.Payload=@(Get-ChildItem -LiteralPath "$fxStage\Payload" -Recurse -File|ForEach-Object{@{File=$_.FullName.Substring(("$fxStage\Payload\").Length);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$fxCopied|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$fxStage\Checks\CopiedPayload.json"
Write-Output 'Copied final visual refinements; original backup retained.'
