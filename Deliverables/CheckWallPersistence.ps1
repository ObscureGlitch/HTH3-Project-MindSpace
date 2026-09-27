$ErrorActionPreference='Stop'
$wallProject='D:\Unity\HTH3 Project'
$wallStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$wallOutput='D:\Hack the Hill\Deliverables\WallPersistenceCodeCheck'
$wallTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
New-Item -ItemType Directory -Path $wallOutput -Force | Out-Null
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$wallTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$wallOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$wallOutput+'\'+$assembly+'.dll"')
 $replacementNames=if($kind -eq 'Runtime'){@('WellnessPlasterLighting.cs')}else{@('MindSpaceWallLighting.cs','TherapyMindSpacePolish.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$wallProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacementNames){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacementNames){$compilerArgs.Add('"'+$wallStaged+'\'+$kind+'\'+$name+'"')}
 $response="$wallOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $wallProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly against installed live sources plus wall-persistence changes."
 } finally {Pop-Location}
}
