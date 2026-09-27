$ErrorActionPreference='Stop'
$bankProject='D:\Unity\HTH3 Project'
$bankStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$bankOutput='D:\Hack the Hill\Deliverables\CumulusBankCodeCheck'
$bankTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 1048576){throw 'Need at least 1 GB available committed memory before CPU checks.'}
New-Item -ItemType Directory -Path $bankOutput -Force | Out-Null
Add-Type -Path @("$bankStaged\Runtime\WellnessCumulusLife.cs","$bankStaged\Editor\CumulusBankChecks.cs","$bankProject\Assets\TherapyGame\Runtime\WellnessCloudSequence.cs","$bankProject\Assets\TherapyGame\Editor\CloudSequenceChecks.cs")
$bankChecks=[TherapyGame.Editor.CumulusBankChecks]::Run()+[TherapyGame.Editor.CloudSequenceChecks]::Run()
Write-Output $bankChecks
[IO.File]::WriteAllText("$bankOutput\BehaviorChecks.txt",$bankChecks)
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$bankTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$bankOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$bankOutput+'\'+$assembly+'.dll"')
 $replacementNames=if($kind -eq 'Runtime'){@('WellnessCloudDeck.cs','WellnessCumulusLife.cs')}else{@('CumulusBankChecks.cs','TherapyCumulusBankUpgrade.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$bankProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacementNames){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacementNames){$compilerArgs.Add('"'+$bankStaged+'\'+$kind+'\'+$name+'"')}
 $response="$bankOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $bankProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly with current live sources and scoped cumulus-bank changes."
 } finally {Pop-Location}
}
