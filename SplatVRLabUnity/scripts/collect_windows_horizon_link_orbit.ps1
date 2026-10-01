[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Executable,
    [Parameter(Mandatory = $true)] [string] $AdbExe,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_-]+$')] [string] $RunId,
    [ValidateSet('baseline', 'pruned100k', 'pruned50k', 'splatfacto_big')]
    [string] $Variant = 'baseline',
    [string] $AdbSerial,
    [string] $OutputDir,
    [string] $PersistentDataPath = (Join-Path $env:USERPROFILE 'AppData\LocalLow\UNIVALI\SplatVRLab'),
    [switch] $LeavePlayerRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$variants = @{
    baseline = @{
        variant = 'desktop_horizon_link_baseline_orbit_full_circle_v01'
        representation = 'baseline_v01'
    }
    pruned100k = @{
        variant = 'desktop_horizon_link_opacity_topk_100k_orbit_full_circle_v01'
        representation = 'opacity_topk_100k_v01'
    }
    pruned50k = @{
        variant = 'desktop_horizon_link_opacity_topk_50k_orbit_full_circle_v01'
        representation = 'opacity_topk_50k_v01'
    }
    splatfacto_big = @{
        variant = 'desktop_horizon_link_splatfacto_big_orbit_full_circle_v01'
        representation = 'splatfacto_big_v01'
    }
}
$expectedVariant = $variants[$Variant].variant
$expectedRepresentation = $variants[$Variant].representation
$trajectoryId = 'chair_orbit_full_circle_r125_v01'
$frameCount = 144
$adbPrefix = @()
if ($AdbSerial) { $adbPrefix = @('-s', $AdbSerial) }

function Get-Names([string] $Path, [switch] $Directories) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return @() }
    if ($Directories) {
        return @(Get-ChildItem -LiteralPath $Path -Directory | ForEach-Object Name)
    }
    return @(Get-ChildItem -LiteralPath $Path -File | ForEach-Object Name)
}

function Get-ExtendedLengthPath([string] $Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath.Length -ge 248 -and $fullPath -match '^[A-Za-z]:\\') {
        return '\\?\' + $fullPath
    }
    return $fullPath
}

function Save-AdbScreenshot([string] $Destination) {
    $arguments = @($adbPrefix) + @('exec-out', 'screencap', '-p')
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $AdbExe
    $startInfo.Arguments = ($arguments -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $adbProcess = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $stream = [IO.File]::Open((Get-ExtendedLengthPath $Destination), [IO.FileMode]::Create)
        try { $adbProcess.StandardOutput.BaseStream.CopyTo($stream) }
        finally { $stream.Dispose() }
        $errorText = $adbProcess.StandardError.ReadToEnd()
        $adbProcess.WaitForExit()
        if ($adbProcess.ExitCode -ne 0) {
            throw "ADB screencap falhou ($($adbProcess.ExitCode)): $errorText"
        }
    }
    finally { $adbProcess.Dispose() }

    $stream = [IO.File]::OpenRead((Get-ExtendedLengthPath $Destination))
    try {
        if ($stream.Length -lt 10000) { throw "Screenshot muito pequeno: $Destination" }
        $signature = New-Object byte[] 8
        [void] $stream.Read($signature, 0, 8)
        if ([BitConverter]::ToString($signature) -ne '89-50-4E-47-0D-0A-1A-0A') {
            throw "ADB não retornou PNG válido: $Destination"
        }
    }
    finally { $stream.Dispose() }
}

function Wait-File([string] $Path, [int] $TimeoutSeconds,
                   [System.Diagnostics.Process] $Player) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) { return }
        $Player.Refresh()
        if ($Player.HasExited) { throw "Player encerrou antes de criar $Path" }
        Start-Sleep -Milliseconds 250
    }
    throw "Timeout esperando $Path"
}

