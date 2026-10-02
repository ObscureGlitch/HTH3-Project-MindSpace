$ErrorActionPreference = 'Stop'
$stage = 'D:\Hack the Hill\Deliverables\CelestialVisibility'
$project = 'D:\Unity\HTH3 Project'
$projectAssets = Join-Path $project 'Assets\TherapyGame'
$compiler = 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$dotnet = 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe'
$check = Join-Path $stage 'Checks'
if ((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 3000000) { throw 'Close Unity to provide memory headroom before these sequential code and shader checks.' }
New-Item -ItemType Directory -Force -Path $check | Out-Null
$results = [System.Collections.Generic.List[string]]::new()
foreach ($assembly in @('Runtime','Editor')) {
    $template = Get-Content -LiteralPath "D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.$assembly.rsp"
    $options = @($template | Where-Object { $_ -notmatch '^".*\.cs"$' -and $_ -notmatch '^[-/](out:|refout:)' } | ForEach-Object { $_.Replace('D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.Runtime.dll', "$check\TherapyGame.Runtime.dll").Replace('D:/Hack the Hill/Deliverables/TherapistCodeCheck/TherapyGame.Runtime.dll', "$check\TherapyGame.Runtime.dll") })
    $sources = @((Get-ChildItem -LiteralPath "$projectAssets\$assembly" -Filter '*.cs' -Recurse).FullName | Where-Object { -not (Test-Path -LiteralPath (Join-Path "$stage\Payload" $_.Substring($projectAssets.Length + 1))) })
    if (Test-Path -LiteralPath "$stage\Payload\$assembly") { $sources += @((Get-ChildItem -LiteralPath "$stage\Payload\$assembly" -Filter '*.cs' -Recurse).FullName) }
    $rsp = "$check\TherapyGame.$assembly.rsp"
    [IO.File]::WriteAllLines($rsp, @($options) + @("-out:`"$check\TherapyGame.$assembly.dll`"") + @($sources | ForEach-Object { '"' + $_ + '"' }))
    Push-Location $project
    try { & $dotnet $compiler "@$rsp" } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "$assembly compilation failed" }
    $results.Add("PASS: current $assembly C# with staged replacements compiled.")
}
Add-Type -Path "$stage\CheckNightShader.cs"
$result = [CheckNightShader]::Run("$stage\Payload\Weather\Shaders","$projectAssets\Weather\Shaders")
$results.Add($result)
Write-Output $result
$results.Add('Full-pass legacy checker cannot preprocess current URP Common.hlsl zero-argument marker macros. Unmodified Unity headers were not changed. Exact modified shared HLSL compiled independently; Unity pass import must be checked in the editor.')
$results.Add('Offline CPU compilation only; final Unity import and visible rendering still need verification.')
[IO.File]::WriteAllLines("$check\CompilationChecks.txt",$results)
