TODO: Implementação do Pipeline Completo do TCC

1. Script de Conversão Customizado (NeRF para 3D-GS)

    [ ] Criar módulo de destilação: Desenvolver um script Python ou adicionar uma célula no Colab com a lógica descrita no "Código 2.7" do seu texto.

    [ ] Extrair densidades: Implementar a função para consultar a densidade volumétrica do modelo NeRF treinado (nerfacto) em um grid espacial.

    [ ] Aplicar supressão de espaço vazio: Filtrar os pontos utilizando um threshold de densidade para remover as áreas irrelevantes.

    [ ] Inicializar Gaussianas: Utilizar as posições válidas e as cores (base_colors) como priors geométricos para ancorar as primitivas do modelo GaussianModel.

    [ ] Exportar nuvem convertida: Salvar o resultado dessa inicialização no formato .ply para ser consumido pela Unreal Engine.

2. Avaliação de Fidelidade Visual (Métricas quantitativas)

    [ ] Preparar dataset de teste: Separar um subconjunto de imagens originais da câmera para servir como Ground Truth.

    [ ] Renderizar vistas correspondentes: Fazer o modelo gerado pelo Nerfstudio renderizar imagens nas exatas mesmas posições de câmera do dataset de teste.

    [ ] Implementar cálculo de PSNR: Criar script para medir o Erro Quadrático Médio (MSE) entre as imagens geradas e as reais.

    [ ] Implementar cálculo de SSIM: Adicionar a medição de similaridade estrutural, luminância e contraste.

    [ ] Implementar cálculo de LPIPS: Utilizar a biblioteca LPIPS (já presente nas dependências do seu Colab) para avaliar a semelhança perceptual profunda via redes neurais.

    [ ] Tabular resultados: Salvar os valores em um arquivo estruturado (CSV) para a análise das cenas de baixa, média e alta complexidade.

3. Integração com a Unreal Engine 5 (Desenvolvimento Local)

    [ ] Configurar ambiente UE5: Criar um projeto limpo voltado para Realidade Virtual e instalar o plugin Meta XR.

    [ ] Criar Componente C++ (AGaussianActor): Desenvolver a classe nativa referenciada no seu texto para gerenciar os dados das Gaussianas.

    [ ] Implementar Structured Buffers: Escrever a lógica em C++ utilizando a camada RHI (Render Hardware Interface) para instanciar e preencher os buffers de memória diretamente na GPU.

    [ ] Ajustar Shaders (HLSL): Garantir que os dados de posição, escala, rotação e cor associados às primitivas estejam sendo lidos corretamente pelo pipeline de renderização.

4. Execução On-Device e Profiling (Meta Quest 3)

    [ ] Configurar arquitetura alvo: Definir os parâmetros de compilação da Unreal Engine para Android e Vulkan, ativando a API OpenXR.

    [ ] Ativar renderização foveada: Ligar o Foveated Rendering exigido no seu texto para reduzir a carga de processamento nas bordas do visor.

    [ ] Realizar Build e Deploy: Compilar a aplicação e instalá-la nativamente (sem depender do Meta Quest Link, a princípio) no headset.

    [ ] Coletar métricas de Profiling: Executar os testes nas 3 cenas planejadas (objeto isolado, ambiente interno, reflexos).

    [ ] Registrar stuttering e VRAM: Anotar as variações de FPS, uso de memória VRAM do chip Snapdragon XR2 Gen 2 e aplicar estratégias de culling adicionais se necessário.











Informações adicionais sobre o desenvolvimento:
 - pra executar o colmap (SfM) no google colab tive que usar somente a CPU, devido a problemas de compatibilidade com o ambiente.
 - para 904 imagens, levou cerca de 45 minutos em  uma máquina T4
