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
- APK de locomoção: `Builds/Android/SplatVRLabUnity-locomotion-dev.apk`.

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
A velocidade de `1,0` está em unidades Unity por segundo porque a escala ainda
não foi calibrada em metros.

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

Para cada repetição, execute a partir da raiz do repositório. O primeiro comando
instala o APK; nas demais repetições use `--skip-install` para conservar a mesma
instalação.

```bash
bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
  --adb "$QUEST_ADB" \
  --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-locomotion-dev.apk \
  --condition static_reference \
  --run-id quest_metrics_v01_static_r01
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

## Referências operacionais

- [Unity Manual — Building applications for Android](https://docs.unity3d.com/6000.0/Documentation/Manual/android-BuildProcess.html)
- [Android Developers — Android Debug Bridge](https://developer.android.com/tools/adb)
- [Meta — OVR Metrics Tool](https://developers.meta.com/horizon/documentation/unity/ts-ovrmetricstool/)
- [Meta — Performance Analyzer e métricas](https://developers.meta.com/horizon/documentation/unity/ts-mqdh-logs-metrics/)
