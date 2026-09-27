$ErrorActionPreference='Stop'
$nightProject='D:\Unity\HTH3 Project'
$nightStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$nightOutput='D:\Hack the Hill\Deliverables\NightSkyCodeCheck'
$nightTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
New-Item -ItemType Directory -Path $nightOutput -Force | Out-Null
Add-Type -Path @("$nightStaged\Runtime\WellnessNightSkyEvents.cs","$nightStaged\Editor\NightSkyEventChecks.cs")
$checks=[TherapyGame.Editor.NightSkyEventChecks]::Run()
Write-Output $checks
[IO.File]::WriteAllText("$nightOutput\SchedulingChecks.txt",$checks)
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$nightTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$nightOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$nightOutput+'\'+$assembly+'.dll"')
 $replacementNames=if($kind -eq 'Runtime'){@('WellnessSkyCycle.cs','WellnessNightSkyEvents.cs','WellnessVoiceChat.cs')}else{@('NightSkyEventChecks.cs','TherapyNightSkyUpgrade.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$nightProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacementNames){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacementNames){$compilerArgs.Add('"'+$nightStaged+'\'+$kind+'\'+$name+'"')}
 $response="$nightOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $nightProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly against current installed live sources plus the scoped night-sky changes."
 } finally {Pop-Location}
}
