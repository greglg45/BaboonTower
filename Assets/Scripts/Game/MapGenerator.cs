using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace BaboonTower.Game
{
    /// <summary>
    /// Types de tuiles pour la génération de map
    /// </summary>
    public enum TileType
    {
        Empty,      // Tuile vide (pour placement de tours)
        Path,       // Chemin pour les ennemis
        Border,     // Contour de route
        Spawn,      // Point de spawn
        Castle,     // Château à défendre
        Decoration  // Décoration (bloque les tours)
    }

    /// <summary>
    /// Direction pour la génération de chemin
    /// </summary>
    public enum Direction
    {
        None = 0,
        Up = 1,
        Right = 2,
        Down = 3,
        Left = 4
    }

    /// <summary>
    /// Configuration d'une map chargée depuis JSON
    /// </summary>
    [System.Serializable]
    public class MapConfig
    {
        public string id;
        public string name;
        public string biome;
        public int difficulty;
        public MapSize mapSize;
        public Position spawnPosition;
        public Position castlePosition;
        public PathGenerationConfig pathGeneration;
        public DecorationConfig decorations;
        public TileSetConfig tileSet;
        public int seed;
        public bool validated;
        public List<Position> generatedPath;
    }

    [System.Serializable]
    public class MapSize
    {
        public int width;
        public int height;
    }

    [System.Serializable]
    public class Position
    {
        public int x;
        public int y;

        public Position(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }
    }

    [System.Serializable]
    public class PathGenerationConfig
    {
        public int targetLength;
        public int lengthMargin;
        public int minLength;
        public int maxLength;
        public int numberOfTurns;
        public int numberOfUTurns;
        public int minSegmentLength;
        public int maxSegmentLength;
        public bool allowDiagonalPath;
    }

    [System.Serializable]
    public class DecorationConfig
    {
        public float density;
        public List<DecorationType> types;
    }

    [System.Serializable]
    public class DecorationType
    {
        public string name;
        public float weight;
        public bool blocksTowers;
        public bool allowOnBorder;
    }

    [System.Serializable]
    public class TileSetConfig
    {
        public string ground;
        public string path;
        public string border;
        public string spawn;
        public string castle;
    }

    [System.Serializable]
    public class MapConfigContainer
    {
        public string version;
        public string currentMapId;
        public List<MapConfig> maps;
        public GenerationSettings generationSettings;
    }

    [System.Serializable]
    public class GenerationSettings
    {
        public int maxGenerationAttempts;
        public bool pathValidationStrict;
        public bool allowSavingGeneratedMaps;
        public bool debugMode;
        public bool visualizeGeneration;
    }

    /// <summary>
    /// Générateur procédural de maps pour Baboon Tower
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string configFileName = "MapConfig.json";
        [SerializeField] private bool debugMode = true;

        [Header("Current Map")]
        [SerializeField] private MapConfig currentMapConfig;
        [SerializeField] private TileType[,] mapGrid;
        [SerializeField] private List<Vector2Int> generatedPath;

        private MapConfigContainer configContainer;
        private System.Random random;

        // Events
        public System.Action<TileType[,]> OnMapGenerated;
        public System.Action<List<Vector2Int>> OnPathGenerated;

        private void Awake()
        {
            LoadConfiguration();
        }

        /// <summary>
        /// Charge la configuration depuis le fichier JSON
        /// </summary>
        private void LoadConfiguration()
        {
            // Essayer plusieurs chemins pour le fichier de configuration
            string[] possiblePaths = new string[]
            {
                Path.Combine(Application.streamingAssetsPath, configFileName),
                Path.Combine(Application.dataPath, "StreamingAssets", configFileName),
                Path.Combine(Application.dataPath, configFileName)
            };

            string configPath = null;
            foreach (string path in possiblePaths)
            {
                Debug.Log($"[MapGenerator] Checking path: {path}");
                if (File.Exists(path))
                {
                    configPath = path;
                    Debug.Log($"[MapGenerator] Config file found at: {path}");
                    break;
                }
            }

            if (configPath != null)
            {
                string json = File.ReadAllText(configPath);
                configContainer = JsonUtility.FromJson<MapConfigContainer>(json);
                Debug.Log($"[MapGenerator] Configuration loaded: {configContainer.maps.Count} maps found");

                // Charger la map par défaut
                LoadMap(configContainer.currentMapId);
            }
            else
            {
                Debug.LogError($"[MapGenerator] Configuration file not found! Searched paths:");
                foreach (string path in possiblePaths)
                {
                    Debug.LogError($"  - {path}");
                }

                // Créer une configuration par défaut
                Debug.LogWarning("[MapGenerator] Creating default configuration...");
                CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// Crée une configuration par défaut si le fichier n'est pas trouvé
        /// </summary>
        private void CreateDefaultConfiguration()
        {
            configContainer = new MapConfigContainer();
            configContainer.version = "1.0.0";
            configContainer.currentMapId = "default_map";
            configContainer.maps = new List<MapConfig>();

            // Créer une map par défaut simple
            MapConfig defaultMap = new MapConfig();
            defaultMap.id = "default_map";
            defaultMap.name = "Default Map";
            defaultMap.biome = "forest";
            defaultMap.difficulty = 1;

            defaultMap.mapSize = new MapSize { width = 30, height = 16 };
            defaultMap.spawnPosition = new Position(0, 8);
            defaultMap.castlePosition = new Position(29, 8);

            defaultMap.pathGeneration = new PathGenerationConfig
            {
                targetLength = 45,
                lengthMargin = 5,
                minLength = 40,
                maxLength = 50,
                numberOfTurns = 2,
                numberOfUTurns = 1,
                minSegmentLength = 3,
                maxSegmentLength = 8,
                allowDiagonalPath = false
            };

            defaultMap.decorations = new DecorationConfig
            {
                density = 0.15f,
                types = new List<DecorationType>()
            };

            defaultMap.tileSet = new TileSetConfig
            {
                ground = "grass",
                path = "dirt",
                border = "grass_dark",
                spawn = "spawn",
                castle = "castle"
            };

            defaultMap.seed = 42;
            defaultMap.validated = false;

            configContainer.maps.Add(defaultMap);

            configContainer.generationSettings = new GenerationSettings
            {
                maxGenerationAttempts = 100,
                pathValidationStrict = true,
                allowSavingGeneratedMaps = true,
                debugMode = true,
                visualizeGeneration = false
            };

            Debug.Log("[MapGenerator] Default configuration created");

            // Charger la map par défaut
            currentMapConfig = defaultMap;
        }

        /// <summary>
        /// Charge une map spécifique par son ID
        /// </summary>
        public void LoadMap(string mapId)
        {
            currentMapConfig = configContainer.maps.FirstOrDefault(m => m.id == mapId);

            if (currentMapConfig == null)
            {
                Debug.LogError($"Map not found: {mapId}");
                return;
            }

            Debug.Log($"Loading map: {currentMapConfig.name} ({currentMapConfig.biome})");
            GenerateMap();
        }

        /// <summary>
        /// Génère la map complète
        /// </summary>
        public void GenerateMap()
        {
            Debug.Log("[MapGenerator] GenerateMap called");

            if (currentMapConfig == null)
            {
                Debug.LogError("[MapGenerator] No map configuration loaded! Trying to load default...");
                LoadConfiguration();

                if (currentMapConfig == null)
                {
                    Debug.LogError("[MapGenerator] Still no configuration after loading attempt!");
                    return;
                }
            }

            Debug.Log($"[MapGenerator] Generating map: {currentMapConfig.name} ({currentMapConfig.id})");

            // Initialiser le générateur aléatoire avec la seed
            random = new System.Random(currentMapConfig.seed);

            // Initialiser la grille
            int width = currentMapConfig.mapSize.width;
            int height = currentMapConfig.mapSize.height;
            mapGrid = new TileType[width, height];

            Debug.Log($"[MapGenerator] Map grid initialized: {width}x{height}");

            // Étape 1: Générer le chemin
            bool pathGenerated = GeneratePath();

            if (!pathGenerated)
            {
                Debug.LogError("[MapGenerator] Failed to generate valid path! Using fallback simple path...");
                // Créer un chemin simple comme fallback
                CreateSimplePath();
            }

            // Étape 2: Ajouter les bordures de route
            AddPathBorders();
            Debug.Log("[MapGenerator] Path borders added");

            // Étape 3: Placer spawn et château
            PlaceSpawnAndCastle();
            Debug.Log("[MapGenerator] Spawn and castle placed");

            // Étape 4: Ajouter les décorations
            AddDecorations();
            Debug.Log("[MapGenerator] Decorations added");

            // Déclencher les events
            Debug.Log("[MapGenerator] Triggering events...");
            OnMapGenerated?.Invoke(mapGrid);
            OnPathGenerated?.Invoke(generatedPath);

            if (debugMode)
            {
                DebugPrintMap();
            }

            Debug.Log("[MapGenerator] Map generation complete!");
        }

        /// <summary>
        /// Crée un chemin simple comme fallback
        /// </summary>
        private void CreateSimplePath()
        {
            generatedPath = new List<Vector2Int>();
            Vector2Int start = currentMapConfig.spawnPosition.ToVector2Int();
            Vector2Int end = currentMapConfig.castlePosition.ToVector2Int();

            // Réinitialiser la grille
            for (int x = 0; x < mapGrid.GetLength(0); x++)
            {
                for (int y = 0; y < mapGrid.GetLength(1); y++)
                {
                    mapGrid[x, y] = TileType.Empty;
                }
            }

            // Créer un chemin en L simple
            Vector2Int current = start;

            // Aller horizontalement jusqu'à la colonne du château
            while (current.x != end.x)
            {
                generatedPath.Add(current);
                mapGrid[current.x, current.y] = TileType.Path;
                current.x += (current.x < end.x) ? 1 : -1;
            }

            // Aller verticalement jusqu'au château
            while (current.y != end.y)
            {
                generatedPath.Add(current);
                mapGrid[current.x, current.y] = TileType.Path;
                current.y += (current.y < end.y) ? 1 : -1;
            }

            // Ajouter la position finale
            generatedPath.Add(end);
            mapGrid[end.x, end.y] = TileType.Path;

            Debug.Log($"[MapGenerator] Simple fallback path created with {generatedPath.Count} tiles");
        }

        /// <summary>
        /// Génère le chemin entre spawn et château
        /// </summary>
        private bool GeneratePath()
        {
            var config = currentMapConfig.pathGeneration;

            Debug.Log($"[MapGenerator] Starting path generation: turns={config.numberOfTurns}");

            // Pour les maps avec beaucoup de virages demandés, utiliser directement le zigzag
            if (config.numberOfTurns >= 3)
            {
                Debug.Log($"[MapGenerator] High turn count requested ({config.numberOfTurns}), using zigzag algorithm");
                CreateZigzagPath(config.numberOfTurns);
                return true;
            }

            // Sinon essayer la génération normale
            int attempts = 0;
            int maxAttempts = 5;

            while (attempts < maxAttempts)
            {
                attempts++;
                generatedPath = new List<Vector2Int>();

                // Réinitialiser la grille
                for (int x = 0; x < mapGrid.GetLength(0); x++)
                {
                    for (int y = 0; y < mapGrid.GetLength(1); y++)
                    {
                        mapGrid[x, y] = TileType.Empty;
                    }
                }

                Vector2Int start = currentMapConfig.spawnPosition.ToVector2Int();
                Vector2Int target = currentMapConfig.castlePosition.ToVector2Int();

                if (GenerateZigzagPath(start, target, config))
                {
                    int pathLength = generatedPath.Count;
                    Debug.Log($"[MapGenerator] Path generated with {pathLength} tiles and {CountTurns()} turns");
                    return true;
                }
            }

            // Fallback final
            Debug.Log("[MapGenerator] Using forced zigzag fallback");
            CreateZigzagPath(config.numberOfTurns);
            return true;
        }

        /// <summary>
        /// Crée un chemin en zigzag avec un nombre garanti de virages
        /// </summary>
        private void CreateZigzagPath(int desiredTurns)
        {
            generatedPath = new List<Vector2Int>();
            Vector2Int start = currentMapConfig.spawnPosition.ToVector2Int();
            Vector2Int end = currentMapConfig.castlePosition.ToVector2Int();

            // Réinitialiser la grille
            for (int x = 0; x < mapGrid.GetLength(0); x++)
            {
                for (int y = 0; y < mapGrid.GetLength(1); y++)
                {
                    mapGrid[x, y] = TileType.Empty;
                }
            }

            generatedPath.Add(start);
            mapGrid[start.x, start.y] = TileType.Path;

            Vector2Int current = start;

            // Calculer la taille des segments basée sur le nombre de virages souhaités
            int totalDistance = Mathf.Abs(end.x - start.x) + Mathf.Abs(end.y - start.y);
            int segmentSize = Mathf.Max(3, totalDistance / (desiredTurns + 1));

            // Créer les waypoints intermédiaires
            List<Vector2Int> waypoints = new List<Vector2Int>();

            // Générer des waypoints en zigzag
            for (int i = 0; i < desiredTurns; i++)
            {
                float t = (float)(i + 1) / (desiredTurns + 1);

                // Position interpolée
                int baseX = Mathf.RoundToInt(Mathf.Lerp(start.x, end.x, t));
                int baseY = Mathf.RoundToInt(Mathf.Lerp(start.y, end.y, t));

                // Ajouter un décalage en zigzag
                if (i % 2 == 0)
                {
                    // Décaler en haut/droite
                    if (Mathf.Abs(end.x - start.x) > Mathf.Abs(end.y - start.y))
                    {
                        baseY = Mathf.Clamp(baseY + random.Next(3, 6), 1, mapGrid.GetLength(1) - 2);
                    }
                    else
                    {
                        baseX = Mathf.Clamp(baseX + random.Next(3, 6), 1, mapGrid.GetLength(0) - 2);
                    }
                }
                else
                {
                    // Décaler en bas/gauche
                    if (Mathf.Abs(end.x - start.x) > Mathf.Abs(end.y - start.y))
                    {
                        baseY = Mathf.Clamp(baseY - random.Next(3, 6), 1, mapGrid.GetLength(1) - 2);
                    }
                    else
                    {
                        baseX = Mathf.Clamp(baseX - random.Next(3, 6), 1, mapGrid.GetLength(0) - 2);
                    }
                }

                waypoints.Add(new Vector2Int(baseX, baseY));
            }

            waypoints.Add(end);

            // Connecter les waypoints avec des chemins en L
            foreach (var waypoint in waypoints)
            {
                // Aller d'abord horizontalement puis verticalement (ou l'inverse)
                bool horizontalFirst = random.NextDouble() > 0.5;

                if (horizontalFirst)
                {
                    // Horizontal
                    while (current.x != waypoint.x)
                    {
                        current.x += (waypoint.x > current.x) ? 1 : -1;
                        if (!generatedPath.Contains(current))
                        {
                            generatedPath.Add(current);
                            mapGrid[current.x, current.y] = TileType.Path;
                        }
                    }

                    // Vertical
                    while (current.y != waypoint.y)
                    {
                        current.y += (waypoint.y > current.y) ? 1 : -1;
                        if (!generatedPath.Contains(current))
                        {
                            generatedPath.Add(current);
                            mapGrid[current.x, current.y] = TileType.Path;
                        }
                    }
                }
                else
                {
                    // Vertical
                    while (current.y != waypoint.y)
                    {
                        current.y += (waypoint.y > current.y) ? 1 : -1;
                        if (!generatedPath.Contains(current))
                        {
                            generatedPath.Add(current);
                            mapGrid[current.x, current.y] = TileType.Path;
                        }
                    }

                    // Horizontal
                    while (current.x != waypoint.x)
                    {
                        current.x += (waypoint.x > current.x) ? 1 : -1;
                        if (!generatedPath.Contains(current))
                        {
                            generatedPath.Add(current);
                            mapGrid[current.x, current.y] = TileType.Path;
                        }
                    }
                }
            }

            int actualTurns = CountTurns();
            Debug.Log($"[MapGenerator] Zigzag path created: {generatedPath.Count} tiles, {actualTurns} turns (target was {desiredTurns})");
        }

        /// <summary>
        /// Génère un chemin en zigzag avec virages garantis
        /// </summary>
        private bool GenerateZigzagPath(Vector2Int start, Vector2Int target, PathGenerationConfig config)
        {
            generatedPath.Clear();
            generatedPath.Add(start);
            mapGrid[start.x, start.y] = TileType.Path;

            Vector2Int current = start;
            int totalTurns = config.numberOfTurns;

            // Créer des waypoints intermédiaires pour forcer les virages
            List<Vector2Int> waypoints = new List<Vector2Int>();

            // Calculer les waypoints en zigzag
            for (int i = 0; i < totalTurns; i++)
            {
                float progress = (float)(i + 1) / (totalTurns + 1);

                // Position interpolée entre start et target
                int baseX = Mathf.RoundToInt(Mathf.Lerp(start.x, target.x, progress));
                int baseY = Mathf.RoundToInt(Mathf.Lerp(start.y, target.y, progress));

                // Créer un zigzag en alternant les décalages
                if (i % 2 == 0)
                {
                    // Décalage vers le haut ou la droite
                    if (Mathf.Abs(target.x - start.x) > Mathf.Abs(target.y - start.y))
                    {
                        // Chemin principalement horizontal, faire des virages verticaux
                        baseY = Mathf.Clamp(baseY + random.Next(3, 7), 2, mapGrid.GetLength(1) - 3);
                    }
                    else
                    {
                        // Chemin principalement vertical, faire des virages horizontaux
                        baseX = Mathf.Clamp(baseX + random.Next(3, 7), 2, mapGrid.GetLength(0) - 3);
                    }
                }
                else
                {
                    // Décalage vers le bas ou la gauche
                    if (Mathf.Abs(target.x - start.x) > Mathf.Abs(target.y - start.y))
                    {
                        baseY = Mathf.Clamp(baseY - random.Next(3, 7), 2, mapGrid.GetLength(1) - 3);
                    }
                    else
                    {
                        baseX = Mathf.Clamp(baseX - random.Next(3, 7), 2, mapGrid.GetLength(0) - 3);
                    }
                }

                waypoints.Add(new Vector2Int(baseX, baseY));
            }

            // Ajouter le château comme dernier waypoint
            waypoints.Add(target);

            Debug.Log($"[MapGenerator] Creating path with {waypoints.Count} waypoints");

            // Connecter les waypoints
            foreach (var waypoint in waypoints)
            {
                if (!ConnectPoints(current, waypoint))
                {
                    Debug.Log($"[MapGenerator] Failed to connect {current} to {waypoint}");
                    return false;
                }
                current = waypoint;
            }

            return true;
        }

        /// <summary>
        /// Connecte deux points avec un chemin en L
        /// </summary>
        private bool ConnectPoints(Vector2Int from, Vector2Int to)
        {
            if (from == to) return true;

            // Décider si on va d'abord horizontalement ou verticalement
            bool goHorizontalFirst = random.NextDouble() > 0.5;

            Vector2Int corner;
            if (goHorizontalFirst)
            {
                corner = new Vector2Int(to.x, from.y);
            }
            else
            {
                corner = new Vector2Int(from.x, to.y);
            }

            // Tracer la première partie du L
            if (!TraceLine(from, corner))
            {
                // Si ça échoue, essayer l'autre sens
                corner = goHorizontalFirst ?
                    new Vector2Int(from.x, to.y) :
                    new Vector2Int(to.x, from.y);

                if (!TraceLine(from, corner))
                {
                    return false;
                }
            }

            // Tracer la deuxième partie du L
            return TraceLine(corner, to);
        }

        /// <summary>
        /// Trace une ligne droite entre deux points
        /// </summary>
        private bool TraceLine(Vector2Int from, Vector2Int to)
        {
            int dx = (to.x > from.x) ? 1 : (to.x < from.x) ? -1 : 0;
            int dy = (to.y > from.y) ? 1 : (to.y < from.y) ? -1 : 0;

            Vector2Int current = from;

            while (current != to)
            {
                current.x += dx;
                current.y += dy;

                // Vérifier les limites
                if (current.x < 0 || current.x >= mapGrid.GetLength(0) ||
                    current.y < 0 || current.y >= mapGrid.GetLength(1))
                {
                    return false;
                }

                // Vérifier qu'on ne croise pas un chemin existant (sauf le château)
                if (mapGrid[current.x, current.y] == TileType.Path && current != to)
                {
                    return false;
                }

                // Vérifier qu'on garde une distance minimale avec les chemins existants
                if (!CheckMinimumDistance(current, from) && current != to)
                {
                    return false;
                }

                // Ajouter au chemin
                generatedPath.Add(current);
                mapGrid[current.x, current.y] = TileType.Path;
            }

            return true;
        }

        /// <summary>
        /// Vérifie qu'on garde une distance minimale avec les chemins existants
        /// </summary>
        private bool CheckMinimumDistance(Vector2Int pos, Vector2Int exceptFrom)
        {
            // Vérifier les 8 cases autour
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue; // Ignorer la case elle-même

                    int checkX = pos.x + dx;
                    int checkY = pos.y + dy;

                    // Ignorer la case d'où on vient
                    if (checkX == exceptFrom.x && checkY == exceptFrom.y) continue;

                    // Vérifier les limites
                    if (checkX >= 0 && checkX < mapGrid.GetLength(0) &&
                        checkY >= 0 && checkY < mapGrid.GetLength(1))
                    {
                        // S'il y a déjà un chemin adjacent (sauf diagonale), c'est invalide
                        if (mapGrid[checkX, checkY] == TileType.Path)
                        {
                            // Permettre les connexions orthogonales seulement depuis exceptFrom
                            if (dx == 0 || dy == 0) // Connexion orthogonale
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Crée un chemin simple avec virages forcés (fallback)
        /// </summary>
        private void CreateForcedTurnPath()
        {
            generatedPath = new List<Vector2Int>();
            Vector2Int start = currentMapConfig.spawnPosition.ToVector2Int();
            Vector2Int end = currentMapConfig.castlePosition.ToVector2Int();

            // Réinitialiser la grille
            for (int x = 0; x < mapGrid.GetLength(0); x++)
            {
                for (int y = 0; y < mapGrid.GetLength(1); y++)
                {
                    mapGrid[x, y] = TileType.Empty;
                }
            }

            generatedPath.Add(start);
            mapGrid[start.x, start.y] = TileType.Path;

            Vector2Int current = start;

            // Créer un chemin en escalier / zigzag
            int stepSize = 4; // Taille des segments
            bool goingRight = end.x > start.x;
            bool goingUp = end.y > start.y;

            while (current != end)
            {
                // Alterner entre horizontal et vertical
                bool moveHorizontal = ((current.x + current.y) / stepSize) % 2 == 0;

                if (moveHorizontal && current.x != end.x)
                {
                    // Mouvement horizontal
                    int targetX = goingRight ?
                        Mathf.Min(current.x + stepSize, end.x) :
                        Mathf.Max(current.x - stepSize, end.x);

                    while (current.x != targetX)
                    {
                        current.x += goingRight ? 1 : -1;
                        generatedPath.Add(current);
                        mapGrid[current.x, current.y] = TileType.Path;
                    }
                }
                else if (!moveHorizontal && current.y != end.y)
                {
                    // Mouvement vertical
                    int targetY = goingUp ?
                        Mathf.Min(current.y + stepSize, end.y) :
                        Mathf.Max(current.y - stepSize, end.y);

                    while (current.y != targetY)
                    {
                        current.y += goingUp ? 1 : -1;
                        generatedPath.Add(current);
                        mapGrid[current.x, current.y] = TileType.Path;
                    }
                }
                else
                {
                    // Si on ne peut pas bouger dans la direction prévue, aller vers la cible
                    if (current.x != end.x)
                    {
                        current.x += (end.x > current.x) ? 1 : -1;
                        generatedPath.Add(current);
                        mapGrid[current.x, current.y] = TileType.Path;
                    }
                    else if (current.y != end.y)
                    {
                        current.y += (end.y > current.y) ? 1 : -1;
                        generatedPath.Add(current);
                        mapGrid[current.x, current.y] = TileType.Path;
                    }
                }

                // Sécurité pour éviter les boucles infinies
                if (generatedPath.Count > 200)
                {
                    Debug.LogWarning("[MapGenerator] Path too long, breaking");
                    break;
                }
            }

            // S'assurer qu'on atteint le château
            if (current != end)
            {
                // Connexion directe finale si nécessaire
                while (current.x != end.x)
                {
                    current.x += (end.x > current.x) ? 1 : -1;
                    if (!generatedPath.Contains(current))
                    {
                        generatedPath.Add(current);
                        mapGrid[current.x, current.y] = TileType.Path;
                    }
                }

                while (current.y != end.y)
                {
                    current.y += (end.y > current.y) ? 1 : -1;
                    if (!generatedPath.Contains(current))
                    {
                        generatedPath.Add(current);
                        mapGrid[current.x, current.y] = TileType.Path;
                    }
                }
            }

            int turns = CountTurns();
            Debug.Log($"[MapGenerator] Forced zigzag path created with {generatedPath.Count} tiles and {turns} turns");
        }

        /// <summary>
        /// Compte le nombre de virages dans le chemin généré
        /// </summary>
        private int CountTurns()
        {
            if (generatedPath.Count < 3) return 0;

            int turns = 0;
            Direction lastDirection = Direction.None;

            for (int i = 1; i < generatedPath.Count; i++)
            {
                Vector2Int delta = generatedPath[i] - generatedPath[i - 1];
                Direction currentDirection = Direction.None;

                if (delta.x > 0) currentDirection = Direction.Right;
                else if (delta.x < 0) currentDirection = Direction.Left;
                else if (delta.y > 0) currentDirection = Direction.Up;
                else if (delta.y < 0) currentDirection = Direction.Down;

                if (lastDirection != Direction.None && currentDirection != lastDirection)
                {
                    turns++;
                }

                lastDirection = currentDirection;
            }

            return turns;
        }

        /// <summary>
        /// Génère des points de virage intermédiaires
        /// </summary>
        private List<Vector2Int> GenerateTurnPoints(Vector2Int start, Vector2Int end, int numberOfTurns)
        {
            List<Vector2Int> turnPoints = new List<Vector2Int>();

            if (numberOfTurns <= 0)
                return turnPoints;

            int mapWidth = mapGrid.GetLength(0);
            int mapHeight = mapGrid.GetLength(1);

            // Créer des points de virage en zigzag
            for (int i = 0; i < numberOfTurns; i++)
            {
                float t = (float)(i + 1) / (numberOfTurns + 1);

                // Point interpolé entre start et end
                int baseX = Mathf.RoundToInt(Mathf.Lerp(start.x, end.x, t));
                int baseY = Mathf.RoundToInt(Mathf.Lerp(start.y, end.y, t));

                // Ajouter un décalage pour créer un virage
                int offsetRange = Mathf.Min(5, mapWidth / 6, mapHeight / 6);
                int offsetX = random.Next(-offsetRange, offsetRange);
                int offsetY = random.Next(-offsetRange, offsetRange);

                // Alterner le côté du décalage pour créer un zigzag
                if (i % 2 == 0)
                {
                    baseX = Mathf.Clamp(baseX + offsetX, 2, mapWidth - 3);
                    baseY = Mathf.Clamp(baseY - offsetY, 2, mapHeight - 3);
                }
                else
                {
                    baseX = Mathf.Clamp(baseX - offsetX, 2, mapWidth - 3);
                    baseY = Mathf.Clamp(baseY + offsetY, 2, mapHeight - 3);
                }

                turnPoints.Add(new Vector2Int(baseX, baseY));
            }

            Debug.Log($"[MapGenerator] Created {turnPoints.Count} turn points for path generation");
            return turnPoints;
        }

        /// <summary>
        /// Obtient la meilleure direction vers une cible
        /// </summary>
        private Direction GetBestDirectionToTarget(Vector2Int from, Vector2Int to, Direction lastDirection)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;

            // Prioriser la direction avec la plus grande distance
            if (Mathf.Abs(dx) > Mathf.Abs(dy))
            {
                return dx > 0 ? Direction.Right : Direction.Left;
            }
            else if (dy != 0)
            {
                return dy > 0 ? Direction.Up : Direction.Down;
            }
            else if (dx != 0)
            {
                return dx > 0 ? Direction.Right : Direction.Left;
            }

            return Direction.None;
        }

        /// <summary>
        /// Obtient des directions alternatives
        /// </summary>
        private List<Direction> GetAlternativeDirections(Vector2Int from, Vector2Int to, Direction preferred)
        {
            List<Direction> alternatives = new List<Direction>();

            // Ajouter les perpendiculaires à la direction préférée
            switch (preferred)
            {
                case Direction.Up:
                case Direction.Down:
                    alternatives.Add(Direction.Left);
                    alternatives.Add(Direction.Right);
                    break;
                case Direction.Left:
                case Direction.Right:
                    alternatives.Add(Direction.Up);
                    alternatives.Add(Direction.Down);
                    break;
            }

            // Ajouter l'opposé en dernier recours
            alternatives.Add(GetOppositeDirection(preferred));

            return alternatives;
        }

        /// <summary>
        /// Vérifie si une position est valide pour un nouveau segment de chemin
        /// </summary>
        private bool IsValidNewPathPosition(Vector2Int pos, Vector2Int from)
        {
            // Vérifier les limites
            if (pos.x < 0 || pos.x >= mapGrid.GetLength(0) ||
                pos.y < 0 || pos.y >= mapGrid.GetLength(1))
            {
                return false;
            }

            // Vérifier si c'est la position du château (toujours valide comme destination)
            if (pos == currentMapConfig.castlePosition.ToVector2Int())
            {
                return true;
            }

            // Vérifier que la case est vide
            if (mapGrid[pos.x, pos.y] != TileType.Empty)
            {
                return false;
            }

            // Vérifier qu'on ne crée pas de contact avec le chemin existant
            // (sauf la case d'où on vient)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int checkX = pos.x + dx;
                    int checkY = pos.y + dy;

                    // Ignorer la case d'où on vient
                    if (checkX == from.x && checkY == from.y) continue;

                    if (checkX >= 0 && checkX < mapGrid.GetLength(0) &&
                        checkY >= 0 && checkY < mapGrid.GetLength(1))
                    {
                        if (mapGrid[checkX, checkY] == TileType.Path)
                        {
                            // On ne veut pas de contact diagonal ou orthogonal
                            // sauf avec la case précédente
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Génère un chemin avec des virages et demi-tours
        /// </summary>
        private bool GeneratePathWithTurns(Vector2Int start, Vector2Int target, PathGenerationConfig config)
        {
            Vector2Int current = start;
            Direction lastDirection = Direction.None;
            int turnsCreated = 0;
            int uTurnsCreated = 0;
            int segmentLength = 0;

            while (current != target && generatedPath.Count < config.maxLength)
            {
                // Déterminer les directions possibles
                List<Direction> possibleDirections = GetValidDirections(current, target, lastDirection);

                if (possibleDirections.Count == 0)
                {
                    // Impasse, échec
                    return false;
                }

                // Choisir une direction
                Direction chosenDirection = Direction.None;

                // Forcer un virage si nécessaire
                bool shouldTurn = ShouldCreateTurn(turnsCreated, config.numberOfTurns, generatedPath.Count, config.targetLength);
                bool shouldUTurn = ShouldCreateUTurn(uTurnsCreated, config.numberOfUTurns, generatedPath.Count, config.targetLength);

                if (shouldUTurn && uTurnsCreated < config.numberOfUTurns)
                {
                    // Essayer de créer un demi-tour
                    chosenDirection = GetUTurnDirection(current, lastDirection, possibleDirections);
                    if (chosenDirection != Direction.None)
                    {
                        uTurnsCreated++;
                        turnsCreated++; // Un demi-tour compte aussi comme un virage
                    }
                }
                else if (shouldTurn && turnsCreated < config.numberOfTurns)
                {
                    // Créer un virage
                    chosenDirection = GetTurnDirection(current, target, lastDirection, possibleDirections);
                    if (chosenDirection != Direction.None && chosenDirection != lastDirection)
                    {
                        turnsCreated++;
                    }
                }

                // Si pas de virage forcé ou échec, choisir la meilleure direction
                if (chosenDirection == Direction.None)
                {
                    chosenDirection = GetBestDirection(current, target, possibleDirections, lastDirection);
                }

                // Avancer dans la direction choisie
                Vector2Int next = GetNextPosition(current, chosenDirection);

                // Vérifier que la position est valide et ne crée pas de contact
                if (!IsValidPathPosition(next))
                {
                    return false;
                }

                // Ajouter au chemin
                generatedPath.Add(next);
                mapGrid[next.x, next.y] = TileType.Path;

                // Mettre à jour les variables
                current = next;

                if (chosenDirection != lastDirection)
                {
                    segmentLength = 1;
                }
                else
                {
                    segmentLength++;

                    // Forcer un changement si le segment est trop long
                    if (segmentLength >= config.maxSegmentLength)
                    {
                        lastDirection = Direction.None; // Forcer un changement au prochain tour
                    }
                }

                lastDirection = chosenDirection;
            }

            return current == target;
        }

        /// <summary>
        /// Obtient les directions valides depuis une position
        /// </summary>
        private List<Direction> GetValidDirections(Vector2Int from, Vector2Int target, Direction lastDirection)
        {
            List<Direction> valid = new List<Direction>();

            // Tester chaque direction
            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                if (dir == Direction.None) continue;

                Vector2Int next = GetNextPosition(from, dir);

                if (IsValidPathPosition(next))
                {
                    valid.Add(dir);
                }
            }

            return valid;
        }

        /// <summary>
        /// Vérifie si une position est valide pour le chemin
        /// </summary>
        private bool IsValidPathPosition(Vector2Int pos)
        {
            // Vérifier les limites
            if (pos.x < 0 || pos.x >= mapGrid.GetLength(0) ||
                pos.y < 0 || pos.y >= mapGrid.GetLength(1))
            {
                return false;
            }

            // Vérifier si c'est la position du château (toujours valide comme destination)
            if (pos == currentMapConfig.castlePosition.ToVector2Int())
            {
                return true;
            }

            // Vérifier que la case est vide
            if (mapGrid[pos.x, pos.y] != TileType.Empty)
            {
                return false;
            }

            // Vérifier qu'il n'y a pas de chemin adjacent (sauf connexion directe)
            int adjacentPaths = 0;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int checkX = pos.x + dx;
                    int checkY = pos.y + dy;

                    if (checkX >= 0 && checkX < mapGrid.GetLength(0) &&
                        checkY >= 0 && checkY < mapGrid.GetLength(1))
                    {
                        if (mapGrid[checkX, checkY] == TileType.Path)
                        {
                            // Ne compter que les connexions orthogonales pour le chemin principal
                            if (dx == 0 || dy == 0)
                            {
                                adjacentPaths++;

                                // Maximum 1 connexion (celle d'où on vient)
                                if (adjacentPaths > 1)
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                // Pas de diagonales autorisées
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Détermine s'il faut créer un virage
        /// </summary>
        private bool ShouldCreateTurn(int currentTurns, int targetTurns, int currentLength, int targetLength)
        {
            if (currentTurns >= targetTurns) return false;

            float progress = (float)currentLength / targetLength;
            float turnProgress = (float)currentTurns / targetTurns;

            // Créer un virage si on est en retard
            return turnProgress < progress && random.NextDouble() < 0.3;
        }

        /// <summary>
        /// Détermine s'il faut créer un demi-tour
        /// </summary>
        private bool ShouldCreateUTurn(int currentUTurns, int targetUTurns, int currentLength, int targetLength)
        {
            if (currentUTurns >= targetUTurns) return false;

            float progress = (float)currentLength / targetLength;
            float uTurnProgress = (float)currentUTurns / targetUTurns;

            // Créer un demi-tour si on est en retard et qu'on a de la place
            return uTurnProgress < progress && random.NextDouble() < 0.2;
        }

        /// <summary>
        /// Obtient la direction pour un demi-tour
        /// </summary>
        private Direction GetUTurnDirection(Vector2Int current, Direction lastDirection, List<Direction> possible)
        {
            // Un demi-tour est l'opposé de la direction actuelle
            Direction opposite = GetOppositeDirection(lastDirection);

            // Mais on ne peut pas revenir directement, il faut tourner
            List<Direction> perpendiculars = GetPerpendicularDirections(lastDirection);

            foreach (var dir in perpendiculars)
            {
                if (possible.Contains(dir))
                {
                    // Vérifier qu'on peut faire le demi-tour complet
                    if (CanCreateUTurn(current, dir))
                    {
                        return dir;
                    }
                }
            }

            return Direction.None;
        }

        /// <summary>
        /// Vérifie si on peut créer un demi-tour
        /// </summary>
        private bool CanCreateUTurn(Vector2Int start, Direction firstTurn)
        {
            // Simuler le demi-tour pour vérifier qu'il est possible
            // Un demi-tour nécessite au moins 3x3 tuiles libres

            // TODO: Implémenter la vérification complète du demi-tour
            // Pour l'instant, on vérifie juste qu'on a de l'espace

            Vector2Int pos = GetNextPosition(start, firstTurn);
            return IsValidPathPosition(pos);
        }

        /// <summary>
        /// Obtient la direction pour un virage
        /// </summary>
        private Direction GetTurnDirection(Vector2Int current, Vector2Int target, Direction lastDirection, List<Direction> possible)
        {
            List<Direction> perpendiculars = GetPerpendicularDirections(lastDirection);

            // Choisir la perpendiculaire qui nous rapproche le plus de la cible
            Direction best = Direction.None;
            float bestDistance = float.MaxValue;

            foreach (var dir in perpendiculars)
            {
                if (possible.Contains(dir))
                {
                    Vector2Int next = GetNextPosition(current, dir);
                    float dist = Vector2Int.Distance(next, target);

                    if (dist < bestDistance)
                    {
                        best = dir;
                        bestDistance = dist;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Obtient la meilleure direction vers la cible
        /// </summary>
        private Direction GetBestDirection(Vector2Int current, Vector2Int target, List<Direction> possible, Direction lastDirection)
        {
            Direction best = Direction.None;
            float bestScore = float.MinValue;

            foreach (var dir in possible)
            {
                Vector2Int next = GetNextPosition(current, dir);
                float distance = Vector2Int.Distance(next, target);

                // Score basé sur la distance et la continuité
                float score = -distance;

                // Bonus pour continuer dans la même direction
                if (dir == lastDirection)
                {
                    score += 0.5f;
                }

                // Pénalité pour revenir en arrière
                if (dir == GetOppositeDirection(lastDirection))
                {
                    score -= 2f;
                }

                if (score > bestScore)
                {
                    best = dir;
                    bestScore = score;
                }
            }

            return best;
        }

        /// <summary>
        /// Obtient la position suivante dans une direction
        /// </summary>
        private Vector2Int GetNextPosition(Vector2Int current, Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return new Vector2Int(current.x, current.y + 1);
                case Direction.Right:
                    return new Vector2Int(current.x + 1, current.y);
                case Direction.Down:
                    return new Vector2Int(current.x, current.y - 1);
                case Direction.Left:
                    return new Vector2Int(current.x - 1, current.y);
                default:
                    return current;
            }
        }

        /// <summary>
        /// Obtient la direction opposée
        /// </summary>
        private Direction GetOppositeDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return Direction.Down;
                case Direction.Right: return Direction.Left;
                case Direction.Down: return Direction.Up;
                case Direction.Left: return Direction.Right;
                default: return Direction.None;
            }
        }

        /// <summary>
        /// Obtient les directions perpendiculaires
        /// </summary>
        private List<Direction> GetPerpendicularDirections(Direction direction)
        {
            List<Direction> perpendiculars = new List<Direction>();

            switch (direction)
            {
                case Direction.Up:
                case Direction.Down:
                    perpendiculars.Add(Direction.Left);
                    perpendiculars.Add(Direction.Right);
                    break;
                case Direction.Left:
                case Direction.Right:
                    perpendiculars.Add(Direction.Up);
                    perpendiculars.Add(Direction.Down);
                    break;
            }

            return perpendiculars;
        }

        /// <summary>
        /// Ajoute les bordures autour du chemin
        /// </summary>
        private void AddPathBorders()
        {
            int width = mapGrid.GetLength(0);
            int height = mapGrid.GetLength(1);

            // Parcourir toutes les tuiles de chemin
            foreach (var pathPos in generatedPath)
            {
                // Vérifier les 8 directions autour
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int x = pathPos.x + dx;
                        int y = pathPos.y + dy;

                        // Vérifier les limites
                        if (x >= 0 && x < width && y >= 0 && y < height)
                        {
                            // Si c'est vide, en faire une bordure
                            if (mapGrid[x, y] == TileType.Empty)
                            {
                                mapGrid[x, y] = TileType.Border;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Place le spawn et le château
        /// </summary>
        private void PlaceSpawnAndCastle()
        {
            Vector2Int spawn = currentMapConfig.spawnPosition.ToVector2Int();
            Vector2Int castle = currentMapConfig.castlePosition.ToVector2Int();

            mapGrid[spawn.x, spawn.y] = TileType.Spawn;
            mapGrid[castle.x, castle.y] = TileType.Castle;
        }

        /// <summary>
        /// Ajoute les décorations sur la map
        /// </summary>
        private void AddDecorations()
        {
            int width = mapGrid.GetLength(0);
            int height = mapGrid.GetLength(1);
            float density = currentMapConfig.decorations.density;

            // Calculer le nombre de décorations
            int totalTiles = width * height;
            int pathAndBorderTiles = generatedPath.Count + CountBorderTiles();
            int availableTiles = totalTiles - pathAndBorderTiles;
            int decorationCount = Mathf.RoundToInt(availableTiles * density);

            // Obtenir toutes les positions disponibles
            List<Vector2Int> availablePositions = new List<Vector2Int>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    TileType tile = mapGrid[x, y];

                    // On peut placer des décorations sur les tuiles vides et certaines bordures
                    if (tile == TileType.Empty || (tile == TileType.Border && random.NextDouble() < 0.3))
                    {
                        availablePositions.Add(new Vector2Int(x, y));
                    }
                }
            }

            // Placer les décorations aléatoirement
            for (int i = 0; i < decorationCount && availablePositions.Count > 0; i++)
            {
                int index = random.Next(availablePositions.Count);
                Vector2Int pos = availablePositions[index];
                availablePositions.RemoveAt(index);

                mapGrid[pos.x, pos.y] = TileType.Decoration;
            }
        }

        /// <summary>
        /// Compte le nombre de tuiles de bordure
        /// </summary>
        private int CountBorderTiles()
        {
            int count = 0;

            for (int x = 0; x < mapGrid.GetLength(0); x++)
            {
                for (int y = 0; y < mapGrid.GetLength(1); y++)
                {
                    if (mapGrid[x, y] == TileType.Border)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Sauvegarde la map générée dans le fichier de configuration
        /// </summary>
        public void SaveGeneratedMap()
        {
            if (!configContainer.generationSettings.allowSavingGeneratedMaps)
            {
                Debug.LogWarning("Saving generated maps is disabled in configuration");
                return;
            }

            // Sauvegarder le chemin généré
            currentMapConfig.generatedPath = new List<Position>();
            foreach (var pos in generatedPath)
            {
                currentMapConfig.generatedPath.Add(new Position(pos.x, pos.y));
            }

            currentMapConfig.validated = true;

            // Sauvegarder dans le fichier JSON
            string configPath = Path.Combine(Application.streamingAssetsPath, configFileName);
            string json = JsonUtility.ToJson(configContainer, true);
            File.WriteAllText(configPath, json);

            Debug.Log($"Map saved: {currentMapConfig.id}");
        }

        /// <summary>
        /// Affiche la map en mode debug
        /// </summary>
        private void DebugPrintMap()
        {
            if (!debugMode) return;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Generated Map: {currentMapConfig.name} ===");
            sb.AppendLine($"Size: {mapGrid.GetLength(0)}x{mapGrid.GetLength(1)}");
            sb.AppendLine($"Path length: {generatedPath.Count}");

            // Afficher la grille
            for (int y = mapGrid.GetLength(1) - 1; y >= 0; y--)
            {
                for (int x = 0; x < mapGrid.GetLength(0); x++)
                {
                    switch (mapGrid[x, y])
                    {
                        case TileType.Empty:
                            sb.Append(".");
                            break;
                        case TileType.Path:
                            sb.Append("#");
                            break;
                        case TileType.Border:
                            sb.Append("o");
                            break;
                        case TileType.Spawn:
                            sb.Append("S");
                            break;
                        case TileType.Castle:
                            sb.Append("C");
                            break;
                        case TileType.Decoration:
                            sb.Append("*");
                            break;
                    }
                }
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Obtient le type de tuile à une position
        /// </summary>
        public TileType GetTileAt(Vector2Int position)
        {
            if (position.x >= 0 && position.x < mapGrid.GetLength(0) &&
                position.y >= 0 && position.y < mapGrid.GetLength(1))
            {
                return mapGrid[position.x, position.y];
            }

            return TileType.Empty;
        }

        /// <summary>
        /// Vérifie si une tour peut être placée à une position
        /// </summary>
        public bool CanPlaceTowerAt(Vector2Int position)
        {
            TileType tile = GetTileAt(position);

            // On peut placer des tours sur les tuiles vides et les bordures
            return tile == TileType.Empty || tile == TileType.Border;
        }

        /// <summary>
        /// Obtient le chemin généré pour les ennemis
        /// </summary>
        public List<Vector3> GetWorldPath(float tileSize)
        {
            List<Vector3> worldPath = new List<Vector3>();

            foreach (var pos in generatedPath)
            {
                Vector3 worldPos = new Vector3(
                    pos.x * tileSize + tileSize * 0.5f,
                    pos.y * tileSize + tileSize * 0.5f,
                    0f
                );
                worldPath.Add(worldPos);
            }

            return worldPath;
        }

        #region Test Methods

        [ContextMenu("Generate New Map")]
        private void TestGenerateMap()
        {
            GenerateMap();
        }

        [ContextMenu("Save Current Map")]
        private void TestSaveMap()
        {
            SaveGeneratedMap();
        }

        [ContextMenu("Load Next Map")]
        private void TestLoadNextMap()
        {
            if (configContainer == null || configContainer.maps.Count == 0) return;

            int currentIndex = configContainer.maps.FindIndex(m => m.id == currentMapConfig.id);
            int nextIndex = (currentIndex + 1) % configContainer.maps.Count;

            LoadMap(configContainer.maps[nextIndex].id);
        }

        #endregion
    }
}