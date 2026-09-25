[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Executable,

    [Parameter(Mandatory = $true)]
    [ValidateSet('static_reference', 'continuous_walk', 'snap_turn')]
    [string] $Condition,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string] $RunId,

    [string] $OutputDir,
    [string] $PersistentDataPath = (Join-Path $env:USERPROFILE 'AppData\LocalLow\UNIVALI\SplatVRLab'),
    [ValidateRange(40, 3600)]
    [int] $CaptureSeconds = 75,
    [ValidateRange(0, 20)]
    [int] $DesktopScreenshotCount = 0,
    [ValidateRange(1, 300)]
    [int] $DesktopScreenshotIntervalSeconds = 5,
    [ValidateRange(0, 300)]
    [int] $DesktopScreenshotInitialDelaySeconds = 30,
    [string] $AdbExe,
    [string] $AdbSerial,
    [ValidateRange(0, 20)]
    [int] $HeadsetScreenshotCount = 0,
    [ValidateRange(1, 300)]
    [int] $HeadsetScreenshotIntervalSeconds = 5,
    [ValidateRange(0, 300)]
    [int] $HeadsetScreenshotInitialDelaySeconds = 30,
    [ValidateRange(1, 60)]
    [int] $ProcessSampleIntervalSeconds = 5,
    [switch] $RequireHeadsetScreenshot,
    [switch] $RequireAutomatedSequence,
    [string] $ExpectedVariant,
    [string] $ExpectedRepresentation,
    [switch] $RequireVisualCapture,
    [switch] $RequireTrackedPoseMarker,
    [ValidateSet('', 'YawOnly', 'FullPoseOnce')]
    [string] $ExpectedAlignmentMode = '',
    [switch] $LeavePlayerRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-UtcTimestamp {
    return [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
}

function Get-FileNames([string] $Directory) {
    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        return @()
    }

    return @(
        Get-ChildItem -LiteralPath $Directory -File |
            ForEach-Object { $_.Name } |
            Sort-Object
    )
}

function Get-NewFileNames([string[]] $Before, [string[]] $After) {
    return @($After | Where-Object { $_ -notin $Before })
}

function Get-ExtendedLengthPath([string] $Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath.Length -ge 248 -and $fullPath -match '^[A-Za-z]:\\') {
        return '\\?\' + $fullPath
    }
    return $fullPath
}

function Copy-SelectedFiles([string] $SourceDirectory, [string[]] $Names, [string] $DestinationDirectory) {
    if ($Names.Count -eq 0) {
        return
    }

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    foreach ($Name in $Names) {
        $source = Get-ExtendedLengthPath (Join-Path $SourceDirectory $Name)
        $destination = Get-ExtendedLengthPath (Join-Path $DestinationDirectory $Name)
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

function Save-DesktopScreenshot([string] $Path) {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $image = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($image)
    try {
        $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $image.Size)
        $image.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $image.Dispose()
    }
}

function Save-AdbBinaryFile([string[]] $CommandArguments, [string] $Destination) {
    $allArguments = @($adbPrefix) + $CommandArguments
    $quotedArguments = @($allArguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    })
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $AdbExe
    $startInfo.Arguments = $quotedArguments -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $adbProcess = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $stream = [IO.File]::Open((Get-ExtendedLengthPath $Destination), [IO.FileMode]::Create)
        try {
            $adbProcess.StandardOutput.BaseStream.CopyTo($stream)
        }
        finally {
            $stream.Dispose()
        }
        $errorText = $adbProcess.StandardError.ReadToEnd()
        $adbProcess.WaitForExit()
        if ($adbProcess.ExitCode -ne 0) {
            throw "ADB falhou ($($adbProcess.ExitCode)): $errorText"
        }
    }
    finally {
        $adbProcess.Dispose()
    }
}

function Test-PngFile([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }
    $stream = [IO.File]::OpenRead((Get-ExtendedLengthPath $Path))
    try {
        if ($stream.Length -lt 8) { return $false }
        $signature = New-Object byte[] 8
        [void]$stream.Read($signature, 0, 8)
        return ([BitConverter]::ToString($signature) -eq '89-50-4E-47-0D-0A-1A-0A')
    }
    finally {
        $stream.Dispose()
    }
}

