# 🌲 MuckClone 3D — Roguelike Survival

Um clone completo e modular do clássico jogo **Muck**, desenvolvido em **Unity 6.3 LTS** com **Universal Render Pipeline (URP)** e arquitetura de software orientada a dados (**Data-Driven** com ScriptableObjects).

![Unity 6](https://img.shields.io/badge/Unity-6.3%20LTS-black?logo=unity)
![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-blue)
![C#](https://img.shields.io/badge/Language-C%23%20.NET%2010-purple?logo=csharp)
![Build](https://img.shields.io/badge/Build-Passing%20(0%20errors)-brightgreen)

---

## 🎮 Visão Geral do Projeto

Este projeto implementa as mecânicas centrais de sobrevivência, física arcade e progressão roguelike inspiradas no jogo *Muck* (criado por Dani). Todo o desenvolvimento segue princípios rígidos de engenharia de software para garantir alto desempenho, desacoplamento e prontidão para multiplayer.

### 🌟 Destaques Técnicos & Funcionalidades

* 🏃 **Física Arcade Estilo Source/Quake:**
  * Movimentação responsiva com aceleração instantânea no solo (55 m/s²) e frenagem limpa (65 m/s²), eliminando qualquer sensação de patinação no gelo.
  * **Bunny Hopping & Air Strafe:** Conservação e ganho de momento linear no ar ao virar a câmera com comandos direcionais.
  * **Slide (Deslize Dinâmico):** Ativado com `Ctrl` em velocidade, reduzindo o colisor pela metade e aplicando aceleração tangencial ladeira abaixo em declives.
  * **Jump Buffer & Coyote Time:** Tolerância de 0.15s para pulos precisos e consistentes.

* 🏝️ **Geração Procedural de Terreno & Recursos:**
  * Malha 3D fractal gerada em tempo de execução com múltiplas oitavas de *Perlin Noise*.
  * **Estética Cartoon Low-Poly (Flat-Shaded):** Triângulos desacoplados sem interpolação de vértices, gerando o visual geométrico facetado característico sem nenhum borrão de cores.
  * **Degraus Discretos de Altitude:** Areia Dourada da Praia ($\le 3$m), Grama Cartoon Muck ($3$m-$15$m), Rocha Estilizada ($15$m-$22$m) e Neve Pura ($> 22$m).
  * **Falloff Natural de Praia:** Curva contínua com `SmoothStep` que faz as colinas descerem suavemente para praias planas ($Y \approx 1$m) e mergulharem no mar ($Y \to -4$m) sem paredões artificiais.
  * **Poisson Disc Sampling 2D:** Algoritmo de Bridson para garantir distribuição homogênea e sem sobreposição de árvores, rochas e baús.
  * **Shader Cel-Shading URP:** Shader HLSL customizado (`URP_TerrainVertexLit.shader`) que lê as cores dos vértices e projeta sombras em degrau nítido.

* 🪓 **Sistema de Coleta, Ferramentas e Inventário:**
  * **Tiers Estritos de Ferramentas:** Tiers de 0 a 6 (Madeira, Sílex/Pedra, Ferro, Ouro, Mitril e Adamantite). Ferramentas de Tier inferior causam apenas dano residual (1) e emitem feedback de bloqueio.
  * **Nós de Recursos com Vida e Feedback:** Árvores e Rochas com pontos de vida, tremor procedural de impacto (*wobble*), barras de vida flutuantes suspensas e explosão de múltiplos drops físicos ao quebrar.
  * **Drops Físicos com Imã Anti-Deslizamento:** Pedaços de madeira e pedra com amortecimento e alto atrito que não rolam ladeira abaixo e voam suavemente até o peito do jogador ao se aproximar.
  * **Inventário & Hotbar:** 20 slots de inventário (5 na Hotbar) com atalhos numéricos (`1` a `5`) e alternância pela roda do mouse (`Scroll`).
  * **Empunhadura em 1ª Pessoa:** Instanciação visual na mão do jogador do item ativo na visão de câmera.
  * **HUD da Hotbar em Tempo Real:** Interface OnGUI na base da tela com contorno dourado no item ativo e mira central em cruz (`+`).

* ⚔️ **Combate, Inimigos e IA com NavMesh:**
  * **NavMesh em Runtime:** Construção automática da malha de navegação `NavMeshSurface` logo após a geração do terreno procedural.
  * **Inimigos com IA:** Goblins com máquina de estados (`Idle`, `Chase`, `Attack`, `Knockback`), perseguição fluida pelo relevo, antecipação de golpe (*windup*) e barras de vida flutuantes permanentes visíveis a até 40m.
  * **Knockback Bidirecional:** O jogador empurra os inimigos para trás com a força de suas ferramentas/armas, e os inimigos empurram fisicamente o `Rigidbody` do jogador com pulso vermelho de dano na tela.
  * **Sobrevivência do Jogador:** Gerenciamento de Vida (100 HP), Fome e Estamina, com regeneração passiva quando alimentado.

* ⚡ **Motor de Power-ups Roguelike & Baús Interativos:**
  * **Fórmula Roguelike Canônica:** Modificadores acumuláveis (*stacking*) infinitos calculados como:
    $$\text{ValorFinal} = (\text{Base} + \sum \text{Flat}) \times (1 + \sum \text{Percent})$$
  * **Power-ups Clássicos Implementados:**
    * 👟 **Dani's Sneaker:** +40% de Velocidade de Movimento e +35% de Pulo por stack.
    * 🧃 **Juice:** +40% de Velocidade de Ataque (reduz cooldown pela metade).
    * 🧲 **Horseshoe:** +25% de Chance de Acerto Crítico.
    * 🗡️ **Crimson Dagger:** +25% de Roubo de Vida (*Lifesteal*) que cura o jogador a cada golpe.
    * 🏋️ **Dumbbell:** +50% de Dano de Ataque bruto.
  * **Baús com Abertura Procedural:** Interação com a tecla `E`, animação suave da tampa abrindo em 90° e ejeção física de orbes flutuantes de Power-ups.
  * **HUD de Power-ups & Telemetria:** Banners comemorativos no topo da tela, badges laterais com a contagem de stacks e exibição em tempo real dos bônus no painel de telemetria superior esquerdo.

---

## ⌨️ Controles do Jogo

| Tecla / Ação | Função |
| :--- | :--- |
| **W, A, S, D** | Movimentação direcional |
| **Mouse (Mover)** | Rotação de câmera em primeira pessoa (Pitch e Yaw) |
| **Espaço** | Pulo / Bunny Hopping com momentum |
| **Shift Esquerdo** | Correr (Sprint) |
| **Ctrl Esquerdo** | Agachar / Deslizar (Slide em velocidade ou ladeira) |
| **Mouse0 (Botão Esquerdo)** | Atacar / Golpear recursos e inimigos (*Procedural Swing*) |
| **E** | Interagir com Baús e objetos do mundo |
| **1, 2, 3, 4, 5** | Selecionar slot da Hotbar |
| **Roda do Mouse (Scroll)** | Alternar slots da Hotbar |
| **G** *(Debug)* | Spawna instantaneamente 1 Goblin na sua frente |
| **P** *(Debug)* | Concede instantaneamente 1 Power-up aleatório |
| **ESC** | Libera/Trava o cursor do mouse na tela |

---

## 🏗️ Arquitetura e Estrutura de Pastas

```
MuckClone/Assets/Scripts/
├── Combat/
│   ├── IDamageable.cs            # Interface de dano e knockback
│   └── PlayerHealth.cs           # Vida, Estamina, Fome e HUD de sobrevivência
├── Enemies/
│   ├── EnemyController.cs        # IA com NavMesh e máquina de estados
│   ├── EnemyData.cs              # ScriptableObject de atributos de monstros
│   └── EnemyHealth.cs            # Vida, flash de dano, drops e barra flutuante
├── Harvesting/
│   └── ResourceNode.cs           # Árvores e rochas com vida, Tiers e wobble
├── Interactables/
│   ├── IInteractable.cs          # Interface de interação com tecla E
│   └── ChestInteractable.cs      # Baú com tampa animada e ejeção de orbes
├── Inventory/
│   ├── HotbarUI.cs               # HUD da base da tela com slots e mira central
│   ├── InventorySlot.cs          # Estrutura serializável de itens e quantidades
│   └── PlayerInventory.cs        # Gerenciamento de 20 slots e empunhadura 1ª pessoa
├── Items/
│   ├── ItemData.cs               # ScriptableObject base para itens e ferramentas
│   └── ItemPickup.cs             # Item 3D no chão com física anti-deslizamento e ímã
├── Player/
│   ├── PlayerInputReader.cs      # Leitura desacoplada do Input System + hardware fallback
│   ├── PlayerInteraction.cs      # Ataques, coleta com ferramentas e interação
│   ├── PlayerLook.cs             # Câmera FPS com pitch clamp (-85° a 85°)
│   └── PlayerMovement.cs         # Física Rigidbody, bunny hop, slide e telemetria
├── PowerUps/
│   ├── PlayerStats.cs            # Motor Roguelike de cálculo de atributos acumuláveis
│   ├── PowerUpData.cs            # ScriptableObject de Power-ups (Sneaker, Juice, etc.)
│   ├── PowerUpPickup.cs          # Orbe flutuante coletável ejetado de baús
│   └── StatType.cs               # Enums e estruturas de modificadores Flat/Percent
└── World/
    ├── PoissonDiscSampler.cs     # Distribuição uniforme de recursos 2D (Bridson)
    └── ProceduralIslandGenerator.cs # Relevo fractal, Flat-Shaded mesh e runtime NavMesh
```

---

## 🚀 Como Executar o Projeto

### Pré-requisitos
* **Unity 6.3 LTS** (versão 6000.3.x ou superior) com módulo Windows Build Support instalado.
* **.NET SDK 10 / 8** (opcional, para compilação rápida via CLI).

### Instruções
1. Clone este repositório:
   ```bash
   git clone https://github.com/SEU_USUARIO/muck-clone-unity6.git
   ```
2. Abra o **Unity Hub**, clique em **Add** e selecione a pasta `MuckClone`.
3. Abra o projeto na versão **Unity 6.3 LTS**.
4. Na aba *Project*, navegue até `Assets/Scenes/` e abra a cena `SampleScene.unity`.
5. Pressione o botão **Play (▶️)** no topo do editor para iniciar o jogo imediatamente.

---

## 🗺️ Roadmap de Desenvolvimento

- [x] **Fase 1:** Player Controller, Câmera em Primeira Pessoa & Física 3D (Rigidbody + Air Strafe + Slide + HUD Telemetria).
- [x] **Fase 2:** Geração Procedural da Ilha & Biomas Cartoon (Flat-Shaded Mesh + Falloff Suave + Poisson Disc + Cel-Shading URP).
- [x] **Fase 3:** Sistema de Coleta, Mineração, Ferramentas com Tiers, Drops Físicos e Hotbar.
- [x] **Fase 4:** Sistema de Combate, Inimigos (Goblins), NavMesh em Runtime e Vida do Jogador.
- [x] **Fase 5:** Motor Roguelike de Power-ups (Stat Modifiers), Baús Interativos e Absorção com Stacks.
- [ ] **Fase 6:** Ciclo Dia/Noite, Diretor Noturno de Hordas por Orçamento Matemático e Chefes (Bob/Big Golem).
- [ ] **Fase 7:** Polimento, Efeitos Visuais (Juice), Áudio e Interface de Usuário Final.

---

## 📄 Licença
Distribuído sob a licença MIT. Consulte `LICENSE` para mais detalhes.
Criado para fins educacionais e estudo de arquitetura técnica de jogos 3D na Unity.
