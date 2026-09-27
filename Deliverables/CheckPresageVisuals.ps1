param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'PresageVisualCheck'))
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'TherapyGame/Runtime/PresageUiRaster.cs')
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
function Assert([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Same([byte[]]$A, [byte[]]$B) {
    for ($i=0; $i -lt $A.Length; $i++) { if ($A[$i] -ne $B[$i]) { return $false } }
    return $true
}
function Save-Pixels([byte[]]$Pixels, [int]$Width, [int]$Height, [string]$Name, [bool]$Checkerboard=$false) {
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height)
    try {
        for ($y=0; $y -lt $Height; $y++) { for ($x=0; $x -lt $Width; $x++) {
            $p = (($Height-1-$y)*$Width+$x)*4
            if($Checkerboard){
                $shade=if(([int][Math]::Floor($x/12)+[int][Math]::Floor($y/12))%2 -eq 0){225}else{245}
                $alpha=$Pixels[$p+3]/255.0
                $bitmap.SetPixel($x,$y,[System.Drawing.Color]::FromArgb(255,
                    [int]($Pixels[$p]*$alpha+$shade*(1-$alpha)),
                    [int]($Pixels[$p+1]*$alpha+$shade*(1-$alpha)),
                    [int]($Pixels[$p+2]*$alpha+$shade*(1-$alpha))))
            }else{
                $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($Pixels[$p+3],$Pixels[$p],$Pixels[$p+1],$Pixels[$p+2]))
            }
        } }
        $bitmap.Save((Join-Path $OutputDirectory $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
}
$guide=[TheLastWatch.UI.PresageUiRaster]::Guide()
Assert ($guide.Length -eq 400*400*4) 'Guide dimensions changed.'
$occupied=0
for ($y=0; $y -lt 400; $y++) { for ($x=0; $x -lt 400; $x++) {
    $alpha=$guide[($y*400+$x)*4+3]
    if ($alpha -gt 0) { $occupied++ }
    Assert ([Math]::Abs([int]$alpha-[int]$guide[($y*400+399-$x)*4+3]) -le 1) 'Guide is not symmetric.'
} }
Assert ($occupied -gt 4000 -and $occupied -lt 9000) 'Guide is missing or filled instead of outlined.'
Assert ($guide[(200*400+200)*4+3] -eq 0) 'Face interior must remain transparent.'
Save-Pixels $guide 400 400 'position-guide.png'
$idle=[byte[]]::new(400*112*4); $idleLater=[byte[]]::new($idle.Length)
$active=[byte[]]::new($idle.Length); $later=[byte[]]::new($idle.Length)
[TheLastWatch.UI.PresageUiRaster]::Pulse($idle,0,0,$false)
[TheLastWatch.UI.PresageUiRaster]::Pulse($idleLater,.7,90,$false)
Assert (Same $idle $idleLater) 'No-signal state must not animate a heartbeat.'
[TheLastWatch.UI.PresageUiRaster]::Pulse($active,.7,72,$true)
[TheLastWatch.UI.PresageUiRaster]::Pulse($later,.8,72,$true)
Assert (!(Same $idle $active)) 'Valid signal should draw a trace.'
Assert (!(Same $active $later)) 'Trace must move with phase.'
$hasBrightRed=$false
for($p=0;$p -lt $active.Length;$p+=4){if($active[$p+3] -gt 180 -and $active[$p] -gt 180 -and $active[$p] -gt 2*$active[$p+1]){$hasBrightRed=$true;break}}
Assert $hasBrightRed 'Active pulse trace should be bright red.'
$transparent=0
for($p=3;$p -lt $active.Length;$p+=4){
  Assert ($idle[$p] -eq 0) 'No signal must draw no opaque background or flatline.'
  if($active[$p] -eq 0){$transparent++}
}
Assert ($transparent -gt 400*112*.65) 'The area behind the waveform must be transparent.'
[TheLastWatch.UI.PresageUiRaster]::Pulse($idleLater,.7,[double]::NaN,$true)
Assert (Same $idle $idleLater) 'Invalid BPM must not draw a trace.'
Save-Pixels $active 400 112 'pulse-active.png'
Save-Pixels $active 400 112 'pulse-transparency-check.png' $true
Save-Pixels $idle 400 112 'pulse-no-signal.png'
$timer=[System.Diagnostics.Stopwatch]::StartNew()
for($i=0;$i -lt 60;$i++){[TheLastWatch.UI.PresageUiRaster]::Pulse($active,$i/60.0,72,$true)}
$timer.Stop()
'PRESAGE_VISUAL_TESTS: PASS'
'Average raster time: {0:N2} ms/frame' -f ($timer.Elapsed.TotalMilliseconds/60)
"Graphics previews saved to $OutputDirectory (not gameplay screenshots)."
