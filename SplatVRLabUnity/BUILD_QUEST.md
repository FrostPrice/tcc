# Compilar e instalar o APK no Meta Quest 3S

Este guia descreve o caminho reproduzível usado para gerar o APK Android do
`SplatVRLabUnity`, instalá-lo por USB no Meta Quest 3S e preservar as evidências
básicas da execução. O resultado é uma aplicação **standalone**: o processamento
ocorre no headset e não por Quest Link.

## Configuração registrada

- Unity: `6000.5.8f1`;
- cena de build: `Assets/Research/Scenes/GsplatViability.unity`;
- identificador Android: `br.edu.univali.splatvrlab`;
- arquitetura: ARM64;
- backend: IL2CPP;
- API gráfica: Vulkan;
- runtime XR: OpenXR;
- APK estacionário: `Builds/Android/SplatVRLabUnity-dev.apk`;
- APK de locomoção manual: `Builds/Android/SplatVRLabUnity-locomotion-dev.apk`;
- APK de controle estacionário automatizado: `Builds/Android/SplatVRLabUnity-automated-static-dev.apk`;
- APK automatizado de caminhada: `Builds/Android/SplatVRLabUnity-automated-walk-dev.apk`;
- APK automatizado de giro: `Builds/Android/SplatVRLabUnity-automated-snapturn-dev.apk`.
- APK estacionário automatizado com poda de 100 mil splats:
  `Builds/Android/SplatVRLabUnity-pruned-100k-static-dev.apk`.
- APKs de captura visual de referência: `Builds/Android/SplatVRLabUnity-visual-baseline-dev.apk`
  e `Builds/Android/SplatVRLabUnity-visual-pruned100k-dev.apk`.

## 1. Preparar o computador e o headset

Na instalação `6000.5.8f1` do Unity Hub, confirme estes módulos:

- Android Build Support;
- Android SDK & NDK Tools;
- OpenJDK.

No Meta Quest 3S:

1. associe o headset a uma conta de desenvolvedor da Meta;
2. ative o **Developer Mode** nas configurações do headset pelo aplicativo Meta
   Horizon;
3. conecte o headset ao computador com um cabo USB que transmita dados;
4. coloque o headset e aceite **Allow USB debugging**;
5. marque a opção para sempre permitir esse computador, se ele for confiável.

Os nomes e a posição das opções no aplicativo Meta Horizon podem mudar entre
versões. O requisito a verificar é que o headset esteja em modo de desenvolvedor
e autorize a depuração USB.

## 2. Configurar a cena

Abra o projeto no Unity e execute:

```text
SplatVRLab > Configure viability harness
```

Ao terminar, procure no Console as mensagens `SETUP_OK` e `VALIDATION_OK` sem
erros posteriores. As mensagens devem identificar a pose
`ns_poster_test_frame_00001`, o quadro `./images/frame_00001.png` e a política de
escala. O configurador interrompe o build se os hashes do PLY ou da pose não
corresponderem à baseline registrada.

### Atenção a alterações manuais da cena

O comando `SplatVRLab > Build Android development APK` executa novamente a
configuração e recria `GsplatViability` a partir da cena básica. Portanto,
alterações manuais feitas apenas na cena gerada podem ser substituídas.

O comando `SplatVRLab > Build Android locomotion development APK` também recria
a cena, mas aplica o perfil versionado de locomoção e gera um APK separado.

Para a baseline estacionária, o script desativa automaticamente
`XR Origin (XR Rig) > Locomotion`. Isso preserva o rastreamento da cabeça, mas
impede que os provedores de gravidade e movimento desloquem a câmera sem um piso.
Não é necessário editar ou apagar esse objeto manualmente.

Orientação, escala e pose inicial não devem ser corrigidas manualmente na cena
gerada. Elas são lidas de
`experiments/unitysplats_viability_v01/reference_pose.json`; qualquer alteração
experimental deve produzir outro artefato identificado e atualizar o manifesto.

Para outras alterações manuais não relacionadas à pose, use este fluxo:

1. execute `Configure viability harness`, que também desativa `Locomotion`;
2. faça as demais alterações na cena `GsplatViability`;
3. salve a cena com `Ctrl+S`;
4. compile manualmente por `File > Build Profiles`, conforme a seção seguinte.

Se precisar reativar a locomoção em outra variante experimental, não delete o
objeto: registre a variante e altere sua caixa de ativação no Inspector.