function Get-PlayerSample([System.Diagnostics.Process] $Player, [double] $PreviousCpuSeconds,
                          [DateTime] $PreviousAtUtc, [int] $LogicalProcessors) {
    $Player.Refresh()
    if ($Player.HasExited) { return $null }
    $sampledAtUtc = [DateTime]::UtcNow
    $cpuSeconds = $Player.TotalProcessorTime.TotalSeconds
    $wallSeconds = ($sampledAtUtc - $PreviousAtUtc).TotalSeconds
    $cpuPercent = if ($wallSeconds -ge 1.0) {
        100.0 * ($cpuSeconds - $PreviousCpuSeconds) / ($wallSeconds * $LogicalProcessors)
    } else { $null }
    return [ordered]@{
        sampled_at_utc = $sampledAtUtc.ToString('O')
        pid = $Player.Id
        cpu_total_seconds = $cpuSeconds
        cpu_utilization_percent_of_host = $cpuPercent
        working_set_bytes = $Player.WorkingSet64
        private_memory_bytes = $Player.PrivateMemorySize64
    }
}

$gpuSampleJobScript = {
    param([int] $PlayerPid)
    $gpuEngines = @()
    $gpuDedicatedBytes = $null
    $gpuCounterError = $null
    try {
        $counters = Get-Counter -Counter @(
            '\GPU Engine(*)\Utilization Percentage',
            '\GPU Process Memory(*)\Dedicated Usage'
        ) -ErrorAction Stop
        $pidPattern = '^pid_' + $PlayerPid + '_'
        $gpuEngines = @($counters.CounterSamples | Where-Object {
            $_.InstanceName -match $pidPattern -and $_.Path -like '*\GPU Engine(*)*'
        } | ForEach-Object {
            [pscustomobject]@{ instance = $_.InstanceName; utilization_percent = $_.CookedValue }
        })
        $gpuMemory = @($counters.CounterSamples | Where-Object {
            $_.InstanceName -match $pidPattern -and $_.Path -like '*\GPU Process Memory(*)*'
        })
        if ($gpuMemory.Count -gt 0) {
            $gpuDedicatedBytes = [long](($gpuMemory | Measure-Object -Property CookedValue -Sum).Sum)
        }
    }
    catch {
        $gpuCounterError = $_.Exception.Message
    }
    [pscustomobject]@{
        sampled_at_utc = [DateTime]::UtcNow.ToString('O')
        pid = $PlayerPid
        gpu_dedicated_usage_bytes = $gpuDedicatedBytes
        gpu_engines = $gpuEngines
        gpu_counter_error = $gpuCounterError
    }
}

function Get-JsonRecords([string] $Directory, [string[]] $Names) {
    $records = @()
    foreach ($Name in $Names | Where-Object { $_.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase) }) {
        $path = Join-Path $Directory $Name
        try {
            $records += [PSCustomObject]@{
                name = $Name
                path = $path
                json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            }
        }
        catch {
            Write-Warning "JSON não pôde ser interpretado: $path. $($_.Exception.Message)"
        }
    }
    return @($records)
}

function Stop-PlayerProcess([System.Diagnostics.Process] $Player, [string] $EvidenceDirectory) {
    if ($LeavePlayerRunning -or -not $Player) {
        return
    }

    try {
        $Player.Refresh()
        if ($Player.HasExited) {
            return
        }

        if ($Player.CloseMainWindow() -and $Player.WaitForExit(10000)) {
            return
        }

        $Player.Refresh()
        if ($Player.HasExited) {
            return
        }

        Stop-Process -Id $Player.Id -Force -ErrorAction Stop
        $Player.WaitForExit(5000) | Out-Null
        'O player não respondeu ao fechamento normal e foi encerrado para liberar a sessão OpenXR.' |
            Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'player_shutdown_status.txt')
    }
    catch {
        $message = "Não foi possível confirmar o encerramento do player: $($_.Exception.Message)"
        $message | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'player_shutdown_status.txt')
        Write-Warning $message
    }
}

