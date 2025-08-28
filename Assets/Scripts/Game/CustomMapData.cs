using UnityEngine;
using System.Collections.Generic;

namespace BaboonTower.Game
{
    /// <summary>
    /// Position custom pour les maps créées avec l'éditeur web
    /// </summary>
    [System.Serializable]
    public class CustomPosition
    {
        public int x;
        public int y;

        public CustomPosition() { }

        public CustomPosition(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }

        public static CustomPosition FromVector2Int(Vector2Int vector)
        {
            return new CustomPosition(vector.x, vector.y);
        }
    }

    /// <summary>
    /// Taille de map custom
    /// </summary>
    [System.Serializable]
    public class CustomMapSize
    {
        public int width;
        public int height;

        public CustomMapSize() { }

        public CustomMapSize(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }

    /// <summary>
    /// Configuration de génération de chemin pour les maps custom
    /// </summary>
    [System.Serializable]
    public class CustomPathConfig
    {
        public int targetLength = 45;
        public int lengthMargin = 5;
        public int numberOfTurns = 2;
        public int numberOfUTurns = 1;
        public int minSegmentLength = 3;
        public int maxSegmentLength = 8;
        public bool allowDiagonalPath = false;
    }

    /// <summary>
    /// Données d'une map personnalisée créée avec l'éditeur web
    /// Compatible avec le format JSON généré par l'éditeur
    /// </summary>
    [System.Serializable]
    public class CustomMapData
    {
        [Header("Map Identity")]
        public string id;
        public string name;
        public string description;
        public string author;
        public string version;

        [Header("Map Configuration")]
        public CustomMapSize mapSize;
        public string biome = "forest"; // forest, desert, snow, volcano
        public int difficulty = 1;
        public float decorationDensity = 0.15f;

        [Header("Spawn Points")]
        public CustomPosition spawnPosition;
        public CustomPosition castlePosition;

        [Header("Path Data")]
        public List<CustomPosition> path;
        public CustomPathConfig pathConfig;

        [Header("Grid Data")]
        public int[][] gridData; // [y][x] - compatible avec le format web

        [Header("Validation")]
        public bool validated = false;
        public int seed = 42;

        [Header("Metadata")]
        public long createdTimestamp;
        public long lastModifiedTimestamp;
        public string checksum;

        public CustomMapData()
        {
            mapSize = new CustomMapSize(30, 16);
            spawnPosition = new CustomPosition(0, 8);
            castlePosition = new CustomPosition(29, 8);
            path = new List<CustomPosition>();
            pathConfig = new CustomPathConfig();
            createdTimestamp = System.DateTimeOffset.Now.ToUnixTimeSeconds();
            lastModifiedTimestamp = createdTimestamp;
        }

        /// <summary>
        /// Valide que les données de la map sont cohérentes
        /// </summary>
        public bool ValidateMapData()
        {
            // Vérifier les données de base
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
            {
                Debug.LogError("[CustomMapData] Invalid map: missing id or name");
                return false;
            }

            if (mapSize == null || mapSize.width <= 0 || mapSize.height <= 0)
            {
                Debug.LogError("[CustomMapData] Invalid map: invalid map size");
                return false;
            }

            if (spawnPosition == null || castlePosition == null)
            {
                Debug.LogError("[CustomMapData] Invalid map: missing spawn or castle position");
                return false;
            }

            // Vérifier que spawn et castle sont dans les limites
            if (!IsPositionInBounds(spawnPosition) || !IsPositionInBounds(castlePosition))
            {
                Debug.LogError("[CustomMapData] Invalid map: spawn or castle position out of bounds");
                return false;
            }

            // Vérifier le chemin
            if (path == null || path.Count < 2)
            {
                Debug.LogError("[CustomMapData] Invalid map: path too short");
                return false;
            }

            // Vérifier que tous les points du chemin sont dans les limites
            foreach (var pathPoint in path)
            {
                if (!IsPositionInBounds(pathPoint))
                {
                    Debug.LogError($"[CustomMapData] Invalid map: path point {pathPoint.x},{pathPoint.y} out of bounds");
                    return false;
                }
            }

            // Vérifier la densité de décoration
            if (decorationDensity < 0f || decorationDensity > 1f)
            {
                Debug.LogWarning("[CustomMapData] Warning: decoration density should be between 0 and 1");
                decorationDensity = Mathf.Clamp01(decorationDensity);
            }

            return true;
        }

