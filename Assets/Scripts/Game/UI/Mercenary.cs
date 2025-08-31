using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using BaboonTower.Game.UI; // Ajout pour TowerPlacementSystem

namespace BaboonTower.Game
{
    /// <summary>
    /// Component pour gérer le comportement d'un mercenaire
    /// </summary>
    public class Mercenary : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private MercenaryData mercenaryData;
        [SerializeField] private int targetPlayerId;
        [SerializeField] private int ownerPlayerId;
        
        [Header("Movement")]
        [SerializeField] private List<Vector3> pathToFollow;
        [SerializeField] private int currentWaypointIndex = 0;
        [SerializeField] private float currentSpeed;
        
        [Header("State")]
        [SerializeField] private float currentHealth;
        [SerializeField] private bool hasReachedHalfway = false;
        [SerializeField] private bool effectActivated = false;
        
        // Visual
        private GameObject visual;
        private GameObject effectVisual;
        
        // References
        private GameController gameController;
        private MapLoaderV3 mapLoader;
        
        // Events
        public System.Action<Mercenary> OnMercenaryReachedEnd;
        public System.Action<Mercenary> OnMercenaryKilled;
        
        private void Awake()
        {
            gameController = FindObjectOfType<GameController>();
            mapLoader = FindObjectOfType<MapLoaderV3>();
        }
        
        private void Update()
        {
            if (pathToFollow != null && pathToFollow.Count > 0)
            {
                FollowPath();
                UpdateEffect();
            }
        }
        
        private void OnDestroy()
        {
            if (effectVisual != null)
            {
                Destroy(effectVisual);
            }
        }
        
        #region Initialization
        
        public void Initialize(MercenaryData data, int targetPlayer, int ownerPlayer, List<Vector3> path)
        {
            mercenaryData = data;
            targetPlayerId = targetPlayer;
            ownerPlayerId = ownerPlayer;
            pathToFollow = new List<Vector3>(path);
            
            currentHealth = data.stats.health;
            currentSpeed = data.stats.moveSpeed;
            
            // Position at spawn
            if (pathToFollow.Count > 0)
            {
                transform.position = pathToFollow[0];
                currentWaypointIndex = 0;
            }
            
            CreateVisual();
            
            // Activate effect immediately for some mercenaries
            if (ShouldActivateImmediately())
            {
                ActivateEffect();
            }
            
            Debug.Log($"[Mercenary] {data.name} initialized, targeting player {targetPlayer}");
        }
        
        private void CreateVisual()
        {
            if (visual != null) Destroy(visual);
            
            // Create main visual
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * mercenaryData.visual.scale;
            
            // Set color
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
                
                Color mercColor = Color.magenta;
                if (!string.IsNullOrEmpty(mercenaryData.visual.color))
                {
                    ColorUtility.TryParseHtmlString(mercenaryData.visual.color, out mercColor);
                }
                
                mat.color = mercColor;
                renderer.material = mat;
            }
            
            // Remove collider
            Collider col = visual.GetComponent<Collider>();
            if (col != null) Destroy(col);
            
