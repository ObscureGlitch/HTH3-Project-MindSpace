$ErrorActionPreference = 'Stop'
$stage = 'D:\Hack the Hill\Deliverables\KoiPond'
$project = 'D:\Unity\HTH3 Project'
$assets = Join-Path $project 'Assets\TherapyGame'
$files = @('Runtime\KoiPondLibrary.cs','Runtime\KoiSwimMotion.cs','Runtime\WellnessKoiPond.cs','Editor\TherapyKoiPondSetup.cs','Editor\KoiSource\KoiModels.json','Exterior\Shaders\QuietPond.shader','Documentation\KoiPond.md')
$shader = 'Exterior\Shaders\QuietPond.shader'
$old = [IO.File]::ReadAllText((Join-Path $assets $shader)).Replace("`r`n","`n")
$new = [IO.File]::ReadAllText((Join-Path "$stage\Payload" $shader)).Replace("`r`n","`n")
$reconstructed = $new.Replace("        _BedVisibility (`"Underwater garden clarity`", Range(0, 1)) = 0`n",'').Replace("                half _BedVisibility;`n",'')
$newBlock = @'
                // Optional koi clarity: preserve the original appearance when unset,
                // while retaining the same waves and reflective grazing-angle sheen.
                half opacity = lerp(lerp(max(_Opacity,.76), .98, fresnel), lerp(.38, .98, fresnel), _BedVisibility);
                half alpha = opacity * (1 - smoothstep(0.97, 1.0, shoreDistance) * 0.48);
'@
$oldBlock = '                half alpha = lerp(max(_Opacity,.76), .98, fresnel) * (1 - smoothstep(0.97, 1.0, shoreDistance) * 0.48);'
$reconstructed = $reconstructed.Replace($newBlock.Replace("`r`n","`n"),$oldBlock)
if ($old -cne $reconstructed) { throw 'The live water shader changed since staging; inspect and rebase instead of overwriting.' }
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path "$stage\Payload" $file))) { throw "Missing payload: $file" }
    if ($file -ne $shader -and (Test-Path -LiteralPath (Join-Path $assets $file))) { throw "Target already exists: $file. Inspect it before any replacement." }
}
$memory = Get-CimInstance Win32_OperatingSystem
if ($memory.FreeVirtualMemory -lt 2300000) { throw 'Less than 2.2 GiB committed-memory headroom. Save and close Unity before importing.' }
$backup = Join-Path $project ('TherapyBackups\KoiPond\Deploy-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path "$backup\Exterior\Shaders" | Out-Null
Copy-Item -LiteralPath (Join-Path $assets $shader) -Destination (Join-Path $backup $shader)
Copy-Item -LiteralPath "$assets\Scenes\TherapyRoom.unity" -Destination "$backup\TherapyRoom-saved-before-import.unity"
$manifest = @()
foreach ($file in $files) {
    $source = Join-Path "$stage\Payload" $file
    $target = Join-Path $assets $file
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    $a = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $b = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($a -ne $b) { throw "Copy verification failed: $file" }
    $manifest += [PSCustomObject]@{File=$file; SHA256=$b}
}
$result = [PSCustomObject]@{Backup=$backup; Files=$manifest; InstalledAt=(Get-Date -Format o); SceneInstallation='Pending Therapy Game > Install Koi Pond'}
[IO.File]::WriteAllText("$stage\Deployment.json", ($result | ConvertTo-Json -Depth 5))
Write-Output ($result | ConvertTo-Json -Depth 5)
