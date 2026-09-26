$ErrorActionPreference = 'Stop'
$textureRoot = 'D:\Hack the Hill\Deliverables\TherapyGame\Textures\Realism'
$materialIds = @('oak_veneer_01', 'wool_boucle', 'rough_linen', 'hessian_230', 'white_plaster_02', 'knitted_fleece')
foreach ($materialId in $materialIds) {
    $materialFolder = Join-Path $textureRoot $materialId
    New-Item -ItemType Directory -Path $materialFolder -Force | Out-Null
    $materialFiles = Invoke-RestMethod -Uri ('https://api.polyhaven.com/files/' + $materialId)
    foreach ($map in @('Diffuse', 'nor_gl', 'Rough')) {
        $download = $materialFiles.$map.'2k'.jpg
        if (-not $download.url) { throw "No 2K JPG available for $materialId / $map" }
        $destination = Join-Path $materialFolder ([IO.Path]::GetFileName(([Uri]$download.url).AbsolutePath))
        if (-not (Test-Path -LiteralPath $destination)) { Invoke-WebRequest -Uri $download.url -OutFile $destination }
        $actual = (Get-FileHash -LiteralPath $destination -Algorithm MD5).Hash.ToLowerInvariant()
        if ($actual -ne $download.md5.ToLowerInvariant()) { throw "Checksum mismatch: $destination" }
        Write-Output "$materialId / $map : verified"
    }
}
