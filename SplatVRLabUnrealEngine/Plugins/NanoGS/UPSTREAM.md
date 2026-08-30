# Proveniência e adaptações locais do NanoGS

- Repositório: <https://github.com/TimChen1383/NanoGaussianSplatting>
- Commit fixado: `075e7cee956958b22bce2593b03f01bb3961fd8a`
- Data do commit: 2026-05-18
- Licença: MIT, preservada em [LICENSE](LICENSE)
- Versões declaradas pelo projeto upstream: Unreal Engine 5.6 e 5.7

O código foi incorporado ao projeto para tornar a prova reprodutível mesmo que o
upstream mude. Há duas adaptações locais, ambas motivadas por falhas observadas no
diagnóstico com UE 5.8.1/Linux antes da execução-alvo em UE 5.7:

1. `GaussianClusterBuilder.h`: os valores padrão de `FBuildSettings` foram movidos
   dos inicializadores de membros para o construtor. O Clang da UE 5.8 recusava o
   construtor como argumento padrão enquanto esses membros ainda possuíam
   inicializadores na classe aninhada.
2. `NanoGS.cpp`: o cálculo/ordenação por compute foi separado do render pass Vulkan.
   Sem essa mudança, o RHI registrava `GetCommandBuffer().IsOutsideRenderPass()` em
   `GaussianSplatCalcViewDataGlobal` e não produzia a captura.

Essas alterações permitem o diagnóstico Vulkan SM5 em UE 5.8.1, mas ainda precisam
ser compiladas e avaliadas na versão-alvo UE 5.7, em estéreo e no Android/Quest.
