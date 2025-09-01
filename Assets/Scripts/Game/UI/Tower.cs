using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BaboonTower.Game
{
    /// <summary>
    /// Component pour gérer le comportement d'une tour
    /// </summary>
    public class Tower : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TowerData towerData;
        [SerializeField] private Vector2Int gridPosition;
        
        [Header("State")]
        [SerializeField] private Enemy currentTarget;
        [SerializeField] private float nextFireTime;
        [SerializeField] private List<GameObject> projectiles = new List<GameObject>();
        
        [Header("Visual")]
        [SerializeField] private Transform turret;
        [SerializeField] private GameObject rangeIndicator;
        [SerializeField] private bool showRange = false;
        
        // References
        private Transform enemiesContainer;
        
        // Properties
        public TowerData Data => towerData;
        public Vector2Int GridPosition => gridPosition;
        public bool IsActive { get; private set; } = true;
        
        private void Awake()
        {
            // Find turret transform
            turret = transform.Find("Turret");
            if (turret == null)
            {
                turret = transform;
            }
        }
        
        private void Start()
        {
            // Find enemies container (parent of all enemies)
            GameObject enemiesGO = GameObject.Find("Enemies");
            if (enemiesGO != null)
            {
                enemiesContainer = enemiesGO.transform;
            }
        }
        
        private void Update()
        {
            if (!IsActive || towerData == null) return;
            
            // Find and track target
            UpdateTarget();
            
            // Rotate turret towards target
            if (currentTarget != null && turret != null)
            {
                RotateTurretTowards(currentTarget.transform.position);
            }
            
            // Fire at target
            if (currentTarget != null && Time.time >= nextFireTime)
            {
                Fire();
            }
            
            // Update projectiles
            UpdateProjectiles();
        }
        
        private void OnDestroy()
        {
            // Clean up projectiles
            foreach (var projectile in projectiles)
            {
                if (projectile != null)
                {
                    Destroy(projectile);
                }
            }
        }
        
        #region Initialization
        
        public void Initialize(TowerData data, Vector2Int gridPos)
        {
            towerData = data;
            gridPosition = gridPos;
            
            CreateRangeIndicator();
            SetRangeIndicatorVisible(false);
            
            Debug.Log($"[Tower] Initialized {data.name} at {gridPos}");
        }
        
        private void CreateRangeIndicator()
        {
            if (rangeIndicator != null) return;
            
            rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rangeIndicator.name = "RangeIndicator";
            rangeIndicator.transform.SetParent(transform);
            rangeIndicator.transform.localPosition = Vector3.zero;
            
            float diameter = towerData.stats.range * 2f;
            rangeIndicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            
            Renderer renderer = rangeIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
                
                Color rangeColor = Color.green;
                if (!string.IsNullOrEmpty(towerData.visual.rangeIndicatorColor))
                {
                    ColorUtility.TryParseHtmlString(towerData.visual.rangeIndicatorColor, out rangeColor);
                }
                
                mat.color = rangeColor;
                renderer.material = mat;
            }
            
            Collider col = rangeIndicator.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
        }
        
        #endregion
        
        #region Targeting
        
        private void UpdateTarget()
        {
            // Check if current target is still valid
            if (currentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
                if (distance > towerData.stats.range || !currentTarget.gameObject.activeInHierarchy)
                {
                    currentTarget = null;
                }
            }
            
            // Find new target if needed
            if (currentTarget == null)
            {
                currentTarget = FindTarget();
            }
        }
        
        private Enemy FindTarget()
        {
            List<Enemy> enemiesInRange = GetEnemiesInRange();
            
            if (enemiesInRange.Count == 0) return null;
            
            // Apply targeting strategy
            switch (towerData.targeting.mode.ToLower())
            {
                case "nearest":
                    return GetNearestEnemy(enemiesInRange);
                    
                case "strongest":
                    return GetStrongestEnemy(enemiesInRange);
                    
                case "fastest":
                    return GetFastestEnemy(enemiesInRange);
                    
                case "weakest":
                    return GetWeakestEnemy(enemiesInRange);
                    
                default:
                    return enemiesInRange[0];
            }
        }
        
        private List<Enemy> GetEnemiesInRange()
        {
            List<Enemy> enemies = new List<Enemy>();
            
            // Find all enemies
            Enemy[] allEnemies = FindObjectsOfType<Enemy>();
            
            foreach (Enemy enemy in allEnemies)
            {
                if (enemy.gameObject.activeInHierarchy)
                {
                    float distance = Vector3.Distance(transform.position, enemy.transform.position);
                    if (distance <= towerData.stats.range)
                    {
                        enemies.Add(enemy);
                    }
                }
            }
            
            return enemies;
        }
        
        private Enemy GetNearestEnemy(List<Enemy> enemies)
        {
            return enemies.OrderBy(e => Vector3.Distance(transform.position, e.transform.position)).FirstOrDefault();
        }
        
        private Enemy GetStrongestEnemy(List<Enemy> enemies)
        {
            // Strongest = most HP or damage, we'll use damage
            return enemies.OrderByDescending(e => e.GetDamage()).FirstOrDefault();
        }
        
        private Enemy GetFastestEnemy(List<Enemy> enemies)
        {
            return enemies.OrderByDescending(e => e.GetMoveSpeed()).FirstOrDefault();
        }
        
        private Enemy GetWeakestEnemy(List<Enemy> enemies)
        {
            // Weakest = least HP/damage
            return enemies.OrderBy(e => e.GetDamage()).FirstOrDefault();
        }
        
        #endregion
        
        #region Combat
        
        private void Fire()
        {
            if (currentTarget == null) return;
            
            nextFireTime = Time.time + (1f / towerData.stats.fireRate);
            
            // Create projectile
            CreateProjectile(currentTarget);
            
            // Play fire sound (if implemented)
            // AudioManager.PlaySound(towerData.audio.fireSound);
        }
        
        private void CreateProjectile(Enemy target)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Projectile";
            projectile.transform.position = turret != null ? turret.position : transform.position;
            projectile.transform.localScale = Vector3.one * 0.2f;
            
            // Set projectile color
            Renderer renderer = projectile.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
                
                Color projectileColor = Color.yellow;
                if (!string.IsNullOrEmpty(towerData.visual.projectileColor))
                {
                    ColorUtility.TryParseHtmlString(towerData.visual.projectileColor, out projectileColor);
                }
                
                mat.color = projectileColor;
                renderer.material = mat;
            }
            
            // Remove collider
            Collider col = projectile.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
            
            // Add projectile component
            Projectile proj = projectile.AddComponent<Projectile>();
            proj.Initialize(towerData, target, this);
            
            projectiles.Add(projectile);
        }
        
        private void UpdateProjectiles()
        {
            projectiles.RemoveAll(p => p == null);
        }
        
        private void RotateTurretTowards(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - turret.position;
            direction.z = 0;
            
            if (direction != Vector3.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                turret.rotation = Quaternion.Euler(0, 0, angle - 90);
            }
        }
        
        #endregion
        
        #region Visual
        
        public void SetRangeIndicatorVisible(bool visible)
        {
            showRange = visible;
            if (rangeIndicator != null)
            {
                rangeIndicator.SetActive(visible);
            }
        }
        
        private void OnMouseEnter()
        {
            SetRangeIndicatorVisible(true);
        }
        
        private void OnMouseExit()
        {
            SetRangeIndicatorVisible(false);
        }
        
        #endregion
        
        #region Public API
        
        public void SetActive(bool active)
        {
            IsActive = active;
        }
        
        public void Upgrade(TowerData newData)
        {
            towerData = newData;
            
            // Update visual
            Transform visual = transform.Find("Visual");
            if (visual != null)
            {
                Renderer renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color towerColor = Color.gray;
                    if (!string.IsNullOrEmpty(newData.visual.towerColor))
                    {
                        ColorUtility.TryParseHtmlString(newData.visual.towerColor, out towerColor);
                    }
                    renderer.material.color = towerColor;
                }
                
                visual.localScale = Vector3.one * newData.visual.scale;
            }
            
            // Update range indicator
            if (rangeIndicator != null)
            {
                float diameter = newData.stats.range * 2f;
                rangeIndicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            }
        }
        
        public void ApplyDamageBoost(float multiplier)
        {
            // This could be used for temporary buffs
            // Would need to track original damage and boost duration
        }
        
        public void ApplyRangeBoost(float multiplier)
        {
            // This could be used for temporary buffs
            // Would need to track original range and boost duration
        }
        
        #endregion
    }
    
    /// <summary>
    /// Component for projectile behavior
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private TowerData towerData;
        private Enemy target;
        private Tower sourceTower;
        private Vector3 lastTargetPosition;
        private bool hasHit = false;
        
        public void Initialize(TowerData data, Enemy targetEnemy, Tower tower)
        {
            towerData = data;
            target = targetEnemy;
            sourceTower = tower;
            
            if (target != null)
            {
                lastTargetPosition = target.transform.position;
            }
        }
        
        private void Update()
        {
            if (hasHit) return;
            
            // Update target position
            if (target != null && target.gameObject.activeInHierarchy)
            {
                lastTargetPosition = target.transform.position;
            }
            
            // Move towards target
            Vector3 direction = (lastTargetPosition - transform.position).normalized;
            transform.position += direction * towerData.stats.projectileSpeed * Time.deltaTime;
            
            // Check if reached target
            float distance = Vector3.Distance(transform.position, lastTargetPosition);
            if (distance < 0.2f)
            {
                OnHit();
            }
            
            // Destroy if too far from map
            if (transform.position.magnitude > 100f)
            {
                Destroy(gameObject);
            }
        }
        
        private void OnHit()
        {
            if (hasHit) return;
            hasHit = true;
            
            // Deal damage to target
            if (target != null && target.gameObject.activeInHierarchy)
            {
                target.TakeDamage(Mathf.RoundToInt(towerData.stats.damage));
                
                // Apply slow effect if any
                if (towerData.stats.slowEffect > 0)
                {
                    // This would need to be implemented in Enemy
                    // target.ApplySlow(towerData.stats.slowEffect, towerData.stats.slowDuration);
                }
            }
            
            // Handle splash damage
            if (towerData.stats.splashDamage > 0 && towerData.stats.splashRadius > 0)
            {
                ApplySplashDamage();
            }
            
            // Destroy projectile
            Destroy(gameObject);
        }
        
        private void ApplySplashDamage()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, towerData.stats.splashRadius);
            
            foreach (Collider col in colliders)
            {
                Enemy enemy = col.GetComponent<Enemy>();
                if (enemy != null && enemy != target)
                {
                    enemy.TakeDamage(Mathf.RoundToInt(towerData.stats.splashDamage));
                }
            }
        }
    }
}