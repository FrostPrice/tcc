# Repetições estacionárias PC/Link com reinício do OVRServer

**Histórico:** este protocolo com reinício foi substituído pela [série 5×4 sem reinício](GUIA_REPETICOES_WINDOWS_SEM_RESET_OVR.md). O script original foi preservado como `scripts/invoke_windows_horizon_link_reset_run_legacy.ps1` para reproduzir a baseline `r06`. Não use os comandos abaixo para novas coletas.

Esta série usa o Quest 3S em Meta Horizon Link, com cliente v207 no headset, v208 no PC e driver NVIDIA 591.59. Antes de **cada** sessão, o script confere o build, o Quest e o driver, encerra apenas `OVRServer_x64.exe`, espera o serviço reiniciar e exige confirmação visual do Link. Depois coleta por 75 s, salva cinco PNGs ADB, confere os identificadores e o encerramento do player e pede confirmação da cena no headset. O reinício é uma etapa de recuperação documentada em `ovr_recovery.json`; ele não muda a representação nem a janela de medição, mas caracteriza esta série sob uma condição operacional própria. As sessões PC/Link não são resultados Android nativos.

A baseline `r06` já foi coletada com driver 591.59, reinício do OVRServer, checagens automáticas aprovadas, encerramento normal do player e confirmação visual do operador. Seu `nvidia-smi.txt` registra a versão do driver; as próximas sessões terão também esse campo em `ovr_recovery.json`. Restam 15 comandos abaixo. Não repita `r06` nem sobrescreva sua evidência. A aparente melhora após o downgrade é observação do operador, ainda não uma causa demonstrada.

Use PowerShell. Conecte o Quest por ADB, deixe o Meta Horizon Link aberto no PC e mantenha a configuração de renderização/Link fixa. Execute **uma linha de coleta por vez**. Quando o script pedir `LINK_OK`, selecione *Launch* no Quest e confirme imagem e rastreamento estáveis e v208 do PC no MQDH. Quando pedir `CENA_OK`, confira que viu a cadeira estável durante a coleta e revise os PNGs indicados pelo script. Se houver `PC Disconnected`, artefatos, cena errada ou falha do coletor, não confirme e não inicie a próxima. Preserve a pasta da tentativa e escolha um ID novo para repetir.

```powershell
$Runner = 'C:\Users\barbo\tcc\SplatVRLabUnity\scripts\invoke_windows_horizon_link_reset_run_legacy.ps1'

# Baseline
# r06 já coletada; começar em r07.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r07
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r08
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant baseline -RunId desktop_horizon_link_baseline_fullpose_r09

# 100k
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r05
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r06
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r07
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned100k -RunId desktop_horizon_link_pruned100k_fullpose_r08

# 50k
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r05
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r06
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r07
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant pruned50k -RunId desktop_horizon_link_pruned50k_fullpose_r08

# Splatfacto-big
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r02
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r03
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r04
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Runner -Variant splatfacto_big -RunId desktop_horizon_link_splatfacto_big_fullpose_r05
```

Os canários anteriores ocupam IDs das variantes 100k e 50k. Estes 16 IDs estavam livres na preparação do guia. Se o Windows negar acesso ao encerramento do `OVRServer_x64.exe`, abra o PowerShell como administrador e repita apenas o comando que falhou. O script recusa sobrescrever um ID que já criou evidência.

Antes de agregar resultados, use `validation_status.txt`, `player_shutdown_status.txt`, `operator_visual_status.txt`, `ovr_recovery.json`, métricas brutas e PNGs de cada pasta. Compare estas sessões entre si e reporte separadamente as sessões anteriores, os canários, os travamentos e a diferença de versões. Não presuma que as primeiras sessões pré recuperação compartilhem exatamente a mesma condição operacional. A órbita PC/VR continua uma etapa independente e ainda não validada.
