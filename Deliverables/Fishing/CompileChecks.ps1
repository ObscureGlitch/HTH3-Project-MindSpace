$ErrorActionPreference='Stop'
$fishingStage='D:\Hack the Hill\Deliverables\Fishing'
$fishingProject='D:\Unity\HTH3 Project'
$fishingAssets=Join-Path $fishingProject 'Assets\TherapyGame'
$fishingChecks=Join-Path $fishingStage 'Checks'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Close Unity for compilation headroom.'}
foreach($kind in @('Runtime','Editor')){
    $template=Get-ChildItem -LiteralPath "$fishingProject\Library\Bee\artifacts" -Filter "TherapyGame.$kind.rsp" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if(!$template){throw "Current Unity $kind compiler response file missing."}
    $options=Get-Content -LiteralPath $template.FullName | Where-Object {$_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^[-/](out:|refout:)'} | ForEach-Object {if($_ -match '^-r:.*TherapyGame\.Runtime(?:\.ref)?\.dll'){'-r:"'+$fishingChecks+'\TherapyGame.Runtime.dll"'}else{$_}}
    $sources=@((Get-ChildItem -LiteralPath "$fishingAssets\$kind" -Filter '*.cs' -Recurse).FullName | Where-Object {-not(Test-Path -LiteralPath (Join-Path "$fishingStage\Payload" $_.Substring($fishingAssets.Length+1)))})
    $sources+=@((Get-ChildItem -LiteralPath "$fishingStage\Payload\$kind" -Filter '*.cs' -Recurse).FullName)
    $response="$fishingChecks\TherapyGame.$kind.rsp"
    [IO.File]::WriteAllLines($response,@($options)+@('-out:"'+$fishingChecks+'\TherapyGame.'+$kind+'.dll"')+@($sources|ForEach-Object {'"'+$_+'"'}))
    Push-Location $fishingProject
    try{& 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"}finally{Pop-Location}
    if($LASTEXITCODE -ne 0){throw "$kind compilation failed."}
    Write-Output "PASS: current $kind scripts plus koi fishing compile."
}
[IO.File]::WriteAllText("$fishingChecks\Compilation.txt",'PASS: Runtime and Editor compiled. Unity checks pending.')
