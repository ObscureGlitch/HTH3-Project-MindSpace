$ErrorActionPreference='Stop'
$quietProject='D:\Unity\HTH3 Project'
$quietStaged='D:\Hack the Hill\Deliverables\TherapyGame'
$quietOutput='D:\Hack the Hill\Deliverables\QuietGlassCodeCheck'
$quietTemplate='D:\Hack the Hill\Deliverables\TherapistCodeCheck'
New-Item -ItemType Directory -Path $quietOutput -Force | Out-Null
Add-Type -Path @("$quietStaged\Runtime\WellnessUiMotion.cs","$quietStaged\Editor\QuietGlassPolishChecks.cs","$quietStaged\Runtime\WellnessQuietGlassLayout.cs","$quietStaged\Editor\QuietGlassLayoutChecks.cs","$quietStaged\Runtime\WellnessCaptions.cs","$quietStaged\Runtime\WellnessPlaylist.cs","$quietStaged\Editor\QuietHudStateChecks.cs","$quietStaged\Runtime\WellnessDoorwayConversation.cs","$quietStaged\Editor\DoorwayConversationChecks.cs")
$checks="PASS: $([TherapyGame.Editor.QuietGlassPolishChecks]::Run()) motion/shape checks; $([TherapyGame.Editor.QuietGlassLayoutChecks]::Run()) layout checks; $([TherapyGame.Editor.QuietHudStateChecks]::Run()) caption/playlist checks; $([TherapyGame.Editor.DoorwayConversationChecks]::Run()) doorway checks."
Write-Output $checks
[IO.File]::WriteAllText("$quietOutput\Checks.txt",$checks)
foreach($kind in @('Runtime','Editor')) {
 $assembly='TherapyGame.'+$kind
 $compilerArgs=[System.Collections.Generic.List[string]]::new()
 foreach($line in [IO.File]::ReadAllLines("$quietTemplate\$assembly.rsp")) {
  if($line -match '^".*\.cs"$' -or $line -match '^[-/](out:|refout:)'){continue}
  if($line -match '^-r:.*TherapyGame\.Runtime\.dll'){$compilerArgs.Add('-r:"'+$quietOutput+'\TherapyGame.Runtime.dll"');continue}
  $compilerArgs.Add($line)
 }
 $compilerArgs.Add('-out:"'+$quietOutput+'\'+$assembly+'.dll"')
 $replacementNames=if($kind -eq 'Runtime'){@('WellnessUiText.cs','WellnessUiMotion.cs','WellnessUiPrimitives.cs','WellnessUiTheme.cs','WellnessQuietGlassLayout.cs','WellnessQuietGlassTheme.cs','WellnessQuietGlassMenu.cs')}else{@('QuietGlassTextChecks.cs','QuietGlassPolishChecks.cs','TherapyQuietGlassPolish.cs','QuietGlassLayoutChecks.cs','TherapyQuietGlassUpgrade.cs')}
 foreach($source in Get-ChildItem -LiteralPath "$quietProject\Assets\TherapyGame\$kind" -Filter '*.cs' -Recurse) {
  if($source.Name -notin $replacementNames){$compilerArgs.Add('"'+$source.FullName+'"')}
 }
 foreach($name in $replacementNames){$compilerArgs.Add('"'+$quietStaged+'\'+$kind+'\'+$name+'"')}
 $response="$quietOutput\$assembly.rsp"
 [IO.File]::WriteAllLines($response,$compilerArgs)
 Push-Location $quietProject
 try {
  & 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe' 'D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@$response"
  if($LASTEXITCODE -ne 0){throw "Scoped compilation failed: $assembly"}
  Write-Output "PASS: $assembly against current live sources plus scoped Quiet Glass changes."
 } finally {Pop-Location}
}
