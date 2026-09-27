$ErrorActionPreference='Stop'
$varietyProject='D:\Unity\HTH3 Project'
$varietyStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$varietyOutput='D:\Hack the Hill\Deliverables\NightSkyVarietyCodeCheck'
$varietyTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 1048576){throw 'Need at least 1 GB available committed memory before sequential CPU checks.'}
New-Item -ItemType Directory -Path $varietyOutput -Force | Out-Null
Add-Type -Path @("$varietyStaged\Runtime\WellnessNightSkyEvents.cs","$varietyStaged\Editor\NightSkyEventChecks.cs")
$checks=[TherapyGame.Editor.NightSkyEventChecks]::Run()
Write-Output $checks
[IO.File]::WriteAllText("$varietyOutput\SchedulingChecks.txt",$checks)
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$varietyTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$varietyOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$varietyOutput+'\'+$assembly+'.dll"')
 $replacementNames=if($kind -eq 'Runtime'){@('WellnessSkyCycle.cs','WellnessNightSkyEvents.cs')}else{@('NightSkyEventChecks.cs','TherapyNightSkyVarietyUpgrade.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$varietyProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacementNames){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacementNames){$compilerArgs.Add('"'+$varietyStaged+'\'+$kind+'\'+$name+'"')}
 $response="$varietyOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $varietyProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly with current installed sources and scoped night-sky variety changes."
 } finally {Pop-Location}
}
