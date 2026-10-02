$ErrorActionPreference = 'Stop'
$stage = 'D:\Hack the Hill\Deliverables\PondFloorNatural'
$projectAssets = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$compiler = 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$dotnet = 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe'
$check = Join-Path $stage 'Checks'
New-Item -ItemType Directory -Force -Path $check | Out-Null
foreach ($assembly in @('Runtime','Editor')) {
    $template = Get-Content -LiteralPath "D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.$assembly.rsp"
    $options = @($template | Where-Object { $_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^-out:' } | ForEach-Object { $_.Replace('D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.Runtime.dll', "$check\TherapyGame.Runtime.dll").Replace('D:/Hack the Hill/Deliverables/TherapistCodeCheck/TherapyGame.Runtime.dll', "$check\TherapyGame.Runtime.dll") })
    $sources = @((Get-ChildItem -LiteralPath "$projectAssets\$assembly" -Filter '*.cs' -Recurse).FullName)
    if (Test-Path -LiteralPath "$stage\Payload\$assembly") { $sources += @((Get-ChildItem -LiteralPath "$stage\Payload\$assembly" -Filter '*.cs' -Recurse).FullName) }
    $rsp = "$check\TherapyGame.$assembly.rsp"
    [IO.File]::WriteAllLines($rsp, @($options) + @("-out:`"$check\TherapyGame.$assembly.dll`"") + @($sources | ForEach-Object { '"' + $_ + '"' }))
    Push-Location 'D:\Unity\HTH3 Project'
    try { & $dotnet $compiler "@$rsp" } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "$assembly compilation failed" }
}
Write-Output 'PASS: current runtime and new natural-floor editor integration compile.'
