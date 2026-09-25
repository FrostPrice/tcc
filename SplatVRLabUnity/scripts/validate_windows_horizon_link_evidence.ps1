#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $TccRoot,
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not $TccRoot) {
    $TccRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}
$TccRoot = [IO.Path]::GetFullPath($TccRoot)
$evidenceRoot = Join-Path $TccRoot 'experiments\unitysplats_viability_v01\evidence'
if (-not $OutputPath) {
    $OutputPath = Join-Path $TccRoot 'experiments\unitysplats_viability_v01\derived\desktop_horizon_link_pcvr_validation_v01\validation.json'
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) {
    throw "Relatório derivado já existe e não será sobrescrito: $OutputPath"
}

function Assert-Check([bool] $Condition, [string] $Message,
                      [System.Collections.Generic.List[string]] $Errors) {
    if (-not $Condition) { $Errors.Add($Message) }
}

function Get-ExtendedLengthPath([string] $Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath.Length -ge 248 -and $fullPath -match '^[A-Za-z]:\\') {
        return '\\?\' + $fullPath
    }
    return $fullPath
}

function Test-DecodablePng([string] $Path) {
    $extendedPath = Get-ExtendedLengthPath $Path
    if (-not [IO.File]::Exists($extendedPath)) { return $false }
    try {
        $stream = [IO.File]::OpenRead($extendedPath)
        try {
            $image = [System.Drawing.Image]::FromStream($stream)
            try { return ($image.Width -gt 0 -and $image.Height -gt 0) }
            finally { $image.Dispose() }
        }
        finally { $stream.Dispose() }
    }
    catch { return $false }
}

$specs = @(
    [pscustomobject]@{
        RunId = 'desktop_horizon_link_baseline_fullpose_r05'
        Variant = 'spark_baseline_visual_full_pose_v01'
        Representation = 'baseline_v01'
        BuildVariant = 'desktop_horizon_link_baseline_visual_full_pose_v01'
        Count = 195760
        Ply = 'experiments\baseline_v01\exp_ns_poster_baseline_v01_baseline_v01.ply'
        Sha256 = '23e3b3d3cd47e1aa0ad1daf7df96c4bd620af9edaf7996ccb865f5b60b80bebb'
    },
    [pscustomobject]@{
        RunId = 'desktop_horizon_link_pruned100k_fullpose_r01'
        Variant = 'spark_opacity_topk_100k_visual_full_pose_v01'
        Representation = 'opacity_topk_100k_v01'
        BuildVariant = 'desktop_horizon_link_opacity_topk_100k_visual_full_pose_v01'
        Count = 100000
        Ply = 'experiments\pruning_v01\exp_ns_poster_opacity_topk_100k_v01.ply'
        Sha256 = 'b2af0f8f9bda2ab2cc54db3e34147b6e02ea73cd71eb682ae803739f4f34c1d3'
    },
    [pscustomobject]@{
        RunId = 'desktop_horizon_link_pruned50k_fullpose_r01'
        Variant = 'spark_opacity_topk_50k_visual_full_pose_v01'
        Representation = 'opacity_topk_50k_v01'
        BuildVariant = 'desktop_horizon_link_opacity_topk_50k_visual_full_pose_v01'
        Count = 50000
        Ply = 'experiments\pruning_50k_v01\exp_ns_poster_opacity_topk_50k_v01.ply'
        Sha256 = 'f4a5a1f80cd2d448338c22b2b21a777e2151f0171ad42dfd74046623742b26e5'
    },
    [pscustomobject]@{
        RunId = 'desktop_horizon_link_splatfacto_big_fullpose_r01'
        Variant = 'spark_splatfacto_big_visual_full_pose_v01'
        Representation = 'splatfacto_big_v01'
        BuildVariant = 'desktop_horizon_link_splatfacto_big_visual_full_pose_v01'
        Count = 470962
        Ply = 'experiments\colab\exports\exp_ns_poster_splatfacto_big_v01\splatfacto\splatfacto_big_v01\gaussian_splat\exp_ns_poster_splatfacto_big_v01_splatfacto_big_v01.ply'
        Sha256 = '70716105acdaa18caa3523b52c69cd8d46ab96650bbf4c6ad42a17868a651505'
    }
)

