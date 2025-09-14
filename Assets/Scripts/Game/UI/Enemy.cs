using UnityEngine;
using System.Collections.Generic;
using BaboonTower.Network;
using System.Linq; // AJOUTER CETTE LIGNE

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
    /// GetCurrentHealth - Getter pour la santé actuelle
    /// </summary>
    public int GetCurrentHealth()
    {
        return currentHealth;
    }
    
    /// <summary>
    /// GetCurrentWaypointIndex - Getter pour l'index du waypoint actuel
    /// </summary>
    public int GetCurrentWaypointIndex()
    {
        return currentWaypointIndex;
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

private void ReachCastle()
    {
        Debug.Log($"Enemy reached castle! Damage: {damage}");

        OnEnemyReachedEnd?.Invoke(this);

        // Infliger des dégâts au château du JOUEUR LOCAL
        if (gameController != null)
        {
            // Obtenir l'ID du joueur local différemment selon Host/Client
            int localPlayerId = GetLocalPlayerId();
            
            if (NetworkManager.Instance?.CurrentMode == NetworkMode.Host)
            {
                gameController.DamageCastle(localPlayerId, damage);
            }
            else
            {
                NetworkManager.Instance?.SendGameMessageToServer("DAMAGE_REQUEST", $"{localPlayerId}|{damage}");
            }
        }

        Destroy(gameObject);
    }

        /// <summary>
        /// Crée le visuel de l'ennemi avec SpriteRenderer
        /// </summary>
        private void CreateVisual()
        {
            if (visual != null) Destroy(visual);

            visual = new GameObject("Visual");
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;

            // Utiliser un SpriteRenderer au lieu d'un cube
            SpriteRenderer spriteRenderer = visual.AddComponent<SpriteRenderer>();
            
            // Créer un sprite carré simple pour l'ennemi
            Texture2D texture = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = enemyColor;
            }
            texture.SetPixels(pixels);
            texture.Apply();

            Sprite enemySprite = Sprite.Create(
                texture,
                new Rect(0, 0, 32, 32),
                new Vector2(0.5f, 0.5f),
                32f // pixels per unit
            );

            spriteRenderer.sprite = enemySprite;
            
            // IMPORTANT : Définir le sorting order pour que l'ennemi soit au-dessus de la route
            spriteRenderer.sortingOrder = 10; // Plus élevé que la route (qui est à 1)
            spriteRenderer.sortingLayerName = "Default"; // Ou créer un layer "Enemies" si nécessaire

            // Taille selon le type
            float scale = enemyType switch
            {
                EnemyType.Small => 0.5f,
                EnemyType.Medium => 0.7f,
                EnemyType.High => 0.9f,
                _ => 0.6f
            };
            visual.transform.localScale = Vector3.one * scale;

            // Ajouter un contour pour mieux voir l'ennemi
            AddOutlineToSprite(spriteRenderer);
        }

        /// <summary>
        /// Ajoute un contour à l'ennemi pour le rendre plus visible
        /// </summary>
        private void AddOutlineToSprite(SpriteRenderer mainSprite)
        {
            // Créer un GameObject enfant pour l'outline
            GameObject outline = new GameObject("Outline");
            outline.transform.SetParent(visual.transform);
            outline.transform.localPosition = Vector3.zero;
            outline.transform.localScale = Vector3.one * 1.1f; // Légèrement plus grand

            SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
            
            // Créer un sprite noir pour l'outline
            Texture2D outlineTexture = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.black;
            }
            outlineTexture.SetPixels(pixels);
            outlineTexture.Apply();

            Sprite outlineSprite = Sprite.Create(
                outlineTexture,
                new Rect(0, 0, 32, 32),
                new Vector2(0.5f, 0.5f),
                32f
            );

            outlineRenderer.sprite = outlineSprite;
            outlineRenderer.sortingOrder = 9; // Juste derrière l'ennemi principal
            outlineRenderer.color = new Color(0, 0, 0, 0.5f); // Semi-transparent
        }

        /// <summary>
        /// Crée la barre de vie avec SpriteRenderer
        /// </summary>
        private void CreateHealthBar()
        {
            if (healthBar != null) Destroy(healthBar.transform.parent.gameObject);

            GameObject barContainer = new GameObject("HealthBar");
            barContainer.transform.SetParent(transform);
            barContainer.transform.localPosition = new Vector3(0, 0.7f, 0);

            // Background de la barre de vie
            healthBarBg = new GameObject("Background");
            healthBarBg.transform.SetParent(barContainer.transform);
            healthBarBg.transform.localPosition = Vector3.zero;

            SpriteRenderer bgRenderer = healthBarBg.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = CreateColorSprite(Color.black, 40, 6);
            bgRenderer.sortingOrder = 11; // Au-dessus de l'ennemi

            // Barre de vie
            healthBar = new GameObject("Fill");
            healthBar.transform.SetParent(barContainer.transform);
            healthBar.transform.localPosition = new Vector3(0, 0, -0.01f);

            SpriteRenderer barRenderer = healthBar.AddComponent<SpriteRenderer>();
            barRenderer.sprite = CreateColorSprite(Color.green, 36, 4);
            barRenderer.sortingOrder = 12; // Au-dessus du background

            UpdateHealthBar();
        }

        /// <summary>
        /// Crée un sprite d'une couleur unie
        /// </summary>
        private Sprite CreateColorSprite(Color color, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                100f // pixels per unit
            );
        }

        /// <summary>
        /// Met à jour la position de la barre de vie
        /// </summary>
        private void UpdateHealthBarPosition()
        {
            // La barre de vie suit l'ennemi automatiquement car elle est enfant
            // Pas besoin de rotation fixe avec les sprites
        }

        /// <summary>
        /// Met à jour la barre de vie
        /// </summary>
        private void UpdateHealthBar()
        {
            if (healthBar == null) return;

            float healthPercent = (float)currentHealth / maxHealth;
            Vector3 scale = healthBar.transform.localScale;
            scale.x = healthPercent;
            healthBar.transform.localScale = scale;

            // Décaler la position pour que la barre se réduise depuis la gauche
            Vector3 pos = healthBar.transform.localPosition;
            pos.x = -(1f - healthPercent) * 0.2f; // Ajuster selon la taille de la barre
            healthBar.transform.localPosition = pos;

            SpriteRenderer renderer = healthBar.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                if (healthPercent > 0.6f)
                    renderer.color = Color.green;
                else if (healthPercent > 0.3f)
                    renderer.color = Color.yellow;
                else
                    renderer.color = Color.red;
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

private void Die()
    {
        Debug.Log($"Enemy died! Gold reward: {goldReward}");

        OnEnemyKilled?.Invoke(this, goldReward);

        // Donner l'or au JOUEUR LOCAL
        if (gameController != null)
        {
            int localPlayerId = GetLocalPlayerId();
            
            if (NetworkManager.Instance?.CurrentMode == NetworkMode.Host)
            {
                gameController.AddGoldToPlayer(localPlayerId, goldReward);
            }
            else
            {
                NetworkManager.Instance?.SendGameMessageToServer("GOLD_REQUEST", $"{localPlayerId}|{goldReward}");
            }
        }

        Destroy(gameObject);
    }
private int GetLocalPlayerId()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.ConnectedPlayers.Count > 0)
        {
            if (NetworkManager.Instance.CurrentMode == NetworkMode.Host)
            {
                var hostPlayer = NetworkManager.Instance.ConnectedPlayers.FirstOrDefault(p => p.isHost);
                return hostPlayer?.playerId ?? 0;
            }
            else
            {
                var clientPlayer = NetworkManager.Instance.ConnectedPlayers.FirstOrDefault(p => !p.isHost);
                return clientPlayer?.playerId ?? 1;
            }
        }
        return 0;
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