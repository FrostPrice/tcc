# Série PC/Link: cinco execuções novas por variante, sem reiniciar OVRServer

As baseline `r07`–`r10` passaram após o downgrade para NVIDIA 591.59 sem reiniciar o OVRServer. Elas são canários e não entram nas **20 execuções novas** deste plano. A baseline `r06` usou reinício do OVRServer e também fica separada. Esta série reúne cinco sessões estacionárias novas de cada representação: baseline, 100k, 50k e `splatfacto-big`.

Os IDs e parâmetros também estão no [manifesto legível por máquina](../experiments/unitysplats_viability_v01/pcvr_windows_59159_no_ovr_reset_series_v01.json). Todos os 20 IDs estavam livres na verificação de preparação.

O [script ativo](scripts/invoke_windows_horizon_link_run.ps1) não contém rotina de encerramento do OVRServer. Em cada chamada, confere build/hash, variante, ADB, cliente Link v207, driver NVIDIA 591.59, ausência de player Unity remanescente e presença do OVRServer. Exige confirmação de Link estável antes de abrir o player; coleta `static_reference` por 75 s com `FullPoseOnce`, cinco capturas ADB e métricas brutas; confere o PID e horário de início do OVRServer após a coleta; exige encerramento normal e confirmação visual da cena. Salva `ovr_state.json` com `series_id = pcvr_windows_59159_no_ovr_reset_5x4_v01`.

Execute **uma linha por vez** no PowerShell, com o headset no Link e o operador observando a cena. Digite `LINK_OK` somente se imagem e rastreamento estiverem normais. Digite `CENA_OK` somente depois de conferir a cadeira nos dois olhos durante a coleta e os PNGs indicados. Se surgir `PC Disconnected`, launcher preso, artefato novo, perda de rastreamento ou falha após a saída, pare e preserve toda a pasta da tentativa. Nunca reutilize um ID existente; se precisar repetir uma falha, use o próximo ID livre e registre a substituição na análise. Faça pausas se necessário.

```powershell
$TccRoot = 'C:\Users\barbo\tcc'
$Runner = Join-Path $TccRoot 'SplatVRLabUnity\scripts\invoke_windows_horizon_link_run.ps1'

# Baseline: cinco novas
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r11
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r12
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r13
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r14
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r15

# 100k: cinco novas
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r05
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r06
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r07
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r08
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r09

# 50k: cinco novas
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r05
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r06
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r07
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r08
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r09

# Splatfacto-big: cinco novas
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r02
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r03
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r04
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r05
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r06
```

Para cada pasta em `experiments/unitysplats_viability_v01/evidence/`, revisar `validation_status.txt`, `player_shutdown_status.txt`, `operator_visual_status.txt`, `ovr_state.json`, cinco PNGs e as métricas brutas antes de agregar. Não apresentar PC/Link como Android nativo. O descompasso cliente Link v207/v208 continua uma limitação documentada; estabilidade nas baseline não comprova causalidade do driver nem garante as outras variantes.
