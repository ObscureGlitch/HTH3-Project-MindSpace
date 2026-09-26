param([switch]$TherapyOnly)
$ErrorActionPreference = 'Stop'
$project = 'D:\Unity\HTH3 Project'
$staging = 'D:\Hack the Hill\Deliverables\TherapyGame'
$sdk = 'D:\Hack the Hill\Deliverables\UnityPackages\io.elevenlabs.agents'
$output = 'D:\Hack the Hill\Deliverables\VoiceRainCodeCheck'
$dotnet = 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe'
$compiler = 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Compile-Assembly([string]$name, [string]$sourceDirectory, [string[]]$references, [bool]$editor = $false) {
    $templateName = if ($editor) { 'TherapyGame.Editor' } else { 'TherapyGame.Runtime' }
    $template = Join-Path $project "Library\Bee\artifacts\1900b0aE.dag\$templateName.rsp"
    $argsList = [System.Collections.Generic.List[string]]::new()
    foreach ($line in [System.IO.File]::ReadAllLines($template)) {
        if ($line -match '^[-/](out:|refout:|analyzer:|additionalfile:)') { continue }
        if ($line -match '^".*\.cs"$') { continue }
        if ($line -match '^-r:.*TherapyGame\.(Runtime|Editor)\.') { continue }
        if ($line -match '^-r:.*ElevenLabs\.Agents\.') { continue }
        $argsList.Add($line)
    }
    $argsList.Add('-out:"' + (Join-Path $output "$name.dll") + '"')
    foreach ($reference in $references) { $argsList.Add('-r:"' + (Join-Path $output "$reference.dll") + '"') }
    foreach ($source in (Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.cs' -Recurse)) {
        $argsList.Add('"' + $source.FullName + '"')
    }
    $response = Join-Path $output "$name.rsp"
    [System.IO.File]::WriteAllLines($response, $argsList)
    Push-Location $project
    try {
        & $dotnet $compiler "@$response"
        if ($LASTEXITCODE -ne 0) { throw "CPU-only compilation failed: $name" }
        Write-Output "PASS: $name"
    } finally { Pop-Location }
}
if (!$TherapyOnly) {
Compile-Assembly 'ElevenLabs.Agents.Core' "$sdk\Runtime\Core" @()
Compile-Assembly 'ElevenLabs.Agents.Native' "$sdk\Runtime\Native" @('ElevenLabs.Agents.Core')
Compile-Assembly 'ElevenLabs.Agents.WebGL' "$sdk\Runtime\WebGL" @('ElevenLabs.Agents.Core')
Compile-Assembly 'ElevenLabs.Agents.WebGL.Editor' "$sdk\Editor" @('ElevenLabs.Agents.Core','ElevenLabs.Agents.WebGL') $true
}
Compile-Assembly 'TherapyGame.Runtime' "$staging\Runtime" @('ElevenLabs.Agents.Core')
Compile-Assembly 'TherapyGame.Editor' "$staging\Editor" @('TherapyGame.Runtime','ElevenLabs.Agents.Core') $true
Write-Output 'Requested assemblies compiled without starting Unity, rendering, a microphone, or a network session.'
