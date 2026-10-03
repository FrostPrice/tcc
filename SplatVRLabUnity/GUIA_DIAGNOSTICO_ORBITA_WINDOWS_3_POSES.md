# Diagnóstico de órbita PC/VR em três poses

**Resultado em 02/10/2026:** a tentativa `desktop_horizon_link_orbit_baseline_threepose_probe_r02` mostrou mudança nas três poses, confirmada no headset e nas capturas ADB. O baseline completo `desktop_horizon_link_orbit_baseline_fullcircle_r01` também concluiu 144/144 poses com mudança visual ao longo do círculo; veja [milestone 0130](../milestones/0130_validacao_orbita_baseline_pcvr.md). O comando abaixo permanece disponível para reproduzir a sonda com um ID ainda não utilizado.

A tentativa PC/VR anterior mostrou movimento na janela Windows, mas a imagem do Quest e suas capturas ADB ficaram na primeira pose. Não há evidência bruta dessa tentativa neste checkout para atribuir uma causa. Após a série estacionária com NVIDIA 591.59 e OVRServer estável, o próximo teste é uma **sonda isolada baseline** em 0°, 90° e 180°; ela não integra as 144 vistas da órbita nativa nem os resultados de FPS estacionário.

O build `SplatVRLabUnity-horizon-link-orbit-baseline-threepose-probe-dev-v01` foi criado com Unity 6000.5.8f1, Direct3D12 e IL2CPP. A cena usa a mesma calibração de pivô e raio 1,25 da órbita nativa; muda apenas o número/espaçamento dos checkpoints para diagnóstico. O coletor salva pose alvo e pose real, uma captura ADB logo após o marcador e outra cinco segundos depois, em cada pose. A comparação RGB amostrada exige diferença média de pelo menos 15/255 entre 0° e 90°, e entre 0° e 180°, nas capturas tardias. Nas capturas nativas usadas para calibrar essa checagem, as diferenças foram aproximadamente 40 e 38/255, respectivamente. A checagem não prova qualidade visual ou conforto, mas recusa uma sequência aparentemente congelada.

Antes de executar, confirme o Link estável no headset, rastreamento ativo e ausência de player antigo. O operador deve observar se a cena realmente muda nas três poses; pode retirar o headset se houver desconforto. Execute **somente uma tentativa** com ID novo:

```powershell
$TccRoot = 'C:\Users\barbo\tcc'
$Name = 'SplatVRLabUnity-horizon-link-orbit-baseline-threepose-probe-dev-v01'
$Exe = Join-Path $TccRoot "SplatVRLabUnity\Builds\Windows\$Name\$Name.exe"
$Adb = Join-Path ${env:ProgramFiles} 'Meta Quest Developer Hub\resources\bin\adb.exe'
$Collector = Join-Path $TccRoot 'SplatVRLabUnity\scripts\collect_windows_horizon_link_orbit.ps1'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Collector -Executable $Exe -AdbExe $Adb -Variant baseline -ThreePoseProbe -RunId desktop_horizon_link_orbit_baseline_threepose_probe_r03
```

Após a coleta, verificar `capture_manifest.json`, `completion.json`, `poses/`, `screenshots/`, `visual_change.json`, `player.log` e o relato do operador. Diferença apenas nas capturas tardias sugere atraso de apresentação; diferença também nas imediatas indica que a janela de captura já basta. Capturas iguais apesar de poses reais diferentes apontam para o caminho de renderização/streaming ou para o método ADB, ainda exigindo diagnóstico. Se a pose real também não mudar, investigar o `XROrigin`/tracking antes do Link. Não iniciar as quatro órbitas de 144 poses até o diagnóstico ser avaliado.