$Executable = [IO.Path]::GetFullPath($Executable)
$AdbExe = [IO.Path]::GetFullPath($AdbExe)
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw "EXE não encontrado: $Executable"
}
if (-not (Test-Path -LiteralPath $AdbExe -PathType Leaf)) {
    throw "ADB não encontrado: $AdbExe"
}
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $OutputDir) {
    $OutputDir = Join-Path $repo "experiments\unitysplats_viability_v01\evidence\$RunId"
}
$OutputDir = [IO.Path]::GetFullPath($OutputDir)
if (Test-Path -LiteralPath $OutputDir) {
    throw "Saída já existe; não será sobrescrita: $OutputDir"
}
$buildRecord = Join-Path (Split-Path -Parent $Executable) 'build_record.json'
if (-not (Test-Path -LiteralPath $buildRecord -PathType Leaf)) {
    throw "Registro de build ausente: $buildRecord"
}
$build = Get-Content -LiteralPath $buildRecord -Raw | ConvertFrom-Json
$executableHash = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash.ToLowerInvariant()
if ($build.variantId -ne $expectedVariant -or
    $build.executableSha256 -ne $executableHash) {
    throw "EXE ou registro de build não corresponde à órbita $Variant."
}
$adbState = & $AdbExe @adbPrefix get-state 2>&1
if ($LASTEXITCODE -ne 0 -or ($adbState | Out-String).Trim() -ne 'device') {
    throw "Quest indisponível no ADB: $adbState"
}

$visualDir = Join-Path $PersistentDataPath 'visual_evaluation'
$metricsDir = Join-Path $PersistentDataPath 'measurements'
$beforeRuns = @(Get-Names $visualDir -Directories)
$beforeMetrics = @(Get-Names $metricsDir)
New-Item -ItemType Directory -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Path (Join-Path $OutputDir 'poses') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $OutputDir 'screenshots') | Out-Null
$executableHash | Set-Content -LiteralPath (Join-Path $OutputDir 'exe_sha256.txt')
Copy-Item -LiteralPath $buildRecord -Destination (Join-Path $OutputDir 'build_record.json')

