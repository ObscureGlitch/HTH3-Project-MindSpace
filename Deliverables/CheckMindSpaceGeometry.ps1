$ErrorActionPreference='Stop'
Add-Type -Path @(
 'D:\Hack the Hill\Deliverables\ExteriorVerification\NumericalShim.cs',
 'D:\Hack the Hill\Deliverables\TherapyGame\Editor\MindSpaceExteriorGeometry.cs'
)
$geometry = [TherapyGame.Editor.MindSpaceExteriorGeometry]::Build()
[TherapyGame.Editor.MindSpaceExteriorGeometry]::Validate($geometry)
$export = @{}
foreach($name in $geometry.Keys) {
 $export[$name] = @($geometry[$name].vertices | ForEach-Object { ,@($_.x,$_.y,$_.z) })
}
$export | ConvertTo-Json -Depth 6 -Compress | Set-Content -LiteralPath 'D:\Hack the Hill\Deliverables\ExteriorVerification\geometry.json' -Encoding utf8
Write-Output 'PASS: numerical geometry preflight and mesh export. No Unity or GPU used.'
