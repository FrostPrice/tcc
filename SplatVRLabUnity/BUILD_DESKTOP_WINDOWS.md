# Build Windows via Meta Horizon Link

Este guia prepara apenas a contingência diagnóstica desktop. O executável
Windows e suas métricas não substituem nem devem ser agregados às medições
Android/Vulkan nativas do Meta Quest 3S.

## O que instalar no Windows

- Git for Windows;
- Unity Hub e Unity `6000.5.8f1` com **Windows Build Support (IL2CPP)**;
- Meta Horizon Link;
- driver NVIDIA atualizado para a RTX 3060.

Visual Studio Community com a carga **Desktop development with C++** só é
necessário se a Unity solicitar componentes C++ ao compilar IL2CPP. Não instalar
SteamVR, ALVR, WiVRn, Android Studio ou SideQuest para esta rota.

## Transferência mínima do repositório

Clone o repositório no Windows e copie sobre ele, a partir do Arch, as pastas:

```text
SplatVRLabUnity/Assets/
SplatVRLabUnity/Packages/
SplatVRLabUnity/ProjectSettings/
```

Não copie `Library/`, `Temp/`, `Logs/`, `Builds/`, `obj/` ou `UserSettings/`.
Essas pastas são específicas da máquina ou saídas geradas.

Além dos arquivos obtidos por Git, a configuração da baseline lê os insumos a
seguir, hoje ignorados pelo repositório por conterem artefatos grandes:

```text
experiments/baseline_v01/exp_ns_poster_baseline_v01_baseline_v01.ply
experiments/unitysplats_viability_v01/reference_pose.json
experiments/unitysplats_viability_v01/visual_baseline_full_pose_profile.json
SplatVRLabUnity/Assets/Research/Data/poster_baseline_v01.ply
SplatVRLabUnity/Assets/Research/Data/poster_baseline_v01.ply.meta
```

Para preparar posteriormente as variantes 100k, 50k e \textit{big}, copie os
PLYs e perfis correspondentes apenas após a baseline funcionar. O conjunto de
evidências histórico não é requisito para gerar o build Windows.

Antes de abrir a Unity, confira o hash do PLY baseline no PowerShell:

```powershell
(Get-FileHash .\experiments\baseline_v01\exp_ns_poster_baseline_v01_baseline_v01.ply -Algorithm SHA256).Hash.ToLower()
```

O valor esperado é:

```text
23e3b3d3cd47e1aa0ad1daf7df96c4bd620af9edaf7996ccb865f5b60b80bebb
```

## Link e build

1. Conecte o Quest 3S por USB, execute o teste de cabo no Horizon Link e inicie
   Quest Link no headset.
2. Confirme no Horizon Link que ele é o runtime OpenXR ativo.
3. Abra `SplatVRLabUnity` na Unity, aguarde a recriação da `Library` e resolva
   qualquer erro de compilação antes de mudar configurações.
4. Execute o menu:

```text
SplatVRLab > Build Windows Horizon Link baseline visual full-pose
```

O comando seleciona Windows x86\_64, Direct3D 12 e IL2CPP, recria a baseline
com o perfil `FullPoseOnce` e não sobrescreve uma saída já existente. O build é
gerado em:

```text
Builds/Windows/SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v01/
```

5. Com o Link já ativo, execute no PowerShell, a partir da raiz do repositório.
   O coletor inicia o jogador, espera a sessão externa, copia exclusivamente os
   JSONs e capturas novos da aplicação, preserva o log, o registro do build e
   um inventário básico do PC. Ele registra métricas da aplicação no Windows;
   não mede o desempenho Android nativo, nem a latência, o codec ou a taxa de
   bits do Horizon Link.

```powershell
.\SplatVRLabUnity\scripts\collect_windows_horizon_link_run.ps1 `
  -Executable .\SplatVRLabUnity\Builds\Windows\SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v01\SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v01.exe `
  -Condition static_reference `
  -RunId desktop_horizon_link_baseline_fullpose_r01 `
  -CaptureSeconds 75 `
  -RequireAutomatedSequence `
  -ExpectedVariant spark_baseline_visual_full_pose_v01 `
  -ExpectedRepresentation baseline_v01 `
  -RequireVisualCapture `
  -RequireTrackedPoseMarker `
  -ExpectedAlignmentMode FullPoseOnce
```

Caso a política de execução do PowerShell bloqueie o arquivo, execute uma vez
naquela janela: `Set-ExecutionPolicy -Scope Process Bypass`; ela não altera a
política permanente do Windows. Para preservar também imagens do monitor do
desktop, acrescente `-DesktopScreenshotCount 5
-DesktopScreenshotIntervalSeconds 5 -DesktopScreenshotInitialDelaySeconds 30`.
Essas imagens são apenas capturas do host, não imagens estereoscópicas do
headset.

As evidências ficam em
`experiments/unitysplats_viability_v01/evidence/desktop_horizon_link_baseline_fullpose_r01/`.
Se `validation_status.txt` indicar falha, preserve o diretório e não avance
para as variantes comparativas.

## Após a baseline válida

Sem alterar a configuração de Link, a versão da Unity, a API gráfica ou a pose,
gere as variantes comparáveis pelos menus:

```text
SplatVRLab > Build Windows Horizon Link pruned-100k visual full-pose
SplatVRLab > Build Windows Horizon Link pruned-50k visual full-pose
```

Execute cada uma apenas depois de a baseline concluir a janela válida. O menu
da variante `splatfacto-big` também está disponível, mas ela é diagnóstico
isolado e não integra a série baseline/100k/50k.

Para 100k e 50k, reutilize o mesmo comando, alterando somente executável,
`RunId`, `ExpectedVariant` e `ExpectedRepresentation`, respectivamente para:

```text
desktop_horizon_link_pruned100k_fullpose_r01
spark_opacity_topk_100k_visual_full_pose_v01
opacity_topk_100k_v01

desktop_horizon_link_pruned50k_fullpose_r01
spark_opacity_topk_50k_visual_full_pose_v01
opacity_topk_50k_v01
```

## Critério de passagem

A baseline deve aparecer estereoscopicamente no headset, receber rastreamento e
concluir a janela de medição. Se houver falha, preserve o log e não execute as
outras variantes. Consulte o plano experimental completo em
`../experiments/unitysplats_viability_v01/desktop_streaming_windows_horizon_link_plan_v01.md`.
