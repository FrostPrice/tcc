# Guia de build e coleta Windows via Meta Horizon Link

Este procedimento gera e coleta a baseline, as variantes pruned 100k e 50k e, por último, o splatfacto-big. Execute os comandos no **PowerShell do Windows**. As medições são do player Windows transmitido pelo Link; não são medições Android nativas do Quest.

**Protocolo atual:** o CSV do OVR Metrics Tool não integra a validação do EXE via Horizon Link. Nas quatro execuções observadas, a gravação estava ativada e nenhum CSV novo foi produzido. Este é um problema observado nesta configuração e também relatado na comunidade da Meta para Air Link; não é uma confirmação oficial de incompatibilidade universal com Link por cabo. Preserve os `validation_status.txt` originais, que registram a exigência antiga. O [relatório derivado PC-VR](../experiments/unitysplats_viability_v01/derived/desktop_horizon_link_pcvr_validation_v01/validation.json) revalidou as quatro capturas tecnicamente sem exigir OVR; a qualidade visual ainda precisa de avaliação humana. A série Windows via Link permanece separada da série Android nativa.
O registro CSV do OVR foi desligado no Quest por ADB e o estado `recordingMetricsEnabled:false` foi confirmado. Não é preciso habilitá-lo para as coletas Windows via Link.

Baseline `r05`, pruned 100k `r01`, pruned 50k `r01` e splatfacto-big `r01` já foram coletados. Os comandos abaixo usam **novos RunIds apenas para eventuais repetições**; não é necessário refazer essas quatro execuções por causa do CSV OVR.

## Preparação

Abra o projeto uma vez na Unity Hub, espere a importação terminar e confira se OpenXR está habilitado para Windows/Standalone. Feche o Editor antes de executar os builds em `-batchmode`. Inicie o Quest Link no headset antes de cada coleta.

Na condição `static_reference`, deixe o headset **imóvel e sem tocar nos controles**, mas mantenha o Quest Link aberto e o rastreamento ativo. `XR_SESSION_STATE_IDLE` significa que a sessão OpenXR não recebeu o headset; essa condição não produz uma coleta VR válida. Se precisar apoiar o headset fora da cabeça, o Meta Quest Developer Hub permite desabilitar temporariamente o sensor de proximidade em **Device Manager > Device Actions > Proximity Sensor**. Confirme que o Link permanece ativo antes de iniciar o coletor; a configuração do sensor não substitui a conexão e o rastreamento.

O caminho abaixo corresponde à cópia local encontrada neste computador. Se o projeto estiver em outro lugar, altere apenas `$TccRoot`.

```powershell
$TccRoot = 'C:\Users\barbo\unity-projects\tcc_windows_transfer\tcc'
$UnityProject = Join-Path $TccRoot 'SplatVRLabUnity'
$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe'
$Collector = Join-Path $UnityProject 'scripts\collect_windows_horizon_link_run.ps1'
$AdbExe = 'C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe'
Set-Location -LiteralPath $TccRoot
Set-ExecutionPolicy -Scope Process Bypass

if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw "Unity não encontrada: $UnityExe" }
if (-not (Test-Path -LiteralPath $Collector -PathType Leaf)) { throw "Coletor não encontrado: $Collector" }
if (-not (Test-Path -LiteralPath $AdbExe -PathType Leaf)) { throw "ADB do Developer Hub não encontrado: $AdbExe" }
& $AdbExe devices -l

$BaselinePly = Join-Path $TccRoot 'experiments\baseline_v01\exp_ns_poster_baseline_v01_baseline_v01.ply'
(Get-FileHash -LiteralPath $BaselinePly -Algorithm SHA256).Hash.ToLowerInvariant()
```

O hash esperado da baseline é `23e3b3d3cd47e1aa0ad1daf7df96c4bd620af9edaf7996ccb865f5b60b80bebb`.

