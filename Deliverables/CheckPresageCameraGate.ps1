$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'TherapyGame\Runtime\PresageCameraCheck.cs')
$check=[TheLastWatch.Integrations.PresageCameraCheck]::new()
function Assert([bool]$condition,[string]$message){if(!$condition){throw $message}}
$check.SetRunning($true)
$check.Tick(0)
Assert (!$check.Passed(0)) 'SDK startup without camera must not pass.'
foreach($time in @(1,1.5,2,2.49,2.5)){
  $check.Frames($time);$check.Validation(0,$time);$check.Metrics($true,$true,$time);$check.Tick($time)
  if($time -lt 2.5){Assert (!$check.Passed($time)) 'Gate passed before sustained 1.5-second confirmation.'}
}
Assert ($check.Passed(2.5)) 'Fresh stable signals should pass after 1.5 seconds.'
$check.Validation(7,4.1);$check.Tick(4.1)
Assert (!$check.Passed(4.1)) 'Loss of chest framing must immediately revoke readiness.'
Assert ($check.Progress(4.1) -eq 0) 'Invalid framing must clear the displayed confirmation progress.'
$check.Reset();$check.SetRunning($true)
foreach($time in @(10,11,12,13)){$check.Frames($time);$check.Validation(0,$time);$check.Metrics($true,$false,$time);$check.Tick($time)}
Assert (!$check.Passed(13)) 'Unreliable breathing must not pass.'
foreach($time in @(14,15,16,17)){$check.Frames($time);$check.Validation(0,$time);$check.Metrics($true,$true,$time);$check.Tick($time)}
Assert ($check.Passed(17)) 'Valid data should recover readiness.'
Assert (!$check.Passed(22)) 'Stale data must fail even before another Tick.'
$check.Reset();Assert (!$check.Passed(23)) 'Pause/retry must clear readiness.'
$check.SetRunning($true)
foreach($time in @(30,31)){$check.Frames($time);$check.Validation(0,$time);$check.Metrics($true,$true,$time);$check.Tick($time)}
# Even a regression and recovery received in a single Unity update must restart confirmation.
$check.Metrics($false,$true,31.1);$check.Metrics($true,$true,31.2);$check.Tick(31.2)
Assert (!$check.Passed(31.6)) 'Brief quality loss must restart confirmation, not pass from an old timer.'
Assert ($check.Progress(40) -eq 0) 'Stale signal must not show a completed bar.'
'CAMERA_GATE_TESTS: PASS'