            // Add name label
            CreateNameLabel();
        }
        
        private void CreateNameLabel()
        {
            GameObject labelGO = new GameObject("NameLabel");
            labelGO.transform.SetParent(transform);
            labelGO.transform.localPosition = new Vector3(0, 1.5f, 0);
            
            TextMesh textMesh = labelGO.AddComponent<TextMesh>();
            textMesh.text = mercenaryData.name;
            textMesh.fontSize = 20;
            textMesh.color = Color.white;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            labelGO.transform.localScale = Vector3.one * 0.1f;
            
            // Make it face camera
            labelGO.AddComponent<FaceCamera>();
        }
        
        #endregion
        
        #region Movement
        
        private void FollowPath()
        {
            if (currentWaypointIndex >= pathToFollow.Count)
            {
                ReachCastle();
                return;
            }
            
            Vector3 targetWaypoint = pathToFollow[currentWaypointIndex];
            targetWaypoint.z = -1f; // In front of map
            
            // Move towards waypoint
            Vector3 direction = (targetWaypoint - transform.position).normalized;
            transform.position += direction * currentSpeed * Time.deltaTime;
            
            // Check if reached waypoint
            float distance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(targetWaypoint.x, targetWaypoint.y)
            );
            
            if (distance < 0.1f)
            {
                currentWaypointIndex++;
                
                // Check if reached halfway
                if (!hasReachedHalfway && currentWaypointIndex >= pathToFollow.Count / 2)
                {
                    hasReachedHalfway = true;
                    OnReachedHalfway();
                }
            }
        }
        
        private void OnReachedHalfway()
        {
            // Handle special effects at halfway point
            switch (mercenaryData.id)
            {
                case "dint": // Coffee break
                    StartCoroutine(CoffeeBreak());
                    break;
            }
        }
        
        private IEnumerator CoffeeBreak()
        {
            if (mercenaryData.effect == null) yield break;
            
            // Stop for coffee
            float originalSpeed = currentSpeed;
            currentSpeed = 0;
            
            // Show coffee effect
            ShowEffectVisual("☕", 2f);
            
            yield return new WaitForSeconds(mercenaryData.effect.coffeeBreakDuration);
            
            // Speed boost after coffee
            currentSpeed = originalSpeed * mercenaryData.effect.boostedSpeedMultiplier;
        }
        
        #endregion
        
        #region Effects
        
        private bool ShouldActivateImmediately()
        {
            // Some mercenaries activate their effect immediately
            return mercenaryData.id == "pbuy" || 
                   mercenaryData.id == "bgir" || 
                   mercenaryData.id == "ybra";
        }
        
        private void ActivateEffect()
        {
            if (effectActivated) return;
            effectActivated = true;
            
            switch (mercenaryData.effect.type)
            {
                case "global_speed_boost":
                    ApplyGlobalSpeedBoost();
                    break;
                    
                case "tower_miss_chance":
                    StartCoroutine(ApplyTowerMissChance());
                    break;
                    
                case "tower_displacement":
                    DisplaceRandomTower();
                    break;
                    
                case "slow_aura":
                    // Handled in UpdateEffect
                    break;
                    
                case "reveal_players_info":
                    RevealPlayersInfo();
                    break;
            }
            
            // Show message if any
            if (!string.IsNullOrEmpty(mercenaryData.effect.message))
            {
                ShowMessage(mercenaryData.effect.message);
            }
        }
        
        private void UpdateEffect()
        {
            if (!effectActivated) return;
            
            // Handle continuous effects
            switch (mercenaryData.effect.type)
            {
                case "slow_aura":
                    ApplySlowAura();
                    break;
                    
                case "tower_miss_chance":
                    // Handled by coroutine
                    break;
            }
        }
        
        private void ApplyGlobalSpeedBoost()
        {
            // Find all enemies on the target player's map
            Enemy[] enemies = FindObjectsOfType<Enemy>();
            
            foreach (Enemy enemy in enemies)
            {
                // Apply speed boost (would need to be implemented in Enemy)
                // enemy.ApplySpeedMultiplier(mercenaryData.effect.speedMultiplier);
            }
            
            // Slow self
            currentSpeed *= mercenaryData.effect.selfSpeedMultiplier;
            
            ShowEffectVisual("💨", 3f);
        }
        
        private IEnumerator ApplyTowerMissChance()
        {
            float elapsed = 0;
            
            while (elapsed < mercenaryData.stats.effectDuration)
            {
                // Create sonic wave effect
                CreateSonicWave();
                
                // Find towers in range
                Tower[] towers = FindObjectsOfType<Tower>();
                
                foreach (Tower tower in towers)
                {
                    float distance = Vector3.Distance(transform.position, tower.transform.position);
                    if (distance <= mercenaryData.stats.effectRadius)
                    {
                        // Apply miss chance (would need to be implemented in Tower)
                        // tower.ApplyMissChance(mercenaryData.effect.missChance);
                    }
                }
                
                yield return new WaitForSeconds(mercenaryData.effect.pulseInterval);
                elapsed += mercenaryData.effect.pulseInterval;
            }
        }
        
        private void DisplaceRandomTower()
        {
            Tower[] towers = FindObjectsOfType<Tower>();
            
            if (towers.Length == 0) return;
            
            // Pick random tower
            Tower targetTower = towers[Random.Range(0, towers.Length)];
            
            // Find new random position
            TowerPlacementSystem placement = FindObjectOfType<TowerPlacementSystem>();
            if (placement != null)
            {
                // Find valid positions
                List<Vector2Int> validPositions = new List<Vector2Int>();
                
                for (int x = 0; x < 30; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        if (placement.IsValidPlacement(pos))
                        {
                            validPositions.Add(pos);
                        }
                    }
                }
                
                if (validPositions.Count > 0)
                {
                    Vector2Int newPos = validPositions[Random.Range(0, validPositions.Count)];
                    
                    // Move tower (would need more implementation)
                    if (mapLoader != null)
                    {
                        targetTower.transform.position = mapLoader.GridToWorldPosition(newPos);
                    }
                    
                    ShowEffectVisual("🌀", 2f);
                }
            }
        }
        
        private void ApplySlowAura()
        {
            // Find enemies behind this mercenary
            Enemy[] enemies = FindObjectsOfType<Enemy>();
            
            foreach (Enemy enemy in enemies)
            {
                // Check if enemy is behind (further from castle)
                if (currentWaypointIndex > 0)
                {
                    float distToThis = Vector3.Distance(enemy.transform.position, transform.position);
                    if (distToThis <= mercenaryData.stats.effectRadius)
                    {
                        // Apply slow (would need to be implemented in Enemy)
                        // enemy.ApplySpeedMultiplier(mercenaryData.effect.slowMultiplier);
                    }
                }
            }
        }
        
        private void RevealPlayersInfo()
        {
            // This would send a network message to reveal enemy info
            if (gameController != null)
            {
                // Implementation would depend on your UI system
                Debug.Log($"[Mercenary] Revealing enemy players info for {mercenaryData.stats.effectDuration} seconds");
            }
        }
        
        #endregion
        
        #region Visual Effects
        
        private void ShowEffectVisual(string symbol, float duration)
        {
            StartCoroutine(ShowEffectVisualCoroutine(symbol, duration));
        }
        
        private IEnumerator ShowEffectVisualCoroutine(string symbol, float duration)
        {
            GameObject effect = new GameObject("Effect");
            effect.transform.SetParent(transform);
            effect.transform.localPosition = new Vector3(0, 2f, 0);
            
            TextMesh text = effect.AddComponent<TextMesh>();
            text.text = symbol;
            text.fontSize = 30;
            text.color = Color.white;
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            effect.transform.localScale = Vector3.one * 0.15f;
            
            effect.AddComponent<FaceCamera>();
            
            yield return new WaitForSeconds(duration);
            
            Destroy(effect);
        }
        
        private void CreateSonicWave()
        {
            GameObject wave = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wave.name = "SonicWave";
            wave.transform.position = transform.position;
            wave.transform.localScale = new Vector3(0.1f, 0.01f, 0.1f);
            
            Renderer renderer = wave.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(1f, 0.4f, 0.7f, 0.3f);
                renderer.material = mat;
            }
            
            Collider col = wave.GetComponent<Collider>();
            if (col != null) Destroy(col);
            
            // Animate expansion
            StartCoroutine(ExpandWave(wave));
        }
        
        private IEnumerator ExpandWave(GameObject wave)
        {
            float duration = 1f;
            float elapsed = 0;
            float maxRadius = mercenaryData.stats.effectRadius * 2f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                float radius = Mathf.Lerp(0.1f, maxRadius, t);
                wave.transform.localScale = new Vector3(radius, 0.01f, radius);
                
                Renderer renderer = wave.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color color = renderer.material.color;
                    color.a = Mathf.Lerp(0.3f, 0f, t);
                    renderer.material.color = color;
                }
                
                yield return null;
            }
            
            Destroy(wave);
        }
        
        private void ShowMessage(string message)
        {
            Debug.Log($"[Mercenary] {mercenaryData.name}: {message}");
            // Could show this as floating text or in UI
        }
        
        #endregion
        
        #region End / Death
        
        private void ReachCastle()
        {
            Debug.Log($"[Mercenary] {mercenaryData.name} reached castle! Damage: {mercenaryData.stats.damageTocastle}");
            
            OnMercenaryReachedEnd?.Invoke(this);
            
            // Deal damage to castle
            if (gameController != null)
            {
                gameController.DamageCastle(targetPlayerId, Mathf.RoundToInt(mercenaryData.stats.damageTocastle));
            }
            
            Destroy(gameObject);
        }
        
        public void TakeDamage(float damage)
        {
            currentHealth -= damage;
            
            if (currentHealth <= 0)
            {
                Die();
            }
        }
        
        private void Die()
        {
            Debug.Log($"[Mercenary] {mercenaryData.name} killed!");
            
            OnMercenaryKilled?.Invoke(this);
            
            Destroy(gameObject);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Simple component to make text face the camera
    /// </summary>
    public class FaceCamera : MonoBehaviour
    {
        private Camera mainCamera;
        
        private void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }
        
        private void LateUpdate()
        {
            if (mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }
    }
}