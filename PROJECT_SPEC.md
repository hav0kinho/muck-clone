# Clone Muck 3D - Especificação Técnica e Diretrizes de Desenvolvimento (Unity 6 / C#)

## 1. Visão Geral
- **Gênero:** Sobrevivência 3D Roguelike Arcade / Ação em primeira pessoa.
- **Engine:** Unity 6 (C#).
- **Render Pipeline recomendado:** Universal Render Pipeline (URP) - Leve, estilizado (Low Poly) e performático.
- **Inspirações:** Muck (Dani), Risk of Rain 2, movimentação estilo Source/Quake.

---

## 2. Pilares de Arquitetura de Software
- **Data-Driven (ScriptableObjects):** Itens, receitas, inimigos, biomas e power-ups como assets serializados.
- **Stat Modifier System:** Atributos desacoplados calculados por composição:
  `ValorFinal = (Base + Sum(FlatModifiers)) * (1 + Sum(PercentModifiers))`
- **Event-Driven:** `System.Action` nativas / SO Events. Sem `GetComponent<T>()` contínuo no `Update()`.
- **Input System:** Novo Unity Input System (`UnityEngine.InputSystem`).
- **Preparação Multiplayer:** Separar lógica pura (dados/eventos) da apresentação visual (`View`, partículas, áudio). Usar identificadores de entidade (`EntityID`) em vez de dependência direta de `transform`.

---

## 3. Módulos do Sistema

### Módulo 1: Geração Procedural de Mundo
- Chunks de malha procedural (`16x16` ou `32x32`).
- Perlin / Simplex Noise com máscara de Falloff radial (ilha circular afundando no mar).
- Normais calculadas dinamicamente.
- Distribuição de recursos via **Poisson Disc Sampling 2D** + Raycast vertical para normal e altura.
- Filtros de spawn:
  - Árvores/Grama: altura `2 < y < 25`, declive `< 30°`.
  - Minérios avançados: `y > 25` ou cavernas.
  - Baús/Totens: declive plano `< 15°`.

### Módulo 2: Movimentação do Jogador & Física
- `Rigidbody` customizado (`FreezeRotation X/Z`, `Interpolate`).
- Aceleração no solo com atrito linear dinâmico.
- Preservação de momento no ar (Bunny Hopping).
- Slide (deslize agachado): impulso tangencial à rampa, colisão com meia altura.
- Câmera em primeira pessoa desacoplada com clamp vertical (-90° a 90°).

### Módulo 3: Coleta, Inventário e Crafting
- **Recursos:** `RequiredToolType`, `Tier` (Madeira=0, Pedra=1, Ferro=2, Ouro=3, Mitril=4, Adamantite=5), `Health`.
- Verificação de Tier: se `ferramenta.Tier >= recurso.Tier`, aplica dano total; senão, dano residual (1) com som metálico.
- Inventário com `InventorySlot` (`ItemData`, `amount`), Hotbar (0 a 4).
- Crafting manual básico + estações (Workbench, Fornalha, Bigorna) por raio de proximidade (2.5m).

### Módulo 4: Combate e Dano
- Detecção: **Hybrid Overlap Volume** (`Physics.OverlapSphereNonAlloc` ativado por Animation Event em janela da animação).
- Prevenção de múltiplos acertos no mesmo golpe: `HashSet<IDamageable>`.
- Juice: Hitstop (congelamento ~0.03s no crítico), números flutuantes de dano (Damage Numbers), knockback vetorial:
  `Fknockback = (DirAtacanteAlvo + Up * 0.3) * Forca`

### Módulo 5: Power-ups Roguelike
- `PowerupEffectSO` modular com ganchos `OnApply`, `OnHit`, `OnKill`.
- Efeitos cumulativos (Stacks).
- Baús: Baú inicial grátis, Baús pagos com ouro dropado de monstros com custo escalonando.

### Módulo 6: Ciclo Dia/Noite & Escala Matemática
- Ciclo: ~180s dia / 120s noite.
- Fórmulas de dificuldade pelo dia $D$ ($D \ge 1$):
  - $M_{HP}(D) = 1.0 + 0.35 \times (D - 1)^{1.25}$
  - $M_{DMG}(D) = 1.0 + 0.20 \times (D - 1)$
  - $Budget(D) = 50 + 30 \times D^{1.4}$
- Bosses: Noite 3 a 5 (primeiro Boss obrigatório, ex: Big Chunk), Noite 6+ múltiplos/rápidos, Totens diurnos para invocar sob demanda.

### Módulo 7: IA de Inimigos
- Unity NavMesh Building Components (`NavMeshSurface.BuildNavMesh()` em runtime após gerar o terreno).
- FSM desacoplada: `Idle/Patrol`, `Chase`, `Attack`, `Stagger/Knockback`.

---

## 4. Roadmap de Execução
1. **Fase 1:** Player Controller, Câmera e Física 3D (Rigidbody + Novo Input System).
2. **Fase 2:** Geração Procedural de Terreno e Recursos (Perlin + Falloff + Poisson).
3. **Fase 3:** Sistema de Coleta, Ferramentas, Inventário e Crafting.
4. **Fase 4:** Combate, Hurtboxes/Hitboxes, Inimigos Básicos e NavMesh em Runtime.
5. **Fase 5:** Power-ups Roguelike, Stat Modifiers e Baús.
6. **Fase 6:** Ciclo Dia/Noite, Spawner de Hordas e Bosses.
7. **Fase 7:** Refinamento, Game Feel (Juice), Áudio e UI.