Quando o rastreamento OpenXR fica válido, o harness move a câmera rastreada para
a posição canônica e alinha somente seu azimute. Inclinação e rotação lateral
continuam determinadas pela cabeça do participante; não é possível forçar uma
orientação completa do HMD sem contrariar o rastreamento físico.

## 3. Gerar o APK

Há duas formas de compilação.

### Baseline gerada automaticamente

Use esta opção quando a cena não tiver alterações manuais que precisem ser
preservadas:

```text
SplatVRLab > Build Android development APK
```

O comando configura o projeto, seleciona Android e gera um Development Build. O
Console deve terminar com `ANDROID_BUILD_OK`.

### Variante de locomoção controlada

Use esta opção para movimento horizontal pelo joystick esquerdo e giro de 30
graus pelo joystick direito:

```text
SplatVRLab > Build Android locomotion development APK
```

O comando lê e valida
`experiments/unitysplats_viability_v01/locomotion_profile.json`, mantém a
gravidade desligada e também desativa teleporte, escalada, movimento por agarrar
e salto. O APK é salvo como
`Builds/Android/SplatVRLabUnity-locomotion-dev.apk`.

O splat não fornece colisores para paredes ou objetos. A variante permite
atravessar a geometria visual e deve ser avaliada somente como navegação simples.

### Variantes automatizadas para a bateria de movimento

Para eliminar a intervenção do participante durante a coleta, há duas variantes
separadas: `SplatVRLab > Build Android automated continuous-walk metrics APK` e
`SplatVRLab > Build Android automated snap-turn metrics APK`. Elas aplicam,
respectivamente, translação da origem a 0,25 unidade Unity/s na direção canônica
e giro horário de 30 graus a cada 0,75 s, somente depois do aquecimento e do
início efetivo da janela de métricas. Os provedores manuais de movimento e giro
ficam desabilitados nesses APKs.

Essas variantes medem uma carga visual de movimento reproduzível; elas não
validam mapeamento de controles, conforto ou usabilidade da locomoção manual.

O menu `SplatVRLab > Build Android automated stationary-control metrics APK`
gera a referência pareada sem translação ou giro. Ele preserva a mesma sequência
de instrumentação pós-aquecimento e os provedores manuais desligados; use-o para
comparar as condições automatizadas sem atribuir diferenças ao APK anterior.
A velocidade de `1,0` está em unidades Unity por segundo porque a escala ainda
não foi calibrada em metros.

### Variante podada de 100 mil splats

O menu `SplatVRLab > Build Android pruned-100k stationary metrics APK` importa
o PLY `opacity_topk_100k_v01` como asset separado, valida seus 100.000 splats e
gera um controle estacionário automatizado. Ele preserva a baseline de 195.760
splats e deve ser medido inicialmente apenas contra o controle estacionário
automatizado equivalente. A configuração e o hash do PLY estão em
`experiments/pruning_v01/manifest.json`.

### Cena com alterações manuais

1. abra `File > Build Profiles`;
2. selecione ou adicione o perfil `Android`;
3. use `Switch Profile` se Android ainda não for o perfil ativo;
4. mantenha `Export Project` e `Build App Bundle` desativados;
5. ative `Development Build` para esta prova diagnóstica;
6. clique em `Build`;
7. salve como `Builds/Android/SplatVRLabUnity-dev.apk`.

Antes da instalação, registre o tamanho e o checksum do artefato:

```bash
ls -lh Builds/Android/SplatVRLabUnity-dev.apk
sha256sum Builds/Android/SplatVRLabUnity-dev.apk
```

O diretório `Builds/` é gerado localmente e não deve ser versionado. Registre o
checksum, o tamanho e a configuração no manifesto do experimento.

## 4. Localizar o ADB fornecido pela Unity

O Android SDK instalado pelo Unity Hub contém o `adb` neste caminho relativo ao
editor:

```text
Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
```

No Linux, abra um terminal na raiz de `SplatVRLabUnity` e defina caminhos para a
sua instalação. Não grave o caminho absoluto da máquina nos arquivos do projeto:

```bash
QUEST_ADB="$HOME/Unity/Hub/Editor/6000.5.8f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
APK_PATH="./Builds/Android/SplatVRLabUnity-dev.apk"
```

Se `adb` já estiver disponível no `PATH`, também é possível usar:

```bash
QUEST_ADB="adb"
```

## 5. Verificar a conexão USB

Execute:

```bash
"$QUEST_ADB" devices -l
```