if ($ExpectedAlignmentMode -and -not $RequireTrackedPoseMarker) {
    throw '-ExpectedAlignmentMode exige -RequireTrackedPoseMarker.'
}
if ($DesktopScreenshotCount -gt 0 -and $DesktopScreenshotInitialDelaySeconds -ge $CaptureSeconds) {
    throw '-DesktopScreenshotInitialDelaySeconds deve ser menor que -CaptureSeconds quando houver capturas.'
}
if ($HeadsetScreenshotCount -gt 0 -and
    $HeadsetScreenshotInitialDelaySeconds + (($HeadsetScreenshotCount - 1) * $HeadsetScreenshotIntervalSeconds) -ge $CaptureSeconds) {
    throw 'Todas as capturas do headset devem caber dentro de -CaptureSeconds.'
}
if ($DesktopScreenshotCount -gt 0 -and
    $DesktopScreenshotInitialDelaySeconds + (($DesktopScreenshotCount - 1) * $DesktopScreenshotIntervalSeconds) -ge $CaptureSeconds) {
    throw 'Todas as capturas desktop devem caber dentro de -CaptureSeconds.'
}
if (($RequireHeadsetScreenshot -or $HeadsetScreenshotCount -gt 0) -and -not $AdbExe) {
    throw 'As evidências do headset exigem -AdbExe com o caminho do adb.exe.'
}

$Executable = [IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw "Executável não encontrado: $Executable"
}

$scriptDirectory = Split-Path -Parent $PSCommandPath
$unityProjectDirectory = Split-Path -Parent $scriptDirectory
$repositoryDirectory = Split-Path -Parent $unityProjectDirectory
if (-not $OutputDir) {
    $OutputDir = Join-Path $repositoryDirectory "experiments\\unitysplats_viability_v01\\evidence\\$RunId"
}
$OutputDir = [IO.Path]::GetFullPath($OutputDir)
if (Test-Path -LiteralPath $OutputDir) {
    throw "O diretório de evidência já existe e não será sobrescrito: $OutputDir"
}

$measurementDirectory = Join-Path $PersistentDataPath 'measurements'
$visualDirectory = Join-Path $PersistentDataPath 'visual_evaluation'
$beforeMeasurements = Get-FileNames $measurementDirectory
$beforeVisuals = Get-FileNames $visualDirectory
$adbPrefix = @()
if ($AdbExe) {
    $AdbExe = [IO.Path]::GetFullPath($AdbExe)
    if (-not (Test-Path -LiteralPath $AdbExe -PathType Leaf)) {
        throw "ADB não encontrado: $AdbExe"
    }
    if ($AdbSerial) { $adbPrefix = @('-s', $AdbSerial) }
    $adbState = & $AdbExe @adbPrefix get-state 2>&1
    if ($LASTEXITCODE -ne 0 -or ($adbState | Out-String).Trim() -ne 'device') {
        throw "Quest indisponível no ADB: $adbState"
    }
}

New-Item -ItemType Directory -Path $OutputDir | Out-Null
$playerLog = Join-Path $OutputDir 'player.log'
$startedAtUtc = Get-UtcTimestamp
$executableInfo = Get-Item -LiteralPath $Executable
$executableHash = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash.ToLowerInvariant()

@($beforeMeasurements) | Set-Content -LiteralPath (Join-Path $OutputDir 'app_measurements_before.txt')
@($beforeVisuals) | Set-Content -LiteralPath (Join-Path $OutputDir 'visual_evaluation_before.txt')
if ($AdbExe) {
    & $AdbExe @adbPrefix devices -l | Set-Content -LiteralPath (Join-Path $OutputDir 'adb_devices.txt')
    & $AdbExe @adbPrefix logcat -c
    if ($LASTEXITCODE -ne 0) {
        'Não foi possível zerar o buffer de logcat antes da execução.' |
            Set-Content -LiteralPath (Join-Path $OutputDir 'logcat_reset_status.txt')
    }
}

try {
    Get-CimInstance Win32_OperatingSystem |
        Select-Object Caption, Version, BuildNumber, OSArchitecture, LastBootUpTime |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDir 'windows_operating_system.json')
    Get-CimInstance Win32_Processor |
        Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDir 'windows_processor.json')
    Get-CimInstance Win32_VideoController |
        Select-Object Name, DriverVersion, AdapterRAM, VideoProcessor |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDir 'windows_graphics.json')
}
catch {
    "Inventário Windows indisponível: $($_.Exception.Message)" |
        Set-Content -LiteralPath (Join-Path $OutputDir 'windows_inventory_error.txt')
}

$nvidiaSmi = Get-Command nvidia-smi.exe -ErrorAction SilentlyContinue
if ($nvidiaSmi) {
    & $nvidiaSmi.Source -q 2>&1 | Set-Content -LiteralPath (Join-Path $OutputDir 'nvidia-smi.txt')
}
else {
    'nvidia-smi.exe não foi localizado no PATH durante a coleta.' |
        Set-Content -LiteralPath (Join-Path $OutputDir 'nvidia-smi_status.txt')
}

