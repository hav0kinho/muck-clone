# Memória do Projeto: Clone Muck 3D (Unity 6.3 LTS / URP)

## 1. Visão Geral e Setup
- **Engine:** Unity 6.3 LTS (URP - Universal Render Pipeline).
- **Projeto:** `MuckClone/`
- **Compilador C#:** .NET SDK 10 / netstandard2.1 (`Assembly-CSharp.csproj` compilando com 0 erros).
- **Pacotes Essenciais:** `com.unity.inputsystem` (1.20.0), `com.unity.ai.navigation` (2.0.15), `com.unity.render-pipelines.universal` (17.3.0).

---

## 2. O que foi Implementado e Validado

### Fase 1: Player Controller, Câmera e Física 3D (Concluída)
- **`Assets/Scripts/Player/PlayerInputReader.cs`:**
  - Leitura desacoplada de input compatível com `InputSystem_Actions.inputactions`.
  - Mecanismo de fallback direto para teclado/mouse (`Keyboard.current`, `Mouse.current`), garantindo zero perda de frames mesmo sem bindings manuais.
  - Leitura de estados contínuos via `.IsPressed()` para `Sprint` e `Crouch`.
  - Atalhos numéricos de 1 a 5 e leitura de roda do mouse (`Mouse.current.scroll`) para seleção de Hotbar.
- **`Assets/Scripts/Player/PlayerLook.cs`:**
  - Rotação vertical da câmera (Pitch) com clamp (-85° a 85°).
  - Rotação horizontal do corpo do jogador (Yaw).
  - Suporte a `invertX` e `invertY` configuráveis no Inspector.
  - Gerenciamento de trava do cursor (`Cursor.lockState = Locked`) com liberação por tecla `ESC` para debug.
- **`Assets/Scripts/Player/PlayerMovement.cs`:**
  - Movimentação com `Rigidbody` customizado (`Interpolate`, `Continuous`, `FreezeRotation`).
  - Criação automática em runtime de `PhysicsMaterial` sem atrito (`dynamicFriction = 0`, `staticFriction = 0`, `frictionCombine = Minimum`) para evitar travamento em rampas e bordas.
  - **Ground Check Robusto:** `Physics.SphereCastNonAlloc` ignorando colisor próprio do player + fallback com `OnCollisionStay`.
  - **Física no Solo (Snappy):** Aceleração de 55 m/s² e frenagem de 65 m/s² (fim da sensação de gelo). Projeção 3D tangencial em rampas (`Vector3.ProjectOnPlane`), permitindo subir e descer ladeiras fluidamente.
  - **Bunny Hopping & Air Strafe:** Algoritmo clássico estilo Quake/Source/Muck com preservação de momento linear no ar.
  - **Pulo:** Com tolerância de buffer (*Jump Buffer* = 0.15s) e tolerância de queda (*Coyote Time* = 0.15s).
  - **Slide (Deslize):** Ativado com `Ctrl` em velocidade ou durante corrida. Reduz altura do colisor pela metade, diminui altura da câmera e aplica aceleração tangencial ladeira abaixo em declives.
  - **Telemetria e HUD On-Screen:** Painel visual no canto superior esquerdo exibindo em tempo real: Estado, Velocidade Horizontal (m/s), Solo (`SIM`/`NÃO`), Inclinação (Slope em graus), Velocidade vertical e linha de Bônus Roguelike em tempo real (`ROGUELIKE: +XX% Vel | +YY% Pulo`).

### Fase 2: Geração Procedural de Terreno & Recursos (Concluída)
- **`Assets/Scripts/World/PoissonDiscSampler.cs`:**
  - Algoritmo de amostragem de Poisson 2D de Bridson para garantir distribuição uniforme e distância mínima entre recursos (sem sobreposição).
- **`Assets/Scripts/World/ProceduralIslandGenerator.cs`:**
  - Geração de malha procedural 3D (`MeshFilter` e `MeshCollider`) com múltiplas oitavas de Perlin Noise fractal.
  - **Falloff de Praia Suave:** Curva contínua com `SmoothStep` que faz as colinas descerem suavemente para praias planas ($Y \approx 1$m) e mergulharem no mar ($Y \to -4$m) sem paredes ou penhascos na beira da água.
  - **Malha Flat-Shaded (Estilo Cartoon Muck):** Triângulos desacoplados sem interpolação de vértices, gerando as facetas geométricas características do jogo (zero borrão).
  - **Degraus Discretos de Altitude (Sem Borrão):**
    - $Y \le 3.0$m: Amarelo Areia Cartoon Vivo (`#FAD13D`).
    - $3.0$m $< Y \le 15.0$m: Verde Grama Muck Vibrante (`#42D13D`).
    - $15.0$m $< Y \le 22.0$m: Rocha Cinza Estilizada (`#6B7580`).
    - $Y > 22.0$m: Branco Neve Pura (`#FAFCFF`).
  - Posicionamento inteligente de recursos por Raycast e filtros de altura/inclinação (árvores nas gramas suaves, rochas em altitudes/declives, baús em platôs).
  - Localização e spawn seguro do jogador no centro plano da ilha.