        /// <summary>
        /// Vérifie qu'une position est dans les limites de la map
        /// </summary>
        private bool IsPositionInBounds(CustomPosition pos)
        {
            return pos.x >= 0 && pos.x < mapSize.width &&
                   pos.y >= 0 && pos.y < mapSize.height;
        }

        /// <summary>
        /// Convertit la grille en format compatible avec TileType[,]
        /// </summary>
        public TileType[,] GetTileTypeGrid()
        {
            TileType[,] grid = new TileType[mapSize.width, mapSize.height];

            if (gridData != null && gridData.Length > 0)
            {
                // Convertir depuis gridData[y][x] vers grid[x,y]
                for (int y = 0; y < mapSize.height && y < gridData.Length; y++)
                {
                    if (gridData[y] != null)
                    {
                        for (int x = 0; x < mapSize.width && x < gridData[y].Length; x++)
                        {
                            // Convertir les valeurs int vers TileType
                            int tileValue = gridData[y][x];
                            grid[x, y] = (TileType)Mathf.Clamp(tileValue, 0, System.Enum.GetValues(typeof(TileType)).Length - 1);
                        }
                    }
                }
            }
            else
            {
                // Si pas de gridData, reconstruire depuis le chemin
                ReconstructGridFromPath(grid);
            }

            return grid;
        }

        /// <summary>
        /// Reconstruit la grille depuis le chemin si gridData n'est pas disponible
        /// </summary>
        private void ReconstructGridFromPath(TileType[,] grid)
        {
            // Initialiser tout en Empty
            for (int x = 0; x < mapSize.width; x++)
            {
                for (int y = 0; y < mapSize.height; y++)
                {
                    grid[x, y] = TileType.Empty;
                }
            }

            // Placer le chemin
            foreach (var pathPoint in path)
            {
                if (IsPositionInBounds(pathPoint))
                {
                    grid[pathPoint.x, pathPoint.y] = TileType.Path;
                }
            }

            // Ajouter les bordures autour du chemin
            foreach (var pathPoint in path)
            {
                if (IsPositionInBounds(pathPoint))
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int nx = pathPoint.x + dx;
                            int ny = pathPoint.y + dy;

                            if (nx >= 0 && nx < mapSize.width &&
                                ny >= 0 && ny < mapSize.height &&
                                grid[nx, ny] == TileType.Empty)
                            {
                                grid[nx, ny] = TileType.Border;
                            }
                        }
                    }
                }
            }

            // Placer spawn et castle
            if (IsPositionInBounds(spawnPosition))
                grid[spawnPosition.x, spawnPosition.y] = TileType.Spawn;

