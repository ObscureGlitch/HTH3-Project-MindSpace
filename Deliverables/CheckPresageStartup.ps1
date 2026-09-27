param([int]$Seconds = 20)
$ErrorActionPreference = 'Stop'
$bridge = 'D:\Unity\HTH3 Project\Assets\StreamingAssets\TherapyGame\PresageBridge\bridge.mjs'
$node = (Get-Command node -ErrorAction Stop).Source
$secret = [Environment]::GetEnvironmentVariable('TLW_PRESAGE_API_KEY','User')
if ([string]::IsNullOrWhiteSpace($secret)) { throw 'User Presage key is missing.' }
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $node
$start.Arguments = '"' + $bridge + '" --preview true --diagnostics'
$start.WorkingDirectory = Split-Path $bridge
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['TLW_PRESAGE_API_KEY'] = $secret.Trim()
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
try {
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit($Seconds * 1000)) {
        $process.StandardInput.WriteLine('stop')
        $process.StandardInput.Close()
        if (!$process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit() }
    }
    $metrics = 0
    $stable = 0
    $bothReliable = 0
    $pulseEvents = 0
    $breathingEvents = 0
    $frames = 0
    $previews = 0
    $lastValidation = $null
    foreach ($line in ($stdout.Result -split '\r?\n')) {
        try { $message = $line | ConvertFrom-Json -ErrorAction Stop } catch { continue }
        switch ($message.type) {
            'diagnostic' { "SIGNAL_STATUS: pulseSamples=$($message.pulseCount) pulseStable=$($message.pulseStable) pulseConfidence=$($message.pulseConfidence) breathingSamples=$($message.breathingCount) breathingStable=$($message.breathingStable) breathingConfidence=$($message.breathingConfidence)" }
            'frame' { $frames++ }
            'preview' { $previews++ }
            'metrics' {
                $metrics++
                if($message.hasPulse){$pulseEvents++}
                if($message.hasBreathing){$breathingEvents++}
                if($message.hasPulse -and $message.pulseStable -and $message.pulseConfidence -ge 60){
                    $stable++
                    if($message.hasBreathing -and $message.breathingStable -and $message.breathingConfidence -ge 60){$bothReliable++}
                }
            }
            'error' { "ERROR: $(([string]$message.message).Replace($secret,'[redacted]'))" }
            'validation' { if ($message.validationCode -ne $lastValidation) { "VALIDATION $($message.validationCode): $(([string]$message.hint).Replace($secret,'[redacted]'))"; $lastValidation = $message.validationCode } }
            'status' { "STATUS: $($message.statusCode)" }
            'ready' { 'BRIDGE_READY' }
        }
    }
    "METRIC_EVENTS=$metrics STABLE_PULSE_EVENTS=$stable EXIT_CODE=$($process.ExitCode)"
    "FRAME_EVENTS=$frames LOCAL_PREVIEW_EVENTS=$previews"
    "PULSE_SNAPSHOTS=$pulseEvents BREATHING_SNAPSHOTS=$breathingEvents BOTH_RELIABLE_SNAPSHOTS=$bothReliable"
    # Only emit recognized dependency errors, never arbitrary native stderr or metric values.
    $errors = $stderr.Result
    if ($errors -match 'ERR_MODULE_NOT_FOUND') { 'DEPENDENCY_ERROR: Node module missing' }
    if ($errors -match 'Cannot find module') { 'DEPENDENCY_ERROR: Module resolution failed' }
    if ($errors -match 'LoadLibrary|specified module could not be found|DLL') { 'NATIVE_ERROR: Windows runtime DLL load failure' }
    if ($errors.Length -gt 0) { 'NATIVE_STDERR_PRESENT=True (contents withheld)' }
} finally {
    if ($process.Id -and !$process.HasExited) { $process.Kill() }
    $process.Dispose()
    $secret = $null
}
