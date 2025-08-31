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
            Debug.Log($"[Enemy] InitializeForMapV3 called - Type: {type}, Path points: {path?.Count ?? 0}");
            
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

                Debug.Log($"[Enemy] Positioned at {startPos}");
            }
            else
            {
                Debug.LogError("[Enemy] No path provided for enemy!");
                return;
            }

            // IMPORTANT: Créer le visuel de l'ennemi !
            CreateVisual();
            
            // Créer la barre de vie
            CreateHealthBar();

            // Marquer comme initialisé
            isInitialized = true;

            Debug.Log($"[Enemy] Initialization complete. GameObject active: {gameObject.activeInHierarchy}");
        }

        /// <summary>
        /// Configure les stats selon le type d'ennemi
        /// </summary>
        private void SetupStatsForType()
        {
            switch (enemyType)
            {
                case EnemyType.Small:
                    moveSpeed = 3f;
                    maxHealth = 5;
                    damage = 1;
                    goldReward = 5;
                    enemyColor = new Color(1f, 0.5f, 0.5f); // Rouge clair
                    break;

                case EnemyType.Medium:
                    moveSpeed = 2f;
                    maxHealth = 10;
                    damage = 2;
                    goldReward = 10;
                    enemyColor = Color.red;
                    break;

                case EnemyType.High:
                    moveSpeed = 1f;
                    maxHealth = 20;
                    damage = 5;
                    goldReward = 20;
                    enemyColor = new Color(0.5f, 0f, 0f); // Rouge foncé
                    break;
            }

            currentHealth = maxHealth;
            Debug.Log($"[Enemy] Stats configured for {enemyType}: Speed={moveSpeed}, HP={maxHealth}, Damage={damage}");
        }

        /// <summary>
        /// Crée le visuel de l'ennemi avec SpriteRenderer
        /// </summary>
        private void CreateVisual()
        {
            Debug.Log($"[Enemy] Creating visual at position {transform.position}");
            
            if (visual != null) Destroy(visual);

            visual = new GameObject("Visual");
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;

            // Utiliser un SpriteRenderer
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
            
            // IMPORTANT : Définir le sorting order pour que l'ennemi soit visible
            spriteRenderer.sortingOrder = 10;
            spriteRenderer.sortingLayerName = "Default";
            
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
            
            Debug.Log($"[Enemy] Visual created - SortingOrder: {spriteRenderer.sortingOrder}, Scale: {scale}");
        }

        /// <summary>
        /// Ajoute un contour à l'ennemi pour le rendre plus visible
        /// </summary>
        private void AddOutlineToSprite(SpriteRenderer mainSprite)
        {
            GameObject outline = new GameObject("Outline");
            outline.transform.SetParent(visual.transform);
            outline.transform.localPosition = Vector3.back * 0.01f; // Légèrement derrière
            outline.transform.localScale = Vector3.one * 1.1f;

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
        }

        /// <summary>
        /// Crée la barre de vie
        /// </summary>
        private void CreateHealthBar()
        {
            // Conteneur pour la barre de vie
            GameObject healthBarContainer = new GameObject("HealthBar");
            healthBarContainer.transform.SetParent(transform);
            healthBarContainer.transform.localPosition = new Vector3(0, 0.8f, -0.1f);

            // Background de la barre
            healthBarBg = new GameObject("Background");
            healthBarBg.transform.SetParent(healthBarContainer.transform);
            healthBarBg.transform.localPosition = Vector3.zero;
            
            SpriteRenderer bgRenderer = healthBarBg.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = CreateBarSprite(40, 6, Color.black);
            bgRenderer.sortingOrder = 11;

            // Barre de vie
            healthBar = new GameObject("Health");
            healthBar.transform.SetParent(healthBarContainer.transform);
            healthBar.transform.localPosition = Vector3.zero;
            
            SpriteRenderer healthRenderer = healthBar.AddComponent<SpriteRenderer>();
            healthRenderer.sprite = CreateBarSprite(38, 4, Color.green);
            healthRenderer.sortingOrder = 12;

            UpdateHealthBar();
        }

        /// <summary>
        /// Crée un sprite pour les barres
        /// </summary>
        private Sprite CreateBarSprite(int width, int height, Color color)
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
                100f
            );
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
            pos.x = -(1f - healthPercent) * 0.2f;
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

        private void Update()
        {
            if (!isInitialized || pathToFollow == null || pathToFollow.Count == 0)
                return;

            MoveAlongPath();
        }

        /// <summary>
        /// Déplace l'ennemi le long du chemin
        /// </summary>
        private void MoveAlongPath()
        {
            if (currentWaypointIndex >= pathToFollow.Count)
            {
                ReachEnd();
                return;
            }

            Vector3 targetPosition = pathToFollow[currentWaypointIndex];
            targetPosition.z = transform.position.z; // Garder la même profondeur

            // Se déplacer vers le waypoint
            Vector3 moveDirection = (targetPosition - transform.position).normalized;
            transform.position += moveDirection * moveSpeed * Time.deltaTime;

            // Vérifier si on a atteint le waypoint
            float distanceToWaypoint = Vector3.Distance(transform.position, targetPosition);
            if (distanceToWaypoint <= waypointReachDistance)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex < pathToFollow.Count)
                {
                    Debug.Log($"[Enemy] Reached waypoint {currentWaypointIndex}/{pathToFollow.Count}");
                }
            }
        }

        /// <summary>
        /// L'ennemi atteint la fin du chemin
        /// </summary>
        private void ReachEnd()
        {
            Debug.Log($"[Enemy] Reached castle! Damage: {damage}");

            OnEnemyReachedEnd?.Invoke(this);

            // Infliger des dégâts au château (seulement si on est l'host)
            if (NetworkManager.Instance?.CurrentMode == NetworkMode.Host && gameController != null)
            {
                var players = gameController.GameStateData?.playersStates;
                if (players != null && players.Count > 0)
                {
                    gameController.DamageCastle(players[0].playerId, damage);
                    Debug.Log($"[Enemy] Damaged player {players[0].playerId}'s castle for {damage} HP");
                }
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Applique des dégâts à l'ennemi
        /// </summary>
        public void TakeDamage(int dmg)
        {
            currentHealth -= dmg;
            UpdateHealthBar();

            Debug.Log($"[Enemy] Took {dmg} damage. Health: {currentHealth}/{maxHealth}");

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
            Debug.Log($"[Enemy] Died! Reward: {goldReward} gold");

            OnEnemyKilled?.Invoke(this, goldReward);

            // Donner l'or au joueur (seulement si on est l'host)
            if (NetworkManager.Instance?.CurrentMode == NetworkMode.Host && gameController != null)
            {
                var players = gameController.GameStateData?.playersStates;
                if (players != null && players.Count > 0)
                {
                    gameController.AddGoldToPlayer(players[0].playerId, goldReward);
                }
            }

            Destroy(gameObject);
        }

        // Getters publics pour Tower.cs
        public int GetDamage() => damage;
        public float GetMoveSpeed() => moveSpeed;
        public EnemyType GetEnemyType() => enemyType;
        public int GetGoldReward() => goldReward;
        public Vector3 GetPosition() => transform.position;

        private void OnDestroy()
        {
            // Nettoyer les events
            OnEnemyReachedEnd = null;
            OnEnemyKilled = null;
        }

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