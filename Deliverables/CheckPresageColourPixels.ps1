$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'TherapyGame/Runtime/PresageColourPixels.cs')
# Source RGBA bottom row blue/white, top row red/green.
$source=[byte[]](0,0,255,255,255,255,255,255,255,0,0,255,0,255,0,255)
$frame=[byte[]]::new(16);$preview=[byte[]]::new(12)
$expected=@{0='255,0,0,255,0,255,0,255,0,0,255,255,255,255,255,255';90='0,0,255,255,255,0,0,255,255,255,255,255,0,255,0,255';180='255,255,255,255,0,0,255,255,0,255,0,255,255,0,0,255';270='0,255,0,255,255,255,255,255,255,0,0,255,0,0,255,255'}
foreach($angle in @(0,90,180,270)){
  [TheLastWatch.Integrations.PresageColourPixels]::Normalize($source,$frame,2,2,$angle,$false)
  if(($frame -join ',') -ne $expected[$angle]){throw "Incorrect colour/orientation at $angle degrees"}
}
[TheLastWatch.Integrations.PresageColourPixels]::Normalize($source,$frame,2,2,0,$true)
if(($frame -join ',') -ne ($source -join ',')){throw 'Vertical mirror correction failed'}
[TheLastWatch.Integrations.PresageColourPixels]::Normalize($source,$frame,2,2,0,$false)
[TheLastWatch.Integrations.PresageColourPixels]::Preview($frame,2,2,$preview,2,2)
if(($preview -join ',') -ne '255,255,255,0,0,255,0,255,0,255,0,0'){throw 'Colour preview must mirror horizontally and store rows bottom-up'}
'RAW_COLOUR_PIXELS: PASS'
