[CmdletBinding()]
param([string] $EvidenceRoot, [string] $OutputFile)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $EvidenceRoot) {
    $EvidenceRoot = Join-Path $repo 'experiments\unitysplats_viability_v01\evidence'
}
if (-not $OutputFile) {
    $OutputFile = Join-Path $repo `
        'experiments\unitysplats_viability_v01\derived\pcvr_windows_orbit_4variants_v01\validation.json'
}

$specs = @(
    @{ key = 'baseline'; variant = 'desktop_horizon_link_baseline_orbit_full_circle_v01'; representation = 'baseline_v01' },
    @{ key = 'pruned100k'; variant = 'desktop_horizon_link_opacity_topk_100k_orbit_full_circle_v01'; representation = 'opacity_topk_100k_v01' },
    @{ key = 'pruned50k'; variant = 'desktop_horizon_link_opacity_topk_50k_orbit_full_circle_v01'; representation = 'opacity_topk_50k_v01' },
    @{ key = 'splatfacto_big'; variant = 'desktop_horizon_link_splatfacto_big_orbit_full_circle_v01'; representation = 'splatfacto_big_v01' }
)
$rows = New-Object System.Collections.Generic.List[object]
$baselineTargets = New-Object System.Collections.Generic.List[string]

foreach ($spec in $specs) {
    $runId = "desktop_horizon_link_orbit_$($spec.key)_fullcircle_r01"
    $dir = Join-Path $EvidenceRoot $runId
    $manifest = Get-Content -LiteralPath (Join-Path $dir 'capture_manifest.json') -Raw |
        ConvertFrom-Json
    $completion = Get-Content -LiteralPath (Join-Path $dir 'completion.json') -Raw |
        ConvertFrom-Json
    $validation = Get-Content -LiteralPath (Join-Path $dir 'orbit_validation.json') -Raw |
        ConvertFrom-Json
    $build = Get-Content -LiteralPath (Join-Path $dir 'build_record.json') -Raw |
        ConvertFrom-Json
    $metricFiles = @(Get-ChildItem -LiteralPath $dir -File -Filter 'unitysplats_viability_v01_*.json')
    if ($metricFiles.Count -ne 1) { throw "Relatório ausente ou duplicado: $runId" }
    $metric = Get-Content -LiteralPath $metricFiles[0].FullName -Raw | ConvertFrom-Json
    $process = Get-Content -LiteralPath (Join-Path $dir 'process_samples.json') -Raw |
        ConvertFrom-Json
    $pngs = @(Get-ChildItem -LiteralPath (Join-Path $dir 'screenshots') -File -Filter '*.png')
    $poses = @(Get-ChildItem -LiteralPath (Join-Path $dir 'poses') -File -Filter '*.json')
    if ($manifest.variant_id -ne $spec.variant -or
        $manifest.representation_variant_id -ne $spec.representation -or
        $manifest.acknowledged_frames -ne 144 -or
        $completion.variantId -ne $spec.variant -or
        $completion.status -ne 'complete' -or
        $completion.acknowledgedFrames -ne 144 -or
        $validation.variant_id -ne $spec.variant -or
        $validation.capture_count -ne 144 -or
        $validation.verified_pose_count -ne 144 -or
        -not $validation.cardinal_change_at_least_15 -or
        $build.variantId -ne $spec.variant -or
        $metric.variantId -ne $spec.variant -or
        $metric.representationVariantId -ne $spec.representation -or
        $metric.conditionId -ne 'orbit_full_circle_capture' -or
        $metric.completionReason -ne 'native_orbit_capture_completed' -or
        $pngs.Count -ne 144 -or $poses.Count -ne 144 -or
        @($process.samples).Count -ne 144) {
        throw "Metadados incompletos ou incoerentes: $runId"
    }
    $recordedHash = (Get-Content -LiteralPath (Join-Path $dir 'exe_sha256.txt') -Raw).Trim()
    if ($recordedHash -ne $build.executableSha256) {
        throw "Hash do executável incoerente: $runId"
    }

    for ($i = 0; $i -lt 144; $i++) {
        $stem = 'pose_{0:D4}' -f $i
        $pose = Get-Content -LiteralPath (Join-Path $dir "poses\$stem.json") -Raw |
            ConvertFrom-Json
        if ($pose.frameIndex -ne $i -or $pose.variantId -ne $spec.variant -or
            $pose.representationVariantId -ne $spec.representation) {
            throw "Pose incoerente: $runId/$stem"
        }
        $target = ($pose.targetCameraWorldPosition | ConvertTo-Json -Compress) + '|' +
            ($pose.targetCameraWorldRotation | ConvertTo-Json -Compress)
        if ($spec.key -eq 'baseline') { $baselineTargets.Add($target) }
        elseif ($target -ne $baselineTargets[$i]) {
            throw "Alvo de câmera diferente da baseline: $runId/$stem"
        }
    }

    $rows.Add([ordered]@{
        run_id = $runId
        variant_id = $spec.variant
        representation_variant_id = $spec.representation
        capture_count = $pngs.Count
        verified_pose_count = $poses.Count
        process_sample_count = @($process.samples).Count
        cardinal_differences_rgb_255 = $validation.cardinal_differences
        minimum_thirty_degree_difference_rgb_255 = $validation.thirty_degree_interval_minimum
        maximum_position_error_unity_units = $validation.maximum_position_error_unity_units
        metric_file = $metricFiles[0].Name
        metric_sha256 = (Get-FileHash -LiteralPath $metricFiles[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        xr_reported_refresh_hz = $metric.xrDisplayRefreshRateHz
        application_interval_mean_ms = $metric.applicationFrameInterval.meanMs
        application_interval_p95_ms = $metric.applicationFrameInterval.p95Ms
    })
}

$result = [ordered]@{
    schema_version = '1.0'
    record_type = 'pcvr_windows_orbit_four_variant_validation'
    scene_id = 'poster'
    trajectory_id = 'chair_orbit_full_circle_r125_v01'
    expected_variants = 4
    validated_variants = $rows.Count
    targets_match_baseline_at_all_144_indices = $true
    runs = @($rows.ToArray())
    interpretation_limit = 'PC renderizado via Meta Horizon Link. Os intervalos da aplicação incluem espera e captura ADB; não representam fluidez de movimento natural nem desempenho standalone.'
}
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputFile) -Force | Out-Null
$result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $OutputFile
$result | ConvertTo-Json -Depth 7
