$ErrorActionPreference='Stop'
$seasonStage='D:\Hack the Hill\Deliverables\Seasons'
$seasonProject='D:\Unity\HTH3 Project'
$seasonAssets=Join-Path $seasonProject 'Assets\TherapyGame'
$seasonChecks=Join-Path $seasonStage 'Checks'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Close Unity for compilation headroom.'}
foreach($kind in @('Runtime','Editor')){
    $options=Get-Content -LiteralPath "D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.$kind.rsp" | Where-Object {$_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^[-/](out:|refout:)'} | ForEach-Object {if($_ -match '^-r:.*TherapyGame\.Runtime\.dll'){'-r:"'+$seasonChecks+'\TherapyGame.Runtime.dll"'}else{$_}}
    $sources=@((Get-ChildItem -LiteralPath "$seasonAssets\$kind" -Filter '*.cs' -Recurse).FullName | Where-Object {-not(Test-Path -LiteralPath (Join-Path "$seasonStage\Payload" $_.Substring($seasonAssets.Length+1)))})
    $sources+=@((Get-ChildItem -LiteralPath "$seasonStage\Payload\$kind" -Filter '*.cs' -Recurse).FullName)
    $response="$seasonChecks\TherapyGame.$kind.rsp"
    [IO.File]::WriteAllLines($response,@($options)+@('-out:"'+$seasonChecks+'\TherapyGame.'+$kind+'.dll"')+@($sources|ForEach-Object {'"'+$_+'"'}))
    Push-Location $seasonProject
    try{& 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"}finally{Pop-Location}
    if($LASTEXITCODE -ne 0){throw "$kind compilation failed."}
    Write-Output "PASS: current $kind scripts plus four seasons compile."
}
[IO.File]::WriteAllText("$seasonChecks\Compilation.txt",'PASS: Runtime and Editor compiled. Unity checks pending.')