$buildRecord = Join-Path (Split-Path -Parent $Executable) 'build_record.json'
if (Test-Path -LiteralPath $buildRecord -PathType Leaf) {
    Copy-Item -LiteralPath $buildRecord -Destination (Join-Path $OutputDir 'build_record.json')
}
else {
    'build_record.json não foi encontrado ao lado do executável.' |
        Set-Content -LiteralPath (Join-Path $OutputDir 'build_record_status.txt')
}

Write-Host "Iniciando player Windows: $Executable"
Write-Host "Permaneça parado na pose inicial, sem tocar nos controles, até o término da captura."
$quotedPlayerLog = '"{0}"' -f $playerLog
$process = Start-Process -FilePath $Executable -ArgumentList @('-logFile', $quotedPlayerLog) `
    -WorkingDirectory (Split-Path -Parent $Executable) -PassThru
$processSamples = New-Object System.Collections.Generic.List[object]
$gpuSamples = New-Object System.Collections.Generic.List[object]
$gpuSampleJob = $null
$headsetCaptureErrors = New-Object System.Collections.Generic.List[string]
$logicalProcessors = [Environment]::ProcessorCount
$previousCpuSeconds = $process.TotalProcessorTime.TotalSeconds
$previousAtUtc = [DateTime]::UtcNow

try {
$desktopCaptureDirectory = Join-Path $OutputDir 'desktop_screenshot_sequence'
$headsetCaptureDirectory = Join-Path $OutputDir 'screenshot_sequence'
if ($DesktopScreenshotCount -gt 0) {
    New-Item -ItemType Directory -Path $desktopCaptureDirectory | Out-Null
}
if ($HeadsetScreenshotCount -gt 0) {
    New-Item -ItemType Directory -Path $headsetCaptureDirectory | Out-Null
}
$desktopIndex = 1
$headsetIndex = 1
$nextProcessSampleAt = 0.0
$clock = [System.Diagnostics.Stopwatch]::StartNew()
$gpuSampleJob = Start-Job -ScriptBlock $gpuSampleJobScript -ArgumentList $process.Id
while ($clock.Elapsed.TotalSeconds -lt $CaptureSeconds) {
    if ($gpuSampleJob -and $gpuSampleJob.State -in @('Completed', 'Failed', 'Stopped')) {
        try {
            foreach ($gpuResult in @(Receive-Job -Job $gpuSampleJob -ErrorAction Stop)) {
                $gpuSamples.Add($gpuResult)
            }
        }
        catch {
            Write-Warning "Amostra GPU indisponível: $($_.Exception.Message)"
        }
        Remove-Job -Job $gpuSampleJob -Force
        $gpuSampleJob = $null
        if ($clock.Elapsed.TotalSeconds -lt ($CaptureSeconds - 10)) {
            $gpuSampleJob = Start-Job -ScriptBlock $gpuSampleJobScript -ArgumentList $process.Id
        }
    }
    if ($clock.Elapsed.TotalSeconds -ge $nextProcessSampleAt) {
        try {
            $sample = Get-PlayerSample $process $previousCpuSeconds $previousAtUtc $logicalProcessors
            if ($sample) {
                $processSamples.Add($sample)
                $previousCpuSeconds = $sample.cpu_total_seconds
                $previousAtUtc = [DateTime]::Parse(
                    $sample.sampled_at_utc,
                    [Globalization.CultureInfo]::InvariantCulture,
                    [Globalization.DateTimeStyles]::RoundtripKind)
            }
        }
        catch {
            Write-Warning "Amostra do processo indisponível: $($_.Exception.Message)"
        }
        $nextProcessSampleAt += $ProcessSampleIntervalSeconds
        while ($nextProcessSampleAt -le $clock.Elapsed.TotalSeconds) {
            $nextProcessSampleAt += $ProcessSampleIntervalSeconds
        }
    }
    if ($desktopIndex -le $DesktopScreenshotCount -and
        $clock.Elapsed.TotalSeconds -ge ($DesktopScreenshotInitialDelaySeconds +
            (($desktopIndex - 1) * $DesktopScreenshotIntervalSeconds))) {
        $frame = 'frame_{0:D2}' -f $desktopIndex
        Save-DesktopScreenshot (Join-Path $desktopCaptureDirectory "$frame.png")
        Get-UtcTimestamp | Set-Content -LiteralPath (Join-Path $desktopCaptureDirectory "$frame.utc.txt")
        $desktopIndex++
    }
    if ($headsetIndex -le $HeadsetScreenshotCount -and
        $clock.Elapsed.TotalSeconds -ge ($HeadsetScreenshotInitialDelaySeconds +
            (($headsetIndex - 1) * $HeadsetScreenshotIntervalSeconds))) {
        $frame = 'frame_{0:D2}' -f $headsetIndex
        try {
            Save-AdbBinaryFile @('exec-out', 'screencap', '-p') `
                (Join-Path $headsetCaptureDirectory "$frame.png")
            if (-not (Test-PngFile (Join-Path $headsetCaptureDirectory "$frame.png"))) {
                throw 'O ADB não retornou um PNG válido.'
            }
            Get-UtcTimestamp | Set-Content -LiteralPath (Join-Path $headsetCaptureDirectory "$frame.utc.txt")
        }
        catch {
            $headsetCaptureErrors.Add("$frame`: $($_.Exception.Message)")
        }
        $headsetIndex++
    }
    Start-Sleep -Milliseconds 200
}
$clock.Stop()
if ($gpuSampleJob -and $gpuSampleJob.State -in @('Completed', 'Failed', 'Stopped')) {
    try {
        foreach ($gpuResult in @(Receive-Job -Job $gpuSampleJob -ErrorAction Stop)) {
            $gpuSamples.Add($gpuResult)
        }
    }
    catch {
        Write-Warning "Amostra GPU final indisponível: $($_.Exception.Message)"
    }
}
$processMetricsRecord = [ordered]@{
    schema_version = '1.1'
    source = 'Windows host process counters for the Unity player PID only'
    process_id = $process.Id
    logical_processor_count = $logicalProcessors
    cpu_utilization_denominator = 'All logical processors on the Windows host'
    gpu_source = 'Windows GPU Engine and GPU Process Memory counters filtered by player PID'
    gpu_limit = 'GPU engine percentages are per engine; do not sum them into a whole-GPU percentage.'
    sample_interval_seconds_requested = $ProcessSampleIntervalSeconds
    samples = @($processSamples.ToArray())
    gpu_samples = @($gpuSamples.ToArray())
}
$processMetricsRecord | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath (Join-Path $OutputDir 'process_metrics.json')

