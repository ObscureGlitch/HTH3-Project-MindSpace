$ErrorActionPreference='Stop'
$fishingStage='D:\Hack the Hill\Deliverables\Fishing'
$fishingProject='D:\Unity\HTH3 Project'
$fishingAssets=Join-Path $fishingProject 'Assets\TherapyGame'
if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*HTH3 Project*'}){throw 'Unity must remain closed.'}
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Insufficient compilation headroom.'}
$evidence=Get-Content -LiteralPath "$fishingStage\Checks\Deployment.json" -Raw | ConvertFrom-Json
foreach($entry in $evidence.Files){if((Get-FileHash -LiteralPath (Join-Path $fishingAssets $entry.File)).Hash -ne $entry.SHA256){throw "Live fishing source changed: $($entry.File)."}}
Copy-Item -LiteralPath "$fishingStage\Payload\Runtime\WellnessFishing.cs" -Destination "$fishingAssets\Runtime\WellnessFishing.cs"
$log="$fishingStage\Checks\FishingFinalReload.log"
$arguments=@('-batchmode','-nographics','-projectPath',('"'+$fishingProject+'"'),'-executeMethod','TherapyGame.Editor.TherapyFishingSetup.VerifyBatch','-logFile',('"'+$log+'"'))
$process=Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
while(!$process.WaitForExit(1000)){}
if($process.ExitCode -ne 0){Get-Content -LiteralPath $log | Where-Object {$_ -match 'error CS\d|InvalidOperationException|Exception:|KOI_|FAILED|Script compilation errors'} | Select-Object -Last 12;throw 'Final fishing reload failed.'}
foreach($entry in $evidence.Files){$entry.SHA256=(Get-FileHash -LiteralPath (Join-Path $fishingAssets $entry.File)).Hash}
$koiPath='Exterior\KoiPond\KoiLibrary.asset'
$koiHash=(Get-FileHash -LiteralPath (Join-Path $fishingAssets $koiPath)).Hash
if($koiHash -ne 'CC22D5919403BF6B2CBCA05B8B56BDE648D7EA96919750CC83583C78B1F3E068'){throw 'Original koi library changed.'}
$evidence.Unchanged+=@{File=$koiPath;SHA256=$koiHash}
$evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$fishingStage\Checks\Deployment.json"
Copy-Item -LiteralPath "$fishingAssets\Documentation\FishingReloadCheck.txt" -Destination "$fishingStage\Checks\FishingReloadCheck.txt"
Write-Output 'PASS: final fishing code recompiled by Unity and saved references reloaded; original koi library unchanged.'