O headset deve aparecer com o estado `device`.

- `unauthorized`: coloque o headset e aceite a solicitação de depuração USB;
- nenhuma linha de dispositivo: reconecte o cabo, desbloqueie o headset e teste
  outra porta ou cabo de dados;
- `no permissions` no Linux: configure as regras `udev` para dispositivos
  Android na distribuição usada e reconecte o headset;
- mais de um dispositivo: acrescente `-s NUMERO_DE_SERIE` aos comandos ADB.

Reiniciar o servidor ADB pode corrigir uma conexão que ficou presa:

```bash
"$QUEST_ADB" kill-server
"$QUEST_ADB" start-server
"$QUEST_ADB" devices -l
```

## 6. Instalar ou atualizar o APK

Com o headset listado como `device`, execute:

```bash
"$QUEST_ADB" install -r "$APK_PATH"
```

O parâmetro `-r` substitui a versão instalada preservando os dados da aplicação.
O resultado esperado é `Success`.

Se ocorrer `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, a versão instalada foi assinada
com outra chave. Primeiro preserve medições existentes e somente depois remova o
pacote:

```bash
"$QUEST_ADB" pull /sdcard/Android/data/br.edu.univali.splatvrlab/files/measurements measurements_before_uninstall
"$QUEST_ADB" uninstall br.edu.univali.splatvrlab
"$QUEST_ADB" install "$APK_PATH"
```

`adb uninstall` apaga os dados locais da aplicação, incluindo medições ainda não
copiadas.

## 7. Abrir e encerrar a aplicação

A aplicação pode ser aberta pela biblioteca do Quest, na seção de aplicativos de
fontes desconhecidas ou de desenvolvimento. Também pode ser iniciada pelo terminal:

```bash
"$QUEST_ADB" shell monkey -p br.edu.univali.splatvrlab -c android.intent.category.LAUNCHER 1
```

Para encerrá-la:

```bash
"$QUEST_ADB" shell am force-stop br.edu.univali.splatvrlab
```

## 8. Preservar logs, captura e medições

Crie um diretório diferente para cada execução. O exemplo abaixo assume que o
terminal continua na raiz de `SplatVRLabUnity`:

```bash
RUN_DIR="../experiments/unitysplats_viability_v01/evidence/quest_run_001"
mkdir -p "$RUN_DIR"
```

Copie os JSONs gerados pelo coletor do projeto:

```bash
"$QUEST_ADB" pull /sdcard/Android/data/br.edu.univali.splatvrlab/files/measurements "$RUN_DIR/measurements"
```

Registre o log do Unity e falhas Android disponíveis no momento:

```bash
"$QUEST_ADB" logcat -d -s Unity AndroidRuntime > "$RUN_DIR/logcat.txt"
```

Capture a saída exibida pelo dispositivo:

```bash
"$QUEST_ADB" exec-out screencap -p > "$RUN_DIR/screenshot.png"
```

Uma captura do dispositivo comprova que houve saída visual, mas não demonstra
sozinha consistência binocular ou qualidade estereoscópica. Essas propriedades
devem ser observadas no headset e registradas separadamente.

Para cada execução, registre também:

- ID do experimento, da cena e da variante;
- checksum e tamanho do APK;
- versão do Unity, UnitySplats e Horizon OS;
- configuração de refresh rate;
- horário aproximado e duração do teste;
- resultado de instalação e lançamento;
- FPS, frame time e memória com o nome exato do contador usado;
- travamentos, stuttering, floaters, escala incorreta, queda da câmera,
  inconsistência binocular e desconforto percebido.

O JSON de métricas registra `referencePoseId`, `referenceFrame`, `scalePolicy`,
`metersPerNerfstudioUnit`, o erro residual do alinhamento, o perfil de locomoção,
os inputs, a velocidade, o tipo de giro, a gravidade e a política de colisão.
Enquanto
`metricScaleCalibrated` for `false`, não descreva a escala observada como métrica.

Não apresente uma execução que apenas abre no Quest como validação de fidelidade
ou desempenho. Builds de desenvolvimento possuem instrumentação adicional e não
devem ser misturadas com builds finais na mesma comparação.

## 9. Bateria controlada de métricas

O protocolo versionado está em
[`quest_metrics_protocol_v01.json`](../experiments/unitysplats_viability_v01/quest_metrics_protocol_v01.json).
Ele fixa cinco repetições para cada condição: `static_reference`,
`continuous_walk` e `snap_turn`. Antes da bateria, instale o **OVR Metrics Tool**
no Quest e ative a gravação CSV em modo de relatório. Se usar o Meta Quest
Developer Hub, desative casting durante o perfilamento.

Para cada repetição, execute a partir da raiz do repositório. A série
`static_reference` já foi concluída e preservada. Para `continuous_walk` e
`snap_turn`, compile e use o APK automatizado correspondente; o primeiro comando
instala o APK e, nas demais repetições, use `--skip-install` para conservar a
mesma instalação.

Para o controle estacionário pareado, use o APK automatizado estático e a opção
`--require-automated-sequence`:

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-automated-static-dev.apk \
  --condition static_reference \
  --run-id quest_metrics_v01_static_control_r01 \
  --require-automated-sequence
```

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-automated-walk-dev.apk \
  --condition continuous_walk \
  --run-id quest_metrics_v01_walk_r01
