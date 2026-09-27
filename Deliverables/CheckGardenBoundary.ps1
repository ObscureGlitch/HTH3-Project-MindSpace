$ErrorActionPreference='Stop'
$boundaryProject='D:\Unity\HTH3 Project'
$boundaryStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$boundaryOutput='D:\Hack the Hill\Deliverables\GardenBoundaryCodeCheck'
$boundaryTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
$boundaryMemory=Get-CimInstance Win32_OperatingSystem
if($boundaryMemory.FreeVirtualMemory -lt 1048576){throw 'Need at least 1 GB available committed memory before the sequential CPU checks.'}
New-Item -ItemType Directory -Path $boundaryOutput -Force | Out-Null
Add-Type -Path "$boundaryStaged\Editor\GardenHorizonGeometry.cs"
$boundaryChecks=[TherapyGame.Editor.GardenHorizonGeometry]::RunChecks()
Write-Output $boundaryChecks
[IO.File]::WriteAllText("$boundaryOutput\GeometryChecks.txt",$boundaryChecks)
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$boundaryTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$boundaryOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$boundaryOutput+'\'+$assembly+'.dll"')
 $replacementNames=if($kind -eq 'Runtime'){@()}else{@('GardenHorizonGeometry.cs','TherapyGardenBoundaryUpgrade.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$boundaryProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacementNames){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacementNames){$compilerArgs.Add('"'+$boundaryStaged+'\'+$kind+'\'+$name+'"')}
 $response="$boundaryOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $boundaryProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly against the current installed sources and the scoped boundary changes."
 } finally {Pop-Location}
}