$playerLog = Join-Path $OutputDir 'player.log'
$process = $null
$runDirectory = $null
$acknowledged = 0
$processSamples = New-Object System.Collections.Generic.List[object]
try {
    Write-Host 'Confirme que o Quest está conectado pelo Meta Horizon Link e que o Meta Quest Link é o runtime OpenXR ativo.'
    Write-Host 'Mantenha o headset rastreado e imóvel; a cena avançará em 144 passos de 2,5°.'
    $quotedLog = '"{0}"' -f $playerLog
    $process = Start-Process -FilePath $Executable -ArgumentList @('-logFile', $quotedLog) `
        -WorkingDirectory (Split-Path -Parent $Executable) -PassThru

    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    while ([DateTime]::UtcNow -lt $deadline) {
        $newRuns = @(Get-Names $visualDir -Directories | Where-Object {
            $_ -notin $beforeRuns -and $_ -match '^orbit_full_circle_[0-9]{8}T[0-9]{9}Z$'
        })
        if ($newRuns.Count -eq 1) {
            $runDirectory = Join-Path $visualDir $newRuns[0]
            break
        }
        if ($newRuns.Count -gt 1) { throw 'Mais de um diretório orbital novo foi encontrado.' }
        $process.Refresh()
        if ($process.HasExited) { throw 'Player encerrou antes de criar a sequência orbital.' }
        Start-Sleep -Milliseconds 500
    }
    if (-not $runDirectory) { throw 'Player não criou um diretório orbital novo em 120 s.' }
    $runDirectory | Set-Content -LiteralPath (Join-Path $OutputDir 'app_run_path.txt')

    for ($index = 0; $index -lt $frameCount; $index++) {
        $stem = 'pose_{0:D4}' -f $index
        $ready = Join-Path $runDirectory "$stem.ready.json"
        Wait-File $ready 90 $process
        $record = Get-Content -LiteralPath $ready -Raw | ConvertFrom-Json
        if ($record.frameIndex -ne $index -or $record.frameCount -ne $frameCount -or
            $record.trajectoryId -ne $trajectoryId -or
            $record.variantId -ne $expectedVariant -or
            $record.representationVariantId -ne $expectedRepresentation -or
            [Math]::Abs([double]$record.angleDegrees - ($index * 2.5)) -gt 0.01) {
            throw "Marcador $stem não corresponde à trajetória e variante esperadas."
        }
        Copy-Item -LiteralPath (Get-ExtendedLengthPath $ready) `
            -Destination (Get-ExtendedLengthPath (Join-Path $OutputDir "poses\$stem.json"))
        $screenshot = Join-Path $OutputDir "screenshots\$stem.png"
        Save-AdbScreenshot $screenshot
        [DateTime]::UtcNow.ToString('O') |
            Set-Content -LiteralPath (Join-Path $OutputDir "screenshots\$stem.utc.txt")
        $process.Refresh()
        if (-not $process.HasExited) {
            $processSamples.Add([ordered]@{
                frame_index = $index
                sampled_at_utc = [DateTime]::UtcNow.ToString('O')
                cpu_total_seconds = $process.TotalProcessorTime.TotalSeconds
                working_set_bytes = $process.WorkingSet64
                private_memory_bytes = $process.PrivateMemorySize64
            })
        }
        'screenshot_captured' | Set-Content -LiteralPath (Join-Path $runDirectory "$stem.ack")
        $acknowledged++
        Write-Host ('Pose {0:D3}/144: {1:F1}°' -f ($index + 1), [double]$record.angleDegrees)
    }

    $completionPath = Join-Path $runDirectory 'completion.json'
    Wait-File $completionPath 30 $process
    $completion = Get-Content -LiteralPath $completionPath -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath $completionPath -Destination (Join-Path $OutputDir 'completion.json')
    if ($completion.status -ne 'complete' -or $completion.acknowledgedFrames -ne 144 -or
        $completion.expectedFrames -ne 144 -or
        $completion.variantId -ne $expectedVariant -or
        $completion.representationVariantId -ne $expectedRepresentation) {
        throw 'O player não confirmou as 144 poses.'
    }

    # The completion marker can precede the metrics flush by a few seconds.
    $metricName = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ([DateTime]::UtcNow -lt $deadline) {
        $newMetrics = @(Get-Names $metricsDir | Where-Object {
            $_ -notin $beforeMetrics -and
            $_ -like "unitysplats_viability_v01_${expectedVariant}_*.json"
        })
        if ($newMetrics.Count -gt 1) {
            throw "Mais de um relatório novo corresponde à variante $expectedVariant."
        }
        if ($newMetrics.Count -eq 1) {
            try {
                $candidate = Get-Content -LiteralPath (Join-Path $metricsDir $newMetrics[0]) -Raw |
                    ConvertFrom-Json
                if ($candidate.variantId -eq $expectedVariant -and
                    $candidate.representationVariantId -eq $expectedRepresentation -and
                    $candidate.conditionId -eq 'orbit_full_circle_capture' -and
                    $candidate.completionReason -eq 'native_orbit_capture_completed') {
                    $metricName = $newMetrics[0]
                    break
                }
            }
            catch {
                # A report still being written is not a completed report.
            }
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $metricName) {
        throw "Relatório válido da órbita $Variant não apareceu em 60 s."
    }
    Copy-Item -LiteralPath (Get-ExtendedLengthPath (Join-Path $metricsDir $metricName)) `
        -Destination (Get-ExtendedLengthPath (Join-Path $OutputDir $metricName))

    [ordered]@{
        schema_version = '1.0'
        capture_kind = 'horizon_link_headset_framebuffer_orbit'
        trajectory_id = $trajectoryId
        variant_id = $expectedVariant
        representation_variant_id = $expectedRepresentation
        acknowledged_frames = $acknowledged
        expected_frames = $frameCount
        interpretation_limit = 'PC renderizado via Horizon Link; não é execução standalone nem medição binocular direta.'
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDir 'capture_manifest.json')
    Write-Host "144 capturas PCVR preservadas em: $OutputDir"
}
catch {
    $_.Exception.ToString() | Set-Content -LiteralPath (Join-Path $OutputDir 'capture_error.txt')
    throw
}
finally {
    [ordered]@{
        source = 'Windows Unity player process; sampled once per captured pose'
        samples = @($processSamples.ToArray())
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDir 'process_samples.json')
    if ($runDirectory -and (Test-Path -LiteralPath $runDirectory -PathType Container)) {
        Copy-Item -LiteralPath $runDirectory -Destination (Join-Path $OutputDir 'app_orbit') -Recurse
    }
    if ($process -and -not $LeavePlayerRunning) {
        $process.Refresh()
        if (-not $process.HasExited) {
            if (-not ($process.CloseMainWindow() -and $process.WaitForExit(10000))) {
                Stop-Process -Id $process.Id -Force
            }
        }
    }
}