```

O script abre o aplicativo, informa a atividade a executar, aguarda 45 s e, se
necessário, espera até 60 s adicionais pelo JSON do aplicativo antes de encerrá-lo.
Em seguida, copia as evidências para
`experiments/unitysplats_viability_v01/evidence/ID_DA_EXECUCAO/`. Não apaga
arquivos no headset. O `FrameMetricsRecorder` inclui no JSON
`measurementStartedAtUtc`, que identifica a janela real de 30 s após os 180
frames de aquecimento; use essa janela, e não o tempo externo aproximado, para
relacionar os dados internos ao CSV do OVR Metrics Tool.
Os índices `app_measurements_new.txt` e `ovr_metrics_new.txt` identificam apenas
os arquivos criados na repetição, sem misturar relatórios históricos do headset.
O coletor também rejeita o relatório se o `conditionId` do APK não corresponder à
condição pedida ou se a sequência automatizada não tiver iniciado.
Para variantes de representação, use também `--expected-variant` e
`--expected-representation`; o JSON interno será rejeitado se identificar outro
APK ou outro PLY.

## 10. Captura visual diagnóstica em pose Nerfstudio

Os menus `SplatVRLab > Build Android baseline visual-reference APK` e
`SplatVRLab > Build Android pruned-100k visual-reference APK` geram APKs que,
após 12 s, escrevem um marcador timestampado da pose rastreada em
`visual_evaluation/`. O screenshot é capturado pelo host via ADB ao final da
sessão; ele é o framebuffer estéreo efetivamente produzido pelo Quest.

Essas variantes usam o alinhamento padrão de posição e yaw. Elas possibilitam
uma inspeção qualitativa pareada em uma pose física estável, mas não são a pose
de rotação completa do Nerfstudio nem uma entrada para métricas pixel a pixel.

Para cada APK, execute uma vez com o marcador obrigatório; o coletor copiará
apenas o JSON novo e fará o screenshot do framebuffer:

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-visual-baseline-dev.apk \
  --condition static_reference \
  --run-id quest_visual_v01_baseline_r01 \
  --require-automated-sequence \
  --expected-variant spark_baseline_visual_reference_v01 \
  --expected-representation baseline_v01 \
  --require-tracked-pose-marker
```

Se o Quest bloquear a abertura por um diálogo do sistema, o coletor detecta a
mensagem `Reprojected OS dialog`, preserva `launch_logcat.txt`, retorna o código
de erro `3` e não inicia uma repetição inválida. Desbloqueie o headset e feche o
diálogo antes de usar outro identificador de execução.

O coletor retorna o código de erro `4` quando o JSON indicar
`application_paused_before_window_completed` ou outro encerramento precoce. Isso
ocorre, por exemplo, se o headset for removido, bloqueado ou se o aplicativo
perder foco durante a janela. Mantenha o headset vestido, o app em primeiro plano
e não reabra o OVR Metrics Tool enquanto a repetição estiver em andamento.

Ao terminar cada repetição, acrescente um `observation.md` com estabilidade de
altura, mapeamento de controles, stuttering, floaters, estéreo observado no
headset, escala não métrica, conforto e falhas. Não derive consistência
estereoscópica de `screenshot.png`.

## 11. Variante visual de pose completa

