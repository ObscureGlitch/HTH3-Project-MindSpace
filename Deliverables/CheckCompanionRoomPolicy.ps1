$ErrorActionPreference='Stop'
$policySdk='D:\Unity\6000.6.3f1\Editor\Data\DotNetSdk'
$policyRoot='D:\Hack the Hill\Deliverables'
$policyOutput=Join-Path $policyRoot 'CompanionRoamingCodeCheck'
$policyRefs=Join-Path $policySdk 'packs\Microsoft.NETCore.App.Ref\8.0.21\ref\net8.0'
New-Item -ItemType Directory -Path $policyOutput -Force | Out-Null
$policyArgs=@('-nologo','-nostdlib+','-target:exe',('-out:"'+$policyOutput+'\CompanionRoomPolicyTests.dll"'))
$policyArgs+=@(Get-ChildItem -LiteralPath $policyRefs -Filter '*.dll' | ForEach-Object {'-r:"'+$_.FullName+'"'})
$policyArgs+=@( ('"'+$policyRoot+'\TherapyGame\Runtime\WellnessCompanionRoomPolicy.cs"'), ('"'+$policyRoot+'\CompanionRoomPolicyTests.cs"') )
[IO.File]::WriteAllLines((Join-Path $policyOutput 'CompanionRoomPolicyTests.rsp'),$policyArgs)
& "$policySdk\dotnet.exe" "$policySdk\sdk\8.0.318\Roslyn\bincore\csc.dll" "@$policyOutput\CompanionRoomPolicyTests.rsp"
if($LASTEXITCODE -ne 0){throw 'Room policy tests failed to compile.'}
Copy-Item -LiteralPath "$policyRoot\CompanionRoomPolicyTests.runtimeconfig.json" -Destination $policyOutput -Force
& "$policySdk\dotnet.exe" "$policyOutput\CompanionRoomPolicyTests.dll"
if($LASTEXITCODE -ne 0){throw 'Room policy tests failed.'}
