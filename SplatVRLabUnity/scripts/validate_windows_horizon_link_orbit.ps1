[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $EvidenceDir,
    [string] $OutputFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$EvidenceDir = [IO.Path]::GetFullPath($EvidenceDir)
if (-not $OutputFile) { $OutputFile = Join-Path $EvidenceDir 'orbit_validation.json' }
if (-not (Test-Path -LiteralPath $EvidenceDir -PathType Container)) {
    throw "Diretório de evidências inexistente: $EvidenceDir"
}

$manifest = Get-Content -LiteralPath (Join-Path $EvidenceDir 'capture_manifest.json') -Raw |
    ConvertFrom-Json
$completion = Get-Content -LiteralPath (Join-Path $EvidenceDir 'completion.json') -Raw |
    ConvertFrom-Json
if ($manifest.expected_frames -ne 144 -or $manifest.acknowledged_frames -ne 144 -or
    $completion.expectedFrames -ne 144 -or $completion.acknowledgedFrames -ne 144 -or
    $completion.status -ne 'complete' -or
    $completion.variantId -ne $manifest.variant_id -or
    $completion.trajectoryId -ne $manifest.trajectory_id) {
    throw 'Manifesto ou conclusão não confirma 144/144 poses da mesma órbita.'
}

Add-Type -AssemblyName System.Drawing
function Get-MeanRgbDifference([string] $First, [string] $Second) {
    $a = [Drawing.Bitmap]::new($First)
    $b = [Drawing.Bitmap]::new($Second)
    try {
        if ($a.Width -ne $b.Width -or $a.Height -ne $b.Height) {
            throw 'Capturas ADB têm dimensões diferentes.'
        }
        $sum = 0.0
        for ($y = 0; $y -lt 32; $y++) {
            $py = [int](($y + 0.5) * $a.Height / 32)
            for ($x = 0; $x -lt 64; $x++) {
                $px = [int](($x + 0.5) * $a.Width / 64)
                $ca = $a.GetPixel($px, $py)
                $cb = $b.GetPixel($px, $py)
                $sum += [Math]::Abs($ca.R - $cb.R) +
                    [Math]::Abs($ca.G - $cb.G) +
                    [Math]::Abs($ca.B - $cb.B)
            }
        }
        return $sum / (64 * 32 * 3)
    }
    finally { $a.Dispose(); $b.Dispose() }
}

$poses = New-Object System.Collections.Generic.List[object]
$screenshots = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt 144; $i++) {
    $stem = 'pose_{0:D4}' -f $i
    $posePath = Join-Path $EvidenceDir "poses\$stem.json"
    $imagePath = Join-Path $EvidenceDir "screenshots\$stem.png"
    if (-not (Test-Path -LiteralPath $posePath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $imagePath -PathType Leaf) -or
        (Get-Item -LiteralPath $imagePath).Length -lt 10000) {
        throw "Pose ou screenshot ausente/inválido: $stem"
    }
    $pose = Get-Content -LiteralPath $posePath -Raw | ConvertFrom-Json
    if ($pose.frameIndex -ne $i -or $pose.frameCount -ne 144 -or
        $pose.trajectoryId -ne $manifest.trajectory_id -or
        $pose.variantId -ne $manifest.variant_id -or
        [Math]::Abs([double]$pose.angleDegrees - 2.5 * $i) -gt 0.01) {
        throw "Marcador incoerente: $stem"
    }
    $poses.Add($pose)
    $screenshots.Add($imagePath)
}

$cardinal = [ordered]@{}
foreach ($i in @(36, 72, 108)) {
    $cardinal["zero_vs_$($i * 2.5)_degrees"] = Get-MeanRgbDifference `
        $screenshots[0] $screenshots[$i]
}
$intervals = New-Object System.Collections.Generic.List[double]
for ($i = 0; $i -lt 132; $i += 12) {
    $intervals.Add((Get-MeanRgbDifference $screenshots[$i] $screenshots[$i + 12]))
}
$positionErrors = @($poses | ForEach-Object { [double]$_.positionErrorUnityUnits })
$rotationErrors = @($poses | ForEach-Object { [double]$_.rotationErrorDegrees })
$metric = @(Get-ChildItem -LiteralPath $EvidenceDir -File -Filter 'unitysplats_viability_v01_*.json')
if ($metric.Count -ne 1) { throw 'Esperado um único relatório de métricas nesta coleta.' }
$report = Get-Content -LiteralPath $metric[0].FullName -Raw | ConvertFrom-Json
if ($report.variantId -ne $manifest.variant_id -or
    $report.completionReason -ne 'native_orbit_capture_completed' -or
    $report.conditionId -ne 'orbit_full_circle_capture') {
    throw 'Relatório de métricas não corresponde à órbita completa.'
}

$result = [ordered]@{
    schema_version = '1.0'
    evidence_directory = $EvidenceDir
    variant_id = $manifest.variant_id
    trajectory_id = $manifest.trajectory_id
    capture_count = $screenshots.Count
    verified_pose_count = $poses.Count
    metric_filename = $metric[0].Name
    method = 'mean absolute RGB difference on a 64x32 uniform sample of the ADB framebuffer'
    cardinal_differences = $cardinal
    cardinal_change_at_least_15 = @($cardinal.Values | Where-Object { $_ -ge 15 }).Count -eq 3
    thirty_degree_interval_minimum = ($intervals | Measure-Object -Minimum).Minimum
    thirty_degree_interval_maximum = ($intervals | Measure-Object -Maximum).Maximum
    maximum_position_error_unity_units = ($positionErrors | Measure-Object -Maximum).Maximum
    maximum_rotation_error_degrees = ($rotationErrors | Measure-Object -Maximum).Maximum
    interpretation_limit = 'ADB captures transmitted PC/VR view; visual change does not establish stereo quality or standalone performance.'
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputFile
$result | ConvertTo-Json -Depth 5
