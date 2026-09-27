$ErrorActionPreference='Stop'
$summonLive='D:\Unity\HTH3 Project'
$summonStage=$PSScriptRoot
$summonOutput=Join-Path $summonStage 'CodeCheck'
if((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 1048576){throw 'Need 1 GB committed headroom for CPU-only checks.'}
New-Item -ItemType Directory -Path $summonOutput -Force | Out-Null
Add-Type -Path "$summonStage\Runtime\WellnessNightSkyEvents.cs", "$summonLive\Assets\TherapyGame\Editor\NightSkyEventChecks.cs", "$summonStage\Tests\AuroraSummonChecks.cs"
[TherapyGame.Editor.NightSkyEventChecks]::Run()
[AuroraSummonChecks]::Run()
function Assert([bool]$condition,[string]$message){if(!$condition){throw $message}}
$summonSky=[IO.File]::ReadAllText("$summonStage\Runtime\WellnessSkyCycle.cs")
foreach($expected in @('if(!CanSummonAurora)return false;','if(DaylightAmount(hour)>.02f)hour=22;','SetWeather(WeatherMode.Clear);','auroraBorealis=true;','UpdateNightEffects(0);','SetNightEffects(liveSky,effects);SetNightEffects(livePond,effects);')){
 Assert ($summonSky.Contains($expected)) "Missing sky integration: $expected"
}
foreach($name in @('WellnessQuietGlassMenu.cs','WellnessVoiceChat.cs')){
 $source=[IO.File]::ReadAllText("$summonStage\Runtime\$name")
 Assert ($source.Contains('Summon northern lights') -and $source.Contains('.SummonAurora()') -and $source.Contains('.CanSummonAurora')) "Missing guarded settings action: $name"
}
$summonControls=@(@(634,590,316,46),@(970,601,418,28),@(634,645,755,27),@(634,679,755,27))
foreach($size in @(@(800,600),@(1024,768),@(1280,720),@(1360,750),@(1920,1080),@(2560,1440),@(3440,1440))){
 $scale=[math]::Min($size[0]/1672.0,$size[1]/941.0)
 foreach($r in $summonControls){Assert ($r[0] -ge 612 -and $r[1] -ge 574 -and ($r[0]+$r[2]) -le 1410 -and ($r[1]+$r[3]) -le 718) 'Summon control exceeds its card.'}
 Assert ((1410*$scale) -lt $size[0] -and (718*$scale) -lt $size[1]) 'Summon card exceeds viewport.'
}
'PASS: both settings paths call the guarded sky action; existing sky/pond rendering is shared; 28 control-bounds cases.'
foreach($kind in @('Runtime','Editor')){
 $assembly='TherapyGame.'+$kind
 $summonArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("D:\Hack the Hill\Deliverables\TherapistCodeCheck\$assembly.rsp")){
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$summonArgs.Add('-r:"'+$summonOutput+'\TherapyGame.Runtime.dll"');continue}
  $summonArgs.Add($line)
 }
 $summonArgs.Add('-out:"'+$summonOutput+'\'+$assembly+'.dll"')
 $summonReplacements=if($kind -eq 'Runtime'){@(Get-ChildItem -LiteralPath "$summonStage\Runtime" -Filter '*.cs')}else{@()}
 $summonNames=@($summonReplacements | ForEach-Object Name)
 foreach($file in Get-ChildItem -LiteralPath "$summonLive\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse){if($file.Name -notin $summonNames){$summonArgs.Add('"'+$file.FullName+'"')}}
 foreach($file in $summonReplacements){$summonArgs.Add('"'+$file.FullName+'"')}
 $response=Join-Path $summonOutput "$assembly.rsp";[IO.File]::WriteAllLines($response,$summonArgs)
 Push-Location $summonLive
 try{
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Compilation failed: $assembly"}
  "PASS: $assembly compiled against current live project with four scoped aurora/settings replacements."
 }finally{Pop-Location}
}
'PASS: no Play, camera/microphone, network session, shader import, second Unity instance, GPU preview or bake started.'