if ($AdbExe) {
    try {
        Save-AdbBinaryFile @('exec-out', 'screencap', '-p') (Join-Path $OutputDir 'screenshot.png')
        if (-not (Test-PngFile (Join-Path $OutputDir 'screenshot.png'))) {
            throw 'O ADB não retornou um PNG válido.'
        }
        'Screenshot do framebuffer do Quest copiado via ADB.' |
            Set-Content -LiteralPath (Join-Path $OutputDir 'headset_screenshot_status.txt')
    }
    catch {
        "Screenshot do Quest indisponível: $($_.Exception.Message)" |
            Set-Content -LiteralPath (Join-Path $OutputDir 'headset_screenshot_status.txt')
    }
    try {
        & $AdbExe @adbPrefix logcat -d -v threadtime |
            Set-Content -LiteralPath (Join-Path $OutputDir 'logcat.txt') -Encoding UTF8
        if ($LASTEXITCODE -ne 0) { throw "ADB logcat retornou $LASTEXITCODE." }
    }
    catch {
        "Logcat indisponível: $($_.Exception.Message)" |
            Set-Content -LiteralPath (Join-Path $OutputDir 'logcat_status.txt')
    }
}

$afterMeasurements = Get-FileNames $measurementDirectory
$afterVisuals = Get-FileNames $visualDirectory
$newMeasurements = Get-NewFileNames $beforeMeasurements $afterMeasurements
$newVisuals = Get-NewFileNames $beforeVisuals $afterVisuals

@($afterMeasurements) | Set-Content -LiteralPath (Join-Path $OutputDir 'app_measurements_after.txt')
@($newMeasurements) | Set-Content -LiteralPath (Join-Path $OutputDir 'app_measurements_new.txt')
@($afterVisuals) | Set-Content -LiteralPath (Join-Path $OutputDir 'visual_evaluation_after.txt')
@($newVisuals) | Set-Content -LiteralPath (Join-Path $OutputDir 'visual_evaluation_new.txt')
Copy-SelectedFiles $measurementDirectory $newMeasurements (Join-Path $OutputDir 'app_measurements')
Copy-SelectedFiles $visualDirectory $newVisuals (Join-Path $OutputDir 'visual_evaluation')

