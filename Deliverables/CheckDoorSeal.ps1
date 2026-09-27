$ErrorActionPreference='Stop'
$doorProject='D:\Unity\HTH3 Project'
$doorStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$doorOutput='D:\Hack the Hill\Deliverables\DoorSealCodeCheck'
$doorTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
New-Item -ItemType Directory -Path $doorOutput -Force | Out-Null
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$doorTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$doorOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$doorOutput+'\'+$assembly+'.dll"')
 $replacementNames=if($kind -eq 'Runtime'){@()}else{@('DoorSealGeometry.cs','TherapyDoorSealFix.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$doorProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacementNames){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacementNames){$compilerArgs.Add('"'+$doorStaged+'\'+$kind+'\'+$name+'"')}
 $response="$doorOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $doorProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly against current installed live sources plus door-seal editor changes."
 } finally {Pop-Location}
}
