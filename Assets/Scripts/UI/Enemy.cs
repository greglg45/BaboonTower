using UnityEngine;
using System.Collections.Generic;
using BaboonTower.Network;

namespace BaboonTower.Game
{
    public enum EnemyType
    {
        Small,
        Medium,
        High
    }

    public class Enemy : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EnemyType enemyType = EnemyType.Small;

        [Header("Stats")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private int maxHealth = 10;
        [SerializeField] private int currentHealth;
        [SerializeField] private int damage = 1;
        [SerializeField] private int goldReward = 5;

        [Header("Visual")]
        [SerializeField] private Color enemyColor = Color.red;
        private GameObject visual;
        private GameObject healthBar;
        private GameObject healthBarBg;

        [Header("Path Following")]
        [SerializeField] private List<Vector3> pathToFollow;
        [SerializeField] private int currentWaypointIndex = 0;
        [SerializeField] private float waypointReachDistance = 0.1f;

        // References
        private GameController gameController;
        private bool isInitialized = false;

        // Events
        public System.Action<Enemy> OnEnemyReachedEnd;
        public System.Action<Enemy, int> OnEnemyKilled;

        /// <summary>
        /// Nouvelle méthode d'initialisation pour MapLoaderV3
        /// </summary>
        public void InitializeForMapV3(EnemyType type, List<Vector3> path, GameController controller)
        {
            enemyType = type;
            gameController = controller;

            // Configurer les stats selon le type
            SetupStatsForType();

            // Configurer le chemin
            if (path != null && path.Count > 0)
            {
                pathToFollow = new List<Vector3>(path);

                // Positionner au spawn
                Vector3 startPos = pathToFollow[0];
                startPos.z = -1f; // Devant la map
                transform.position = startPos;
                currentWaypointIndex = 0;

                Debug.Log($"Enemy initialized with path of {pathToFollow.Count} waypoints. Starting at {startPos}");
            }
            else
            {
                Debug.LogError("No path provided for enemy!");
            }

            // Créer le visuel
            CreateVisual();
            CreateHealthBar();

            isInitialized = true;
        }

        private void Awake()
        {
            if (!isInitialized)
            {
                SetupStatsForType();
            }
        }

        private void Update()
        {
            if (isInitialized && pathToFollow != null && pathToFollow.Count > 0)
            {
                FollowPath();
                UpdateHealthBarPosition();
            }
        }

        /// <summary>
        /// Configure les stats selon le type d'ennemi (selon le GDD)
        /// </summary>
        private void SetupStatsForType()
        {
            switch (enemyType)
            {
                case EnemyType.Small:
                    moveSpeed = 3f;
                    maxHealth = 5;
                    damage = 1;
                    goldReward = 3;
                    enemyColor = new Color(0.5f, 1f, 0.5f); // Vert clair
                    break;

                case EnemyType.Medium:
                    moveSpeed = 2f;
                    maxHealth = 10;
                    damage = 2;
                    goldReward = 5;
                    enemyColor = new Color(1f, 1f, 0f); // Jaune
                    break;

                case EnemyType.High:
                    moveSpeed = 1f;
                    maxHealth = 20;
                    damage = 3;
                    goldReward = 10;
                    enemyColor = new Color(1f, 0.5f, 0.5f); // Rouge clair
                    break;
            }

            currentHealth = maxHealth;
        }

        /// <summary>
        /// Suit le chemin défini par les waypoints
        /// </summary>
        private void FollowPath()
        {
            if (currentWaypointIndex >= pathToFollow.Count)
            {
                ReachCastle();
                return;
            }

            // Obtenir le prochain waypoint
            Vector3 targetWaypoint = pathToFollow[currentWaypointIndex];
            targetWaypoint.z = -1f;

            // Se déplacer vers le waypoint
            Vector3 currentPos = transform.position;
            Vector3 direction = (targetWaypoint - currentPos).normalized;

            // Déplacement
            Vector3 newPosition = currentPos + direction * moveSpeed * Time.deltaTime;
            newPosition.z = -1f;
            transform.position = newPosition;

            // Vérifier si on a atteint le waypoint
            float distance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(targetWaypoint.x, targetWaypoint.y)
            );

            if (distance < waypointReachDistance)
            {
                currentWaypointIndex++;
            }
        }