- **`Assets/Shaders/URP_TerrainVertexLit.shader`:**
  - Shader URP customizado para leitura de cores de vértices com degrau de iluminação Cel-Shading (Muck style) e luz ambiente integrada.

### Fase 3: Coleta, Recursos, Ferramentas e Inventário (Concluída)
- **`Assets/Scripts/Items/ItemData.cs`:**
  - ScriptableObject Data-Driven para itens, ferramentas, armas e consumíveis.
  - Campos: ID, Nome, Ícone, Categoria (`Resource`, `Tool`, `Weapon`, `Consumable`), Tipo de Ferramenta (`None`, `Axe`, `Pickaxe`, `Sword`), Tier (0 a 6), Dano, Alcance, Cooldown de ataque e Stack máximo.
- **`Assets/Scripts/Items/ItemPickup.cs`:**
  - Objeto físico 3D no chão com `Rigidbody` e `BoxCollider` anti-deslizamento (`dynamicFriction = 0.9`, `staticFriction = 0.95`, `linearDamping = 2.5`, `angularDamping = 3.5`).
  - Impulso radial (*pop velocity*) ao quebrar recursos.
  - Atração magnética suave ao se aproximar do jogador (raio 4.5m) e absorção direta no inventário (`collectDistance = 1.35m`).
  - Detecção via `FindFirstObjectByType<PlayerInventory>()` (independente de tags).
- **`Assets/Scripts/Inventory/InventorySlot.cs` & `PlayerInventory.cs`:**
  - Sistema de 20 slots (5 da Hotbar + 15 de mochila).
  - Seleção ágil de Hotbar via teclas 1 a 5 e roda do mouse (`scroll`).
  - Empunhadura procedural em primeira pessoa do item ativo na mão (`HandHoldPoint`).
  - Empilhamento automático e verificação de capacidade.
- **`Assets/Scripts/Harvesting/ResourceNode.cs`:**
  - Árvores e Rochas com sistema de vida e verificação de Tier.
  - Lógica de Tier estrita: ferramentas de Tier igual ou superior causam dano total; ferramentas inferiores causam dano residual (1). Bônus para ferramenta correta (Machado para árvore, Picareta para pedra).
  - **Juice de Impacto:** Tremor procedural (*wobble*) ao ser atingido e explosão radial de drops físicos ao ser destruído.
  - **Barra de Vida Flutuante:** Renderizada via OnGUI sobre árvores (3.0m) e pedras (1.6m), com cor dinâmica (Verde $\to$ Vermelho) e valor numérico (`Tree [30/30]`).
- **`Assets/Scripts/Player/PlayerInteraction.cs`:**
  - Ataque e coleta via Raycast / SphereCast do centro da câmera.
  - Animação procedural de balanço da ferramenta / mão (*procedural swing*).
  - Controle de cadência (*cooldown*) e alcance dinâmico de acordo com a ferramenta equipada.
- **`Assets/Scripts/Inventory/HotbarUI.cs`:**
  - Interface OnGUI renderizada em tempo real com os 5 slots da Hotbar na base da tela.
  - Borda dourada no slot ativo, exibição de nome, quantidade, tag de Tier (`[T0]`, `[T1]`) e mira central (+).

### Fase 4: Combate, Inimigos e IA Roguelike (Concluída)
- **`Assets/Scripts/Combat/IDamageable.cs`:**
  - Interface desacoplada para dano e knockback físico.
- **`Assets/Scripts/Combat/PlayerHealth.cs`:**
  - Gerenciamento de Vida (100 HP), Estamina (100) e Fome (100%).
  - Dreno de fome ao longo do tempo e regeneração de vida passiva quando alimentado.
  - Flash de dano vermelho na tela e knockback físico aplicado ao `Rigidbody` do jogador.
  - Método `Heal(amount)` para suporte a consumíveis e roubo de vida (*Lifesteal*).
  - HUD clássico do Muck no canto inferior esquerdo com as 3 barras coloridas (Vermelho, Laranja e Amarelo).
- **`Assets/Scripts/Enemies/EnemyData.cs`:**
  - ScriptableObject para atributos de monstros (vida, velocidade, dano, cadência, alcance, raio de aggro, custo de orçamento noturno e drops).
- **`Assets/Scripts/Enemies/EnemyHealth.cs`:**
  - Barra de vida flutuante OnGUI permanente visível a até 40m (`Goblin [65/65]`).
  - Flash vermelho de dano e explosão de drops físicos ao morrer.
