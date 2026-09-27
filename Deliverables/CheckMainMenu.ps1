$ErrorActionPreference='Stop'
$menuProject='D:\Unity\HTH3 Project'
$menuStage='D:\Hack the Hill\Deliverables\TherapyGame'
$menuOutput='D:\Hack the Hill\Deliverables\MainMenuCodeCheck'
$menuTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 1048576){throw 'Need 1 GB available committed memory for CPU checks.'}
New-Item -ItemType Directory -Path $menuOutput -Force | Out-Null
Add-Type -Path "$menuStage\Runtime\WellnessMainMenuFlow.cs"
[TheLastWatch.UI.WellnessMainMenuFlow]::Checks()
$menuCode=[IO.File]::ReadAllText("$menuStage\Runtime\WellnessMainMenu.cs")
foreach($forbidden in @('Microphone.Start','WebCamTexture','Conversation.Start','new Camera','new RenderTexture','SetConsent(true')){
 if($menuCode.Contains($forbidden)){throw "Unexpected capture/render call: $forbidden"}
}
foreach($expected in @('Hold(chat);Hold(hud);Hold(player);Hold(chat.biometricCoach);Hold(chat.biometrics);','SetConsent(false,false)','DefaultExecutionOrder(-10000)','Restore(true)')){
 if(!$menuCode.Contains($expected)){throw "Menu startup/restore guard missing: $expected"}
}
foreach($kind in @('Runtime','Editor')){
 $assembly='TherapyGame.'+$kind
 $menuArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$menuTemplate\$assembly.rsp")){
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$menuArgs.Add('-r:"'+$menuOutput+'\TherapyGame.Runtime.dll"');continue}
  $menuArgs.Add($line)
 }
 $menuArgs.Add('-out:"'+$menuOutput+'\'+$assembly+'.dll"')
 $menuNames=if($kind -eq 'Runtime'){@('WellnessMainMenu.cs','WellnessMainMenuFlow.cs')}else{@('TherapyMainMenuSetup.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$menuProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse){
  if($source.Name -notin $menuNames){$menuArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $menuNames){$menuArgs.Add('"'+$menuStage+'\'+$kind+'\'+$name+'"')}
 $menuResponse="$menuOutput\$assembly.rsp";[IO.File]::WriteAllLines($menuResponse,$menuArgs)
 Push-Location $menuProject
 try{
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$menuResponse"
  if($LASTEXITCODE -ne 0){throw "Main-menu compilation failed: $assembly"}
  "PASS: $assembly compiled with current live sources and only main-menu additions."
 }finally{Pop-Location}
}
'PASS: title-screen capture and rendering guard source checks. In-game verification remains separate.'
