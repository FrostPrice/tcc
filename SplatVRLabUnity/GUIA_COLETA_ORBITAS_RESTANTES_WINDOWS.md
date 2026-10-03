# Coleta PC/VR das três órbitas restantes

A baseline PC/VR completou 144/144 poses e foi validada na [milestone 0130](../milestones/0130_validacao_orbita_baseline_pcvr.md). As variantes 100k, 50k e `splatfacto-big` também foram coletadas e validadas na [milestone 0132](../milestones/0132_validacao_quatro_orbitas_pcvr_windows.md). Os comandos abaixo mostram como repetir o procedimento com novos IDs `r02`; os IDs `r01` já estão ocupados. Elas pertencem à contingência Windows/Meta Horizon Link, separada da execução nativa Quest. O driver NVIDIA verificado na preparação foi 591.59. Não reinicie o OVRServer entre as runs desta série.

Em uma reprodução, execute os blocos **um por vez**, em PowerShell no Windows, com o Quest no Meta Horizon Link e a cena visível no headset. Mantenha o headset rastreado e imóvel durante cada órbita. Cada coleta pode levar cerca de 15–20 minutos e salva 144 capturas ADB. O coletor recusa um diretório de saída existente e preserva uma tentativa parcial. Não reutilize um ID existente.

## Preparação, uma vez por sessão

```powershell
$TccRoot = 'C:\Users\barbo\tcc'
$UnityProject = Join-Path $TccRoot 'SplatVRLabUnity'
$AdbExe = 'C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe'
$Collector = Join-Path $UnityProject 'scripts\collect_windows_horizon_link_orbit.ps1'
$Validator = Join-Path $UnityProject 'scripts\validate_windows_horizon_link_orbit.ps1'
& $AdbExe get-state
Get-Process | Where-Object { $_.ProcessName -like 'SplatVRLabUnity-horizon-link-orbit*' }
```

`get-state` deve responder `device`; a última linha deve ficar vazia antes de iniciar cada variante.

## 100k

```powershell
$Name = 'SplatVRLabUnity-horizon-link-orbit-pruned100k-fullcircle-dev-v01'
$RunId = 'desktop_horizon_link_orbit_pruned100k_fullcircle_r02'
$Exe = Join-Path $UnityProject "Builds\Windows\$Name\$Name.exe"
$Evidence = Join-Path $TccRoot "experiments\unitysplats_viability_v01\evidence\$RunId"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Collector -Executable $Exe -AdbExe $AdbExe -Variant pruned100k -RunId $RunId
if ($LASTEXITCODE -ne 0) { throw "Coleta falhou: $RunId; preserve $Evidence" }
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Validator -EvidenceDir $Evidence
if ($LASTEXITCODE -ne 0) { throw "Validação falhou: $RunId" }
```

## 50k

```powershell
$Name = 'SplatVRLabUnity-horizon-link-orbit-pruned50k-fullcircle-dev-v01'
$RunId = 'desktop_horizon_link_orbit_pruned50k_fullcircle_r02'
$Exe = Join-Path $UnityProject "Builds\Windows\$Name\$Name.exe"
$Evidence = Join-Path $TccRoot "experiments\unitysplats_viability_v01\evidence\$RunId"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Collector -Executable $Exe -AdbExe $AdbExe -Variant pruned50k -RunId $RunId
if ($LASTEXITCODE -ne 0) { throw "Coleta falhou: $RunId; preserve $Evidence" }
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Validator -EvidenceDir $Evidence
if ($LASTEXITCODE -ne 0) { throw "Validação falhou: $RunId" }
```

## Splatfacto-big

```powershell
$Name = 'SplatVRLabUnity-horizon-link-orbit-splatfacto-big-fullcircle-dev-v01'
$RunId = 'desktop_horizon_link_orbit_splatfacto_big_fullcircle_r02'
$Exe = Join-Path $UnityProject "Builds\Windows\$Name\$Name.exe"
$Evidence = Join-Path $TccRoot "experiments\unitysplats_viability_v01\evidence\$RunId"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Collector -Executable $Exe -AdbExe $AdbExe -Variant splatfacto_big -RunId $RunId
if ($LASTEXITCODE -ne 0) { throw "Coleta falhou: $RunId; preserve $Evidence" }
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Validator -EvidenceDir $Evidence
if ($LASTEXITCODE -ne 0) { throw "Validação falhou: $RunId" }
```

Em cada pasta de evidência, confira `completion.json`, `capture_manifest.json`, `orbit_validation.json`, `poses/`, `screenshots/` e o relatório `unitysplats_viability_v01_*.json`. `orbit_validation.json` deve registrar `capture_count: 144`, `verified_pose_count: 144` e `cardinal_change_at_least_15: true`. Relate se a vista girou ao longo de toda a órbita e se o Link ficou estável. Copie as pastas completas para o Linux sem substituir as evidências já existentes.