$applicationRecords = Get-JsonRecords $measurementDirectory $newMeasurements
$measurementRecords = @($applicationRecords | Where-Object { $_.json.completionReason })
$validRun = $true
$validationMessages = New-Object System.Collections.Generic.List[string]
if ($processSamples.Count -eq 0) {
    $validRun = $false
    $validationMessages.Add('Nenhuma amostra de CPU e memória do processo Unity foi coletada.')
}

if ($measurementRecords.Count -ne 1) {
    $validRun = $false
    $validationMessages.Add("Esperado exatamente um JSON de métricas novo; encontrados: $($measurementRecords.Count).")
}
else {
    $report = $measurementRecords[0].json
    if ($report.completionReason -ne 'measurement_window_completed') {
        $validRun = $false
        $validationMessages.Add("A janela interna não foi concluída: $($report.completionReason).")
    }
    if ($report.conditionId -ne $Condition) {
        $validRun = $false
        $validationMessages.Add("conditionId divergente: esperado $Condition; obtido $($report.conditionId).")
    }
    if ($ExpectedVariant -and $report.variantId -ne $ExpectedVariant) {
        $validRun = $false
        $validationMessages.Add("variantId divergente: esperado $ExpectedVariant; obtido $($report.variantId).")
    }
    if ($ExpectedRepresentation -and $report.representationVariantId -ne $ExpectedRepresentation) {
        $validRun = $false
        $validationMessages.Add("representationVariantId divergente: esperado $ExpectedRepresentation; obtido $($report.representationVariantId).")
    }
    if ($RequireAutomatedSequence -and -not $report.automatedSequenceStarted) {
        $validRun = $false
        $validationMessages.Add('A sequência automatizada obrigatória não iniciou.')
    }
}

$visualRecords = Get-JsonRecords $visualDirectory $newVisuals
if ($RequireVisualCapture) {
    $referenceCaptures = @($visualRecords | Where-Object {
        $_.json.captureKind -eq 'monoscopic_reference_pose_diagnostic' -and
        (Test-Path -LiteralPath (Join-Path $visualDirectory $_.json.imageFile) -PathType Leaf)
    })
    if ($referenceCaptures.Count -lt 1) {
        $validRun = $false
        $validationMessages.Add('A captura visual monoscópica de referência não produziu PNG e JSON novos.')
    }
}

if ($RequireTrackedPoseMarker) {
    $trackedMarkers = @($visualRecords | Where-Object {
        $_.json.captureKind -eq 'tracked_xr_stereo_screencap_marker'
    })
    if ($trackedMarkers.Count -ne 1) {
        $validRun = $false
        $validationMessages.Add("Esperado exatamente um marcador de pose rastreada novo; encontrados: $($trackedMarkers.Count).")
    }
    else {
        $marker = $trackedMarkers[0].json
        if (-not $marker.alignmentCompleted) {
            $validRun = $false
            $validationMessages.Add('O marcador informa alinhamento XR não concluído.')
        }
        if ($ExpectedAlignmentMode -and $marker.alignmentMode -ne $ExpectedAlignmentMode) {
            $validRun = $false
            $validationMessages.Add("alignmentMode divergente: esperado $ExpectedAlignmentMode; obtido $($marker.alignmentMode).")
        }
    }
}

if ($AdbExe) {
    if (-not (Test-PngFile (Join-Path $OutputDir 'screenshot.png'))) {
        if ($RequireHeadsetScreenshot) {
            $validRun = $false
            $validationMessages.Add('O ADB não produziu screenshot.png válido do framebuffer do Quest.')
        }
    }
    if ($HeadsetScreenshotCount -gt 0) {
        $validFrames = @(Get-ChildItem -LiteralPath $headsetCaptureDirectory -Filter 'frame_*.png' -File |
            Where-Object { Test-PngFile $_.FullName })
        if ($validFrames.Count -ne $HeadsetScreenshotCount -or $headsetCaptureErrors.Count -gt 0) {
            $validRun = $false
            $validationMessages.Add("Sequência ADB incompleta: esperadas $HeadsetScreenshotCount capturas; válidas $($validFrames.Count).")
        }
    }
    $logcatPath = Join-Path $OutputDir 'logcat.txt'
    if (-not (Test-Path -LiteralPath $logcatPath -PathType Leaf) -or
        (Get-Item -LiteralPath $logcatPath -ErrorAction SilentlyContinue).Length -eq 0) {
        $validRun = $false
        $validationMessages.Add('O ADB não produziu logcat.txt com dados da sessão.')
    }
}

