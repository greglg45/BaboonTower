using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BaboonTower.Game
{
    /// <summary>
    /// Charge les maps crÃ©Ã©es avec l'Ã©diteur web
    /// </summary>
    public class MapLoaderV3 : MonoBehaviour
    {
        [Header("Map Files")]
        [SerializeField] private string customMapsFolder = "CustomMaps";
        public string defaultMapFile = "example_zigzag_map.json";
        
        [Header("Current Map")]
        [SerializeField] private CustomMapData currentMapData;
        [SerializeField] private string currentMapName;
        
        [Header("Visual Settings")]
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private bool showDebugInfo = true;
        
        [Header("Tile Prefabs")]
        [SerializeField] private GameObject emptyTilePrefab;
        [SerializeField] private GameObject pathTilePrefab;
        [SerializeField] private GameObject borderTilePrefab;
        [SerializeField] private GameObject spawnTilePrefab;
        [SerializeField] private GameObject castleTilePrefab;
        
        [Header("Decoration Prefabs")]
        [SerializeField] private List<GameObject> forestDecorations = new List<GameObject>();
        [SerializeField] private List<GameObject> desertDecorations = new List<GameObject>();
        [SerializeField] private List<GameObject> snowDecorations = new List<GameObject>();
        [SerializeField] private List<GameObject> volcanoDecorations = new List<GameObject>();
        
        [Header("Fallback Materials")]
        [SerializeField] private Material emptyMaterial;
        [SerializeField] private Material pathMaterial;
        [SerializeField] private Material borderMaterial;
        [SerializeField] private Material spawnMaterial;
        [SerializeField] private Material castleMaterial;
        
        // Grid data
        private TileType[,] mapGrid;
        private GameObject[,] tileObjects;
        private List<Vector3> worldPath;
        private List<CustomMapData> availableMaps;
        
        // Properties for compatibility
        public Vector2Int SpawnPos { get; private set; }
        public Vector2Int CastlePos { get; private set; }
        public List<Vector3> WorldPath => worldPath;
        public TileType[,] MapGrid => mapGrid;
        
        // Events
        public System.Action<CustomMapData> OnMapLoaded;
        public System.Action<List<Vector3>> OnPathCreated;
        
        private void Awake()
        {
            LoadAvailableMaps();
            worldPath = new List<Vector3>();
        }
        
        private void Start()
        {
            // Charger la map par dÃ©faut si elle existe
            if (!string.IsNullOrEmpty(defaultMapFile))
            {
                LoadMapFromFile(defaultMapFile);
            }
        }
        
        /// <summary>
        /// Charge toutes les maps disponibles
        /// </summary>
        private void LoadAvailableMaps()
        {
            availableMaps = new List<CustomMapData>();
            
            string mapsPath = Path.Combine(Application.streamingAssetsPath, customMapsFolder);
            
            if (!Directory.Exists(mapsPath))
            {
                Debug.LogWarning($"[MapLoaderV3] Custom maps folder not found: {mapsPath}");
                Directory.CreateDirectory(mapsPath);
                return;
            }
            
            string[] jsonFiles = Directory.GetFiles(mapsPath, "*.json");
            
            foreach (string file in jsonFiles)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    CustomMapData mapData = JsonUtility.FromJson<CustomMapData>(json);
                    
                    if (mapData != null)
                    {
                        availableMaps.Add(mapData);
                        Debug.Log($"[MapLoaderV3] Loaded map: {mapData.name} from {Path.GetFileName(file)}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MapLoaderV3] Error loading map from {file}: {e.Message}");
                }
            }
            
            Debug.Log($"[MapLoaderV3] Total maps loaded: {availableMaps.Count}");
        }
        
        /// <summary>
        /// Charge une map depuis un fichier
        /// </summary>
        public void LoadMapFromFile(string fileName)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, customMapsFolder, fileName);
            
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[MapLoaderV3] Map file not found: {fullPath}");
                return;
            }
            
            try
            {
                string json = File.ReadAllText(fullPath);
                CustomMapData mapData = JsonUtility.FromJson<CustomMapData>(json);
                
                if (mapData != null)
                {
                    LoadMap(mapData);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MapLoaderV3] Error loading map: {e.Message}");
            }
        }
        
        /// <summary>
        /// Charge une map par son ID
        /// </summary>
        public void LoadMapById(string mapId)
        {
            CustomMapData map = availableMaps.FirstOrDefault(m => m.id == mapId);
            
            if (map != null)
            {
                LoadMap(map);
            }
            else
            {
                Debug.LogError($"[MapLoaderV3] Map not found with ID: {mapId}");
            }
        }
        
        /// <summary>
        /// Charge une map
        /// </summary>
        public void LoadMap(CustomMapData mapData)
        {
            if (mapData == null)
            {
                Debug.LogError("[MapLoaderV3] Map data is null!");
                return;
            }
            
            Debug.Log($"[MapLoaderV3] Loading map: {mapData.name}");
            
            currentMapData = mapData;
            currentMapName = mapData.name;
            
            // Initialiser la grille
            CreateGridFromData(mapData);
            
            // CrÃ©er le chemin
            CreatePathFromData(mapData);
            
            // CrÃ©er les visuels
            CreateVisualMap();
            
            // Ajouter les dÃ©corations
            AddRandomDecorations();
            
            // Trigger events
            OnMapLoaded?.Invoke(mapData);
            OnPathCreated?.Invoke(worldPath);
            
            Debug.Log($"[MapLoaderV3] Map loaded successfully: {mapData.name}");
        }
        
        /// <summary>
        /// CrÃ©e la grille depuis les donnÃ©es
        /// </summary>
        private void CreateGridFromData(CustomMapData mapData)
        {
            int width = mapData.mapSize.width;
            int height = mapData.mapSize.height;
            
            mapGrid = new TileType[width, height];
            tileObjects = new GameObject[width, height];
            
            // Si on a les donnÃ©es de grille complÃ¨tes
            if (mapData.gridData != null && mapData.gridData.Length > 0)
            {
                for (int y = 0; y < height && y < mapData.gridData.Length; y++)
                {
                    if (mapData.gridData[y] != null)
                    {
                        for (int x = 0; x < width && x < mapData.gridData[y].Length; x++)
                        {
                            mapGrid[x, y] = (TileType)mapData.gridData[y][x];
                        }
                    }
                }
            }
            else
            {
                // Reconstruire depuis le chemin si pas de gridData
                ReconstructGridFromPath(mapData);
            }
            
            // DÃ©finir spawn et chÃ¢teau
            SpawnPos = mapData.spawnPosition.ToVector2Int();
            CastlePos = mapData.castlePosition.ToVector2Int();
            
            mapGrid[SpawnPos.x, SpawnPos.y] = TileType.Spawn;
            mapGrid[CastlePos.x, CastlePos.y] = TileType.Castle;
        }
        
        /// <summary>
        /// Reconstruit la grille depuis le chemin
        /// </summary>
        private void ReconstructGridFromPath(CustomMapData mapData)
        {
            // Initialiser tout en Empty
            for (int x = 0; x < mapData.mapSize.width; x++)
            {
                for (int y = 0; y < mapData.mapSize.height; y++)
                {
                    mapGrid[x, y] = TileType.Empty;
                }
            }
            
            // Placer le chemin
            foreach (var pathPoint in mapData.path)
            {
                Vector2Int pos = pathPoint.ToVector2Int();
                if (pos.x >= 0 && pos.x < mapData.mapSize.width &&
                    pos.y >= 0 && pos.y < mapData.mapSize.height)
                {
                    mapGrid[pos.x, pos.y] = TileType.Path;
                }
            }
            
            // Ajouter les bordures
            foreach (var pathPoint in mapData.path)
            {
                Vector2Int pos = pathPoint.ToVector2Int();
                
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        int nx = pos.x + dx;
                        int ny = pos.y + dy;
                        
                        if (nx >= 0 && nx < mapData.mapSize.width &&
                            ny >= 0 && ny < mapData.mapSize.height &&
                            mapGrid[nx, ny] == TileType.Empty)
                        {
                            mapGrid[nx, ny] = TileType.Border;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// CrÃ©e le chemin depuis les donnÃ©es
        /// </summary>
        private void CreatePathFromData(CustomMapData mapData)
        {
            worldPath.Clear();
            
            // Ajouter le spawn
            worldPath.Add(GridToWorldPosition(SpawnPos));
            
            // Ajouter tous les points du chemin
            foreach (var pathPoint in mapData.path)
            {
                Vector2Int pos = pathPoint.ToVector2Int();
                worldPath.Add(GridToWorldPosition(pos));
            }
            
            // Ajouter le chÃ¢teau si pas dÃ©jÃ  dans le chemin
            Vector3 castleWorldPos = GridToWorldPosition(CastlePos);
            if (worldPath.Count == 0 || Vector3.Distance(worldPath[worldPath.Count - 1], castleWorldPos) > 0.1f)
            {
                worldPath.Add(castleWorldPos);
            }
            
            Debug.Log($"[MapLoaderV3] Created path with {worldPath.Count} waypoints");
        }
        
        /// <summary>
        /// CrÃ©e la reprÃ©sentation visuelle
        /// </summary>
        private void CreateVisualMap()
        {
            // Nettoyer l'ancienne map
            ClearVisualMap();
            
            GameObject tilesContainer = new GameObject("MapTiles");
            tilesContainer.transform.SetParent(transform);
            
            for (int x = 0; x < mapGrid.GetLength(0); x++)
            {
                for (int y = 0; y < mapGrid.GetLength(1); y++)
                {
                    Vector2Int gridPos = new Vector2Int(x, y);
                    TileType tileType = mapGrid[x, y];
                    
                    GameObject tile = CreateTile(gridPos, tileType, tilesContainer.transform);
                    tileObjects[x, y] = tile;
                }
            }
            
            // CrÃ©er les marqueurs spÃ©ciaux pour spawn et chÃ¢teau
            CreateSpecialMarkers();
        }

        /// <summary>
        /// CrÃ©e une tuile visuelle
        /// </summary>
private GameObject CreateTile(Vector2Int gridPos, TileType type, Transform parent)
{
    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
    tile.transform.SetParent(parent);
    
    // Position SANS rotation - les quads sont déjà orientés face à la caméra
    Vector3 worldPos = GridToWorldPosition(gridPos);
    worldPos.z = 0f; // Assurez-vous que Z=0
    tile.transform.position = worldPos;
    tile.transform.rotation = Quaternion.identity; // PAS de rotation
    tile.transform.localScale = Vector3.one * tileSize * 0.95f;

    // Couleurs selon le type
    Renderer renderer = tile.GetComponent<Renderer>();
    if (renderer != null)
    {
        Material mat = new Material(Shader.Find("Standard"));
        
        switch (type)
        {
            case TileType.Path:
                mat.color = new Color(0.54f, 0.27f, 0.07f); // Marron
                break;
            case TileType.Border:
                mat.color = new Color(0.18f, 0.31f, 0.18f); // Vert foncé
                break;
            case TileType.Spawn:
                mat.color = Color.green;
                break;
            case TileType.Castle:
                mat.color = Color.blue;
                break;
            default:
                mat.color = new Color(0.6f, 0.6f, 0.6f); // Gris clair
                break;
        }
        
        renderer.material = mat;
    }

    tile.name = $"Tile_{gridPos.x}_{gridPos.y}_{type}";

    // Enlever le collider
    Collider col = tile.GetComponent<Collider>();
    if (col != null) Destroy(col);
    
    // LOG pour debug
    if (type == TileType.Path || type == TileType.Spawn || type == TileType.Castle)
    {
        Debug.Log($"Created tile {type} at position {worldPos}");
    }
    
    return tile;
}

        /// <summary>
        /// Obtient le prefab pour un type de tuile
        /// </summary>
        private GameObject GetTilePrefab(TileType type)
        {
            switch (type)
            {
                case TileType.Path:
                    return pathTilePrefab;
                case TileType.Border:
                    return borderTilePrefab;
                case TileType.Spawn:
                    return spawnTilePrefab;
                case TileType.Castle:
                    return castleTilePrefab;
                default:
                    return emptyTilePrefab;
            }
        }
        
        /// <summary>
        /// Obtient le matÃ©riau pour un type de tuile
        /// </summary>
        private Material GetTileMaterial(TileType type)
        {
            switch (type)
            {
                case TileType.Path:
                    return pathMaterial ?? CreateColorMaterial(new Color(0.54f, 0.27f, 0.07f));
                case TileType.Border:
                    return borderMaterial ?? CreateColorMaterial(new Color(0.18f, 0.31f, 0.18f));
                case TileType.Spawn:
                    return spawnMaterial ?? CreateColorMaterial(Color.green);
                case TileType.Castle:
                    return castleMaterial ?? CreateColorMaterial(Color.blue);
                default:
                    return emptyMaterial ?? CreateColorMaterial(new Color(0.29f, 0.29f, 0.29f));
            }
        }
        
        /// <summary>
        /// CrÃ©e un matÃ©riau avec une couleur
        /// </summary>
        private Material CreateColorMaterial(Color color)
        {
            Material mat = new Material(Shader.Find("Unlit/Color"));
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
            }
            mat.color = color;
            return mat;
        }
        
        /// <summary>
        /// CrÃ©e les marqueurs pour spawn et chÃ¢teau
        /// </summary>
        private void CreateSpecialMarkers()
        {
            // Marqueur de Spawn
            GameObject spawnMarker = new GameObject("SpawnMarker");
            spawnMarker.transform.SetParent(transform);
            spawnMarker.transform.position = GridToWorldPosition(SpawnPos) + Vector3.up * 0.1f;
            
            TextMesh spawnText = spawnMarker.AddComponent<TextMesh>();
            spawnText.text = "SPAWN";
            spawnText.fontSize = 20;
            spawnText.color = Color.green;
            spawnText.alignment = TextAlignment.Center;
            spawnText.anchor = TextAnchor.MiddleCenter;
            spawnMarker.transform.rotation = Quaternion.Euler(90, 0, 0);
            spawnMarker.transform.localScale = Vector3.one * 0.1f;
            
            // Marqueur de ChÃ¢teau
            GameObject castleMarker = new GameObject("CastleMarker");
            castleMarker.transform.SetParent(transform);
            castleMarker.transform.position = GridToWorldPosition(CastlePos) + Vector3.up * 0.1f;
            
            TextMesh castleText = castleMarker.AddComponent<TextMesh>();
            castleText.text = "CASTLE";
            castleText.fontSize = 20;
            castleText.color = Color.blue;
            castleText.alignment = TextAlignment.Center;
            castleText.anchor = TextAnchor.MiddleCenter;
            castleMarker.transform.rotation = Quaternion.Euler(90, 0, 0);
            castleMarker.transform.localScale = Vector3.one * 0.1f;
        }
        
        /// <summary>
        /// Ajoute des dÃ©corations alÃ©atoires
        /// </summary>
        private void AddRandomDecorations()
        {
            if (currentMapData == null) return;
            
            GameObject decorContainer = new GameObject("Decorations");
            decorContainer.transform.SetParent(transform);
            
            List<GameObject> decorPrefabs = GetDecorationPrefabs(currentMapData.biome);
            
            if (decorPrefabs == null || decorPrefabs.Count == 0)
            {
                Debug.LogWarning($"[MapLoaderV3] No decoration prefabs for biome: {currentMapData.biome}");
                return;
            }
            
            // Calculer le nombre de dÃ©corations
            int totalTiles = mapGrid.GetLength(0) * mapGrid.GetLength(1);
            int occupiedTiles = 0;
            
            for (int x = 0; x < mapGrid.GetLength(0); x++)
            {
                for (int y = 0; y < mapGrid.GetLength(1); y++)
                {
                    if (mapGrid[x, y] != TileType.Empty)
                    {
                        occupiedTiles++;
                    }
                }
            }
            
            int availableTiles = totalTiles - occupiedTiles;
            int decorCount = Mathf.RoundToInt(availableTiles * currentMapData.decorationDensity);
            
            // Placer les dÃ©corations
            List<Vector2Int> availablePositions = new List<Vector2Int>();
            
            for (int x = 0; x < mapGrid.GetLength(0); x++)
            {
                for (int y = 0; y < mapGrid.GetLength(1); y++)
                {
                    if (mapGrid[x, y] == TileType.Empty || 
                        (mapGrid[x, y] == TileType.Border && Random.value < 0.3f))
                    {
                        availablePositions.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            for (int i = 0; i < decorCount && availablePositions.Count > 0; i++)
            {
                int index = Random.Range(0, availablePositions.Count);
                Vector2Int pos = availablePositions[index];
                availablePositions.RemoveAt(index);
                
                GameObject decorPrefab = decorPrefabs[Random.Range(0, decorPrefabs.Count)];
                
                if (decorPrefab != null)
                {
                    GameObject decor = Instantiate(decorPrefab, decorContainer.transform);
                    decor.transform.position = GridToWorldPosition(pos) + Vector3.up * 0.1f;
                    decor.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    decor.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
                    decor.name = $"Decor_{pos.x}_{pos.y}";
                }
                else
                {
                    // Fallback: crÃ©er un cube simple
                    GameObject decor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    decor.transform.SetParent(decorContainer.transform);
                    decor.transform.position = GridToWorldPosition(pos) + Vector3.up * 0.3f;
                    decor.transform.localScale = Vector3.one * 0.6f;
                    
                    Renderer renderer = decor.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = CreateColorMaterial(new Color(0.2f, 0.5f, 0.2f));
                    }
                }
                
                // Marquer la tuile comme dÃ©coration
                mapGrid[pos.x, pos.y] = TileType.Decoration;
            }
            
            Debug.Log($"[MapLoaderV3] Added {decorCount} decorations");
        }
        
        /// <summary>
        /// Obtient les prefabs de dÃ©coration pour un biome
        /// </summary>
        private List<GameObject> GetDecorationPrefabs(string biome)
        {
            switch (biome.ToLower())
            {
                case "forest":
                    return forestDecorations;
                case "desert":
                    return desertDecorations;
                case "snow":
                    return snowDecorations;
                case "volcano":
                    return volcanoDecorations;
                default:
                    return forestDecorations; // Fallback
            }
        }
        
        /// <summary>
        /// Nettoie la map visuelle
        /// </summary>
        private void ClearVisualMap()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            
            if (tileObjects != null)
            {
                for (int x = 0; x < tileObjects.GetLength(0); x++)
                {
                    for (int y = 0; y < tileObjects.GetLength(1); y++)
                    {
                        if (tileObjects[x, y] != null)
                        {
                            DestroyImmediate(tileObjects[x, y]);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Convertit une position de grille en position monde
        /// </summary>
        public Vector3 GridToWorldPosition(Vector2Int gridPos)
        {
            return new Vector3(
                gridPos.x * tileSize + tileSize * 0.5f,
                gridPos.y * tileSize + tileSize * 0.5f,
                0f
            );
        }
        
        /// <summary>
        /// Convertit une position monde en position de grille
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / tileSize),
                Mathf.FloorToInt(worldPos.y / tileSize)
            );
        }
        
        /// <summary>
        /// VÃ©rifie si une tour peut Ãªtre placÃ©e
        /// </summary>
        public bool CanPlaceTowerAt(Vector2Int gridPos)
        {
            if (gridPos.x < 0 || gridPos.x >= mapGrid.GetLength(0) ||
                gridPos.y < 0 || gridPos.y >= mapGrid.GetLength(1))
                return false;
            
            TileType tile = mapGrid[gridPos.x, gridPos.y];
            return tile == TileType.Empty || tile == TileType.Border;
        }
        
        /// <summary>
        /// Obtient la liste des maps disponibles
        /// </summary>
        public List<string> GetAvailableMapNames()
        {
            return availableMaps.Select(m => m.name).ToList();
        }
        
        /// <summary>
        /// Obtient la liste des IDs de maps
        /// </summary>
        public List<string> GetAvailableMapIds()
        {
            return availableMaps.Select(m => m.id).ToList();
        }
        
        #region Editor Methods
        
        [ContextMenu("Reload Available Maps")]
        private void ReloadMaps()
        {
            LoadAvailableMaps();
            Debug.Log($"[MapLoaderV3] Reloaded {availableMaps.Count} maps");
        }
        
        [ContextMenu("Clear Visual Map")]
        private void EditorClearMap()
        {
            ClearVisualMap();
        }
        
        [ContextMenu("Load First Map")]
        private void LoadFirstMap()
        {
            if (availableMaps != null && availableMaps.Count > 0)
            {
                LoadMap(availableMaps[0]);
            }
        }
        
        #endregion
        
        private void OnDrawGizmos()
        {
            if (!showDebugInfo || worldPath == null || worldPath.Count < 2) return;
            
            // Dessiner le chemin
            Gizmos.color = Color.yellow;
            for (int i = 0; i < worldPath.Count - 1; i++)
            {
                Gizmos.DrawLine(worldPath[i], worldPath[i + 1]);
                Gizmos.DrawWireSphere(worldPath[i], 0.1f);
            }
            
            // Marquer spawn et chÃ¢teau
            if (SpawnPos != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(GridToWorldPosition(SpawnPos), 0.3f);
            }
            
            if (CastlePos != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(GridToWorldPosition(CastlePos), 0.3f);
            }
        }
    }
}