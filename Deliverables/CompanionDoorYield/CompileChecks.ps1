$ErrorActionPreference='Stop'
$doorStage='D:\Hack the Hill\Deliverables\CompanionDoorYield'
$doorProject='D:\Unity\HTH3 Project'
$doorAssets=Join-Path $doorProject 'Assets\TherapyGame'
$doorChecks=Join-Path $doorStage 'Checks'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Close Unity to provide memory headroom for compilation.'}
New-Item -ItemType Directory -Force -Path $doorChecks | Out-Null
foreach($kind in @('Runtime','Editor')) {
    $options=Get-Content -LiteralPath "D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.$kind.rsp" | Where-Object {$_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^[-/](out:|refout:)'} | ForEach-Object {if($_ -match '^-r:.*TherapyGame\.Runtime\.dll'){ '-r:"'+$doorChecks+'\TherapyGame.Runtime.dll"' }else{$_}}
    $sources=@((Get-ChildItem -LiteralPath "$doorAssets\$kind" -Filter '*.cs' -Recurse).FullName | Where-Object {-not(Test-Path -LiteralPath (Join-Path "$doorStage\Payload" $_.Substring($doorAssets.Length+1)))})
    $sources+=@((Get-ChildItem -LiteralPath "$doorStage\Payload\$kind" -Filter '*.cs' -Recurse).FullName)
    $response="$doorChecks\TherapyGame.$kind.rsp"
    [IO.File]::WriteAllLines($response,@($options)+@('-out:"'+$doorChecks+'\TherapyGame.'+$kind+'.dll"')+@($sources | ForEach-Object {'"'+$_+'"'}))
    Push-Location $doorProject
    try { & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response" }finally{Pop-Location}
    if($LASTEXITCODE -ne 0){throw "$kind compilation failed"}
    Write-Output "PASS: current $kind scripts plus door clearance fix compile."
}
[IO.File]::WriteAllText("$doorChecks\Compilation.txt",'PASS: runtime and editor scripts compiled. Unity doorway command and navmesh regressions pending on reopen.')
