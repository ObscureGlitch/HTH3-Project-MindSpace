$ErrorActionPreference = 'Stop'
$winterStage = 'D:\Hack the Hill\Deliverables\Seasons'
$winterProject = 'D:\Unity\HTH3 Project'
$winterAssets = Join-Path $winterProject 'Assets\TherapyGame'
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'Save the scene and close Unity first.' }
if ((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory -lt 2500000) { throw 'Not enough committed memory for Unity validation.' }
$winterBaseline = @(Get-Content -LiteralPath "$winterStage\WinterVarietyBaseline.json" -Raw | ConvertFrom-Json)
$winterNew = @('Editor\WinterTreeGeometry.cs', 'Editor\WinterTreePreview.cs')
foreach ($winterFile in $winterBaseline) {
    $winterTarget = Join-Path $winterAssets $winterFile.File
    if ((Get-FileHash -LiteralPath $winterTarget).Hash -ne $winterFile.SHA256) { throw "Live file changed since preparation: $($winterFile.File). Compare before copying." }
}
foreach ($winterFile in $winterNew) {
    if (Test-Path -LiteralPath (Join-Path $winterAssets $winterFile)) { throw "New helper already exists: $winterFile. Compare before copying." }
}
$winterFiles = @($winterBaseline.File) + $winterNew
foreach ($winterFile in $winterFiles) {
    if (!(Test-Path -LiteralPath "$winterStage\Payload\$winterFile")) { throw "Missing payload: $winterFile" }
}
$winterBackup = Join-Path $winterProject ('TherapyBackups\NaturalWinterAndSnow\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $winterBackup | Out-Null
foreach ($winterFile in $winterBaseline) {
    $winterDestination = Join-Path $winterBackup $winterFile.File
    New-Item -ItemType Directory -Path (Split-Path -Parent $winterDestination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $winterAssets $winterFile.File) -Destination $winterDestination
}
$winterMesh = "$winterAssets\Exterior\Seasons\BareWinterBranches.asset"
$winterScene = "$winterAssets\Scenes\TherapyRoom.unity"
Copy-Item -LiteralPath $winterMesh -Destination "$winterBackup\BareWinterBranches-before.asset"
Copy-Item -LiteralPath $winterScene -Destination "$winterBackup\TherapyRoom-before.unity"
$winterSceneHash = (Get-FileHash -LiteralPath $winterScene).Hash
$winterCopied = @()
foreach ($winterFile in $winterFiles) {
    $winterSource = "$winterStage\Payload\$winterFile"
    $winterTarget = Join-Path $winterAssets $winterFile
    Copy-Item -LiteralPath $winterSource -Destination $winterTarget
    $winterHash = (Get-FileHash -LiteralPath $winterTarget).Hash
    if ($winterHash -ne (Get-FileHash -LiteralPath $winterSource).Hash) { throw "Copy mismatch: $winterFile" }
    $winterCopied += [pscustomobject]@{ File = $winterFile; SHA256 = $winterHash }
}
Write-Output "Copied nine checked scripts. Backup: $winterBackup"
foreach ($winterMethod in @('RepairBranchesBatch', 'VerifyBranchesBatch')) {
    if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'Another Unity editor opened before validation.' }
    $winterLog = "$winterStage\Checks\Unity-Natural-$winterMethod.log"
    Write-Output "Starting $winterMethod in Edit mode."
    $winterRun = Start-Process -FilePath 'D:\Unity\6000.6.3f1\Editor\Unity.exe' -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $winterProject + '"'), '-executeMethod', ('TherapyGame.Editor.TherapySeasonSetup.' + $winterMethod), '-logFile', ('"' + $winterLog + '"')) -WindowStyle Hidden -PassThru
    $winterRun.WaitForExit()
    if ($winterRun.ExitCode -ne 0) {
        $winterReport = if ($winterMethod -eq 'RepairBranchesBatch') { 'WinterBranchesCheck.txt' } else { 'WinterBranchesReloadCheck.txt' }
        if (Test-Path -LiteralPath "$winterAssets\Documentation\$winterReport") { Get-Content -LiteralPath "$winterAssets\Documentation\$winterReport" }
        Select-String -LiteralPath $winterLog -Pattern 'error CS\d+|WINTER_BRANCHES|InvalidOperationException|Aborting batchmode' | ForEach-Object { $_.Line }
        throw "$winterMethod failed. Backup retained at $winterBackup."
    }
}
if ((Get-FileHash -LiteralPath $winterScene).Hash -ne $winterSceneHash) { throw 'Saved scene changed unexpectedly during mesh-only repair.' }
foreach ($winterReport in @('WinterBranchesCheck.txt', 'WinterBranchesReloadCheck.txt')) {
    $winterText = Get-Content -LiteralPath "$winterAssets\Documentation\$winterReport" -Raw
    if ($winterText -notmatch '^PASS:') { throw "Failed validation report: $winterReport" }
    Copy-Item -LiteralPath "$winterAssets\Documentation\$winterReport" -Destination "$winterStage\Checks\$winterReport"
    Write-Output $winterText
}
$winterManifest = Get-Content -LiteralPath "$winterStage\Deployment.json" -Raw | ConvertFrom-Json
foreach ($winterFile in $winterCopied) {
    $winterEntry = $winterManifest.Files | Where-Object File -eq $winterFile.File
    if ($winterEntry) { $winterEntry.SHA256 = $winterFile.SHA256 }
    else { $winterManifest.Files = @($winterManifest.Files) + $winterFile }
}
$winterManifest.Verification = 'PASS: natural per-tree winter geometry, connected bent forks and twigs, saved-mesh reload, explicit Rain/Snow selection and particle switching, palette restoration and unchanged scene hash. Play visual check pending.'
$winterManifest.VerifiedAt = Get-Date -Format o
$winterManifest | Add-Member -NotePropertyName NaturalWinterAndSnowBackup -NotePropertyValue $winterBackup -Force
[IO.File]::WriteAllText("$winterStage\Deployment.json", ($winterManifest | ConvertTo-Json -Depth 6))
$winterResult = [pscustomobject]@{
    Status = 'PASS'; Backup = $winterBackup; SceneUnchanged = $true; SceneSHA256 = $winterSceneHash
    MeshSHA256 = (Get-FileHash -LiteralPath $winterMesh).Hash; Files = $winterCopied
    Verification = $winterManifest.Verification; VerifiedAt = $winterManifest.VerifiedAt
}
[IO.File]::WriteAllText("$winterStage\Checks\WinterVarietyDeployment.json", ($winterResult | ConvertTo-Json -Depth 6))
$winterResult | ConvertTo-Json -Depth 6