            if (IsPositionInBounds(castlePosition))
                grid[castlePosition.x, castlePosition.y] = TileType.Castle;
        }

        /// <summary>
        /// Met à jour le timestamp de modification
        /// </summary>
        public void UpdateModificationTime()
        {
            lastModifiedTimestamp = System.DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Génère un checksum simple pour la validation
        /// </summary>
        public string GenerateChecksum()
        {
            string data = $"{id}_{name}_{mapSize.width}_{mapSize.height}_{path.Count}_{spawnPosition.x}_{spawnPosition.y}_{castlePosition.x}_{castlePosition.y}";
            checksum = data.GetHashCode().ToString();
            return checksum;
        }

        /// <summary>
        /// Convertit vers MapConfig (pour compatibilité avec MapGenerator)
        /// </summary>
        public MapConfig ToMapConfig()
        {
            MapConfig config = new MapConfig();
            config.id = this.id;
            config.name = this.name;
            config.biome = this.biome;
            config.difficulty = this.difficulty;
            config.validated = this.validated;
            config.seed = this.seed;

            // Convertir les tailles
            config.mapSize = new MapSize { width = this.mapSize.width, height = this.mapSize.height };

            // Convertir les positions
            config.spawnPosition = new Position(this.spawnPosition.x, this.spawnPosition.y);
            config.castlePosition = new Position(this.castlePosition.x, this.castlePosition.y);

            // Convertir le chemin
            config.generatedPath = new List<Position>();
            foreach (var pathPoint in this.path)
            {
                config.generatedPath.Add(new Position(pathPoint.x, pathPoint.y));
            }

            // Configuration de génération de chemin
            config.pathGeneration = new PathGenerationConfig
            {
                targetLength = this.pathConfig?.targetLength ?? 45,
                lengthMargin = this.pathConfig?.lengthMargin ?? 5,
                numberOfTurns = this.pathConfig?.numberOfTurns ?? 2,
                numberOfUTurns = this.pathConfig?.numberOfUTurns ?? 1,
                minSegmentLength = this.pathConfig?.minSegmentLength ?? 3,
                maxSegmentLength = this.pathConfig?.maxSegmentLength ?? 8,
                allowDiagonalPath = this.pathConfig?.allowDiagonalPath ?? false
            };

            // Configuration des décorations
            config.decorations = new DecorationConfig
            {
                density = this.decorationDensity,
                types = new List<DecorationType>() // À remplir selon les besoins
            };

            // Configuration du tileset
            config.tileSet = new TileSetConfig
            {
                ground = "grass",
                path = "dirt",
                border = "grass_dark",
                spawn = "spawn",
                castle = "castle"
            };

            return config;
        }

        /// <summary>
        /// Crée une CustomMapData depuis une MapConfig
        /// </summary>
        public static CustomMapData FromMapConfig(MapConfig config)
        {
            CustomMapData customData = new CustomMapData();
            customData.id = config.id;
            customData.name = config.name;
            customData.biome = config.biome;
            customData.difficulty = config.difficulty;
            customData.validated = config.validated;
            customData.seed = config.seed;
            customData.decorationDensity = config.decorations?.density ?? 0.15f;

            // Convertir les tailles
            customData.mapSize = new CustomMapSize(config.mapSize.width, config.mapSize.height);

            // Convertir les positions
            customData.spawnPosition = new CustomPosition(config.spawnPosition.x, config.spawnPosition.y);
            customData.castlePosition = new CustomPosition(config.castlePosition.x, config.castlePosition.y);

            // Convertir le chemin
            customData.path = new List<CustomPosition>();
            if (config.generatedPath != null)
            {
                foreach (var pathPoint in config.generatedPath)
                {
                    customData.path.Add(new CustomPosition(pathPoint.x, pathPoint.y));
                }
            }

            // Configuration du chemin
            if (config.pathGeneration != null)
            {
                customData.pathConfig = new CustomPathConfig
                {
                    targetLength = config.pathGeneration.targetLength,
                    lengthMargin = config.pathGeneration.lengthMargin,
                    numberOfTurns = config.pathGeneration.numberOfTurns,
                    numberOfUTurns = config.pathGeneration.numberOfUTurns,
                    minSegmentLength = config.pathGeneration.minSegmentLength,
                    maxSegmentLength = config.pathGeneration.maxSegmentLength,
                    allowDiagonalPath = config.pathGeneration.allowDiagonalPath
                };
            }

            customData.GenerateChecksum();
            return customData;
        }
    }

    /// <summary>
    /// Container pour une liste de maps custom (pour la sérialisation JSON)
    /// </summary>
    [System.Serializable]
    public class CustomMapCollection
    {
        public string version = "1.0.0";
        public List<CustomMapData> maps;
        public string defaultMapId;

        public CustomMapCollection()
        {
            maps = new List<CustomMapData>();
        }

        public CustomMapData GetMapById(string id)
        {
            return maps.Find(m => m.id == id);
        }

        public void AddMap(CustomMapData map)
        {
            // Supprimer l'ancienne version si elle existe
            maps.RemoveAll(m => m.id == map.id);
            maps.Add(map);
        }

        public bool RemoveMap(string id)
        {
            return maps.RemoveAll(m => m.id == id) > 0;
        }
    }
}