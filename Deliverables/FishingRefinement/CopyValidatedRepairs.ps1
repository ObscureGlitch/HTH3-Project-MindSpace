$ErrorActionPreference='Stop'
$refStage='D:\Hack the Hill\Deliverables\FishingRefinement'
$refAssets='D:\Unity\HTH3 Project\Assets\TherapyGame'
if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'Save and close TherapyRoom before copying.'}
$refCopied=Get-Content -LiteralPath "$refStage\Checks\CopiedPayload.json" -Raw|ConvertFrom-Json
foreach($entry in $refCopied.Payload){if((Get-FileHash -LiteralPath (Join-Path $refAssets $entry.File)).Hash -ne $entry.SHA256){throw "Installed file changed: $($entry.File)"}}
foreach($file in Get-ChildItem -LiteralPath "$refStage\Payload" -Recurse -File){$relative=$file.FullName.Substring(("$refStage\Payload\").Length);Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $refAssets $relative)}
$refCopied.Payload=@(Get-ChildItem -LiteralPath "$refStage\Payload" -Recurse -File|ForEach-Object{@{File=$_.FullName.Substring(("$refStage\Payload\").Length);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$refCopied|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$refStage\Checks\CopiedPayload.json"
Write-Output 'Copied validated refinement repairs; original backup retained.'
