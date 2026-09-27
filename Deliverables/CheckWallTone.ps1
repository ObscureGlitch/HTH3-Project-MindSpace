$ErrorActionPreference='Stop'
$wallProject='D:\Unity\HTH3 Project'
$wallStage='D:\Hack the Hill\Deliverables\TherapyGame'
$wallOutput='D:\Hack the Hill\Deliverables\WallToneCodeCheck'
$wallTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 1048576){throw 'Need 1 GB available committed memory for CPU checks.'}
New-Item -ItemType Directory -Path $wallOutput -Force | Out-Null
Add-Type -Path "$wallStage\Editor\PlasterFaceProjection.cs"
[TherapyGame.Editor.PlasterFaceProjection]::Checks()
foreach($kind in @('Runtime','Editor')){
 $assembly='TherapyGame.'+$kind
 $wallArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$wallTemplate\$assembly.rsp")){
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$wallArgs.Add('-r:"'+$wallOutput+'\TherapyGame.Runtime.dll"');continue}
  $wallArgs.Add($line)
 }
 $wallArgs.Add('-out:"'+$wallOutput+'\'+$assembly+'.dll"')
 foreach($source in Get-ChildItem -LiteralPath "$wallProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse){
  if($source.Name -notin @('PlasterFaceProjection.cs','TherapyWallToneRepair.cs')){$wallArgs.Add('"'+$source.FullName+'"')}
 }
 if($kind -eq 'Editor'){foreach($name in @('PlasterFaceProjection.cs','TherapyWallToneRepair.cs')){$wallArgs.Add('"'+$wallStage+'\Editor\'+$name+'"')}}
 $wallResponse="$wallOutput\$assembly.rsp";[IO.File]::WriteAllLines($wallResponse,$wallArgs)
 Push-Location $wallProject
 try{
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$wallResponse"
  if($LASTEXITCODE -ne 0){throw "Wall repair compilation failed: $assembly"}
  "PASS: $assembly compiled with current live sources and only the new wall repair added."
 }finally{Pop-Location}
}