Stop-PlayerProcess $process $OutputDir

if (-not (Test-Path -LiteralPath $playerLog -PathType Leaf)) {
    'player.log não foi produzido pelo executável.' |
        Set-Content -LiteralPath (Join-Path $OutputDir 'player_log_status.txt')
    $validRun = $false
    $validationMessages.Add('player.log não foi produzido pelo executável.')
}

$finishedAtUtc = Get-UtcTimestamp
$runMetadata = [ordered]@{
    schema_version = '1.1'
    run_id = $RunId
    execution_scope = 'desktop_streaming_diagnostic'
    condition_id = $Condition
    expected_variant_id = $ExpectedVariant
    expected_representation_variant_id = $ExpectedRepresentation
    expected_alignment_mode = $ExpectedAlignmentMode
    require_visual_capture = [bool]$RequireVisualCapture
    require_tracked_pose_marker = [bool]$RequireTrackedPoseMarker
    require_headset_screenshot = [bool]$RequireHeadsetScreenshot
    executable_path = $Executable
    executable_sha256 = $executableHash
    executable_size_bytes = $executableInfo.Length
    persistent_data_path = $PersistentDataPath
    host_capture_started_at_utc = $startedAtUtc
    host_capture_finished_at_utc = $finishedAtUtc
    external_capture_seconds = $CaptureSeconds
    desktop_screenshot_sequence_count = $DesktopScreenshotCount
    desktop_screenshot_sequence_interval_seconds = $DesktopScreenshotIntervalSeconds
    desktop_screenshot_sequence_initial_delay_seconds = $DesktopScreenshotInitialDelaySeconds
    adb_executable = $AdbExe
    adb_serial = $AdbSerial
    screenshot_sequence_count = $HeadsetScreenshotCount
    screenshot_sequence_interval_seconds = $HeadsetScreenshotIntervalSeconds
    screenshot_sequence_initial_delay_seconds = $HeadsetScreenshotInitialDelaySeconds
    process_metrics_file = 'process_metrics.json'
    application_measurement_index = 'app_measurements_new.txt'
    visual_evaluation_index = 'visual_evaluation_new.txt'
    validation_status_file = 'validation_status.txt'
    notes = 'app_measurements e process_metrics medem o player Unity no PC Windows. screenshot.png, screenshot_sequence e logcat vêm do Quest via ADB durante o Horizon Link. OVR Metrics Tool foi excluído do protocolo Windows via Link por não produzir CSV nas tentativas observadas; as evidências anteriores permanecem preservadas. Capturas ADB do framebuffer não são métricas de fidelidade. O coletor não mede latência, codec ou bitrate do Link. Não agregar esta série à execução Android nativa.'
}
$runMetadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDir 'run_metadata.json')

if ($validationMessages.Count -eq 0) {
    'Execução válida segundo as verificações solicitadas.' |
        Set-Content -LiteralPath (Join-Path $OutputDir 'validation_status.txt')
}
else {
    $validationMessages | Set-Content -LiteralPath (Join-Path $OutputDir 'validation_status.txt')
}

Write-Host "Evidências preservadas em: $OutputDir"
if (-not $validRun) {
    Write-Error 'A execução preservou evidências, mas não passou nas verificações. Consulte validation_status.txt.'
}
}
finally {
    if ($gpuSampleJob) {
        Remove-Job -Job $gpuSampleJob -Force -ErrorAction SilentlyContinue
    }
    if ($processSamples.Count -gt 0 -and
        -not (Test-Path -LiteralPath (Join-Path $OutputDir 'process_metrics.json') -PathType Leaf)) {
        try {
            [ordered]@{
                schema_version = '1.0'
                source = 'Partial Windows Unity player process samples; collection aborted'
                process_id = $process.Id
                samples = @($processSamples.ToArray())
                gpu_samples = @($gpuSamples.ToArray())
            } | ConvertTo-Json -Depth 8 |
                Set-Content -LiteralPath (Join-Path $OutputDir 'process_metrics.json')
        }
        catch {
            Write-Warning "Não foi possível preservar amostras parciais do processo: $($_.Exception.Message)"
        }
    }
    Stop-PlayerProcess $process $OutputDir
}
