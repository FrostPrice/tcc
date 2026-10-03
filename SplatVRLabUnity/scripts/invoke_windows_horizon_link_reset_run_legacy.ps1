[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('baseline', 'pruned100k', 'pruned50k', 'splatfacto_big')]
    [string] $Variant,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^desktop_horizon_link_[A-Za-z0-9._-]+$')]
    [string] $RunId,

    [string] $AdbExe = 'C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe',
    [switch] $SkipOvrReset,
    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$specs = @{
    baseline = @{
        executable = 'SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v02'
        buildVariant = 'desktop_horizon_link_baseline_visual_full_pose_v01'
        appVariant = 'spark_baseline_visual_full_pose_v01'
        representation = 'baseline_v01'
    }
    pruned100k = @{
        executable = 'SplatVRLabUnity-horizon-link-pruned100k-fullpose-dev-v02'
        buildVariant = 'desktop_horizon_link_opacity_topk_100k_visual_full_pose_v01'
        appVariant = 'spark_opacity_topk_100k_visual_full_pose_v01'
        representation = 'opacity_topk_100k_v01'
    }
    pruned50k = @{
        executable = 'SplatVRLabUnity-horizon-link-pruned50k-fullpose-dev-v02'
        buildVariant = 'desktop_horizon_link_opacity_topk_50k_visual_full_pose_v01'
        appVariant = 'spark_opacity_topk_50k_visual_full_pose_v01'
        representation = 'opacity_topk_50k_v01'
    }
    splatfacto_big = @{
        executable = 'SplatVRLabUnity-horizon-link-splatfacto-big-fullpose-dev-v02'
        buildVariant = 'desktop_horizon_link_splatfacto_big_visual_full_pose_v01'
        appVariant = 'spark_splatfacto_big_visual_full_pose_v01'
        representation = 'splatfacto_big_v01'
    }
}

$spec = $specs[$Variant]
if ($RunId -notmatch ('^desktop_horizon_link_' + [regex]::Escape($Variant) + '_fullpose_r\d{2}$')) {
    throw "RunId não corresponde à variante $Variant`: $RunId"
}
$projectDirectory = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$repositoryDirectory = Split-Path -Parent $projectDirectory
$buildDirectory = Join-Path (Join-Path $projectDirectory 'Builds\Windows') $spec.executable
$executable = Join-Path $buildDirectory ($spec.executable + '.exe')
$buildRecordPath = Join-Path $buildDirectory 'build_record.json'
$outputDirectory = Join-Path $repositoryDirectory "experiments\unitysplats_viability_v01\evidence\$RunId"
$collector = Join-Path $projectDirectory 'scripts\collect_windows_horizon_link_run.ps1'

if (Test-Path -LiteralPath $outputDirectory) {
    throw "RunId já usado; preserve a evidência e escolha outro: $RunId"
}
if (-not (Test-Path -LiteralPath $executable -PathType Leaf) -or
    -not (Test-Path -LiteralPath $buildRecordPath -PathType Leaf)) {
    throw "Build ausente para $Variant`: $buildDirectory"
}
$record = Get-Content -LiteralPath $buildRecordPath -Raw | ConvertFrom-Json
$hash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
if ($record.variantId -ne $spec.buildVariant -or $hash -ine $record.executableSha256) {
    throw "Build não confere com a variante ou SHA-256 esperado: $executable"
}
$players = @(Get-Process -Name 'SplatVRLabUnity*' -ErrorAction SilentlyContinue)
if ($players.Count -gt 0) {
    throw "Player Unity ainda ativo (PID: $(($players | ForEach-Object Id) -join ', '))."
}
$AdbExe = [IO.Path]::GetFullPath($AdbExe)
if (-not (Test-Path -LiteralPath $AdbExe -PathType Leaf)) {
    throw "ADB não encontrado: $AdbExe"
}
$adbState = (& $AdbExe get-state 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $adbState -ne 'device') {
    throw "Quest indisponível no ADB: $adbState"
}
$linkVersionLine = & $AdbExe shell dumpsys package com.oculus.xrstreamingclient |
    Select-String -Pattern '^\s*versionName=' | Select-Object -First 1
