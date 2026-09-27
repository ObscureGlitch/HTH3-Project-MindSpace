$ErrorActionPreference='Stop'
$uiLive='D:\Unity\HTH3 Project'
$uiStage=$PSScriptRoot
$uiOutput=Join-Path $uiStage 'CodeCheck'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 1048576){throw 'Need 1 GB committed headroom for CPU-only checks.'}
New-Item -ItemType Directory -Path $uiOutput -Force | Out-Null
Add-Type -Path "$uiStage\Runtime\WellnessConsentChoices.cs", "$uiLive\Assets\TherapyGame\Runtime\PresageCameraCheck.cs"
[TheLastWatch.UI.WellnessConsentChoices]::Checks()
function Assert([bool]$condition,[string]$message){if(!$condition){throw $message}}
$uiGate=[TheLastWatch.Integrations.PresageCameraCheck]::new()
$uiGate.SetRunning($true);$uiGate.Tick(0)
Assert (!$uiGate.Passed(0)) 'SDK startup alone cannot pass.'
foreach($time in @(1,1.5,2,2.49,2.5)){
 $uiGate.Frames($time);$uiGate.Validation(0,$time);$uiGate.Metrics($true,$true,$time);$uiGate.Tick($time)
 if($time -lt 2.5){Assert (!$uiGate.Passed($time)) 'Camera entry before confirmation.'}
}
Assert ($uiGate.Passed(2.5)) 'Fresh stable data should pass.'
$uiGate.Validation(7,3);$uiGate.Tick(3)
Assert (!$uiGate.Passed(3)) 'Position regression must disable entry.'
Assert ($uiGate.Progress(3) -eq 0) 'Regression must clear the confirmation bar.'
$uiGate.Reset();$uiGate.SetRunning($true)
foreach($time in @(10,11,12)){$uiGate.Frames($time);$uiGate.Validation(0,$time);$uiGate.Metrics($true,$false,$time);$uiGate.Tick($time)}
Assert (!$uiGate.Passed(12)) 'One signal cannot unlock entry.'
foreach($time in @(13,14,15)){$uiGate.Frames($time);$uiGate.Validation(0,$time);$uiGate.Metrics($true,$true,$time);$uiGate.Tick($time)}
Assert ($uiGate.Passed(15)) 'Both signals recover readiness.'
Assert (!$uiGate.Passed(20)) 'Stale data cannot unlock entry.'
$uiGate.Reset();Assert (!$uiGate.Passed(20)) 'Pause/retry must clear readiness.'
'PASS: real camera-gate fresh/stale/position/signal/confirmation regressions; no devices used.'
$uiSource=[IO.File]::ReadAllText("$uiStage\Runtime\WellnessVoiceOnboarding.cs")
$uiVoice=[IO.File]::ReadAllText("$uiStage\Runtime\WellnessVoiceChat.cs")
$uiTheme=[IO.File]::ReadAllText("$uiStage\Runtime\WellnessOnboardingTheme.cs")
foreach($forbidden in @('Microphone.Start','WebCamTexture','new Camera','new RenderTexture','PlayerPrefs','SetConsent(true','ResumeMeasurement(')){
 Assert (!$uiSource.Contains($forbidden)) "Device/session operation unexpectedly in UI renderer: $forbidden"
}
foreach($expected in @('ChooseVoice(consentChoices.Voice)','consentChoices.Reset();ChooseVoice(false)','check.Passed(Time.unscaledTime)','provider.SetPreviewEnabled(false);provider.SetConsent(false,false);cameraSetup=false','GUI.color=Color.white;GUI.enabled=true;GUI.DrawTexture(preview,provider.CameraPreview,ScaleMode.ScaleToFit,false)')){
 Assert ($uiSource.Contains($expected)) "Missing consent/preview safety path: $expected"
}
Assert (($uiVoice.Split('biometrics.SetConsent(true,').Length-1) -eq 1) 'Camera enable must occur only in confirmed ChooseVoice path.'
Assert ($uiVoice.Contains('consentChoices.Reset();onboardingDetails=onboardingDiagnostics=false;')) 'New play run must clear drafts.'
Assert (!$uiVoice.Contains('private void DrawConsent(') -and !$uiVoice.Contains('private void DrawCameraSetup(')) 'Legacy renderers should be replaced, not shadowed.'
Assert ($uiTheme.Contains('WellnessUiText.Static')) 'Hover text colors must stay constant.'
# Fixed hit regions stay inside the two reference panels at each canvas scale.
$uiControls=@(@(234,184,714,76),@(234,269,714,89),@(234,367,714,76),@(274,541,308,50),@(600,541,308,50),@(655,443,295,52),@(447,521,176,46),@(634,521,222,46),@(867,521,84,46))
$uiChecks=0
foreach($size in @(@(800,600),@(1024,768),@(1280,720),@(1360,750),@(1920,1080),@(2560,1440),@(3440,1440))){
 $scale=[math]::Min($size[0]/1182.0,$size[1]/665.0);$ox=($size[0]-1182*$scale)/2;$oy=($size[1]-665*$scale)/2
 foreach($r in $uiControls){
  Assert ($r[0] -ge 202 -and ($r[0]+$r[2]) -le 978 -and $r[1] -ge 55 -and ($r[1]+$r[3]) -le 613) 'Control outside reference panel.'
  Assert ($ox+$r[0]*$scale -ge 0 -and $oy+$r[1]*$scale -ge 0 -and $ox+($r[0]+$r[2])*$scale -le $size[0] -and $oy+($r[1]+$r[3])*$scale -le $size[1]) 'Scaled control clipping.'
  $uiChecks++
 }
}
"PASS: $uiChecks button/permission bounds checks across seven sizes; stable text, color preview, explicit opt-ins and back/decline paths."
foreach($kind in @('Runtime','Editor')){
 $assembly='TherapyGame.'+$kind
 $uiArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("D:\Hack the Hill\Deliverables\TherapistCodeCheck\$assembly.rsp")){
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$uiArgs.Add('-r:"'+$uiOutput+'\TherapyGame.Runtime.dll"');continue}
  $uiArgs.Add($line)
 }
 $uiArgs.Add('-out:"'+$uiOutput+'\'+$assembly+'.dll"')
 $uiReplacements=if($kind -eq 'Runtime'){@(Get-ChildItem -LiteralPath "$uiStage\Runtime" -Filter '*.cs')}else{@()}
 $uiNames=@($uiReplacements | ForEach-Object Name)
 foreach($file in Get-ChildItem -LiteralPath "$uiLive\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse){if($file.Name -notin $uiNames){$uiArgs.Add('"'+$file.FullName+'"')}}
 foreach($file in $uiReplacements){$uiArgs.Add('"'+$file.FullName+'"')}
 $response=Join-Path $uiOutput "$assembly.rsp";[IO.File]::WriteAllLines($response,$uiArgs)
 Push-Location $uiLive
 try{
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Compilation failed: $assembly"}
  "PASS: $assembly compiled against current live project with only onboarding replacements."
 }finally{Pop-Location}
}
'PASS: no camera, microphone, provider session, Play mode, bake, shader compilation, or GPU preview started by these checks.'
