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

function Copy-SelectedFiles([string] $SourceDirectory, [string[]] $Names, [string] $DestinationDirectory) {
    if ($Names.Count -eq 0) {
        return
    }

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    foreach ($Name in $Names) {
        Copy-Item -LiteralPath (Join-Path $SourceDirectory $Name) -Destination $DestinationDirectory -Force
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

if ($ExpectedAlignmentMode -and -not $RequireTrackedPoseMarker) {
    throw '-ExpectedAlignmentMode exige -RequireTrackedPoseMarker.'
}
if ($DesktopScreenshotCount -gt 0 -and $DesktopScreenshotInitialDelaySeconds -ge $CaptureSeconds) {
    throw '-DesktopScreenshotInitialDelaySeconds deve ser menor que -CaptureSeconds quando houver capturas.'
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

New-Item -ItemType Directory -Path $OutputDir | Out-Null
$playerLog = Join-Path $OutputDir 'player.log'
$startedAtUtc = Get-UtcTimestamp
$executableInfo = Get-Item -LiteralPath $Executable
$executableHash = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash.ToLowerInvariant()

@($beforeMeasurements) | Set-Content -LiteralPath (Join-Path $OutputDir 'app_measurements_before.txt')
@($beforeVisuals) | Set-Content -LiteralPath (Join-Path $OutputDir 'visual_evaluation_before.txt')

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

if ($DesktopScreenshotCount -gt 0) {
    $desktopCaptureDirectory = Join-Path $OutputDir 'desktop_screenshot_sequence'
    New-Item -ItemType Directory -Path $desktopCaptureDirectory | Out-Null
    Start-Sleep -Seconds $DesktopScreenshotInitialDelaySeconds
    for ($index = 1; $index -le $DesktopScreenshotCount; $index++) {
        $screenshotPath = Join-Path $desktopCaptureDirectory ("frame_{0:D2}.png" -f $index)
        Save-DesktopScreenshot $screenshotPath
        Get-UtcTimestamp | Set-Content -LiteralPath (Join-Path $desktopCaptureDirectory ("frame_{0:D2}.utc.txt" -f $index))
        if ($index -lt $DesktopScreenshotCount) {
            Start-Sleep -Seconds $DesktopScreenshotIntervalSeconds
        }
    }

    $elapsedScreenshotTime = $DesktopScreenshotInitialDelaySeconds + (($DesktopScreenshotCount - 1) * $DesktopScreenshotIntervalSeconds)
    if ($elapsedScreenshotTime -lt $CaptureSeconds) {
        Start-Sleep -Seconds ($CaptureSeconds - $elapsedScreenshotTime)
    }
}
else {
    Start-Sleep -Seconds $CaptureSeconds
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

if (-not $LeavePlayerRunning -and -not $process.HasExited) {
    if (-not $process.CloseMainWindow()) {
        'O player permaneceu aberto após a coleta; feche-o manualmente quando terminar a inspeção.' |
            Set-Content -LiteralPath (Join-Path $OutputDir 'player_shutdown_status.txt')
    }
    else {
        $process.WaitForExit(10000) | Out-Null
    }
}

if (-not (Test-Path -LiteralPath $playerLog -PathType Leaf)) {
    'player.log não foi produzido pelo executável.' |
        Set-Content -LiteralPath (Join-Path $OutputDir 'player_log_status.txt')
}

$finishedAtUtc = Get-UtcTimestamp
$runMetadata = [ordered]@{
    schema_version = '1.0'
    run_id = $RunId
    execution_scope = 'desktop_streaming_diagnostic'
    condition_id = $Condition
    expected_variant_id = $ExpectedVariant
    expected_representation_variant_id = $ExpectedRepresentation
    expected_alignment_mode = $ExpectedAlignmentMode
    require_visual_capture = [bool]$RequireVisualCapture
    require_tracked_pose_marker = [bool]$RequireTrackedPoseMarker
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
    application_measurement_index = 'app_measurements_new.txt'
    visual_evaluation_index = 'visual_evaluation_new.txt'
    validation_status_file = 'validation_status.txt'
    notes = 'Métricas de frame, CPU/GPU e XR são reportadas pela aplicação Unity no desktop Windows. Capturas desktop, se habilitadas, registram o monitor do host e não constituem captura direta de cada olho nem métrica de fidelidade offline. A coleta não produz métricas OVR/Android, nem latência, codec, bitrate ou métricas de composição do Meta Horizon Link.'
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