Os menus `SplatVRLab > Build Android baseline visual full-pose APK` e
`SplatVRLab > Build Android pruned-100k visual full-pose APK` criam variantes
diagnósticas para a pose `ns_poster_test_frame_00001`. Após o tracking iniciar,
elas aplicam uma única transformação no pai do XR Origin para que a câmera fique
na posição **e** rotação da pose Nerfstudio. Movimentos posteriores do HMD
continuam sendo aplicados como deltas rastreados; isto não é uma configuração de
conforto e não deve ser usada para avaliar locomoção.

O marcador JSON deve informar `alignmentMode: "FullPoseOnce"`,
`alignmentCompleted: true` e erro de rotação próximo de zero no instante de 12 s.
O coletor abaixo rejeita automaticamente qualquer outro modo:

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-visual-baseline-fullpose-dev.apk \
  --condition static_reference \
  --run-id quest_visual_v02_baseline_fullpose_r01 \
  --require-automated-sequence \
  --expected-variant spark_baseline_visual_full_pose_v01 \
  --expected-representation baseline_v01 \
  --require-tracked-pose-marker \
  --expected-alignment-mode FullPoseOnce
```

Repita trocando o APK, identificadores esperados e `run-id` por
`SplatVRLabUnity-visual-pruned100k-fullpose-dev.apk`,
`spark_opacity_topk_100k_visual_full_pose_v01`, `opacity_topk_100k_v01` e
`quest_visual_v02_pruned100k_fullpose_r01`, respectivamente. Mantenha o
headset imóvel desde a abertura até a captura: a comparação é uma inspeção
qualitativa pareada, não PSNR, SSIM ou LPIPS.

## 12. Série de desempenho com pose completa

O protocolo
[`full_pose_performance_protocol_v01.json`](../experiments/unitysplats_viability_v01/full_pose_performance_protocol_v01.json)
requer cinco repetições estacionárias por representação. Ative antes o CSV do
OVR Metrics Tool e mantenha o headset imóvel. Na primeira repetição de cada APK,
omita `--skip-install`; nas quatro seguintes, acrescente-o ao final.

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-visual-baseline-fullpose-dev.apk \
  --condition static_reference \
  --run-id quest_fullpose_perf_v01_baseline_r01 \
  --require-automated-sequence \
  --expected-variant spark_baseline_visual_full_pose_v01 \
  --expected-representation baseline_v01 \
  --require-tracked-pose-marker \
  --expected-alignment-mode FullPoseOnce
```

Para `r02` a `r05`, troque apenas o `run-id` e acrescente `--skip-install` em
uma linha própria. Depois, faça o mesmo com o APK podado e os valores
`quest_fullpose_perf_v01_pruned100k_r01` a `r05`,
`spark_opacity_topk_100k_visual_full_pose_v01` e `opacity_topk_100k_v01`.
Quando as dez capturas estiverem válidas, gere os resultados derivados uma única
vez:

```bash
python3 experiments/unitysplats_viability_v01/scripts/analyze_full_pose_performance_v01.py
```

O analisador cria `derived/quest_full_pose_performance_v01/` e falha se houver
algum relatório, marcador ou CSV ausente ou incompatível. Ele não altera as
evidências brutas.

## 13. Variante de 50 mil Gaussianas

O APK `SplatVRLabUnity-visual-pruned50k-fullpose-dev.apk` corresponde à
representação `opacity_topk_50k_v01`. Primeiro, produza uma captura de
confirmação da pose completa:

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-visual-pruned50k-fullpose-dev.apk \
  --condition static_reference \
  --run-id quest_visual_v03_pruned50k_fullpose_r01 \
  --require-automated-sequence \
  --expected-variant spark_opacity_topk_50k_visual_full_pose_v01 \
  --expected-representation opacity_topk_50k_v01 \
  --require-tracked-pose-marker \
  --expected-alignment-mode FullPoseOnce
```

Se esse marcador concluir corretamente, colete cinco repetições para desempenho
com os identificadores `quest_fullpose_perf_v01_pruned50k_r01` a `r05` e os
mesmos argumentos; acrescente `--skip-install` de `r02` em diante. Após a quinta:

```bash
python3 experiments/unitysplats_viability_v01/scripts/analyze_full_pose_performance_v01.py \
  --include-50k
