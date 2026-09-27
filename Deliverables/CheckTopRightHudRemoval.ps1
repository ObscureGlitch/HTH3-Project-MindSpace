$ErrorActionPreference='Stop'
$hudProject='D:\Unity\HTH3 Project'
$hudStage='D:\Hack the Hill\Deliverables\TopRightHudPatch\Runtime\WellnessHud.cs'
$hudOutput='D:\Hack the Hill\Deliverables\TopRightHudCodeCheck'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 1048576){throw 'Need 1 GB available committed memory for the CPU code check.'}
New-Item -ItemType Directory -Path $hudOutput -Force | Out-Null
$hudSource=[IO.File]::ReadAllText($hudStage)
if($hudSource.Contains('Settings   Esc') -or $hudSource.Contains('chat.BiometricBadge') -or $hudSource.Contains('Backdrop(layout.settings)')){throw 'Top-right HUD draw calls remain.'}
$hudVoice=[IO.File]::ReadAllText("$hudProject\Assets\TherapyGame\Runtime\WellnessVoiceChat.cs")
if(!$hudVoice.Contains('if (keyboard.escapeKey.wasPressedThisFrame) OpenSettings();')){throw 'Esc settings shortcut missing.'}
if(!$hudVoice.Contains('GUILayout.Label(biometrics.StatusText,smallStyle);')){throw 'Camera status must remain available in companion controls.'}
foreach($draw in @('DrawClock(layout.clock);','DrawPulseMonitor(layout.pulse);','DrawMusic(layout.music);','DrawCaptions(layout.captions);','DrawJourney(layout.journey);')){
 if(!$hudSource.Contains($draw)){throw "Unrelated HUD drawing changed: $draw"}
}
$hudArgs=[System.Collections.Generic.List[string]]::new()
foreach($line in [IO.File]::ReadAllLines('D:\Hack the Hill\Deliverables\TherapistCodeCheck\TherapyGame.Runtime.rsp')){
 if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
 $hudArgs.Add($line)
}
$hudArgs.Add('-out:"'+$hudOutput+'\TherapyGame.Runtime.dll"')
foreach($source in Get-ChildItem -LiteralPath "$hudProject\Assets\TherapyGame\Runtime" -Filter '*.cs' -Recurse){
 if($source.Name -ne 'WellnessHud.cs'){$hudArgs.Add('"'+$source.FullName+'"')}
}
$hudArgs.Add('"'+$hudStage+'"')
$hudResponse="$hudOutput\TherapyGame.Runtime.rsp"
[IO.File]::WriteAllLines($hudResponse,$hudArgs)
Push-Location $hudProject
try{
 & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$hudResponse"
 if($LASTEXITCODE -ne 0){throw 'HUD compilation failed.'}
 'PASS: runtime assembly compiles with current live sources and only WellnessHud.cs replaced.'
 'PASS: top-right labels/dot removed; Esc shortcut, camera status in controls, and other HUD displays retained.'
}finally{Pop-Location}
