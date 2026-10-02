$ErrorActionPreference = 'Stop'
$stage = 'D:\Hack the Hill\Deliverables\KoiSchoolExpansion'
$projectAssets = 'D:\Unity\HTH3 Project\Assets\TherapyGame'
$compiler = 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$dotnet = 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe'
$check = Join-Path $stage 'Checks'
New-Item -ItemType Directory -Force -Path $check | Out-Null
foreach ($assembly in @('Runtime','Editor')) {
    $template = Get-Content -LiteralPath "D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.$assembly.rsp"
    $options = @($template | Where-Object { $_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^-out:' } | ForEach-Object { $_.Replace('D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.Runtime.dll', "$check\TherapyGame.Runtime.dll").Replace('D:/Hack the Hill/Deliverables/TherapistCodeCheck/TherapyGame.Runtime.dll', "$check\TherapyGame.Runtime.dll") })
    $sources = @((Get-ChildItem -LiteralPath "$projectAssets\$assembly" -Filter '*.cs' -Recurse).FullName | Where-Object { -not (Test-Path -LiteralPath (Join-Path "$stage\Payload" $_.Substring($projectAssets.Length + 1))) })
    $sources += @((Get-ChildItem -LiteralPath "$stage\Payload\$assembly" -Filter '*.cs' -Recurse).FullName)
    $rsp = "$check\TherapyGame.$assembly.rsp"
    # Generated compiler response, not a project source edit.
    [IO.File]::WriteAllLines($rsp, @($options) + @("-out:`"$check\TherapyGame.$assembly.dll`"") + @($sources | ForEach-Object { '"' + $_ + '"' }))
    Push-Location 'D:\Unity\HTH3 Project'
    try { & $dotnet $compiler "@$rsp" } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "$assembly compilation failed" }
}
$netRef = 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\packs\Microsoft.NETCore.App.Ref'
$ref = (Get-ChildItem -LiteralPath $netRef -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName + '\ref\net8.0'
$references = @((Get-ChildItem -LiteralPath $ref -Filter '*.dll').FullName | ForEach-Object { '-r:"' + $_ + '"' })
[IO.File]::WriteAllLines("$check\MotionTests.rsp", @('-target:exe', '-nostdlib+', "-out:`"$check\MotionTests.dll`"") + $references + @("`"$stage\Payload\Runtime\KoiSwimMotion.cs`"", "`"$stage\Payload\Runtime\KoiSchoolLayout.cs`"", "`"$stage\MotionTests.cs`""))
[IO.File]::WriteAllText("$check\MotionTests.runtimeconfig.json", '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& $dotnet $compiler "@$check\MotionTests.rsp"
if ($LASTEXITCODE -ne 0) { throw 'Motion test compilation failed' }
& $dotnet "$check\MotionTests.dll"
if ($LASTEXITCODE -ne 0) { throw 'Motion tests failed' }