if (-not $linkVersionLine -or $linkVersionLine.ToString() -notmatch 'versionName=(\S+)') {
    throw 'Não foi possível identificar a versão do cliente Link no Quest.'
}
$headsetLinkVersion = $Matches[1]
if (-not $headsetLinkVersion.StartsWith('207.', [StringComparison]::Ordinal)) {
    throw "Versão do Link no Quest mudou para $headsetLinkVersion; reveja a série antes de continuar."
}
$nvidiaSmi = Join-Path $env:WINDIR 'System32\nvidia-smi.exe'
if (-not (Test-Path -LiteralPath $nvidiaSmi -PathType Leaf)) {
    throw "nvidia-smi não encontrado: $nvidiaSmi"
}
$driverVersion = @(& $nvidiaSmi --query-gpu=driver_version --format=csv,noheader 2>&1 |
    ForEach-Object { $_.ToString().Trim() } | Where-Object { $_ })
if ($LASTEXITCODE -ne 0 -or $driverVersion.Count -ne 1 -or $driverVersion[0] -ne '591.59') {
    throw "Driver NVIDIA diferente de 591.59 nesta série: $($driverVersion -join ', ')"
}

Write-Host "Pré-checagem: $Variant; $RunId; build/hash OK; Quest Link $headsetLinkVersion; NVIDIA $($driverVersion[0]); nenhum player antigo."
if ($DryRun) {
    Write-Host 'DryRun: OVRServer e player não foram iniciados nem encerrados.'
    return
}

$ovr = @(Get-Process -Name 'OVRServer_x64' -ErrorAction SilentlyContinue)
if ($ovr.Count -ne 1) {
    throw "Esperado exatamente um OVRServer_x64 ativo; encontrados: $($ovr.Count)."
}
$oldPid = $ovr[0].Id
$oldStartedAt = $ovr[0].StartTime.ToString('O')
$newPid = $oldPid
$newStartedAt = $oldStartedAt
if ($SkipOvrReset) {
    Write-Host "Mantendo OVRServer_x64 PID $oldPid ativo; nenhum reinício será solicitado."
} else {
    Write-Host "Encerrando apenas OVRServer_x64 PID $oldPid para reiniciar o Link."
    Stop-Process -Id $oldPid -Force -ErrorAction Stop

    $replacement = $null
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        Start-Sleep -Seconds 1
        $candidates = @(Get-Process -Name 'OVRServer_x64' -ErrorAction SilentlyContinue |
            Where-Object { $_.Id -ne $oldPid })
        if ($candidates.Count -eq 1) {
            $replacement = $candidates[0]
            break
        }
    }
    if (-not $replacement) {
        throw 'O OVRServer não reiniciou em 45 s. Nenhum player foi aberto.'
    }
    $newPid = $replacement.Id
    $newStartedAt = $replacement.StartTime.ToString('O')
    Write-Host "OVRServer reiniciou: PID $oldPid -> $newPid."
}
Write-Host 'No Quest, abra Meta Horizon Link, selecione Launch e confirme imagem e rastreamento.'
$answer = Read-Host 'Digite LINK_OK somente quando o ambiente Link estiver estável e o MQDH ainda mostrar PC v208'
if ($answer -cne 'LINK_OK') {
    throw 'Link não confirmado. Nenhum player foi aberto; preserve este RunId livre.'
}