```

Isso gera `derived/quest_full_pose_performance_with_50k_v01/` e preserva a
análise original baseline × 100k sem sobrescrevê-la.

## 14. Diagnóstico de fidelidade do UnitySplats

As duas variantes abaixo mantêm a baseline de 195.760 Gaussianas, a pose
`FullPoseOnce`, URP, OpenXR e todos os demais parâmetros do renderer. Cada uma
altera **um** fator: `gamma-linear-off` desabilita `GammaToLinear`, enquanto
`sh0` usa somente o termo DC (`SHDegree = 0`). Elas são diagnósticos de
fidelidade, não configurações de desempenho ou correções de cor.

Construa pelo menu correspondente em `SplatVRLab`, instale o APK recém-gerado e
faça uma captura por variante. O JSON de métricas retém os cinco parâmetros do
renderer e a validação em batch escreve os valores em `VALIDATION_OK`.

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-visual-baseline-gamma-linear-off-fullpose-dev.apk \
  --condition static_reference \
  --run-id quest_renderer_fidelity_v01_gamma_linear_off_baseline_r01 \
  --require-automated-sequence \
  --expected-variant spark_baseline_visual_gamma_linear_off_full_pose_v01 \
  --expected-representation baseline_v01 \
  --require-tracked-pose-marker \
  --expected-alignment-mode FullPoseOnce
```

Para o segundo diagnóstico, substitua o APK por
`SplatVRLabUnity-visual-baseline-sh0-fullpose-dev.apk`, o identificador por
`quest_renderer_fidelity_v01_sh0_baseline_r01` e a variante esperada por
`spark_baseline_visual_sh0_full_pose_v01`. Em ambos os casos, mantenha o
headset imóvel; preserve um JSON de métricas, marcador de pose, CSV OVR e
screenshot por execução. Os screenshots são evidência qualitativa e não devem
receber PSNR, SSIM ou LPIPS.

## 15. Diagnóstico da compactação Spark

A variante `uncompressed_baseline_visual_full_pose_v01` importa o mesmo PLY da
baseline sem compactação (`Uncompressed`). Ela conserva `GammaToLinear=true`,
`SHDegree=3`, a pose `FullPoseOnce` e os demais controles da seção 14. Não é
uma variante de desempenho: o seu tamanho, compatibilidade e desempenho no
Quest são evidências do diagnóstico. A validação e o JSON do aplicativo
registram `representationImportCompression=Uncompressed`.

Construa pelo menu `SplatVRLab > Build Android baseline uncompressed visual
full-pose APK` e colete somente uma execução estática:

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-visual-baseline-uncompressed-fullpose-dev.apk \
  --condition static_reference \
  --run-id quest_renderer_fidelity_v02_uncompressed_baseline_r01 \
  --require-automated-sequence \
  --expected-variant uncompressed_baseline_visual_full_pose_v01 \
  --expected-representation baseline_uncompressed_import_v01 \
  --require-tracked-pose-marker \
  --expected-alignment-mode FullPoseOnce
```

Compare qualitativamente apenas com a baseline Spark de pose completa. Preserve
JSON, marcador, CSV OVR e screenshot; screenshots continuam inadequados para
PSNR, SSIM ou LPIPS.

## 16. Diagnóstico da saída monoscópica

`SplatVRLabUnity-visual-baseline-monoscopic-output-fullpose-dev.apk` preserva a
baseline Spark e grava uma câmera monoscópica offscreen no próprio Quest. Ela
serve para contrastar a saída URP com o screenshot estéreo externo; não é uma
métrica de fidelidade pixel a pixel. Exija a captura visual no coletor:

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-visual-baseline-monoscopic-output-fullpose-dev.apk \
  --condition static_reference \
  --run-id quest_renderer_output_v01_monoscopic_baseline_r02_urp_camera \
  --require-automated-sequence \
  --require-visual-capture \
  --expected-variant spark_baseline_monoscopic_output_full_pose_v01 \
  --expected-representation baseline_v01 \
  --require-tracked-pose-marker \
  --expected-alignment-mode FullPoseOnce
```

Uma imagem preta de uma implementação que use `Camera.Render()` manual no
Android deve ser registrada como falha de integração; não use-a para comparar
cores. A captura válida precisa indicar pixels não pertencentes ao fundo no JSON
monoscópico.

## Referências operacionais

- [Unity Manual — Building applications for Android](https://docs.unity3d.com/6000.0/Documentation/Manual/android-BuildProcess.html)
- [Android Developers — Android Debug Bridge](https://developer.android.com/tools/adb)
- [Meta — OVR Metrics Tool](https://developers.meta.com/horizon/documentation/unity/ts-ovrmetricstool/)
- [Meta — Performance Analyzer e métricas](https://developers.meta.com/horizon/documentation/unity/ts-mqdh-logs-metrics/)
