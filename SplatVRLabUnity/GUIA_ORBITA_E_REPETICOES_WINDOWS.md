# Coletas PCVR separadas: quatro órbitas e 16 sessões estacionárias

Este guia usa o Meta Horizon Link no Windows. As órbitas geram vistas pareadas com o Quest nativo; as sessões `static_reference` geram quatro novas repetições por representação, além da primeira sessão PCVR já validada. **Uma órbita não conta como repetição estacionária.** PCVR é contingência diagnóstica, não execução Android standalone.

## Preparação

No PowerShell, ajuste somente `$TccRoot` para a pasta do projeto no Windows. Feche a Unity antes de executar builds em batch. Ative o Meta Horizon Link no Quest e confirme que o headset permanece rastreado. Interrompa o procedimento se houver desconforto.

```powershell
$TccRoot = 'C:\Users\barbo\unity-projects\tcc_windows_transfer\tcc'
$UnityProject = Join-Path $TccRoot 'SplatVRLabUnity'
$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe'
$AdbExe = 'C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe'
$OrbitCollector = Join-Path $UnityProject 'scripts\collect_windows_horizon_link_orbit.ps1'
$StaticCollector = Join-Path $UnityProject 'scripts\collect_windows_horizon_link_run.ps1'
Set-Location -LiteralPath $TccRoot
Set-ExecutionPolicy -Scope Process Bypass
& $AdbExe devices -l
```

Para cada órbita, confira no headset que a cena aparece antes de deixar os 144 checkpoints prosseguirem. O coletor só confirma um checkpoint depois de salvar seu PNG. Não reutilize um `RunId` ou sobrescreva uma pasta de evidências.

## Parte A — uma órbita por variante

Na Unity, execute os quatro itens de menu `SplatVRLab > Chair orbit > Build Windows Horizon Link ... full-circle EXE` (baseline, pruned-100k, pruned-50k e splatfacto-big). Os builds de cada variante são isolados e não substituem os EXEs estacionários. Se preferir batch mode, com o Editor fechado:

```powershell
$OrbitBuilds = @(
  'BuildHorizonLinkBaselineFullCircleOrbit',
  'BuildHorizonLinkPruned100kFullCircleOrbit',
  'BuildHorizonLinkPruned50kFullCircleOrbit',
  'BuildHorizonLinkSplatfactoBigFullCircleOrbit'
)
foreach ($Method in $OrbitBuilds) {
  & $UnityExe -batchmode -quit -projectPath $UnityProject `
    -executeMethod "SplatVRLab.Editor.SplatVRLabWindowsBuild.$Method" `
    -logFile (Join-Path $TccRoot "windows-orbit-build-$Method.log")
  if ($LASTEXITCODE -ne 0) { throw "Build falhou: $Method" }
}
```

Os builds recusam diretórios de saída já existentes e não devem ser repetidos sem examinar primeiro os resultados. Depois, execute **uma coleta por vez**; cada comando pode levar vários minutos:

```powershell
$OrbitExes = @{
  baseline = 'SplatVRLabUnity-horizon-link-orbit-baseline-fullcircle-dev-v01'
  pruned100k = 'SplatVRLabUnity-horizon-link-orbit-pruned100k-fullcircle-dev-v01'
  pruned50k = 'SplatVRLabUnity-horizon-link-orbit-pruned50k-fullcircle-dev-v01'
  splatfacto_big = 'SplatVRLabUnity-horizon-link-orbit-splatfacto-big-fullcircle-dev-v01'
}

function Invoke-OrbitCapture([string] $Variant) {
  $Name = $OrbitExes[$Variant]
  if (-not $Name) { throw "Variante orbital inválida: $Variant" }
  $Exe = Join-Path $UnityProject "Builds\Windows\$Name\$Name.exe"
  $RunId = "desktop_horizon_link_orbit_${Variant}_fullcircle_r01"
  & $OrbitCollector -Executable $Exe -AdbExe $AdbExe -Variant $Variant -RunId $RunId
  if (-not $?) { throw "Órbita falhou: $RunId" }
  $Completion = Join-Path $TccRoot "experiments\unitysplats_viability_v01\evidence\$RunId\completion.json"
  Get-Content -LiteralPath $Completion -Raw
}

Invoke-OrbitCapture baseline
Invoke-OrbitCapture pruned100k
Invoke-OrbitCapture pruned50k
Invoke-OrbitCapture splatfacto_big
```

Se alguma falhar, **não** execute novamente com o mesmo ID: preserve a pasta parcial e use `r02` para a tentativa seguinte. Não apague uma coleta completa para repetir. Copie os quatro diretórios de evidência para o projeto principal no Linux sem substituir diretórios existentes.

## Parte B — quatro novas sessões estacionárias por variante