        /// <summary>
        /// L'ennemi atteint le château
        /// </summary>
        private void ReachCastle()
        {
            Debug.Log($"Enemy reached castle! Damage: {damage}");

            OnEnemyReachedEnd?.Invoke(this);

            // Infliger des dégâts au château (seulement si on est l'host)
            if (NetworkManager.Instance?.CurrentMode == NetworkMode.Host && gameController != null)
            {
                var players = gameController.GameStateData?.playersStates;
                if (players != null && players.Count > 0)
                {
                    gameController.DamageCastle(players[0].playerId, damage);
                    Debug.Log($"Damaged player {players[0].playerId}'s castle for {damage} HP");
                }
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Crée le visuel de l'ennemi
        /// </summary>
        private void CreateVisual()
        {
            if (visual != null) Destroy(visual);

            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;

            // Taille selon le type
            float scale = enemyType switch
            {
                EnemyType.Small => 0.5f,
                EnemyType.Medium => 0.7f,
                EnemyType.High => 0.9f,
                _ => 0.6f
            };
            visual.transform.localScale = Vector3.one * scale;

            // Couleur et matériau
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                renderer.material.color = enemyColor;
            }

            Collider col = visual.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        /// <summary>
        /// Crée la barre de vie
        /// </summary>
        private void CreateHealthBar()
        {
            if (healthBar != null) Destroy(healthBar.transform.parent.gameObject);

            GameObject barContainer = new GameObject("HealthBar");
            barContainer.transform.SetParent(transform);
            barContainer.transform.localPosition = new Vector3(0, 0.7f, -0.1f);

            // Background
            healthBarBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            healthBarBg.name = "Background";
            healthBarBg.transform.SetParent(barContainer.transform);
            healthBarBg.transform.localPosition = Vector3.zero;
            healthBarBg.transform.localScale = new Vector3(0.8f, 0.15f, 1);

            Renderer bgRenderer = healthBarBg.GetComponent<Renderer>();
            if (bgRenderer != null)
            {
                bgRenderer.material = new Material(Shader.Find("Sprites/Default"));
                bgRenderer.material.color = Color.black;
            }

            Collider bgCol = healthBarBg.GetComponent<Collider>();
            if (bgCol != null) Destroy(bgCol);

            // Barre de vie
            healthBar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            healthBar.name = "Fill";
            healthBar.transform.SetParent(barContainer.transform);
            healthBar.transform.localPosition = new Vector3(0, 0, -0.01f);
            healthBar.transform.localScale = new Vector3(0.7f, 0.1f, 1);

            Renderer barRenderer = healthBar.GetComponent<Renderer>();
            if (barRenderer != null)
            {
                barRenderer.material = new Material(Shader.Find("Sprites/Default"));
                barRenderer.material.color = Color.green;
            }

            Collider barCol = healthBar.GetComponent<Collider>();
            if (barCol != null) Destroy(barCol);
        }

        /// <summary>
        /// Met à jour la position de la barre de vie
        /// </summary>
        private void UpdateHealthBarPosition()
        {
            if (healthBar != null && healthBar.transform.parent != null)
            {
                healthBar.transform.parent.rotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Met à jour la barre de vie
        /// </summary>
        private void UpdateHealthBar()
        {
            if (healthBar == null) return;

            float healthPercent = (float)currentHealth / maxHealth;
            Vector3 scale = healthBar.transform.localScale;
            scale.x = 0.7f * healthPercent;
            healthBar.transform.localScale = scale;

            Renderer renderer = healthBar.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (healthPercent > 0.6f)
                    renderer.material.color = Color.green;
                else if (healthPercent > 0.3f)
                    renderer.material.color = Color.yellow;
                else
                    renderer.material.color = Color.red;
            }
        }

        /// <summary>
        /// Applique des dégâts à l'ennemi
        /// </summary>
        public void TakeDamage(int dmg)
        {
            currentHealth -= dmg;
            UpdateHealthBar();

            Debug.Log($"Enemy took {dmg} damage. Health: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// L'ennemi meurt
        /// </summary>
        private void Die()
        {
            Debug.Log($"Enemy died! Gold reward: {goldReward}");

            OnEnemyKilled?.Invoke(this, goldReward);

            // Donner l'or au joueur (seulement si on est l'host)
            if (NetworkManager.Instance?.CurrentMode == NetworkMode.Host && gameController != null)
            {
                var players = gameController.GameStateData?.playersStates;
                if (players != null && players.Count > 0)
                {
                    gameController.AddGoldToPlayer(players[0].playerId, goldReward);
                    Debug.Log($"Gave {goldReward} gold to player {players[0].playerId}");
                }
            }

            Destroy(gameObject);
        }

        // Getters publics
        public EnemyType GetEnemyType() => enemyType;
        public int GetGoldReward() => goldReward;
        public int GetDamage() => damage;
        public Vector3 GetPosition() => transform.position;
        public float GetMoveSpeed() => moveSpeed;

        // Debug
        private void OnDrawGizmos()
        {
            if (pathToFollow != null && pathToFollow.Count > 1)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < pathToFollow.Count - 1; i++)
                {
                    Gizmos.DrawLine(pathToFollow[i], pathToFollow[i + 1]);
                }

                if (currentWaypointIndex < pathToFollow.Count)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(pathToFollow[currentWaypointIndex], 0.3f);
                }
            }
        }
    }
}