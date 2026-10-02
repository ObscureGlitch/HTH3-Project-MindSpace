$ErrorActionPreference='Stop'
$photoStage='D:\Hack the Hill\Deliverables\PhotoCamera'
$photoProject='D:\Unity\HTH3 Project'
$photoAssets=Join-Path $photoProject 'Assets\TherapyGame'
$photoChecks=Join-Path $photoStage 'Checks'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000){throw 'Close Unity to provide compilation headroom.'}
New-Item -ItemType Directory -Force -Path $photoChecks | Out-Null
foreach($kind in @('Runtime','Editor')){
    $options=Get-Content -LiteralPath "D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.$kind.rsp" | Where-Object {$_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^[-/](out:|refout:)'} | ForEach-Object {if($_ -match '^-r:.*TherapyGame\.Runtime\.dll'){'-r:"'+$photoChecks+'\TherapyGame.Runtime.dll"'}else{$_}}
    $sources=@((Get-ChildItem -LiteralPath "$photoAssets\$kind" -Filter '*.cs' -Recurse).FullName | Where-Object {-not(Test-Path -LiteralPath (Join-Path "$photoStage\Payload" $_.Substring($photoAssets.Length+1)))})
    $sources+=@((Get-ChildItem -LiteralPath "$photoStage\Payload\$kind" -Filter '*.cs' -Recurse).FullName)
    $response="$photoChecks\TherapyGame.$kind.rsp"
    [IO.File]::WriteAllLines($response,@($options)+@('-out:"'+$photoChecks+'\TherapyGame.'+$kind+'.dll"')+@($sources|ForEach-Object {'"'+$_+'"'}))
    Push-Location $photoProject
    try{& 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"}finally{Pop-Location}
    if($LASTEXITCODE -ne 0){throw "$kind compilation failed."}
    Write-Output "PASS: current $kind scripts plus player photo camera compile."
}
[IO.File]::WriteAllText("$photoChecks\Compilation.txt",'PASS: runtime and editor scripts compiled. Unity verification pending.')
