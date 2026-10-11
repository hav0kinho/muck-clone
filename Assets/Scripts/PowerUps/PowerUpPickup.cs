using UnityEngine;

namespace MuckClone.PowerUps
{
    /// <summary>
    /// Representa um orbe de Power-up ejetado de um baú ou chefe.
    /// Flutua no ar com rotação contínua e é absorvido ao toque pelo jogador.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PowerUpPickup : MonoBehaviour
    {
        [Header("Dados do Power-up")]
        [SerializeField] private PowerUpData powerUpData;

        [Header("Comportamento")]
        [SerializeField] private float floatSpeed = 2.5f;
        [SerializeField] private float floatAmplitude = 0.2f;
        [SerializeField] private float magnetDistance = 3.5f;
        [SerializeField] private float magnetSpeed = 10f;

        private Vector3 _startPosition;
        private Transform _targetPlayer;
        private bool _isCollected;

        public PowerUpData PowerUp => powerUpData;

        private void Start()
        {
            _startPosition = transform.position;

            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.6f;

            if (transform.childCount == 0)
            {
                CreateVisualOrb();
            }
        }

        public void Initialize(PowerUpData data, Vector3 spawnPosition)
        {
            powerUpData = data;
            transform.position = spawnPosition;
            _startPosition = spawnPosition;

            CreateVisualOrb();
        }

        private void CreateVisualOrb()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(orb.GetComponent<Collider>());
            orb.transform.SetParent(transform, false);
            orb.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            var rend = orb.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Color col = powerUpData != null ? powerUpData.themeColor : Color.cyan;
                rend.material = new Material(s) { color = col };
            }
        }

        private void Update()
        {
            if (_isCollected) return;

            // Rotação e flutuação vertical
            transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
            float newY = _startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // Procura o jogador por proximidade
            if (_targetPlayer == null)
            {
                var stats = FindFirstObjectByType<PlayerStats>();
                if (stats != null) _targetPlayer = stats.transform;
            }

            if (_targetPlayer != null)
            {
                float dist = Vector3.Distance(transform.position, _targetPlayer.position + Vector3.up);
                if (dist <= magnetDistance)
                {
                    transform.position = Vector3.MoveTowards(transform.position, _targetPlayer.position + Vector3.up, magnetSpeed * Time.deltaTime);
                    if (dist < 1.0f)
                    {
                        Collect(_targetPlayer.gameObject);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;

            var stats = other.GetComponentInParent<PlayerStats>();
            if (stats != null)
            {
                Collect(stats.gameObject);
            }
        }

        private void Collect(GameObject playerGo)
        {
            if (_isCollected) return;

            var stats = playerGo.GetComponent<PlayerStats>() ?? playerGo.GetComponentInParent<PlayerStats>();
            if (stats != null && powerUpData != null)
            {
                _isCollected = true;
                stats.AddPowerUp(powerUpData, 1);
                Destroy(gameObject);
            }
        }
    }
}
