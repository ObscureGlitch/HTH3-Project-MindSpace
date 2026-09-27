$ErrorActionPreference='Stop'
$unity='D:/Unity/6000.6.3f1/Editor/Data'
$refs="$unity/MonoBleedingEdge/lib/mono/unityjit-win32"
$exe=Join-Path $PSScriptRoot 'CheckPresageFramePipe.exe'
& "$unity/DotNetSdk/dotnet.exe" "$unity/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" -nologo -noconfig -nostdlib -target:exe "-out:$exe" "-r:$refs/mscorlib.dll" "-r:$refs/System.dll" "-r:$refs/System.Core.dll" (Join-Path $PSScriptRoot 'CheckPresageFramePipe.cs') (Join-Path $PSScriptRoot 'TherapyGame/Runtime/PresagePrivatePipe.cs')
if($LASTEXITCODE -ne 0){throw 'Unity Mono test compilation failed'}
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName="$unity/MonoBleedingEdge/bin/mono.exe"
$start.Arguments='"'+$exe+'" "C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe" "'+(Join-Path $PSScriptRoot 'CheckPresageFramePipe.mjs')+'"'
$start.UseShellExecute=$false;$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
# The default Mono CLI profile is not Unity Editor's Windows profile. Explicitly match the Editor.
$start.EnvironmentVariables['MONO_PATH']=$refs
$process=[Diagnostics.Process]::Start($start)
try{
  $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
  if(!$process.WaitForExit(45000)){throw 'Unity Mono pipe test timed out'}
  $output.Result
  if($process.ExitCode -ne 0){throw 'Unity Mono pipe test failed'}
}finally{if(!$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}