$ovrRecordPath = if ($SkipOvrReset) { 'ovr_state.json' } else { 'ovr_recovery.json' }
$protocolId = if ($SkipOvrReset) {
    'pcvr_quest207_pc208_nvidia59159_no_ovr_reset_v01'
} else {
    'pcvr_quest207_pc208_ovr_reset_before_each_run_v01'
}
$recovery = [ordered]@{
    schema_version = '1.1'
    protocol_id = $protocolId
    run_id = $RunId
    variant = $Variant
    headset_link_version = $headsetLinkVersion
    pc_link_version_operator_confirmed = '208'
    nvidia_driver_version = $driverVersion[0]
    old_ovr_pid = $oldPid
    old_ovr_started_at_local = $oldStartedAt
    new_ovr_pid = $newPid
    new_ovr_started_at_local = $newStartedAt
    ovr_reset_requested = (-not $SkipOvrReset.IsPresent)
    operator_confirmed_link_before_run = $true
    collector_started_at_utc = [DateTime]::UtcNow.ToString('O')
}

try {
    & $collector -Executable $executable -Condition static_reference -RunId $RunId `
        -OutputDir $outputDirectory -CaptureSeconds 75 -AdbExe $AdbExe `
        -HeadsetScreenshotCount 5 -HeadsetScreenshotIntervalSeconds 5 `
        -HeadsetScreenshotInitialDelaySeconds 30 -RequireHeadsetScreenshot `
        -RequireAutomatedSequence -ExpectedVariant $spec.appVariant `
        -ExpectedRepresentation $spec.representation -RequireVisualCapture `
        -RequireTrackedPoseMarker -ExpectedAlignmentMode FullPoseOnce
}
finally {
    if (Test-Path -LiteralPath $outputDirectory -PathType Container) {
        $afterOvr = @(Get-Process -Name 'OVRServer_x64' -ErrorAction SilentlyContinue)
        $recovery['ovr_pid_after_run'] = if ($afterOvr.Count -eq 1) { $afterOvr[0].Id } else { $null }
        $recovery['ovr_started_at_after_run_local'] = if ($afterOvr.Count -eq 1) { $afterOvr[0].StartTime.ToString('O') } else { $null }
        $recovery['ovr_process_unchanged_during_run'] = ($afterOvr.Count -eq 1 -and
            $afterOvr[0].Id -eq $newPid -and
            $afterOvr[0].StartTime.ToString('O') -eq $newStartedAt)
        $recovery | ConvertTo-Json -Depth 4 |
            Set-Content -LiteralPath (Join-Path $outputDirectory $ovrRecordPath)
    }
}

if ($SkipOvrReset -and -not $recovery['ovr_process_unchanged_during_run']) {
    throw 'OVRServer mudou ou desapareceu durante a sessão sem reinício; preserve a evidência e não valide esta tentativa.'
}

$automaticStatus = (Get-Content -LiteralPath (Join-Path $outputDirectory 'validation_status.txt') -TotalCount 1)
$shutdownStatus = (Get-Content -LiteralPath (Join-Path $outputDirectory 'player_shutdown_status.txt') -TotalCount 1)
Write-Host "Verificação automática: $automaticStatus"
Write-Host "Encerramento do player: $shutdownStatus"
if ($automaticStatus -notmatch '^Verificações automáticas passaram' -or
    ($shutdownStatus -notmatch 'encerrou após CloseMainWindow; ExitCode=0\.' -and
     $shutdownStatus -notmatch 'já havia terminado; ExitCode=0\.')) {
    throw 'A sessão não passou nas verificações automáticas ou no encerramento. Não inicie a próxima.'
}
Write-Host "Revise as cinco imagens em $outputDirectory\screenshot_sequence e o que viu no headset."
$answer = Read-Host 'Digite CENA_OK somente se a cena correta esteve visível e estável nos dois olhos durante a coleta'
if ($answer -cne 'CENA_OK') {
    'Operador não confirmou a cena estável; sessão não validada visualmente.' |
        Set-Content -LiteralPath (Join-Path $outputDirectory 'operator_visual_status.txt')
    throw 'Revisão visual não confirmada. Não inicie a próxima run.'
}
'Operador confirmou cena visível e estável; verificar também os PNGs ADB antes da agregação.' |
    Set-Content -LiteralPath (Join-Path $outputDirectory 'operator_visual_status.txt')
Write-Host "Run concluída: $RunId."
