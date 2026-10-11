using System.Collections.Generic;
using MuckClone.Combat;
using MuckClone.Enemies;
using MuckClone.Harvesting;
using MuckClone.Interactables;
using MuckClone.Inventory;
using MuckClone.Items;
using MuckClone.Player;
using MuckClone.PowerUps;
using Unity.AI.Navigation;
using UnityEngine;

namespace MuckClone.World
{
    /// <summary>
    /// Gerador procedural de ilha 3D estilizada Low Poly / Cartoon (estilo Muck).
    /// Gera malha Flat-Shaded (faces discretas sem borrão), cores vibrantes com degraus definidos,
    /// transição suave de praias para o oceano, constrói NavMesh em runtime e instancia recursos e inimigos.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class ProceduralIslandGenerator : MonoBehaviour
    {
        [Header("Dimensões do Terreno")]
        [Tooltip("Tamanho total da ilha em unidades do mundo (largura e profundidade)")]
        [SerializeField] private int mapSize = 200;
        [Tooltip("Resolução de blocos (quads por eixo) - 60 a 90 é ideal para Low Poly nítido")]
        [Range(30, 120)]
        [SerializeField] private int resolution = 75;
        [Tooltip("Altura máxima dos picos montanhosos")]
        [SerializeField] private float maxHeight = 26f;

        [Header("Perlin Noise (Relevo)")]
        [SerializeField] private int seed = 42;
        [SerializeField] private float noiseScale = 0.016f;
        [Range(1, 6)]
        [SerializeField] private int octaves = 4;
        [Range(0.1f, 1f)]
        [SerializeField] private float persistence = 0.45f;
        [SerializeField] private float lacunarity = 2.1f;
        [SerializeField] private Vector2 noiseOffset = Vector2.zero;

        [Header("Transição Suave de Praia & Mar (Falloff)")]
        [Tooltip("Ponto relativo do raio onde as colinas começam a declinar suavemente para a praia")]
        [Range(0.4f, 0.85f)]
        [SerializeField] private float beachStartRatio = 0.60f;
        [Tooltip("Profundidade onde a margem afunda no oceano nas extremidades")]
        [SerializeField] private float oceanEdgeDepth = -4f;

        [Header("Cores Vivas e Biomas Discretos (Cartoon Muck)")]
        [Tooltip("Amarelo Areia Cartoon")]
        [SerializeField] private Color sandColor = new Color(0.98f, 0.82f, 0.24f);
        [Tooltip("Verde Grama Cartoon Muck")]
        [SerializeField] private Color grassColor = new Color(0.26f, 0.82f, 0.24f);
        [Tooltip("Cinza Rocha Estilizado")]
        [SerializeField] private Color rockColor = new Color(0.42f, 0.46f, 0.50f);
        [Tooltip("Branco Neve Pura")]
        [SerializeField] private Color snowColor = new Color(0.98f, 0.99f, 1.0f);

        [Header("Degraus Discretos de Altitude (Sem Gradiente/Borrão)")]
        [Tooltip("Abaixo desta altura = Areia")]
        [SerializeField] private float sandHeightThreshold = 3.0f;
        [Tooltip("Abaixo desta altura = Verde Grama")]
        [SerializeField] private float grassHeightThreshold = 15.0f;
        [Tooltip("Abaixo desta altura = Rocha / Acima = Neve")]
        [SerializeField] private float rockHeightThreshold = 22.0f;

        [Header("Distribuição de Recursos (Poisson Disc)")]
        [Tooltip("Distância mínima entre recursos instanciados")]
        [SerializeField] private float resourceSpacing = 7f;
        [SerializeField] private Transform resourcesParent;

        [Header("Itens de Coleta (Opcional - gera em tempo de execução se vazio)")]
        [SerializeField] private ItemData woodItemData;
        [SerializeField] private ItemData stoneItemData;
        [SerializeField] private ItemData starterAxeData;
        [SerializeField] private ItemData starterPickaxeData;

        [Header("Prefabs de Recursos")]
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private GameObject rockPrefab;
        [SerializeField] private GameObject chestPrefab;

        [Header("Inimigos de Teste")]
        [SerializeField] private EnemyData goblinEnemyData;
        [SerializeField] private int initialGoblinCount = 3;

        [Header("Spawn do Jogador")]
        [Tooltip("Transform do Player para posicionar no centro seguro da ilha")]
        [SerializeField] private Transform playerTransform;

        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private MeshRenderer _meshRenderer;
        private Mesh _generatedMesh;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();
            _meshRenderer = GetComponent<MeshRenderer>();
            EnsureTerrainMaterial();
        }

