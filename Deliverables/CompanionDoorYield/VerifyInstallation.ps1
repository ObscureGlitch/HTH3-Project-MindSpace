$ErrorActionPreference='Stop'
$doorStage='D:\Hack the Hill\Deliverables\CompanionDoorYield'
$doorAssets='D:\Unity\HTH3 Project\Assets\TherapyGame'
if(Get-Process -Name Unity -ErrorAction SilentlyContinue){throw 'Wait until the headless Unity check exits.'}
$doorDeployment=Get-Content -LiteralPath "$doorStage\Deployment.json" -Raw | ConvertFrom-Json
foreach($file in @('Runtime\WellnessDoor.cs','Runtime\WellnessCompanionMovement.cs','Editor\TherapyDoorwayClearanceChecks.cs')){
    if((Get-FileHash -LiteralPath (Join-Path "$doorStage\Payload" $file)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $doorAssets $file)).Hash){throw "Payload mismatch: $file"}
}
if((Get-FileHash -LiteralPath "$doorAssets\Scenes\TherapyRoom.unity").Hash -ne
   (Get-FileHash -LiteralPath "$($doorDeployment.Backup)\TherapyRoom-saved-before-import.unity").Hash){throw 'Saved scene changed during verification.'}
$doorReport=Get-Content -LiteralPath "$doorAssets\Documentation\DoorwayClearanceCheck.txt" -Raw
if(-not $doorReport.StartsWith('PASS:')){throw $doorReport}
if((Get-Content -LiteralPath "$doorAssets\DoorwayClearanceRequest.txt" -Raw).Trim() -ne 'verified'){throw 'Unity verification is not marked complete.'}
foreach($entry in $doorDeployment.Files){$entry.SHA256=(Get-FileHash -LiteralPath (Join-Path $doorAssets $entry.File)).Hash}
$doorDeployment.Verification='PASS: headless Unity Edit-mode controller and navigation regressions; saved scene unchanged. Play-mode movement timing not tested.'
[IO.File]::WriteAllText("$doorStage\Deployment.json",($doorDeployment|ConvertTo-Json -Depth 5))
Copy-Item -LiteralPath "$doorAssets\Documentation\DoorwayClearanceCheck.txt" -Destination "$doorStage\Checks\DoorwayClearanceCheck.txt"
Write-Output $doorReport
Write-Output 'PASS: deployed code matches checked payload; saved TherapyRoom scene unchanged.'
