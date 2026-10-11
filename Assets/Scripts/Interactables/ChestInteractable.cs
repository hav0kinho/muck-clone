using System.Collections;
using MuckClone.PowerUps;
using UnityEngine;

namespace MuckClone.Interactables
{
    /// <summary>
    /// Baú interativo gerado no mundo (estilo Muck).
    /// Ao interagir com a tecla 'E', abre a tampa e ejeta um orbe de Power-up Roguelike.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ChestInteractable : MonoBehaviour, IInteractable
    {
        [Header("Configuração do Baú")]
        [SerializeField] private string chestName = "Baú Comum";
        [SerializeField] private int coinCost = 0; // 0 = grátis
        [SerializeField] private PowerUpData[] possiblePowerUps;

        [Header("Referências Visuais")]
        [SerializeField] private Transform lidTransform;

        private bool _isOpened;
        private static GUIStyle _promptStyle;

        public bool IsOpened => _isOpened;

        private void Start()
        {
            if (transform.childCount == 0)
            {
                CreateFallbackChestVisual();
            }
        }

        public string GetInteractionPrompt()
        {
            if (_isOpened) return "";
            if (coinCost > 0) return $"[E] Abrir {chestName} ({coinCost} Moedas)";
            return $"[E] Abrir {chestName}";
        }

        public void Interact(GameObject player)
        {
            if (_isOpened) return;
            _isOpened = true;

            StartCoroutine(OpenChestRoutine(player));
        }

        private IEnumerator OpenChestRoutine(GameObject player)
        {
            // 1. Animação suave de abertura da tampa (rotaciona 90 graus para trás)
            if (lidTransform != null)
            {
                Quaternion startRot = lidTransform.localRotation;
                Quaternion openRot = Quaternion.Euler(-90f, 0, 0);

                float elapsed = 0f;
                float duration = 0.35f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    lidTransform.localRotation = Quaternion.Slerp(startRot, openRot, elapsed / duration);
                    yield return null;
                }
            }

            yield return new WaitForSeconds(0.1f);

            // 2. Ejeção do Power-up no ar
            SpawnPowerUpOrb();
        }

        private void SpawnPowerUpOrb()
        {
            PowerUpData chosenData = null;

            if (possiblePowerUps != null && possiblePowerUps.Length > 0)
            {
                chosenData = possiblePowerUps[Random.Range(0, possiblePowerUps.Length)];
            }
            else
            {
                // Fallback procedural: cria um dos Power-ups icônicos do Muck
                chosenData = CreateDefaultPowerUp();
            }

            Vector3 spawnPos = transform.position + Vector3.up * 1.4f;
            GameObject orbGo = new GameObject($"PowerUp_{chosenData.powerUpName}");
            var pickup = orbGo.AddComponent<PowerUpPickup>();
            pickup.Initialize(chosenData, spawnPos);
        }

        private PowerUpData CreateDefaultPowerUp()
        {
            // Seleciona aleatoriamente entre 5 clássicos do Muck
            int roll = Random.Range(0, 5);

            PowerUpData p = ScriptableObject.CreateInstance<PowerUpData>();
            switch (roll)
            {
                case 0:
                    p.id = "danis_sneaker";
                    p.powerUpName = "Dani's Sneaker";
                    p.description = "Aumenta a velocidade em +40% e pulo em +35%.";
                    p.themeColor = new Color(0.2f, 0.75f, 1.0f);
                    p.modifiers = new StatModifier[]
                    {
                        new StatModifier(StatType.MoveSpeed, ModifierType.Percent, 0.40f),
                        new StatModifier(StatType.JumpForce, ModifierType.Percent, 0.35f)
                    };
                    break;
                case 1:
                    p.id = "crimson_dagger";
                    p.powerUpName = "Crimson Dagger";
                    p.description = "Cura vida ao causar dano (Lifesteal 25%).";
                    p.themeColor = new Color(0.95f, 0.2f, 0.25f);
                    p.modifiers = new StatModifier[]
                    {
                        new StatModifier(StatType.Lifesteal, ModifierType.Flat, 0.25f)
                    };
                    break;
                case 2:
                    p.id = "horseshoe";
                    p.powerUpName = "Horseshoe";
                    p.description = "Aumenta a chance de acerto crítico em +25%.";
                    p.themeColor = new Color(1.0f, 0.85f, 0.2f);
                    p.modifiers = new StatModifier[]
                    {
                        new StatModifier(StatType.CritChance, ModifierType.Flat, 0.25f)
                    };
                    break;
                case 3:
                    p.id = "juice";
                    p.powerUpName = "Juice";
                    p.description = "Aumenta a velocidade de ataque em +40%.";
                    p.themeColor = new Color(0.95f, 0.5f, 0.15f);
                    p.modifiers = new StatModifier[]
                    {
                        new StatModifier(StatType.AttackSpeed, ModifierType.Percent, 0.40f)
                    };
                    break;
                default:
                    p.id = "dumbbell";
                    p.powerUpName = "Dumbbell";
                    p.description = "Aumenta o dano de ataque em +50%.";
                    p.themeColor = new Color(0.85f, 0.3f, 0.95f);
                    p.modifiers = new StatModifier[]
                    {
                        new StatModifier(StatType.AttackDamage, ModifierType.Percent, 0.50f)
                    };
                    break;
            }

            return p;
        }

        private void CreateFallbackChestVisual()
        {
            // Base do baú
            var baseBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(baseBox.GetComponent<Collider>());
            baseBox.transform.SetParent(transform, false);
            baseBox.transform.localScale = new Vector3(1.1f, 0.55f, 0.8f);
            baseBox.transform.localPosition = new Vector3(0, 0.28f, 0);

            // Tampa móvel (pivô na borda traseira)
            var lidPivot = new GameObject("ChestLid");
            lidPivot.transform.SetParent(transform, false);
            lidPivot.transform.localPosition = new Vector3(0, 0.55f, -0.4f);
            lidTransform = lidPivot.transform;

            var lidBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(lidBox.GetComponent<Collider>());
            lidBox.transform.SetParent(lidPivot.transform, false);
            lidBox.transform.localScale = new Vector3(1.15f, 0.35f, 0.85f);
            lidBox.transform.localPosition = new Vector3(0, 0.17f, 0.4f);

            // Material estilizado de madeira e borda dourada
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var matWood = new Material(s) { color = new Color(0.6f, 0.38f, 0.18f) };
            var matGold = new Material(s) { color = new Color(0.95f, 0.75f, 0.2f) };

            baseBox.GetComponent<Renderer>().material = matWood;
            lidBox.GetComponent<Renderer>().material = matGold;
        }

        private void OnGUI()
        {
            if (_isOpened) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist > 3.8f) return;

            // Checa se a câmera está apontando para o baú
            Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.8f);
            if (screenPos.z <= 0) return;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 chestScreen = new Vector2(screenPos.x, Screen.height - screenPos.y);

            if (Vector2.Distance(center, chestScreen) < 140f)
            {
                if (_promptStyle == null)
                {
                    _promptStyle = new GUIStyle(GUI.skin.box)
                    {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                }

                float width = 220f;
                float height = 28f;
                Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.5f + 35f, width, height);

                Color old = GUI.color;
                GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
                GUI.Box(rect, GetInteractionPrompt(), _promptStyle);
                GUI.color = old;
            }
        }
    }
}