- **`Assets/Scripts/Enemies/EnemyController.cs`:**
  - Máquina de estados: `Idle`, `Chase`, `Attack`, `Knockback`.
  - Navegação fluida com `NavMeshAgent`.
  - Ataque corpo a corpo com antecipação visual (*windup*), dano e knockback no jogador.
  - Reação a impactos: o monstro é empurrado fisicamente pelos golpes do jogador e retoma a perseguição enfurecido.
- **`Assets/Scripts/Player/PlayerInteraction.cs`:**
  - Integrado com `IDamageable`: acerta tanto nós de recursos quanto inimigos, causando dano, aplicando knockback e ativando Lifesteal.
- **Runtime NavMesh & Spawn de Teste:**
  - `ProceduralIslandGenerator` constrói o `NavMeshSurface` dinamicamente após gerar o terreno.
  - Spawna 1 Goblin frontal direto a 8 metros do jogador e mais 3 em círculo.
  - Tecla de Debug `G`: Spawna instantaneamente um Goblin 5m à frente da câmera.

### Fase 5: Motor de Power-ups Roguelike e Baús (Concluída)
- **`Assets/Scripts/PowerUps/StatType.cs` & `StatModifier.cs`:**
  - Enum de atributos: `MoveSpeed`, `AttackDamage`, `AttackSpeed`, `JumpForce`, `CritChance`, `CritMultiplier`, `Lifesteal`, `MaxHealth`, `HealthRegen`.
  - Estrutura serializável de modificadores acumuláveis (`Flat` e `Percent`).
- **`Assets/Scripts/PowerUps/PowerUpData.cs`:**
  - ScriptableObject Data-Driven para criação de Power-ups com raridade, cores e descrições.
- **`Assets/Scripts/PowerUps/PlayerStats.cs`:**
  - Motor central Roguelike: $\text{ValorFinal} = (\text{Base} + \sum \text{Flat}) \times (1 + \sum \text{Percent})$.
  - Banner animado no topo da tela ao coletar e HUD lateral direito com badges dos Power-ups acumulados.
- **Acesso Lazy aos Stats (Resolução de Ciclo de Vida):**
  - Implementação de propriedades dinâmicas `Stats` em `PlayerMovement` e `PlayerInteraction`, garantindo que referências adicionadas em tempo de execução nunca fiquem nulas mesmo após o `Awake()`.
- **Valores Calibrados na Escala Muck:**
  - *Dani's Sneaker*: +40% de Velocidade e +35% de Pulo por stack.
  - *Juice*: +40% de Velocidade de Ataque (reduz cooldown pela metade).
  - *Horseshoe*: +25% de Chance Crítica por ferradura.
  - *Crimson Dagger*: +25% de Roubo de Vida (*Lifesteal*).
  - *Dumbbell*: +50% de Dano de Ataque bruto.
- **`Assets/Scripts/Interactables/IInteractable.cs` & `ChestInteractable.cs`:**
  - Sistema de interação com tecla `E` e prompt na mira (`[E] Abrir Baú`).
  - Animação procedural da tampa abrindo em 90° e ejeção física do orbe de Power-up.
- **`Assets/Scripts/PowerUps/PowerUpPickup.cs`:**
  - Orbe flutuante e rotativo com atração magnética e absorção de bônus ao se aproximar.
- **Facilidades de Teste:**
  - Baú inicial gerado automaticamente ao lado do jogador (`Chest_Inicial`) a 4.5 metros.
  - Tecla de Debug `P`: Concede instantaneamente um Power-up aleatório ao jogador para teste de acúmulo.

---

## 3. Próximo Passo Imediato (Fase 6 do Roadmap)

Na próxima etapa, implementaremos a **Fase 6: Ciclo Dia/Noite, Spawner de Hordas por Orçamento Matemático e Chefes**:
1. **Ciclo Dia/Noite (`DayNightCycle.cs`):**
   - Rotação suave do sol (Directional Light), alterando luz e sombras (Dia: 180s, Noite: 120s).
   - HUD mostrando o dia atual ("Dia 1") e o relógio/ícone solar/lunar.
2. **Diretor de Hordas por Orçamento Matemático (`HordeDirector.cs`):**
   - Orçamento noturno escalonado: $\text{Budget}(D) = 50 + 30 \times D^{1.4}$.
   - Gastos: Goblin = 10 pts, Golem de Pedra = 40 pts, Chefe Bob/Big Golem = 200 pts.
   - Spawns ao redor do jogador fora do campo de visão direto.
3. **Escalonamento Matemático de Monstros:**
   - Vida: $M_{HP}(D) = 1.0 + 0.35 \times (D - 1)^{1.25}$.
   - Dano: $M_{DMG}(D) = 1.0 + 0.20 \times (D - 1)$.
