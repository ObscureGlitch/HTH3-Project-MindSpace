$ErrorActionPreference='Stop'
$livingProject='D:\Unity\HTH3 Project'
$livingStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$livingOutput='D:\Hack the Hill\Deliverables\LivingHudCodeCheck'
$livingTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
New-Item -ItemType Directory -Path $livingOutput -Force | Out-Null
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$livingTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$livingOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$livingOutput+'\'+$assembly+'.dll"')
 $replacementNames=if($kind -eq 'Runtime'){@('WellnessHud.cs','WellnessUiTheme.cs','WellnessExplorer.cs','WellnessRoomInput.cs','WellnessVoiceChat.cs','WellnessQuietGlassMenu.cs','WellnessDoorwayConversation.cs','WellnessFireflies.cs','WellnessSkyCycle.cs','WellnessCloudDeck.cs')}else{@('TherapyLivingHudSetup.cs','DoorwayConversationChecks.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$livingProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacementNames){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacementNames){$compilerArgs.Add('"'+$livingStaged+'\'+$kind+'\'+$name+'"')}
 $response="$livingOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $livingProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly against current installed live sources plus Living HUD changes."
 } finally {Pop-Location}
}
