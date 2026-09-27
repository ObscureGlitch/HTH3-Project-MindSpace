[CmdletBinding()]
param(
    [string]$Model = 'gemini-3.1-pro-preview',
    [string[]]$AgentId,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$apiRoot = 'https://api.elevenlabs.io/v1'
$settingsPath = Join-Path $PSScriptRoot 'TherapyGame\Integrations\Settings\VoiceCompanions.asset'
$apiKey = [Environment]::GetEnvironmentVariable('ELEVENLABS_API_KEY')
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    $apiKey = [Environment]::GetEnvironmentVariable('TLW_ELEVENLABS_API_KEY')
}
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw 'Set ELEVENLABS_API_KEY (or TLW_ELEVENLABS_API_KEY) in this process before running this script. Never paste the key into a tracked file or Unity asset.'
}

$headers = @{
    'xi-api-key' = $apiKey
    Accept = 'application/json'
}

if ($null -eq $AgentId -or $AgentId.Count -eq 0) {
    if (!(Test-Path -LiteralPath $settingsPath)) {
        throw "Voice companion settings were not found at $settingsPath"
    }
    $AgentId = @(
        Select-String -LiteralPath $settingsPath -Pattern '^\s+agentId:\s+(\S+)\s*$' |
            ForEach-Object { $_.Matches[0].Groups[1].Value }
    )
}
$AgentId = @($AgentId | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
if ($AgentId.Count -eq 0) {
    throw 'No ElevenLabs agent IDs were supplied or found in VoiceCompanions.asset.'
}

Write-Output "Checking ElevenLabs model availability for '$Model'..."
$models = Invoke-RestMethod -Method Get -Uri "$apiRoot/convai/llm/list" -Headers $headers
$supported = @($models.llms | ForEach-Object { $_.llm })
if ($Model -notin $supported) {
    $gemini = @($supported | Where-Object { $_ -like 'gemini-*' } | Sort-Object)
    throw "ElevenLabs does not currently report '$Model' as available. Reported Gemini models: $($gemini -join ', ')"
}

$results = foreach ($id in $AgentId) {
    $escapedId = [Uri]::EscapeDataString($id)
    $uri = "$apiRoot/convai/agents/$escapedId"
    $agent = Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
    $current = [string]$agent.conversation_config.agent.prompt.llm

    if (!$Apply) {
        [pscustomobject]@{
            Agent = $agent.name
            AgentId = $id
            CurrentModel = $current
            RequestedModel = $Model
            Applied = $false
        }
        continue
    }

    if ($current -ne $Model) {
        $payload = @{
            conversation_config = @{
                agent = @{
                    prompt = @{
                        llm = $Model
                    }
                }
            }
            version_description = "Use $Model for MindSpace voice conversations"
        } | ConvertTo-Json -Depth 8

        Invoke-RestMethod -Method Patch -Uri $uri -Headers $headers -ContentType 'application/json' -Body $payload | Out-Null
    }

    $verified = Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
    $verifiedModel = [string]$verified.conversation_config.agent.prompt.llm
    if ($verifiedModel -ne $Model) {
        throw "Agent '$id' still reports '$verifiedModel' after the update; expected '$Model'."
    }
    [pscustomobject]@{
        Agent = $verified.name
        AgentId = $id
        PreviousModel = $current
        CurrentModel = $verifiedModel
        Applied = $true
    }
}

$results | Format-Table -AutoSize
if (!$Apply) {
    Write-Output 'Dry run only. Re-run with -Apply to publish versioned agent updates.'
}
