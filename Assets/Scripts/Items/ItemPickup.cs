using UnityEngine;
using MuckClone.Inventory;

namespace MuckClone.Items
{
    /// <summary>
    /// Representa um item físico no chão que pode ser coletado pelo jogador.
    /// Inclui física anti-deslizamento (BoxCollider com alto atrito e amortecimento),
    /// e atração magnética direta com absorção garantida no inventário.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class ItemPickup : MonoBehaviour
    {
        [Header("Dados do Item")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int amount = 1;

        [Header("Comportamento Físico & Coleta")]
        [Tooltip("Tempo de espera inicial antes de permitir coleta (efeito pop)")]
        [SerializeField] private float pickupDelay = 0.25f;
        [Tooltip("Distância máxima para início da atração magnética")]
        [SerializeField] private float magnetDistance = 4.5f;
        [Tooltip("Distância mínima para absorção imediata no inventário")]
        [SerializeField] private float collectDistance = 1.35f;
        [Tooltip("Velocidade com que o item voa até o jogador")]
        [SerializeField] private float magnetSpeed = 12f;

        private Rigidbody _rb;
        private BoxCollider _col;
        private PlayerInventory _targetInventory;
        private float _spawnTime;
        private bool _isCollected;
        private bool _isBeingMagnetized;

        public ItemData Item => itemData;
        public int Amount => amount;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<BoxCollider>();
            _spawnTime = Time.time;

            ConfigurePhysics();
        }

        private void Start()
        {
            if (transform.childCount == 0 && GetComponent<MeshFilter>() == null)
            {
                CreateFallbackVisual();
            }
        }

        private void ConfigurePhysics()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_col == null) _col = GetComponent<BoxCollider>();

            _col.size = new Vector3(0.4f, 0.4f, 0.4f);

            // Material com alto atrito para fixar no solo e nunca deslizar indefinidamente
            PhysicsMaterial mat = new PhysicsMaterial("ItemFrictionMat")
            {
                dynamicFriction = 0.9f,
                staticFriction = 0.95f,
                bounciness = 0.25f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            _col.sharedMaterial = mat;

            // Amortecimento linear e angular para assentar o item rapidamente no chão
            _rb.linearDamping = 2.5f;
            _rb.angularDamping = 3.5f;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Initialize(ItemData data, int count, Vector3 popVelocity)
        {
            itemData = data;
            amount = count;
            _spawnTime = Time.time;

            ConfigurePhysics();

            _rb.linearVelocity = popVelocity;
            _rb.angularVelocity = Random.insideUnitSphere * 8f;

            CreateFallbackVisual();
        }

        private void CreateFallbackVisual()
        {
            if (itemData == null) return;

            // Remove visuais prévios duplicados
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            var visualGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(visualGo.GetComponent<Collider>());
            visualGo.transform.SetParent(transform, false);
            visualGo.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            var renderer = visualGo.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(shader) { color = itemData.fallbackColor };
                renderer.sharedMaterial = mat;
            }
        }

        private void Update()
        {
            // Rotação visual estilizada
            transform.Rotate(Vector3.up, 70f * Time.deltaTime, Space.World);

            if (_isCollected || Time.time < _spawnTime + pickupDelay) return;

            // Busca o PlayerInventory sem depender de Tags
            if (_targetInventory == null)
            {
                _targetInventory = FindFirstObjectByType<PlayerInventory>();
            }

            if (_targetInventory != null)
            {
                Vector3 playerChestPos = _targetInventory.transform.position + Vector3.up * 0.9f;
                float dist = Vector3.Distance(transform.position, playerChestPos);

                if (dist <= magnetDistance)
                {
                    // Ativa modo atração magnética
                    if (!_isBeingMagnetized)
                    {
                        _isBeingMagnetized = true;
                        _rb.isKinematic = true;
                        _col.isTrigger = true; // Atravessa obstáculos ao voar para o jogador
                    }

                    transform.position = Vector3.MoveTowards(transform.position, playerChestPos, magnetSpeed * Time.deltaTime);

                    if (dist <= collectDistance)
                    {
                        TryCollect(_targetInventory);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < _spawnTime + pickupDelay || _isCollected) return;

            var inv = other.GetComponentInParent<PlayerInventory>();
            if (inv != null)
            {
                TryCollect(inv);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < _spawnTime + pickupDelay || _isCollected) return;

            var inv = collision.gameObject.GetComponentInParent<PlayerInventory>();
            if (inv != null)
            {
                TryCollect(inv);
            }
        }

        private void TryCollect(PlayerInventory inventory)
        {
            if (_isCollected || inventory == null || itemData == null) return;

            int remaining = inventory.AddItem(itemData, amount);
            if (remaining <= 0)
            {
                _isCollected = true;
                Destroy(gameObject);
            }
            else
            {
                amount = remaining; // Se inventário estiver cheio, mantém o que sobrou
            }
        }
    }
}
