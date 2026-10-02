$ErrorActionPreference='Stop'
$seasonStage='D:\Hack the Hill\Deliverables\Seasons'
$seasonAssets='D:\Unity\HTH3 Project\Assets\TherapyGame'
$seasonManifest=Get-Content -LiteralPath "$seasonStage\Deployment.json" -Raw | ConvertFrom-Json
$seasonFiles=foreach($entry in $seasonManifest.Files){
    $source=Join-Path "$seasonStage\Payload" $entry.File
    $target=Join-Path $seasonAssets $entry.File
    $hash=(Get-FileHash -LiteralPath $source).Hash
    if((Get-FileHash -LiteralPath $target).Hash -ne $hash){throw "Installed source differs: $($entry.File)"}
    [pscustomobject]@{File=$entry.File;SHA256=$hash}
}
foreach($report in @('SeasonsCheck.txt','SeasonsReloadCheck.txt')){
    $body=Get-Content -LiteralPath "$seasonAssets\Documentation\$report" -Raw
    if($body -notmatch '^PASS:'){throw "Unity check failed: $report"}
    Copy-Item -LiteralPath "$seasonAssets\Documentation\$report" -Destination "$seasonStage\Checks\$report"
}
function Get-SceneObjects([string]$path){
    $result=@{}
    $text=Get-Content -LiteralPath $path -Raw
    foreach($match in [regex]::Matches($text,'(?ms)^--- !u!\d+ &(-?\d+).*?(?=^--- !u!|\z)')){
        $result[$match.Groups[1].Value]=$match.Value
    }
    return $result
}
$before=Get-SceneObjects (Join-Path $seasonManifest.Backup 'TherapyRoom-before-seasons.unity')
$after=Get-SceneObjects "$seasonAssets\Scenes\TherapyRoom.unity"
$removed=@($before.Keys|Where-Object {-not $after.ContainsKey($_)})
if($removed.Count){throw 'Original scene objects were removed.'}
# Only the room component list, garden child list, sky and rain references change.
$allowed=@('652317645','775736850','650455085','887939353')
$changed=@($before.Keys|Where-Object {$after.ContainsKey($_)-and $before[$_] -cne $after[$_]})
foreach($id in $changed){if($id -notin $allowed){throw "Unexpected existing scene object changed: $id"}}
$added=@($after.Keys|Where-Object {-not $before.ContainsKey($_)})
if($added.Count -ne 11){throw "Unexpected seasonal object count: $($added.Count)"}
$seasonManifest.Files=$seasonFiles
$seasonManifest.Verification='PASS: compilation, Unity install, saved-scene reload, climate/palette lifecycle and scoped object-ID diff. Play/GPU/input checks pending.'
$seasonManifest|Add-Member -NotePropertyName VerifiedAt -NotePropertyValue (Get-Date -Format o) -Force
[IO.File]::WriteAllText("$seasonStage\Deployment.json",($seasonManifest|ConvertTo-Json -Depth 5))
$result=[pscustomobject]@{
    Status='PASS'
    OriginalSceneObjects=$before.Count
    RemovedSceneObjects=$removed.Count
    ChangedExistingObjects=$changed
    AddedSeasonObjects=$added.Count
    InstalledScripts=$seasonFiles.Count
    Pending='Visual, GPU and input checks in Play mode'
}
[IO.File]::WriteAllText("$seasonStage\Checks\Verification.json",($result|ConvertTo-Json -Depth 5))
$result|ConvertTo-Json -Depth 5