**Importante:** todos os comandos abaixo montam `$Executable` com `Join-Path $UnityProject` e passam ao coletor um caminho absoluto. Mantenha o nome de cada `.exe` em uma única linha. O coletor não sobrescreve diretórios de evidências existentes; para repetir uma coleta, use um `RunId` novo e preserve as tentativas anteriores.

Os builds `v02` habilitam a captura visual monoscópica após a janela de métricas. As coletas exigem o PNG e o JSON dessa captura com `-RequireVisualCapture`, além do marcador de pose rastreada. Preserve a baseline `v01` e as tentativas `r01`, `r02` e `r03` como evidências anteriores. A `r02` falhou na cópia do JSON para um caminho Windows longo; o coletor foi corrigido.
Na tentativa `r03`, o player gerou PNG e JSON, mas o OpenXR permaneceu em `IDLE` e não recebeu rastreamento do headset. A `r04` produziu métricas e capturas, mas as três primeiras capturas ADB estavam pretas; a `r05` trouxe uma sequência estéreo utilizável. Um status válido confirma os requisitos automáticos, mas a qualidade da imagem no headset precisa de avaliação separada. Se as capturas ADB estiverem pretas, não as apresente como evidência visual da cena. Registre os artefatos visuais observados nas versões pruned.

O coletor usa o ADB do Meta Quest Developer Hub para acrescentar `screenshot.png`, cinco capturas em `screenshot_sequence/` e `logcat.txt`. `process_metrics.json` registra CPU e memória filtradas pelo PID do player Windows a cada cinco segundos solicitados; as amostras GPU, também por PID, ficam em `gpu_samples` e são coletadas em segundo plano para não atrasar as capturas. A CPU é a fração da capacidade total dos processadores lógicos do PC; a memória inclui conjunto de trabalho e bytes privados. Os percentuais GPU são por mecanismo e não devem ser somados como um percentual único da placa. O JSON do aplicativo e essas amostras medem o PC; screenshots e logcat vêm do headset. Não agregue as métricas PC-VR à série Android nativa.

O coletor agora tenta fechar o player ao terminar, inclusive se ocorrer erro. Se o Link continuar preso **depois que o comando voltar ao prompt**, confira se restou algum player e encerre apenas esse processo antes de tentar conectar novamente:

```powershell
$StalePlayers = Get-Process -Name 'SplatVRLabUnity-horizon-link-*' -ErrorAction SilentlyContinue
$StalePlayers | Select-Object Id, ProcessName
$StalePlayers | Stop-Process -Force
```

Não execute esse bloco durante os 75 segundos de coleta. Se a imagem continuar quebrada apesar de o player ter fechado, registre o problema visual separadamente; a captura monoscópica não verifica a imagem estereoscópica transmitida pelo Link.

## 1. Baseline

Se o executável da baseline já existir, o bloco `if` pula o build. Sem a mensagem final abaixo, esse bloco não mostraria nenhuma saída. O build também não sobrescreve uma saída existente.

```powershell
$BaselineExe = Join-Path $UnityProject 'Builds\Windows\SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v02\SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v02.exe'

if (-not (Test-Path -LiteralPath $BaselineExe -PathType Leaf)) {
    & $UnityExe `
      -batchmode -quit `
      -projectPath $UnityProject `
      -executeMethod SplatVRLab.Editor.SplatVRLabWindowsBuild.BuildHorizonLinkBaselineVisualFullPose `
      -logFile (Join-Path $TccRoot 'windows-unity-baseline-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'O build baseline falhou. Consulte windows-unity-baseline-build.log.' }
}
if (-not (Test-Path -LiteralPath $BaselineExe -PathType Leaf)) { throw "Executável não encontrado: $BaselineExe" }
Write-Host "Baseline pronta: $BaselineExe"
```

Com o Link ativo, execute a coleta:

```powershell
$BaselineRunId = 'desktop_horizon_link_baseline_fullpose_r06'
& $Collector `
  -Executable $BaselineExe `
  -Condition static_reference `
  -RunId $BaselineRunId `
  -CaptureSeconds 75 `
  -AdbExe $AdbExe `
  -HeadsetScreenshotCount 5 `
  -HeadsetScreenshotIntervalSeconds 5 `
  -HeadsetScreenshotInitialDelaySeconds 30 `
  -RequireHeadsetScreenshot `
  -RequireAutomatedSequence `
  -ExpectedVariant spark_baseline_visual_full_pose_v01 `
  -ExpectedRepresentation baseline_v01 `
  -RequireVisualCapture `
  -RequireTrackedPoseMarker `
  -ExpectedAlignmentMode FullPoseOnce

$BaselineStatus = Join-Path $TccRoot "experiments\unitysplats_viability_v01\evidence\$BaselineRunId\validation_status.txt"
Get-Content -LiteralPath $BaselineStatus
```

Avance somente se o status disser **“Execução válida segundo as verificações solicitadas.”** e a cena tiver aparecido corretamente nos dois olhos.

## 2. Pruned 100k

```powershell
$Pruned100kExe = Join-Path $UnityProject 'Builds\Windows\SplatVRLabUnity-horizon-link-pruned100k-fullpose-dev-v02\SplatVRLabUnity-horizon-link-pruned100k-fullpose-dev-v02.exe'

