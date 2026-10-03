# Coletas PC/Link sem reiniciar OVRServer

**Histórico:** as baseline `r07`–`r10` foram coletadas com este procedimento. Para as cinco execuções **novas** por variante, use o [guia da série 5×4](GUIA_REPETICOES_WINDOWS_SEM_RESET_OVR.md) e o script ativo sem reinício. Os comandos abaixo são do plano anterior e alguns IDs já estão ocupados.

O primeiro teste desta condição foi baseline `r07`, com o mesmo build e coletor estacionário da `r06`, mas **sem** encerrar `OVRServer_x64.exe`. O script verifica Quest Link v207, driver NVIDIA 591.59, build/hash, ausência de player Unity remanescente e presença de um OVRServer. Antes de abrir o player, o operador confirma Link estável; durante 75 s, observa a cena no headset. O script registra o PID do OVRServer antes e depois em `ovr_state.json` e rejeita a tentativa se o processo mudar. Também mantém as verificações de variante, pose, capturas ADB, métricas e encerramento do player.

Na `r07`, as checagens automáticas passaram, o player encerrou via `CloseMainWindow` com código 0, os cinco PNGs ADB mostram a cena com a cadeira, o PID do OVRServer permaneceu 4424 e o operador confirmou cena e Link estáveis durante e após a sessão. Essa é **uma** sessão validada sob a nova condição; não demonstra que todas as variantes ou repetições terão o mesmo comportamento.

```powershell
$Runner = 'C:\Users\barbo\tcc\SplatVRLabUnity\scripts\invoke_windows_horizon_link_reset_run_legacy.ps1'

# Baseline r07 concluída; repetir a mesma condição nas próximas sessões.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r08 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r09 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r10 -SkipOvrReset

powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r05 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r06 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r07 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r08 -SkipOvrReset

powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r05 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r06 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r07 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r08 -SkipOvrReset

powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r02 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r03 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r04 -SkipOvrReset
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r05 -SkipOvrReset
```

Digite `LINK_OK` apenas se o ambiente do Link estiver visível e rastreado. Digite `CENA_OK` apenas se a cadeira permaneceu visível e estável durante a sessão e as capturas ADB indicadas mostrarem a cena correta. Pare se surgir `PC Disconnected`, artefatos, launcher preso, perda de rastreamento ou travamento após fechar o player. Preserve a pasta da tentativa, inclusive falhas. Não reutilize `r07` se ela tiver produzido evidência.

A baseline `r06` foi coletada com reinício do OVRServer e driver 591.59; ela pertence a outra condição operacional. Não some `r06` à série sem reinício sem explicitar a diferença. Execute as próximas 15 linhas uma por vez, inspecionando cada sessão antes da seguinte; os IDs devem permanecer exclusivos. O teste isolado não demonstra causalidade do driver.
