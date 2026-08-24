# SplatVRLabUnity

Protótipo mínimo para avaliar uma representação 3D Gaussian Splatting no
Meta Quest 3S usando Unity 6, URP, OpenXR e UnitySplats.

## Versões da baseline

- Unity: `6000.5.8f1`;
- template: `VR` (`com.unity.template.vr@10.0.0`);
- UnitySplats: `v1.1.0`, commit `4db31bead537bf8e2be4fb2f2fb711b33262c12d`;
- Unity.WebP: `0.3.22`;
- representação inicial: `Spark`, SH grau 3, sem redução de splats;
- fonte: `experiments/baseline_v01/exp_ns_poster_baseline_v01_baseline_v01.ply`.
- pose fixa: `experiments/unitysplats_viability_v01/reference_pose.json`.

O PLY e a cópia importada em `Assets/Research/Data/` não são versionados. O
script de configuração copia o artefato registrado na raiz do repositório e
mantém a representação importada no cache local da Unity.

## Conteúdo mínimo do projeto

O conteúdo demonstrativo do template VR foi removido porque não participa da
cena de avaliação. Isso inclui `SampleScene`, onboarding, demonstrações de mãos,
UI do template e os assets de exemplo do XR Interaction Toolkit que não são
referenciados pelo harness. Também foram retirados os pacotes de tutorial,
multiplayer, Android XR genérico e rastreamento de mãos.

A parte de `Starter Assets` ainda presente é intencional: o `XR Origin`, os
controladores, as ações de input e os provedores de locomoção usados pela cena
básica dependem desses arquivos. Não reimporte indiscriminadamente os samples do
template ao abrir o projeto; se uma atualização do rig exigir outro sample,
registre a versão e valide novamente suas dependências antes de adicioná-lo.

## Configurar ou reproduzir o harness

Com o projeto aberto, execute:

```text
SplatVRLab > Configure viability harness
```

O comando:

1. fixa identificador Android, ARM64, IL2CPP e Vulkan;
2. adiciona `Gsplat URP Feature` aos renderers Android e Standalone;
3. importa o PLY como `Spark`, explicitando coordenadas RUB de 3D-GS/Nerfstudio;
4. valida os hashes do PLY e da pose de avaliação antes de gerar a cena;
5. converte automaticamente o mundo Nerfstudio, com eixo vertical `+Z`, para o
   mundo Unity, com eixo vertical `+Y`, e posiciona o modelo para que a câmera de
   referência fique em `(0, 1.6, 0)`;
6. cria `Assets/Research/Scenes/GsplatViability.unity` a partir da cena VR básica;
7. desativa a locomoção e a gravidade para manter a baseline estacionária;
8. alinha a posição e o azimute do HMD quando o rastreamento OpenXR se torna válido;
9. define essa cena como a única cena habilitada no build;
10. adiciona a identidade da pose e a política de escala à coleta de intervalos
   de quadro e dos contadores disponibilizados por
   `FrameTimingManager`.

O mesmo procedimento pode ser executado sem interface:

```bash
/home/mateusbarbosa/Unity/Hub/Editor/6000.5.8f1/Editor/Unity \
  -batchmode -nographics -quit \
  -projectPath /home/mateusbarbosa/Documents/univali/tcc/SplatVRLabUnity \
  -executeMethod SplatVRLab.Editor.SplatVRLabSetup.Configure
```

Os registros de execução são gravados em
`Application.persistentDataPath/measurements`. O campo
`unityReportedGraphicsMemoryMb` reproduz o valor exposto pela API da Unity e não
deve ser descrito como VRAM dedicada do Quest.

## Sequência de validação

1. abrir `GsplatViability` e confirmar o modelo no editor Vulkan;
2. executar `SplatVRLab > Capture reference-pose diagnostic` e conferir a pose
   fixa sem alterar manualmente a cena;
3. no Unity Hub, adicionar à versão `6000.5.8f1` os módulos **Android Build
   Support**, **Android SDK & NDK Tools** e **OpenJDK**;
4. executar `SplatVRLab > Build Android development APK` e instalar o APK
   gerado em `Builds/Android/SplatVRLabUnity-dev.apk` no Quest 3S;
5. preservar o JSON bruto e capturas de ambos os olhos antes de otimizar o modelo.

Para gerar uma captura desktop apenas diagnóstica, use:

```text
SplatVRLab > Capture reference-pose diagnostic
```

A câmera dessa captura usa a matriz e os intrínsecos registrados para o quadro
retido `frame_00001`. O comando rejeita automaticamente uma imagem praticamente
uniforme, para que uma captura somente do fundo não seja registrada como
renderização válida. A pose local foi recuperada deterministicamente, mas ainda
precisa ser comparada com o `dataparser_transforms.json` preservado pela execução
Colab; além disso, os intrínsecos registrados antecedem a undistortion da
avaliação. A captura é, portanto, diagnóstica e não deve ser usada diretamente
para PSNR, SSIM ou LPIPS.

A escala atual é `canonical_non_metric`: uma unidade normalizada do Nerfstudio é
mapeada para uma unidade Unity somente para tornar a colocação repetível. Isso
não equivale a metros reais e ainda não permite concluir sobre escala binocular
ou conforto no Quest.

O APK produzido pelo menu é um build de desenvolvimento destinado à primeira
prova de execução e à coleta de falhas. Métricas finais devem registrar claramente
se o build é de desenvolvimento ou não; os dois modos não devem ser misturados na
mesma comparação.

As instruções completas para gerar o APK, conectá-lo por ADB, instalá-lo no
headset e copiar logs e medições estão em
[BUILD_QUEST.md](BUILD_QUEST.md).

## Estado da prova nativa

O APK de desenvolvimento foi compilado, instalado e aberto nativamente no Meta
Quest 3S. A primeira execução fez o XR Origin cair porque o piso havia sido
removido e a gravidade do template continuava ativa. Desativar `Locomotion`
eliminou a queda e essa decisão agora faz parte do gerador da cena.

A orientação e o enquadramento foram automatizados e validados em uma captura
desktop Vulkan da pose `ns_poster_test_frame_00001`. Um novo APK foi gerado com
essa configuração, mas o APK que comprovou a execução nativa é anterior à
mudança. O novo artefato ainda precisa ser instalado e testado para validar o
alinhamento de posição e azimute no headset. Fidelidade, escala métrica,
consistência binocular e desempenho no dispositivo continuam pendentes.
