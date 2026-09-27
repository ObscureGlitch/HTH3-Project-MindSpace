$ErrorActionPreference='Stop'
$cloudProject='D:\Unity\HTH3 Project'
$cloudStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$cloudOutput='D:\Hack the Hill\Deliverables\CloudTypesCodeCheck'
$cloudTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 1048576){throw 'Need at least 1 GB available committed memory for sequential CPU checks.'}
New-Item -ItemType Directory -Path $cloudOutput -Force | Out-Null
Add-Type -Path @("$cloudStaged\Runtime\WellnessCloudSequence.cs","$cloudStaged\Editor\CloudSequenceChecks.cs")
$cloudChecks=[TherapyGame.Editor.CloudSequenceChecks]::Run()
Write-Output $cloudChecks
[IO.File]::WriteAllText("$cloudOutput\SchedulingChecks.txt",$cloudChecks)
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$cloudTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$cloudOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$cloudOutput+'\'+$assembly+'.dll"')
 $replacementNames=if($kind -eq 'Runtime'){@('WellnessCloudDeck.cs','WellnessCloudSequence.cs','WellnessHud.cs','WellnessQuietGlassMenu.cs')}else{@('CloudSequenceChecks.cs','TherapyCloudTypesUpgrade.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$cloudProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacementNames){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacementNames){$compilerArgs.Add('"'+$cloudStaged+'\'+$kind+'\'+$name+'"')}
 $response="$cloudOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $cloudProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly with current installed sources and scoped cloud changes."
 } finally {Pop-Location}
}
