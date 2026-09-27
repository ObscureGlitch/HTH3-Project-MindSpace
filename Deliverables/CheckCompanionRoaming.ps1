$ErrorActionPreference='Stop'
$roamingProject='D:\Unity\HTH3 Project'
$roamingStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$roamingOutput='D:\Hack the Hill\Deliverables\CompanionRoamingCodeCheck'
$roamingTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
New-Item -ItemType Directory -Path $roamingOutput -Force | Out-Null
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$roamingTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$roamingOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$roamingOutput+'\'+$assembly+'.dll"')
 $replacements=if($kind -eq 'Runtime'){@('WellnessTherapist.cs','WellnessVoiceChat.cs','WellnessDoor.cs','WellnessCompanionMovement.cs','WellnessCompanionNavigation.cs','WellnessCompanionRoomPolicy.cs')}else{@('TherapyCompanionRoamingUpgrade.cs','CompanionRoamingValidation.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$roamingProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacements){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacements){$compilerArgs.Add('"'+$roamingStaged+'\'+$kind+'\'+$name+'"')}
 $response="$roamingOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $roamingProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly compiled against installed dependencies with roaming companions."
 } finally {Pop-Location}
}