if (-not (Test-Path -LiteralPath $Pruned100kExe -PathType Leaf)) {
    & $UnityExe `
      -batchmode -quit `
      -projectPath $UnityProject `
      -executeMethod SplatVRLab.Editor.SplatVRLabWindowsBuild.BuildHorizonLinkPruned100kVisualFullPose `
      -logFile (Join-Path $TccRoot 'windows-unity-pruned100k-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'O build 100k falhou. Consulte windows-unity-pruned100k-build.log.' }
}
if (-not (Test-Path -LiteralPath $Pruned100kExe -PathType Leaf)) { throw "Executável não encontrado: $Pruned100kExe" }
Write-Host "Pruned 100k pronto: $Pruned100kExe"
```

```powershell
$Pruned100kRunId = 'desktop_horizon_link_pruned100k_fullpose_r02'
& $Collector `
  -Executable $Pruned100kExe `
  -Condition static_reference `
  -RunId $Pruned100kRunId `
  -CaptureSeconds 75 `
  -AdbExe $AdbExe `
  -HeadsetScreenshotCount 5 `
  -HeadsetScreenshotIntervalSeconds 5 `
  -HeadsetScreenshotInitialDelaySeconds 30 `
  -RequireHeadsetScreenshot `
  -RequireAutomatedSequence `
  -ExpectedVariant spark_opacity_topk_100k_visual_full_pose_v01 `
  -ExpectedRepresentation opacity_topk_100k_v01 `
  -RequireVisualCapture `
  -RequireTrackedPoseMarker `
  -ExpectedAlignmentMode FullPoseOnce

$Pruned100kStatus = Join-Path $TccRoot "experiments\unitysplats_viability_v01\evidence\$Pruned100kRunId\validation_status.txt"
Get-Content -LiteralPath $Pruned100kStatus
```

Avance somente após validar a execução e a imagem 100k no headset.

## 3. Pruned 50k

```powershell
$Pruned50kExe = Join-Path $UnityProject 'Builds\Windows\SplatVRLabUnity-horizon-link-pruned50k-fullpose-dev-v02\SplatVRLabUnity-horizon-link-pruned50k-fullpose-dev-v02.exe'

if (-not (Test-Path -LiteralPath $Pruned50kExe -PathType Leaf)) {
    & $UnityExe `
      -batchmode -quit `
      -projectPath $UnityProject `
      -executeMethod SplatVRLab.Editor.SplatVRLabWindowsBuild.BuildHorizonLinkPruned50kVisualFullPose `
      -logFile (Join-Path $TccRoot 'windows-unity-pruned50k-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'O build 50k falhou. Consulte windows-unity-pruned50k-build.log.' }
}
if (-not (Test-Path -LiteralPath $Pruned50kExe -PathType Leaf)) { throw "Executável não encontrado: $Pruned50kExe" }
Write-Host "Pruned 50k pronto: $Pruned50kExe"
```

```powershell
$Pruned50kRunId = 'desktop_horizon_link_pruned50k_fullpose_r02'
& $Collector `
  -Executable $Pruned50kExe `
  -Condition static_reference `
  -RunId $Pruned50kRunId `
  -CaptureSeconds 75 `
  -AdbExe $AdbExe `
  -HeadsetScreenshotCount 5 `
  -HeadsetScreenshotIntervalSeconds 5 `
  -HeadsetScreenshotInitialDelaySeconds 30 `
  -RequireHeadsetScreenshot `
  -RequireAutomatedSequence `
  -ExpectedVariant spark_opacity_topk_50k_visual_full_pose_v01 `
  -ExpectedRepresentation opacity_topk_50k_v01 `
  -RequireVisualCapture `
  -RequireTrackedPoseMarker `
  -ExpectedAlignmentMode FullPoseOnce

$Pruned50kStatus = Join-Path $TccRoot "experiments\unitysplats_viability_v01\evidence\$Pruned50kRunId\validation_status.txt"
Get-Content -LiteralPath $Pruned50kStatus
```

## 4. Splatfacto-big: diagnóstico isolado

Execute **somente depois** de baseline, 100k e 50k. Preserve o resultado separadamente e **não o inclua nas médias baseline/100k/50k**.

```powershell
$SplatfactoBigExe = Join-Path $UnityProject 'Builds\Windows\SplatVRLabUnity-horizon-link-splatfacto-big-fullpose-dev-v02\SplatVRLabUnity-horizon-link-splatfacto-big-fullpose-dev-v02.exe'

if (-not (Test-Path -LiteralPath $SplatfactoBigExe -PathType Leaf)) {
    & $UnityExe `
      -batchmode -quit `
      -projectPath $UnityProject `
      -executeMethod SplatVRLab.Editor.SplatVRLabWindowsBuild.BuildHorizonLinkSplatfactoBigVisualFullPose `
      -logFile (Join-Path $TccRoot 'windows-unity-splatfacto-big-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'O build splatfacto-big falhou. Consulte windows-unity-splatfacto-big-build.log.' }
}
if (-not (Test-Path -LiteralPath $SplatfactoBigExe -PathType Leaf)) { throw "Executável não encontrado: $SplatfactoBigExe" }
Write-Host "Splatfacto-big pronto: $SplatfactoBigExe"
```

```powershell
$SplatfactoBigRunId = 'desktop_horizon_link_splatfacto_big_fullpose_r02'
& $Collector `
  -Executable $SplatfactoBigExe `
  -Condition static_reference `
  -RunId $SplatfactoBigRunId `
  -CaptureSeconds 75 `
  -AdbExe $AdbExe `
  -HeadsetScreenshotCount 5 `
  -HeadsetScreenshotIntervalSeconds 5 `
  -HeadsetScreenshotInitialDelaySeconds 30 `
  -RequireHeadsetScreenshot `
  -RequireAutomatedSequence `
  -ExpectedVariant spark_splatfacto_big_visual_full_pose_v01 `
  -ExpectedRepresentation splatfacto_big_v01 `
  -RequireVisualCapture `
  -RequireTrackedPoseMarker `
  -ExpectedAlignmentMode FullPoseOnce

$SplatfactoBigStatus = Join-Path $TccRoot "experiments\unitysplats_viability_v01\evidence\$SplatfactoBigRunId\validation_status.txt"
Get-Content -LiteralPath $SplatfactoBigStatus
```

O status do coletor confirma a presença dos arquivos e marcadores exigidos. Registre também o que foi observado visualmente no headset. Não trate essa execução por Link como resultado nativo Android/Vulkan.
