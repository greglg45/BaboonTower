using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using BaboonTower.Network;

namespace BaboonTower.Game.UI
{
    /// <summary>
    /// Système de placement des tours avec preview et validation
    /// </summary>
    public class TowerPlacementSystem : MonoBehaviour
    {
        [Header("Placement Settings")]
        [SerializeField] private LayerMask placementLayer = -1;
        [SerializeField] private float gridSize = 1f;
        [SerializeField] private Color validPlacementColor = new Color(0, 1, 0, 0.5f);
        [SerializeField] private Color invalidPlacementColor = new Color(1, 0, 0, 0.5f);
        
        [Header("Range Indicator")]
        [SerializeField] private Color validRangeColor = new Color(0, 1, 0, 0.3f);
        [SerializeField] private Color invalidRangeColor = new Color(1, 0, 0, 0.3f);
        [SerializeField] private int rangeCircleSegments = 64;
        [SerializeField] private float rangeLineWidth = 0.1f;
        
        [Header("Preview")]
        [SerializeField] private Material previewMaterial;
        private GameObject previewObject;
        private GameObject rangeIndicator;
        
        // State
        private bool isPlacing = false;
        private TowerData currentTowerData;
        private Vector2Int lastGridPos;
        
        // References
        private MapLoaderV3 mapLoader;
        private GameController gameController;
        private Camera mainCamera;
        
        // Placed towers tracking
        private Dictionary<Vector2Int, GameObject> placedTowers = new Dictionary<Vector2Int, GameObject>();
        
        // Events
        public System.Action<TowerData, Vector2Int> OnTowerPlaced;
        public System.Action OnPlacementCancelled;
        
        private void Awake()
        {
            mapLoader = FindObjectOfType<MapLoaderV3>();
            gameController = FindObjectOfType<GameController>();
            mainCamera = Camera.main;
            
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }
        
        private void Update()
        {
            if (!isPlacing) return;
            
            // Handle placement
            UpdatePreviewPosition();
            
            // Left click to place
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                TryPlaceTower();
            }
            