Já existem uma sessão PCVR válida para cada representação: baseline `r05`; 100k, 50k e `splatfacto-big` `r01`. As novas sessões serão baseline `r06`–`r09` e as demais `r02`–`r05`. Execute-as separadamente das órbitas, com o headset imóvel, Link ativo e os mesmos parâmetros da primeira sessão: `static_reference`, 75 s, `FullPoseOnce`, cinco capturas ADB após 30 s com intervalo de 5 s, captura visual e marcador de pose obrigatórios. Use os EXEs estacionários `v02` já validados; não use os EXEs de órbita.

```powershell
$StaticVariants = @{
  baseline = @{
    exe = 'SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v02'
    expected = 'spark_baseline_visual_full_pose_v01'
    representation = 'baseline_v01'
    first = 6
  }
  pruned100k = @{
    exe = 'SplatVRLabUnity-horizon-link-pruned100k-fullpose-dev-v02'
    expected = 'spark_opacity_topk_100k_visual_full_pose_v01'
    representation = 'opacity_topk_100k_v01'
    first = 2
  }
  pruned50k = @{
    exe = 'SplatVRLabUnity-horizon-link-pruned50k-fullpose-dev-v02'
    expected = 'spark_opacity_topk_50k_visual_full_pose_v01'
    representation = 'opacity_topk_50k_v01'
    first = 2
  }
  splatfacto_big = @{
    exe = 'SplatVRLabUnity-horizon-link-splatfacto-big-fullpose-dev-v02'
    expected = 'spark_splatfacto_big_visual_full_pose_v01'
    representation = 'splatfacto_big_v01'
    first = 2
  }
}

function Invoke-StaticCapture([string] $Variant, [int] $Repeat) {
  $Spec = $StaticVariants[$Variant]
  if (-not $Spec -or $Repeat -lt 1 -or $Repeat -gt 4) {
    throw 'Informe uma variante válida e Repeat de 1 a 4.'
  }
  $Number = $Spec.first + $Repeat - 1
  $RunId = 'desktop_horizon_link_{0}_fullpose_r{1:D2}' -f $Variant, $Number
  $Exe = Join-Path $UnityProject "Builds\Windows\$($Spec.exe)\$($Spec.exe).exe"
  & $StaticCollector -Executable $Exe -Condition static_reference -RunId $RunId `
    -CaptureSeconds 75 -AdbExe $AdbExe `
    -HeadsetScreenshotCount 5 -HeadsetScreenshotIntervalSeconds 5 `
    -HeadsetScreenshotInitialDelaySeconds 30 -RequireHeadsetScreenshot `
    -RequireAutomatedSequence -ExpectedVariant $Spec.expected `
    -ExpectedRepresentation $Spec.representation -RequireVisualCapture `
    -RequireTrackedPoseMarker -ExpectedAlignmentMode FullPoseOnce
  if (-not $?) { throw "Coleta estacionária falhou: $RunId" }
  $Status = Join-Path $TccRoot "experiments\unitysplats_viability_v01\evidence\$RunId\validation_status.txt"
  Get-Content -LiteralPath $Status
}

# Execute cada chamada individualmente, conferindo status e imagem antes da próxima.
Invoke-StaticCapture baseline 1
Invoke-StaticCapture baseline 2
Invoke-StaticCapture baseline 3
Invoke-StaticCapture baseline 4

Invoke-StaticCapture pruned100k 1
Invoke-StaticCapture pruned100k 2
Invoke-StaticCapture pruned100k 3
Invoke-StaticCapture pruned100k 4

Invoke-StaticCapture pruned50k 1
Invoke-StaticCapture pruned50k 2
Invoke-StaticCapture pruned50k 3
Invoke-StaticCapture pruned50k 4

Invoke-StaticCapture splatfacto_big 1
Invoke-StaticCapture splatfacto_big 2
Invoke-StaticCapture splatfacto_big 3
Invoke-StaticCapture splatfacto_big 4
```

Em caso de falha, preserve a tentativa e escolha um `RunId` novo de forma explícita; a função acima usa IDs fixos e recusa sobrescrita. Faça pausas entre sessões e interrompa o uso se houver desconforto. Não altere resolução, taxa de atualização, bitrate/codec do Link ou configuração da Unity entre as variantes sem iniciar uma série nova e documentar a mudança.

## Interpretação

O conjunto PCVR terá cinco sessões estacionárias por variante **se as 16 novas passarem pela validação**. Baseline/100k/50k poderão ser comparadas descritivamente às cinco execuções nativas de cada uma, sempre em tabelas separadas por plataforma. Para `splatfacto-big`, estão planejadas mais quatro execuções nativas no Quest, coletadas a partir do Linux, para completar uma série própria de cinco. Até que elas existam e passem pela validação, não se deve apresentar um pareamento n=5 contra n=5 nem combinar `big` à série nativa principal. As quatro órbitas serão evidência visual de cobertura angular, não réplicas independentes de desempenho.
