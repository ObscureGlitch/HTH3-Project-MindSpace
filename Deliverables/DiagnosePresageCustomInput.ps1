$ErrorActionPreference='Stop'
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName='C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'
$start.Arguments='"'+(Join-Path $PSScriptRoot 'DiagnosePresageCustomInput.mjs')+'" "D:/Unity/HTH3 Project/Assets/StreamingAssets/TherapyGame/PresageBridge/node_modules/@smartspectra/node-sdk"'
$start.UseShellExecute=$false;$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
$start.EnvironmentVariables['TLW_PRESAGE_API_KEY']=[Environment]::GetEnvironmentVariable('TLW_PRESAGE_API_KEY','User')
$process=[Diagnostics.Process]::Start($start)
try{
 $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
 if(!$process.WaitForExit(40000)){throw 'Synthetic input diagnostic timed out'}
 foreach($line in ($output.Result -split '\r?\n')){if($line.StartsWith('PRESAGE_CHECK:')){$line}}
 'EXIT_CODE='+$process.ExitCode
}finally{if(!$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}