$results = @()
foreach ($spec in $specs) {
    $errors = New-Object System.Collections.Generic.List[string]
    $directory = Join-Path $evidenceRoot $spec.RunId
    Assert-Check (Test-Path -LiteralPath $directory -PathType Container) 'Diretório de evidência ausente.' $errors
    if ($errors.Count -gt 0) {
        $results += [ordered]@{ run_id = $spec.RunId; status = 'invalid'; errors = @($errors.ToArray()) }
        continue
    }

    $metadata = Get-Content -LiteralPath (Join-Path $directory 'run_metadata.json') -Raw | ConvertFrom-Json
    $build = Get-Content -LiteralPath (Join-Path $directory 'build_record.json') -Raw | ConvertFrom-Json
    $process = Get-Content -LiteralPath (Join-Path $directory 'process_metrics.json') -Raw | ConvertFrom-Json
    $appFiles = @(Get-ChildItem -LiteralPath (Join-Path $directory 'app_measurements') -File -Filter '*.json')
    $visualFiles = @(Get-ChildItem -LiteralPath (Join-Path $directory 'visual_evaluation') -File -Filter '*.json')
    Assert-Check ($appFiles.Count -eq 1) 'Esperado exatamente um JSON novo de métricas do aplicativo.' $errors
    Assert-Check ($visualFiles.Count -eq 2) 'Esperados dois JSONs visuais: captura e marcador XR.' $errors
    if ($appFiles.Count -ne 1 -or $visualFiles.Count -ne 2) {
        $results += [ordered]@{ run_id = $spec.RunId; status = 'invalid'; errors = @($errors.ToArray()) }
        continue
    }

    $app = Get-Content -LiteralPath $appFiles[0].FullName -Raw | ConvertFrom-Json
    $visual = @($visualFiles | ForEach-Object { Get-Content -LiteralPath (Get-ExtendedLengthPath $_.FullName) -Raw | ConvertFrom-Json })
    $reference = @($visual | Where-Object captureKind -EQ 'monoscopic_reference_pose_diagnostic')
    $marker = @($visual | Where-Object captureKind -EQ 'tracked_xr_stereo_screencap_marker')
    $plyPath = Join-Path $TccRoot $spec.Ply
    $plyHash = if (Test-Path -LiteralPath $plyPath -PathType Leaf) {
        (Get-FileHash -LiteralPath $plyPath -Algorithm SHA256).Hash.ToLowerInvariant()
    } else { $null }

    Assert-Check ($metadata.run_id -eq $spec.RunId) 'RunId divergente no metadata.' $errors
    Assert-Check ($metadata.expected_variant_id -eq $spec.Variant) 'Variant esperada divergente no metadata.' $errors
    Assert-Check ($metadata.expected_representation_variant_id -eq $spec.Representation) 'Representação esperada divergente no metadata.' $errors
    Assert-Check ($build.variantId -eq $spec.BuildVariant) 'Build record não corresponde à variante.' $errors
    Assert-Check ($build.executableSha256 -eq $metadata.executable_sha256) 'Hash do EXE diverge entre build e execução.' $errors
    Assert-Check ($app.variantId -eq $spec.Variant) 'Variant executada divergente.' $errors
    Assert-Check ($app.representationVariantId -eq $spec.Representation) 'Representação executada divergente.' $errors
    Assert-Check ($app.representationGaussianCount -eq $spec.Count) 'Contagem de splats divergente.' $errors
    Assert-Check ($app.representationPlySha256 -eq $spec.Sha256 -and $plyHash -eq $spec.Sha256) 'Hash PLY divergente do arquivo de origem.' $errors
    Assert-Check ($app.conditionId -eq 'static_reference') 'Condição divergente.' $errors
    Assert-Check ($app.completionReason -eq 'measurement_window_completed') 'Janela de medição não concluída.' $errors
    Assert-Check ([bool]$app.automatedSequenceStarted) 'Sequência automatizada não iniciada.' $errors
    Assert-Check ([bool]$app.referencePoseAlignmentCompleted) 'Pose de referência não alinhada no aplicativo.' $errors
    Assert-Check ($app.referencePoseAlignmentMode -eq 'FullPoseOnce') 'Modo de alinhamento divergente no aplicativo.' $errors
    Assert-Check ($app.applicationFrameInterval.sampleCount -gt 0 -and $app.unityGpuFrameTime.sampleCount -gt 0) 'Amostras de frame/GPU Unity ausentes.' $errors
    Assert-Check ($reference.Count -eq 1 -and $marker.Count -eq 1) 'Captura visual ou marcador XR ausente.' $errors
    if ($reference.Count -eq 1) {
        Assert-Check (Test-DecodablePng (Join-Path (Join-Path $directory 'visual_evaluation') $reference[0].imageFile)) 'PNG monoscópico ausente ou ilegível.' $errors
    }
    if ($marker.Count -eq 1) {
        Assert-Check ([bool]$marker[0].alignmentCompleted -and $marker[0].alignmentMode -eq 'FullPoseOnce') 'Marcador XR sem alinhamento FullPoseOnce.' $errors
    }

    $frames = @(Get-ChildItem -LiteralPath (Join-Path $directory 'screenshot_sequence') -File -Filter 'frame_*.png')
    Assert-Check ($frames.Count -eq 5) 'Esperadas cinco capturas ADB do headset.' $errors
    foreach ($frame in $frames) {
        Assert-Check (Test-DecodablePng $frame.FullName) "PNG ADB ilegível: $($frame.Name)" $errors
    }
    Assert-Check (Test-DecodablePng (Join-Path $directory 'screenshot.png')) 'Screenshot final do Quest ausente ou ilegível.' $errors
    Assert-Check ((Get-Item -LiteralPath (Join-Path $directory 'logcat.txt')).Length -gt 0) 'Logcat vazio.' $errors
    Assert-Check ((Get-Item -LiteralPath (Join-Path $directory 'player.log')).Length -gt 0) 'Log do player vazio.' $errors

    $cpuSamples = @($process.samples)
    $gpuSamples = @($process.gpu_samples)
    $usableCpu = @($cpuSamples | Where-Object { $_.working_set_bytes -gt 0 -and $null -ne $_.cpu_utilization_percent_of_host })
    $usableGpu = @($gpuSamples | Where-Object { $_.gpu_engines.Count -gt 0 -and -not $_.gpu_counter_error })
    Assert-Check ($cpuSamples.Count -gt 0 -and $usableCpu.Count -gt 0) 'CPU/RAM do PID do player ausentes.' $errors
    Assert-Check ($usableGpu.Count -gt 0) 'Amostras GPU válidas do PID do player ausentes.' $errors

    $historicalStatus = @(Get-Content -LiteralPath (Join-Path $directory 'validation_status.txt'))
    $results += [ordered]@{
        run_id = $spec.RunId
        status = if ($errors.Count -eq 0) { 'valid_technical_capture' } else { 'invalid' }
        representation_id = $spec.Representation
        splat_count = $spec.Count
        ply_sha256 = $spec.Sha256
        original_validation_status = $historicalStatus
        cpu_ram_sample_count = $cpuSamples.Count
        usable_gpu_sample_count = $usableGpu.Count
        headset_sequence_png_count = $frames.Count
        visual_quality = 'separate_manual_assessment_required'
        errors = @($errors.ToArray())
    }
}

$report = [ordered]@{
    schema_version = '1.0'
    policy_id = 'desktop_horizon_link_pcvr_without_ovr_v01'
    generated_at_utc = [DateTime]::UtcNow.ToString('O')
    source_evidence_root = $evidenceRoot
    raw_evidence_preserved = $true
    ovr_metrics_used = $false
    interpretation = 'Validade técnica da captura do EXE Windows via Link. Não mede a execução Android nativa e não aprova automaticamente a qualidade visual.'
    all_technical_captures_valid = (@($results | Where-Object status -NE 'valid_technical_capture').Count -eq 0)
    runs = $results
}
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Relatório derivado preservado em: $OutputPath"
if (-not $report.all_technical_captures_valid) {
    Write-Error 'Uma ou mais execuções não passaram na validação PC-VR sem OVR.'
}