        private void Start()
        {
            GenerateIsland();
        }

        private void EnsureTerrainMaterial()
        {
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

            Shader vertexLitShader = Shader.Find("Custom/URP_TerrainVertexLit");
            if (vertexLitShader != null)
            {
                if (_meshRenderer.sharedMaterial == null || _meshRenderer.sharedMaterial.shader != vertexLitShader)
                {
                    var mat = new Material(vertexLitShader) { name = "IslandTerrain_CartoonMat" };
                    _meshRenderer.sharedMaterial = mat;
                }
            }
        }

        [ContextMenu("Regenerar Ilha")]
        public void GenerateIsland()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshCollider == null) _meshCollider = GetComponent<MeshCollider>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

            EnsureTerrainMaterial();
            BuildFlatShadedMesh();
            BakeNavMesh();
            SpawnResources();
            SetupPlayerSystems();
            SpawnInitialTestEnemies();
        }

        private void BuildFlatShadedMesh()
        {
            _generatedMesh = new Mesh();
            _generatedMesh.name = "ProceduralIslandMesh_FlatShaded";
            _generatedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            float stepSize = (float)mapSize / resolution;
            float halfSize = mapSize * 0.5f;

            var prng = new System.Random(seed);
            Vector2[] octaveOffsets = new Vector2[octaves];
            for (int i = 0; i < octaves; i++)
            {
                float offsetX = prng.Next(-10000, 10000) + noiseOffset.x;
                float offsetY = prng.Next(-10000, 10000) + noiseOffset.y;
                octaveOffsets[i] = new Vector2(offsetX, offsetY);
            }

            float[,] heightMap = new float[resolution + 1, resolution + 1];
            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float worldX = x * stepSize - halfSize;
                    float worldZ = z * stepSize - halfSize;

                    float amplitude = 1f;
                    float frequency = noiseScale;
                    float noiseHeight = 0f;
                    float maxPossibleHeight = 0f;

                    for (int o = 0; o < octaves; o++)
                    {
                        float sampleX = (worldX + octaveOffsets[o].x) * frequency;
                        float sampleZ = (worldZ + octaveOffsets[o].y) * frequency;
                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);

                        noiseHeight += perlinValue * amplitude;
                        maxPossibleHeight += amplitude;
                        amplitude *= persistence;
                        frequency *= lacunarity;
                    }

                    float rawHeight = (noiseHeight / maxPossibleHeight) * maxHeight;

                    float dist = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);
                    float normDist = dist / halfSize;

                    float finalY;
                    if (normDist <= beachStartRatio)
                    {
                        finalY = rawHeight;
                    }
                    else
                    {
                        float t = Mathf.Clamp01((normDist - beachStartRatio) / (1f - beachStartRatio));
                        float smoothT = Mathf.SmoothStep(0f, 1f, t);
                        float targetCoast = Mathf.Lerp(rawHeight, 1.2f, smoothT * 0.7f);
                        finalY = Mathf.Lerp(targetCoast, oceanEdgeDepth, smoothT);
                    }

                    heightMap[x, z] = finalY;
                }
            }

            int totalQuads = resolution * resolution;
            int totalVertices = totalQuads * 6;

            Vector3[] vertices = new Vector3[totalVertices];
            Vector2[] uvs = new Vector2[totalVertices];
            Color[] colors = new Color[totalVertices];
            int[] triangles = new int[totalVertices];

            int vIdx = 0;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float x0 = x * stepSize - halfSize;
                    float x1 = (x + 1) * stepSize - halfSize;
                    float z0 = z * stepSize - halfSize;
                    float z1 = (z + 1) * stepSize - halfSize;

                    float y00 = heightMap[x, z];
                    float y10 = heightMap[x + 1, z];
                    float y01 = heightMap[x, z + 1];
                    float y11 = heightMap[x + 1, z + 1];

                    Vector3 p00 = new Vector3(x0, y00, z0);
                    Vector3 p10 = new Vector3(x1, y10, z0);
                    Vector3 p01 = new Vector3(x0, y01, z1);
                    Vector3 p11 = new Vector3(x1, y11, z1);

                    // Triângulo 1
                    float avgHeightT1 = (y00 + y01 + y10) / 3f;
                    Color colorT1 = GetDiscreteColor(avgHeightT1);

                    vertices[vIdx] = p00; uvs[vIdx] = new Vector2(0, 0); colors[vIdx] = colorT1; triangles[vIdx] = vIdx; vIdx++;
                    vertices[vIdx] = p01; uvs[vIdx] = new Vector2(0, 1); colors[vIdx] = colorT1; triangles[vIdx] = vIdx; vIdx++;
                    vertices[vIdx] = p10; uvs[vIdx] = new Vector2(1, 0); colors[vIdx] = colorT1; triangles[vIdx] = vIdx; vIdx++;

                    // Triângulo 2
                    float avgHeightT2 = (y10 + y01 + y11) / 3f;
                    Color colorT2 = GetDiscreteColor(avgHeightT2);

                    vertices[vIdx] = p10; uvs[vIdx] = new Vector2(1, 0); colors[vIdx] = colorT2; triangles[vIdx] = vIdx; vIdx++;
                    vertices[vIdx] = p01; uvs[vIdx] = new Vector2(0, 1); colors[vIdx] = colorT2; triangles[vIdx] = vIdx; vIdx++;
                    vertices[vIdx] = p11; uvs[vIdx] = new Vector2(1, 1); colors[vIdx] = colorT2; triangles[vIdx] = vIdx; vIdx++;
                }
            }

            _generatedMesh.vertices = vertices;
            _generatedMesh.triangles = triangles;
            _generatedMesh.uv = uvs;
            _generatedMesh.colors = colors;
            _generatedMesh.RecalculateNormals();
            _generatedMesh.RecalculateBounds();

            _meshFilter.sharedMesh = _generatedMesh;
            _meshCollider.sharedMesh = _generatedMesh;
        }

        private void BakeNavMesh()
        {
            var navSurface = GetComponent<NavMeshSurface>() ?? gameObject.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;
            navSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            navSurface.BuildNavMesh();
        }

        private Color GetDiscreteColor(float height)
        {
            if (height <= sandHeightThreshold) return sandColor;
            if (height <= grassHeightThreshold) return grassColor;
            if (height <= rockHeightThreshold) return rockColor;
            return snowColor;
        }

        private void SpawnResources()
        {
            if (resourcesParent != null)
            {
                while (resourcesParent.childCount > 0)
                {
                    DestroyImmediate(resourcesParent.GetChild(0).gameObject);
                }
            }
            else
            {
                GameObject parentGo = GameObject.Find("IslandResources");
                if (parentGo == null) parentGo = new GameObject("IslandResources");
                resourcesParent = parentGo.transform;
            }

            Vector2 regionSize = new Vector2(mapSize, mapSize);
            List<Vector2> samplePoints = PoissonDiscSampler.GeneratePoints(resourceSpacing, regionSize, 30, seed);
            float halfSize = mapSize * 0.5f;

            foreach (Vector2 point in samplePoints)
            {
                float worldX = point.x - halfSize;
                float worldZ = point.y - halfSize;
                Vector3 rayOrigin = new Vector3(worldX, maxHeight + 20f, worldZ);

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxHeight + 50f))
                {
                    float y = hit.point.y;
                    float slope = Vector3.Angle(Vector3.up, hit.normal);

                    // 1. Árvores: grama suave (3 < y < 15)
                    if (y > sandHeightThreshold + 0.2f && y < grassHeightThreshold && slope < 28f)
                    {
                        var treeGo = InstantiateResource(treePrefab, PrimitiveType.Cylinder, hit.point, hit.normal, new Vector3(0.85f, 2.8f, 0.85f), new Color(0.18f, 0.55f, 0.15f), "Tree");
                        var node = treeGo.GetComponent<ResourceNode>() ?? treeGo.AddComponent<ResourceNode>();
                        node.Initialize(ResourceType.Tree, ToolType.Axe, 0, 30f, GetOrCreateWoodData(), 3, 5);
                    }
                    // 2. Rochas / Minérios: rocha ou declive
                    else if ((y >= grassHeightThreshold && y < maxHeight - 2f) || (y > sandHeightThreshold && slope >= 30f && slope <= 48f))
                    {
                        var rockGo = InstantiateResource(rockPrefab, PrimitiveType.Sphere, hit.point, hit.normal, new Vector3(1.6f, 1.3f, 1.6f), new Color(0.40f, 0.44f, 0.48f), "Rock");
                        var node = rockGo.GetComponent<ResourceNode>() ?? rockGo.AddComponent<ResourceNode>();
                        node.Initialize(ResourceType.Rock, ToolType.Pickaxe, 1, 45f, GetOrCreateStoneData(), 3, 6);
                    }
                    // 3. Baús: praia ou pradarias planas
                    else if (y > 2.0f && slope < 10f)
                    {
                        float roll = (float)new System.Random((int)(point.x * 100 + point.y)).NextDouble();
                        if (roll < 0.16f)
                        {
                            var chestGo = InstantiateResource(chestPrefab, PrimitiveType.Cube, hit.point + Vector3.up * 0.35f, hit.normal, new Vector3(1f, 0.7f, 1f), new Color(0.95f, 0.65f, 0.15f), "Chest");
                            chestGo.AddComponent<ChestInteractable>();
                        }
                    }
                }
            }
        }

        private GameObject InstantiateResource(GameObject prefab, PrimitiveType fallbackPrimitive, Vector3 position, Vector3 normal, Vector3 scale, Color fallbackColor, string name)
        {
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, position, Quaternion.identity, resourcesParent);
            }
            else
            {
                go = GameObject.CreatePrimitive(fallbackPrimitive);
                go.name = name;
                go.transform.position = position;
                go.transform.localScale = scale;
                go.transform.SetParent(resourcesParent);
                go.transform.up = Vector3.Lerp(Vector3.up, normal, 0.35f);

                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    renderer.material.color = fallbackColor;
                }
            }
            return go;
        }

        private void SetupPlayerSystems()
        {
            if (playerTransform == null)
            {
                var pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null) playerTransform = pm.transform;
                else
                {
                    var p = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                    if (p != null) playerTransform = p.transform;
                }
            }

            if (playerTransform == null) return;

            // 1. Garante que os componentes de vida, interação, inventário, stats Roguelike e HUD existam no Player
            var playerHealth = playerTransform.GetComponent<PlayerHealth>() ?? playerTransform.gameObject.AddComponent<PlayerHealth>();
            var playerStats = playerTransform.GetComponent<PlayerStats>() ?? playerTransform.gameObject.AddComponent<PlayerStats>();
            var inventory = playerTransform.GetComponent<PlayerInventory>() ?? playerTransform.gameObject.AddComponent<PlayerInventory>();
            var interaction = playerTransform.GetComponent<PlayerInteraction>() ?? playerTransform.gameObject.AddComponent<PlayerInteraction>();
            var hotbarUI = playerTransform.GetComponent<HotbarUI>() ?? playerTransform.gameObject.AddComponent<HotbarUI>();

            // Equipamentos iniciais de sobrevivência na Hotbar para teste imediato
            if (inventory.GetSlot(0).IsEmpty)
            {
                inventory.AddItem(GetOrCreateStarterAxeData(), 1);
            }
            if (inventory.GetSlot(1).IsEmpty)
            {
                inventory.AddItem(GetOrCreateStarterPickaxeData(), 1);
            }

            // 2. Posiciona o Player em solo firme seguro no centro
            for (float r = 0; r < 30f; r += 2f)
            {
                Vector3 origin = new Vector3(r, maxHeight + 25f, r);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxHeight + 60f))
                {
                    float slope = Vector3.Angle(Vector3.up, hit.normal);
                    if (hit.point.y > sandHeightThreshold && slope < 10f)
                    {
                        playerTransform.position = hit.point + Vector3.up * 2f;
                        var rb = playerTransform.GetComponent<Rigidbody>();
                        if (rb != null) rb.linearVelocity = Vector3.zero;
                        break;
                    }
                }
            }
        }

        private void SpawnInitialTestEnemies()
        {
            if (playerTransform == null)
            {
                SetupPlayerSystems();
            }

            if (playerTransform == null) return;

            // 1. Spawna 1 Goblin DIRETO NA FRENTE DO JOGADOR (visível imediatamente ao dar Play)
            Vector3 frontSpot = playerTransform.position + playerTransform.forward * 8f;
            SpawnSingleGoblin(frontSpot, "Goblin_Frontal");

            // 2. Spawna 1 Baú de Teste ao lado do jogador para interação imediata com E
            Vector3 chestSpot = playerTransform.position + playerTransform.right * 4.5f;
            SpawnSingleChest(chestSpot, "Chest_Inicial");

            // 3. Spawna os demais Goblins em círculo ao redor (12m a 20m)
            for (int i = 0; i < initialGoblinCount; i++)
            {
                float angle = (i * (360f / initialGoblinCount) + 40f) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Random.Range(12f, 22f);
                Vector3 targetPos = playerTransform.position + offset;
                SpawnSingleGoblin(targetPos, $"Goblin_Circulo_{i + 1}");
            }
        }

        public GameObject SpawnSingleChest(Vector3 targetPos, string name = "Chest")
        {
            Vector3 rayOrigin = new Vector3(targetPos.x, maxHeight + 25f, targetPos.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxHeight + 60f))
            {
                GameObject chestGo = new GameObject(name);
                chestGo.transform.position = hit.point;
                chestGo.transform.SetParent(resourcesParent);

                var col = chestGo.AddComponent<BoxCollider>();
                col.size = new Vector3(1.2f, 0.9f, 0.9f);
                col.center = new Vector3(0, 0.45f, 0);

                chestGo.AddComponent<ChestInteractable>();

                Debug.Log($"[MuckClone] {name} spawnado em: {hit.point}");
                return chestGo;
            }
            return null;
        }

        public GameObject SpawnSingleGoblin(Vector3 targetPos, string name = "Goblin")
        {
            Vector3 rayOrigin = new Vector3(targetPos.x, maxHeight + 25f, targetPos.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxHeight + 60f))
            {
                Vector3 spawnPos = hit.point;

                // Garante que o ponto está sobre a malha de navegação (NavMesh)
                if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit navHit, 6f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    spawnPos = navHit.position;
                }

                GameObject enemyGo = new GameObject(name);
                enemyGo.transform.position = spawnPos;
                enemyGo.transform.SetParent(resourcesParent);

                var col = enemyGo.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.radius = 0.45f;
                col.center = new Vector3(0, 0.9f, 0);

                var rb = enemyGo.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                var agent = enemyGo.AddComponent<UnityEngine.AI.NavMeshAgent>();
                agent.height = 1.8f;
                agent.radius = 0.45f;
                agent.baseOffset = 0f;
                agent.Warp(spawnPos);

                var health = enemyGo.AddComponent<EnemyHealth>();
                health.Initialize(GetOrCreateGoblinData());

                var controller = enemyGo.AddComponent<EnemyController>();

                Debug.Log($"[MuckClone] {name} spawnado com sucesso na coordenada: {spawnPos}");
                return enemyGo;
            }

            return null;
        }

        private void Update()
        {
            // Tecla de Debug G: Spawna instantaneamente um Goblin na frente da câmera
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.gKey.wasPressedThisFrame)
            {
                if (playerTransform != null)
                {
                    Vector3 spawnPoint = playerTransform.position + playerTransform.forward * 5f;
                    SpawnSingleGoblin(spawnPoint, "Goblin_Debug_Manual");
                }
            }

            // Tecla de Debug P: Concede um Power-up aleatório ao jogador para testar acúmulo de bônus
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
            {
                if (playerTransform != null)
                {
                    var stats = playerTransform.GetComponent<PlayerStats>();
                    if (stats != null)
                    {
                        var chest = new GameObject("TempChest").AddComponent<ChestInteractable>();
                        var method = typeof(ChestInteractable).GetMethod("CreateDefaultPowerUp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (method != null)
                        {
                            var p = (PowerUpData)method.Invoke(chest, null);
                            stats.AddPowerUp(p, 1);
                        }
                        Destroy(chest.gameObject);
                    }
                }
            }
        }

        private ItemData GetOrCreateWoodData()
        {
            if (woodItemData == null)
            {
                woodItemData = ScriptableObject.CreateInstance<ItemData>();
                woodItemData.id = "wood";
                woodItemData.itemName = "Madeira";
                woodItemData.category = ItemCategory.Resource;
                woodItemData.maxStack = 64;
                woodItemData.fallbackColor = new Color(0.6f, 0.38f, 0.2f);
            }
            return woodItemData;
        }

        private ItemData GetOrCreateStoneData()
        {
            if (stoneItemData == null)
            {
                stoneItemData = ScriptableObject.CreateInstance<ItemData>();
                stoneItemData.id = "stone";
                stoneItemData.itemName = "Pedra";
                stoneItemData.category = ItemCategory.Resource;
                stoneItemData.maxStack = 64;
                stoneItemData.fallbackColor = new Color(0.5f, 0.52f, 0.55f);
            }
            return stoneItemData;
        }

        private ItemData GetOrCreateStarterAxeData()
        {
            if (starterAxeData == null)
            {
                starterAxeData = ScriptableObject.CreateInstance<ItemData>();
                starterAxeData.id = "starter_axe";
                starterAxeData.itemName = "Machado de Madeira";
                starterAxeData.category = ItemCategory.Tool;
                starterAxeData.toolType = ToolType.Axe;
                starterAxeData.tier = 0;
                starterAxeData.damage = 12f;
                starterAxeData.attackCooldown = 0.38f;
                starterAxeData.attackRange = 3.2f;
                starterAxeData.maxStack = 1;
                starterAxeData.fallbackColor = new Color(0.75f, 0.45f, 0.2f);
            }
            return starterAxeData;
        }

        private ItemData GetOrCreateStarterPickaxeData()
        {
            if (starterPickaxeData == null)
            {
                starterPickaxeData = ScriptableObject.CreateInstance<ItemData>();
                starterPickaxeData.id = "starter_pickaxe";
                starterPickaxeData.itemName = "Picareta de Madeira";
                starterPickaxeData.category = ItemCategory.Tool;
                starterPickaxeData.toolType = ToolType.Pickaxe;
                starterPickaxeData.tier = 1;
                starterPickaxeData.damage = 10f;
                starterPickaxeData.attackCooldown = 0.42f;
                starterPickaxeData.attackRange = 3.2f;
                starterPickaxeData.maxStack = 1;
                starterPickaxeData.fallbackColor = new Color(0.55f, 0.58f, 0.62f);
            }
            return starterPickaxeData;
        }

        private EnemyData GetOrCreateGoblinData()
        {
            if (goblinEnemyData == null)
            {
                goblinEnemyData = ScriptableObject.CreateInstance<EnemyData>();
                goblinEnemyData.enemyId = "goblin";
                goblinEnemyData.enemyName = "Goblin";
                goblinEnemyData.maxHealth = 65f;
                goblinEnemyData.moveSpeed = 5.2f;
                goblinEnemyData.attackDamage = 10f;
                goblinEnemyData.attackCooldown = 1.0f;
                goblinEnemyData.attackRange = 1.8f;
                goblinEnemyData.aggroRadius = 18f;
                goblinEnemyData.playerKnockbackForce = 9f;
                goblinEnemyData.knockbackVulnerability = 1.2f;
                goblinEnemyData.dropItem = GetOrCreateWoodData();
                goblinEnemyData.fallbackColor = new Color(0.2f, 0.72f, 0.25f);
            }
            return goblinEnemyData;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, new Vector3(mapSize, maxHeight, mapSize));
        }
    }
}