            // Right click or ESC to cancel
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPlacement();
            }
        }
        
        #region Public Methods
        
        /// <summary>
        /// Start the tower placement process
        /// </summary>
        public void StartPlacement(TowerData towerData)
        {
            if (isPlacing)
            {
                CancelPlacement();
            }
            
            currentTowerData = towerData;
            isPlacing = true;
            
            CreatePreviewObject();
            CreateRangeIndicator();
            
            Debug.Log($"[TowerPlacement] Started placement for {towerData.name}");
        }
        
        /// <summary>
        /// Cancel the current placement
        /// </summary>
        public void CancelPlacement()
        {
            if (!isPlacing) return;
            
            isPlacing = false;
            currentTowerData = null;
            
            if (previewObject != null)
            {
                Destroy(previewObject);
                previewObject = null;
            }
            
            if (rangeIndicator != null)
            {
                Destroy(rangeIndicator);
                rangeIndicator = null;
            }
            
            OnPlacementCancelled?.Invoke();
            
            Debug.Log("[TowerPlacement] Placement cancelled");
        }
        
        /// <summary>
        /// Check if a grid position is valid for tower placement
        /// </summary>
        public bool IsValidPlacement(Vector2Int gridPos)
        {
            // Check if position is already occupied
            if (placedTowers.ContainsKey(gridPos))
            {
                return false;
            }
            
            // Check with MapLoaderV3
            if (mapLoader != null)
            {
                return mapLoader.CanPlaceTowerAt(gridPos);
            }
            
            // Fallback check
            return true;
        }
        
        #endregion
        
        #region Placement Logic
        
        private void UpdatePreviewPosition()
        {
            Vector3 mouseWorldPos = GetMouseWorldPosition();
            Vector2Int gridPos = WorldToGridPosition(mouseWorldPos);
            
            if (gridPos != lastGridPos)
            {
                lastGridPos = gridPos;
                UpdatePreview(gridPos);
            }
        }
        
        private void UpdatePreview(Vector2Int gridPos)
        {
            if (previewObject == null) return;
            
            Vector3 worldPos = GridToWorldPosition(gridPos);
            previewObject.transform.position = worldPos;
            
            bool isValid = IsValidPlacement(gridPos);
            
            // Update preview color
            Renderer renderer = previewObject.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Color color = isValid ? validPlacementColor : invalidPlacementColor;
                
                if (previewMaterial != null)
                {
                    renderer.material = previewMaterial;
                    renderer.material.color = color;
                }
                else
                {
                    renderer.material.color = color;
                }
            }
            
            // Update range indicator
            if (rangeIndicator != null)
            {
                rangeIndicator.transform.position = new Vector3(worldPos.x, worldPos.y, -0.5f);
                
                // Mettre à jour la couleur du cercle de portée
                LineRenderer lineRenderer = rangeIndicator.GetComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    Color rangeColor = isValid ? validRangeColor : invalidRangeColor;
                    lineRenderer.startColor = rangeColor;
                    lineRenderer.endColor = rangeColor;
                }
                
                // Mettre à jour la couleur de la zone de portée
                Transform rangeArea = rangeIndicator.transform.Find("RangeArea");
                if (rangeArea != null)
                {
                    MeshRenderer areaMeshRenderer = rangeArea.GetComponent<MeshRenderer>();
                    if (areaMeshRenderer != null)
                    {
                        Color areaColor = isValid ? 
                            new Color(validRangeColor.r, validRangeColor.g, validRangeColor.b, 0.15f) : 
                            new Color(invalidRangeColor.r, invalidRangeColor.g, invalidRangeColor.b, 0.15f);
                        areaMeshRenderer.material.color = areaColor;
                    }
                }
            }
        }
        
        private void TryPlaceTower()
        {
            Vector3 mouseWorldPos = GetMouseWorldPosition();
            Vector2Int gridPos = WorldToGridPosition(mouseWorldPos);
            
            if (!IsValidPlacement(gridPos))
            {
                Debug.Log($"[TowerPlacement] Invalid placement at {gridPos}");
                // Could play error sound here
                return;
            }
            
            // Check if player can afford it
            if (gameController != null && !gameController.CanAfford(currentTowerData.cost.gold))
            {
                Debug.Log("[TowerPlacement] Not enough gold!");
                return;
            }
            
            // Place the tower
            PlaceTower(gridPos);
        }
        
        private void PlaceTower(Vector2Int gridPos)
        {
            Vector3 worldPos = GridToWorldPosition(gridPos);
            
            // Create the actual tower
            GameObject towerObj = CreateTowerObject(currentTowerData, worldPos);
            
            // Track the placed tower
            placedTowers[gridPos] = towerObj;
            
            // Add Tower component
            Tower towerComponent = towerObj.AddComponent<Tower>();
            towerComponent.Initialize(currentTowerData, gridPos);
            
            // Trigger event
            OnTowerPlaced?.Invoke(currentTowerData, gridPos);
            
            // Send network message if networked
            if (NetworkManager.Instance != null)
            {
                string data = $"{currentTowerData.id}|{gridPos.x}|{gridPos.y}";
                NetworkManager.Instance.SendGameMessageToServer("PLACE_TOWER", data);
            }
            
            Debug.Log($"[TowerPlacement] Placed {currentTowerData.name} at {gridPos}");
            
            // End placement mode
            CancelPlacement();
        }
        
        #endregion
        
        #region Object Creation
        
        private void CreatePreviewObject()
        {
            if (previewObject != null)
            {
                Destroy(previewObject);
            }
            
            // Create preview object at world level, NOT as child of UI
            previewObject = CreateTowerObject(currentTowerData, Vector3.zero);
            previewObject.name = "TowerPreview";
            
            // IMPORTANT: Make sure it's not a child of any UI element
            previewObject.transform.SetParent(null);
            
            // Make it semi-transparent
            Renderer renderer = previewObject.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                renderer = previewObject.AddComponent<MeshRenderer>();
            }
            
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = validPlacementColor;
                renderer.material = mat;
            }
            
            // Disable any colliders
            Collider[] colliders = previewObject.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
            
            // Position it initially at mouse position
            UpdatePreviewPosition();
        }
        
        private void CreateRangeIndicator()
        {
            if (rangeIndicator != null)
            {
                Destroy(rangeIndicator);
            }
            
            if (currentTowerData == null) return;
            
            // Créer un GameObject pour l'indicateur de portée
            rangeIndicator = new GameObject("RangeIndicator");
            
            // Option 1: Utiliser un LineRenderer pour dessiner un cercle
            LineRenderer lineRenderer = rangeIndicator.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = validRangeColor;
            lineRenderer.endColor = validRangeColor;
            lineRenderer.startWidth = rangeLineWidth;
            lineRenderer.endWidth = rangeLineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            
            // Créer les points du cercle
            float radius = currentTowerData.stats.range;
            lineRenderer.positionCount = rangeCircleSegments + 1;
            
            for (int i = 0; i <= rangeCircleSegments; i++)
            {
                float angle = (float)i / rangeCircleSegments * 2f * Mathf.PI;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                lineRenderer.SetPosition(i, new Vector3(x, y, -0.1f));
            }
            
            // Option 2: Ajouter aussi un disque semi-transparent
            GameObject rangeArea = new GameObject("RangeArea");
            rangeArea.transform.SetParent(rangeIndicator.transform);
            rangeArea.transform.localPosition = Vector3.zero;
            
            // Créer un mesh circulaire pour la zone
            MeshFilter meshFilter = rangeArea.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = rangeArea.AddComponent<MeshRenderer>();
            
            Mesh circleMesh = CreateCircleMesh(radius, rangeCircleSegments);
            meshFilter.mesh = circleMesh;
            
            Material areaMaterial = new Material(Shader.Find("Sprites/Default"));
            areaMaterial.color = new Color(validRangeColor.r, validRangeColor.g, validRangeColor.b, 0.15f);
            meshRenderer.material = areaMaterial;
            
            // S'assurer que le range indicator est au bon niveau Z
            rangeIndicator.transform.position = new Vector3(0, 0, -0.5f);
        }
        
        /// <summary>
        /// Crée un mesh circulaire pour afficher la zone de portée
        /// </summary>
        private Mesh CreateCircleMesh(float radius, int segments)
        {
            Mesh mesh = new Mesh();
            
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            
            // Centre du cercle
            vertices[0] = Vector3.zero;
            
            // Créer les vertices du cercle
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * 2f * Mathf.PI;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                vertices[i + 1] = new Vector3(x, y, 0);
            }
            
            // Créer les triangles
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0; // Centre
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        private GameObject CreateTowerObject(TowerData towerData, Vector3 position)
        {
            GameObject tower = new GameObject($"Tower_{towerData.name}");
            tower.transform.position = position;
            
            // Create visual
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(tower.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * towerData.visual.scale;
            
            // Set color based on tower type
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
                Color towerColor = Color.gray;
                
                if (!string.IsNullOrEmpty(towerData.visual.towerColor))
                {
                    ColorUtility.TryParseHtmlString(towerData.visual.towerColor, out towerColor);
                }
                
                mat.color = towerColor;
                renderer.material = mat;
            }
            
            // Add a simple turret on top
            GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            turret.name = "Turret";
            turret.transform.SetParent(tower.transform);
            turret.transform.localPosition = new Vector3(0, 0.5f, 0);
            turret.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            turret.transform.rotation = Quaternion.Euler(0, 0, 90);
            
            Renderer turretRenderer = turret.GetComponent<Renderer>();
            if (turretRenderer != null && renderer != null)
            {
                turretRenderer.material = renderer.material;
            }
            
            return tower;
        }
        
        #endregion
        
        #region Utility Methods
        
        private Vector3 GetMouseWorldPosition()
        {
            if (mainCamera == null) return Vector3.zero;
            
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            
            // For 2D, we want to hit the z=0 plane
            float distance = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * distance;
        }
        
        private Vector2Int WorldToGridPosition(Vector3 worldPos)
        {
            // If we have MapLoaderV3, use its conversion
            if (mapLoader != null)
            {
                return mapLoader.WorldToGridPosition(worldPos);
            }
            
            // Fallback conversion
            int x = Mathf.RoundToInt(worldPos.x / gridSize);
            int y = Mathf.RoundToInt(worldPos.y / gridSize);
            return new Vector2Int(x, y);
        }
        
        private Vector3 GridToWorldPosition(Vector2Int gridPos)
        {
            // If we have MapLoaderV3, use its conversion
            if (mapLoader != null)
            {
                return mapLoader.GridToWorldPosition(gridPos);
            }
            
            // Fallback conversion
            return new Vector3(
                gridPos.x * gridSize + gridSize * 0.5f,
                gridPos.y * gridSize + gridSize * 0.5f,
                0f
            );
        }
        
        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject();
        }
        
        #endregion
        
        #region Public API for External Systems
        
        /// <summary>
        /// Get a tower at a specific grid position
        /// </summary>
        public GameObject GetTowerAt(Vector2Int gridPos)
        {
            return placedTowers.ContainsKey(gridPos) ? placedTowers[gridPos] : null;
        }
        
        /// <summary>
        /// Remove a tower at a specific grid position
        /// </summary>
        public bool RemoveTowerAt(Vector2Int gridPos)
        {
            if (!placedTowers.ContainsKey(gridPos)) return false;
            
            GameObject tower = placedTowers[gridPos];
            placedTowers.Remove(gridPos);
            
            if (tower != null)
            {
                Destroy(tower);
            }
            
            return true;
        }
        
        /// <summary>
        /// Get all placed towers
        /// </summary>
        public List<GameObject> GetAllTowers()
        {
            return new List<GameObject>(placedTowers.Values);
        }
        
        #endregion
    }
}