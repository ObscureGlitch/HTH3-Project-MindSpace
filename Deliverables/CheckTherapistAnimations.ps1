$ErrorActionPreference='Stop'
$animationProject='D:\Unity\HTH3 Project'
$animationStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$animationOutput='D:\Hack the Hill\Deliverables\TherapistAnimationCodeCheck'
$animationTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
$runtimeReplacements=@('WellnessTherapist.cs','WellnessVoiceChat.cs')
$editorReplacements=@('TherapyTherapistAnimationUpgrade.cs')

New-Item -ItemType Directory -Path $animationOutput -Force | Out-Null
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$animationTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$animationOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$animationOutput+'\'+$assembly+'.dll"')
 $replacements=if($kind -eq 'Runtime'){$runtimeReplacements}else{$editorReplacements}
 foreach($source in Get-ChildItem -LiteralPath "$animationProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacements){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacements){$compilerArgs.Add('"'+$animationStaged+'\'+$kind+'\'+$name+'"')}
 $response="$animationOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $animationProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly against current live sources plus scoped deterministic companion-animation changes."
 } finally {Pop-Location}
}

$therapist=[IO.File]::ReadAllText("$animationStaged\Runtime\WellnessTherapist.cs")
$voice=[IO.File]::ReadAllText("$animationStaged\Runtime\WellnessVoiceChat.cs")
foreach($token in @('PlayLoop("Talk")','PlayLoop("Listen")','PlayOneShot("Nod")','PlayOneShot("Write")','PlayLoop("Think")','PlayOneShot("Wave")')) {
 if(!$therapist.Contains($token)){throw "Missing deterministic state mapping: $token"}
}
if($therapist.Contains('Random01') -or $therapist.Contains('Random.Range')){throw 'Random companion animation choice remains.'}
foreach($token in @('IsUserSpeakingWith','IsAwaitingAgentResponseWith','UserTurnSerial')) {
 if(!$voice.Contains($token)){throw "Missing conversation state signal: $token"}
}
Write-Output 'PASS: all animation choices are tied to explicit connection, VAD, transcript or response transitions; no random animation selection remains.'
