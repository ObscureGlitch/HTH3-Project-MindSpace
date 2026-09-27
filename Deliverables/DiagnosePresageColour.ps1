param([ValidateRange(5,55)][int]$Seconds=20,[switch]$CompareFormats)
$ErrorActionPreference='Stop'
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName='C:\Users\obscu\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
$diagnosticScript=if($CompareFormats){'DiagnosePresageFormats.mjs'}else{'DiagnosePresageColour.mjs'}
$start.Arguments='"'+(Join-Path $PSScriptRoot $diagnosticScript)+'" "D:\Unity\HTH3 Project\Assets\StreamingAssets\TherapyGame\PresageBridge\node_modules\@smartspectra\node-sdk" '+$Seconds
$start.UseShellExecute=$false;$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
$start.EnvironmentVariables['TLW_PRESAGE_API_KEY']=[Environment]::GetEnvironmentVariable('TLW_PRESAGE_API_KEY','User')
$process=[Diagnostics.Process]::new();$process.StartInfo=$start
try{
  [void]$process.Start();$output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
  if(!$process.WaitForExit(($Seconds+5)*1000)){$process.Kill();$process.WaitForExit();'DIAGNOSTIC_TIMEOUT'}
  'EXIT_CODE='+$process.ExitCode
  'OUTPUT_CHARACTERS='+$output.Result.Length
  'STDERR_CHARACTERS='+$errors.Result.Length
  $diagnosticKey=$start.EnvironmentVariables['TLW_PRESAGE_API_KEY']
  $safeErrors=$errors.Result
  if($diagnosticKey){$safeErrors=$safeErrors.Replace($diagnosticKey,'[redacted]')}
  $safeErrors=$safeErrors -replace 'https?://\S+','[URL omitted]'
  $safeErrors -split '\r?\n' | Where-Object {$_ -match 'RET_CHECK|Calculator::|CalculatorGraph|timestamp mismatch|packet type|Tensor|INVALID_ARGUMENT|INTERNAL:|Graph has errors|Failed to run|Graph execution'} | Select-Object -Last 12
  foreach($category in @('MODULE_NOT_FOUND','ERR_REQUIRE_ASYNC_MODULE','TypeError','SyntaxError','ReferenceError','DLL','access violation')){
    if($errors.Result -match [regex]::Escape($category)){'DIAGNOSTIC_ERROR_CATEGORY='+$category}
  }
  foreach($line in ($output.Result -split '\r?\n')){
    try{$item=$line|ConvertFrom-Json -ErrorAction Stop}catch{continue}
    if($null -ne $item.frames){'format={0} status={1} validation={2} monochrome={3} frames={4}' -f $item.format,$item.status,$item.validation,$item.monochrome,$item.frames}
    if($null -ne $item.errorCode){'ERROR_CODE='+$item.errorCode}
    if($item.startupFailed){'STARTUP_FAILED'}
    if($CompareFormats -and $null -ne $item.validations){'FORMAT_COMPARISON='+($item|ConvertTo-Json -Compress)}
  }
}finally{
  if($process.Id -and !$process.HasExited){$process.Kill()}
  $process.Dispose()
}
