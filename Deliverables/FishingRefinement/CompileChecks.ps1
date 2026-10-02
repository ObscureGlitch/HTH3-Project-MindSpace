$ErrorActionPreference='Stop'
$castStage='D:\Hack the Hill\Deliverables\FishingRefinement'
$castProject='D:\Unity\HTH3 Project'
$castAssets=Join-Path $castProject 'Assets\TherapyGame'
$castChecks=Join-Path $castStage 'Checks'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Insufficient compilation headroom.'}
foreach($kind in @('Runtime','Editor')){
    $template=Get-ChildItem -LiteralPath "$castProject\Library\Bee\artifacts" -Filter "TherapyGame.$kind.rsp" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if(!$template){throw "Unity $kind compiler response missing."}
    $options=Get-Content -LiteralPath $template.FullName | Where-Object {$_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^[-/](out:|refout:)'} | ForEach-Object {if($_ -match '^-r:.*TherapyGame\.Runtime(?:\.ref)?\.dll'){'-r:"'+$castChecks+'\TherapyGame.Runtime.dll"'}else{$_}}
    $sources=@((Get-ChildItem -LiteralPath "$castAssets\$kind" -Filter '*.cs' -Recurse).FullName | Where-Object {-not(Test-Path -LiteralPath (Join-Path "$castStage\Payload" $_.Substring($castAssets.Length+1)))})
    $sources+=@((Get-ChildItem -LiteralPath "$castStage\Payload\$kind" -Filter '*.cs' -Recurse).FullName)
    $response="$castChecks\TherapyGame.$kind.rsp"
    [IO.File]::WriteAllLines($response,@($options)+@('-out:"'+$castChecks+'\TherapyGame.'+$kind+'.dll"')+@($sources|ForEach-Object {'"'+$_+'"'}))
    Push-Location $castProject
    try{& 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"}finally{Pop-Location}
    if($LASTEXITCODE -ne 0){throw "$kind compilation failed."}
    Write-Output "PASS: current $kind scripts plus koi pantheon compile."
}
[IO.File]::WriteAllText("$castChecks\Compilation.txt",'PASS: Runtime and Editor compiled. See Deployment.json for subsequent Unity validation status.')
