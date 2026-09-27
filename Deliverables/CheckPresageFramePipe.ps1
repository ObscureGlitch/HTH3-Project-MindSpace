$ErrorActionPreference='Stop'
$name='MindSpacePresage-'+[Guid]::NewGuid().ToString('N')
$pipe=[IO.Pipes.NamedPipeServerStream]::new($name,[IO.Pipes.PipeDirection]::InOut,1,[IO.Pipes.PipeTransmissionMode]::Byte,([IO.Pipes.PipeOptions]::Asynchronous -bor [IO.Pipes.PipeOptions]::CurrentUserOnly))
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName='C:\Users\obscu\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
$start.Arguments='"'+(Join-Path $PSScriptRoot 'CheckPresageFramePipe.mjs')+'" '+$name
$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true
$process=[Diagnostics.Process]::new();$process.StartInfo=$start
try{
  $connection=$pipe.WaitForConnectionAsync();[void]$process.Start()
  if(!$connection.Wait(5000)){throw 'Node did not connect to the current-user-only pipe'}
  $header=[byte[]]::new(24)
  [Buffer]::BlockCopy([BitConverter]::GetBytes([int]2),0,$header,0,4)
  [Buffer]::BlockCopy([BitConverter]::GetBytes([int]1),0,$header,4,4)
  [Buffer]::BlockCopy([BitConverter]::GetBytes([int]8),0,$header,8,4)
  [Buffer]::BlockCopy([BitConverter]::GetBytes([int]2),0,$header,12,4)
  [Buffer]::BlockCopy([BitConverter]::GetBytes([double]1000),0,$header,16,8)
  $pixels=[byte[]](255,0,0,255,0,0,255,255)
  $pipe.Write($header,0,7);$pipe.Write($header,7,17);$pipe.Write($pixels,0,8);$pipe.Flush();$pipe.Dispose()
  if(!$process.WaitForExit(5000)){throw 'Pipe receiver did not stop'}
  if($process.ExitCode -ne 0){throw 'Pipe receiver failed'}
  $process.StandardOutput.ReadToEnd()
}finally{$pipe.Dispose();if($process.Id -and !$process.HasExited){$process.Kill()};$process.Dispose()}
