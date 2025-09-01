using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BaboonTower.Network;

namespace BaboonTower.Game
{
    public class WaveManager : MonoBehaviour
    {
        [Header("Wave Configuration")]
        [SerializeField] private float spawnDelay = 1f;
        [SerializeField] private int baseEnemiesPerWave = 5;
        [SerializeField] private float difficultyMultiplier = 1.2f;

        [Header("Current Wave Status")]
        [SerializeField] private int currentWaveNumber = 0;
        [SerializeField] private int enemiesSpawned = 0;
        [SerializeField] private int enemiesAlive = 0;
        [SerializeField] private bool waveInProgress = false;
         
        // Références pour MapLoaderV3
        private MapLoaderV3 mapLoader;
        private GameController gameController;
        private Coroutine currentWaveCoroutine;
        private List<Enemy> activeEnemies = new List<Enemy>();

        private void Awake()
        {
            Debug.Log("WaveManager Awake - MapLoaderV3 Compatible");
        }

        private void Start()
        {
            // Chercher MapLoaderV3 directement
            mapLoader = FindObjectOfType<MapLoaderV3>();
            gameController = FindObjectOfType<GameController>();

            if (mapLoader == null)
            {
                Debug.LogError("MapLoaderV3 not found! WaveManager needs MapLoaderV3 to spawn enemies.");
            }
            else
            {
                Debug.Log($"WaveManager using MapLoaderV3 - Map loaded: {mapLoader.WorldPath?.Count ?? 0} waypoints");
            }

            if (gameController == null)
            {
                Debug.LogError("GameController not found!");
            }

            Debug.Log("WaveManager initialized with MapLoaderV3 system");
        }

        private void OnDestroy()
        {
            if (currentWaveCoroutine != null)
            {
                StopCoroutine(currentWaveCoroutine);
            }
        }

        /// <summary>
        /// Vérifie si une vague est en cours
        /// </summary>
        public bool IsWaveInProgress()
        {
            return waveInProgress || enemiesAlive > 0;
        }

        /// <summary>
        /// Démarre une nouvelle vague - APPELÉE PAR GameController
        /// </summary>
public void StartWave(int waveNumber)
{
    // IMPORTANT: Tous les joueurs (host ET clients) doivent spawn leurs propres ennemis
    // Chaque joueur gère ses propres ennemis localement
    
    if (waveInProgress)
    {
        Debug.LogWarning("Wave already in progress!");
        return;
    }

    if (mapLoader == null)
    {
        Debug.LogError("Cannot start wave: MapLoaderV3 is null!");
        return;
    }

    Debug.Log($"[{(NetworkManager.Instance?.CurrentMode == NetworkMode.Host ? "HOST" : "CLIENT")}] Starting wave {waveNumber}");
    StartCoroutine(SpawnWave(waveNumber));
}

        /// <summary>
        /// Arrête la vague en cours
        /// </summary>
        public void StopCurrentWave()
        {
            Debug.Log("Stopping current wave");

            if (currentWaveCoroutine != null)
            {
                StopCoroutine(currentWaveCoroutine);
                currentWaveCoroutine = null;
            }

            waveInProgress = false;

            // Détruire tous les ennemis restants
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }
            activeEnemies.Clear();
            enemiesAlive = 0;
        }

        /// <summary>
        /// Coroutine pour spawner les ennemis de la vague
        /// </summary>
        private IEnumerator SpawnWave(int waveNumber)
        {
			waveInProgress = true;
			currentWaveNumber = waveNumber;
			enemiesSpawned = 0;  // Ajouter cette ligne
			enemiesAlive = 0;    // Ajouter cette ligne
            // Calculer le nombre d'ennemis pour cette vague
            int totalEnemies = Mathf.RoundToInt(baseEnemiesPerWave * Mathf.Pow(difficultyMultiplier, waveNumber - 1));
            totalEnemies = Mathf.Max(3, totalEnemies);

            // Répartition des types d'ennemis selon la vague
            int smallEnemies = Mathf.Max(1, totalEnemies / 2);
            int mediumEnemies = waveNumber > 2 ? Mathf.Max(1, totalEnemies / 3) : 0;
            int highEnemies = waveNumber > 5 ? Mathf.Max(1, totalEnemies / 6) : 0;

            // Ajuster pour avoir le bon total
            int total = smallEnemies + mediumEnemies + highEnemies;
            if (total < totalEnemies)
            {
                smallEnemies += (totalEnemies - total);
            }

            Debug.Log($"Wave {waveNumber}: Total={totalEnemies} | Small={smallEnemies}, Medium={mediumEnemies}, High={highEnemies}");

            // Spawn des petits ennemis
            for (int i = 0; i < smallEnemies; i++)
            {
                SpawnEnemy(EnemyType.Small);
                yield return new WaitForSeconds(spawnDelay);
            }

            // Spawn des ennemis moyens
            for (int i = 0; i < mediumEnemies; i++)
            {
                SpawnEnemy(EnemyType.Medium);
                yield return new WaitForSeconds(spawnDelay * 1.5f);
            }

            // Spawn des gros ennemis
            for (int i = 0; i < highEnemies; i++)
            {
                SpawnEnemy(EnemyType.High);
                yield return new WaitForSeconds(spawnDelay * 2f);
            }

            Debug.Log($"Wave {waveNumber} spawning complete. Total spawned: {enemiesSpawned}");

            // Attendre que tous les ennemis soient morts
            while (enemiesAlive > 0)
            {
                activeEnemies.RemoveAll(e => e == null);
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log($"Wave {waveNumber} complete! All enemies defeated.");
            waveInProgress = false;

			if (gameController != null)
			{
				gameController.OnLocalWaveCompleted();
			}
        }

        /// <summary>
        /// Spawn un ennemi d'un type donné - Adapté pour MapLoaderV3
        /// </summary>
        private void SpawnEnemy(EnemyType type)
        {
            if (mapLoader == null)
            {
                Debug.LogError("Cannot spawn enemy: MapLoaderV3 is null!");
                return;
            }

            // Vérifier que la map est chargée
            if (mapLoader.WorldPath == null || mapLoader.WorldPath.Count < 2)
            {
                Debug.LogError("Cannot spawn enemy: Map path not available!");
                return;
            }

            // Créer l'ennemi
            GameObject enemyObj = new GameObject($"Enemy_{type}_{enemiesSpawned}");
            Enemy enemy = enemyObj.AddComponent<Enemy>();

            // Initialiser l'ennemi avec le chemin de MapLoaderV3
            enemy.InitializeForMapV3(type, mapLoader.WorldPath, gameController);

            // Configurer les callbacks
            enemy.OnEnemyKilled += OnEnemyKilled;
            enemy.OnEnemyReachedEnd += OnEnemyReachedEnd;

            // Ajouter à la liste
            activeEnemies.Add(enemy);

            enemiesSpawned++;
            enemiesAlive++;

            Vector3 spawnWorldPos = mapLoader.GridToWorldPosition(mapLoader.SpawnPos);
            Debug.Log($"Spawned {type} enemy at {spawnWorldPos}. Alive: {enemiesAlive}");
        }

        /// <summary>
        /// Appelé quand un ennemi est tué
        /// </summary>
        private void OnEnemyKilled(Enemy enemy, int goldReward)
        {
            enemiesAlive--;
            activeEnemies.Remove(enemy);
            Debug.Log($"Enemy killed! Reward: {goldReward} gold. Enemies remaining: {enemiesAlive}");
        }

        /// <summary>
        /// Appelé quand un ennemi atteint le château
        /// </summary>
        private void OnEnemyReachedEnd(Enemy enemy)
        {
            enemiesAlive--;
            activeEnemies.Remove(enemy);
            Debug.Log($"Enemy reached castle! Enemies remaining: {enemiesAlive}");
        }

        /// <summary>
        /// Méthodes de debug
        /// </summary>
		[ContextMenu("Force Start Wave 1")]
		private void DebugStartWave1()
		{
			StartWave(1);
		}

        [ContextMenu("Print Wave Status")]
        private void DebugPrintStatus()
        {
            Debug.Log($"=== WAVE MANAGER STATUS ===");
            Debug.Log($"Current Wave: {currentWaveNumber}");
            Debug.Log($"Wave In Progress: {waveInProgress}");
            Debug.Log($"Enemies Spawned: {enemiesSpawned}");
            Debug.Log($"Enemies Alive: {enemiesAlive}");
            Debug.Log($"Active Enemies Count: {activeEnemies.Count}");
            Debug.Log($"MapLoader Available: {mapLoader != null}");

            if (mapLoader != null)
            {
                Debug.Log($"Map Spawn: {mapLoader.SpawnPos}");
                Debug.Log($"Map Castle: {mapLoader.CastlePos}");
                Debug.Log($"Map Path Points: {mapLoader.WorldPath?.Count ?? 0}");
            }

            Debug.Log($"===========================");
        }

        /// <summary>
        /// Affichage GUI pour le debug
        /// </summary>
        private void OnGUI()
        {
            if (Application.isPlaying)
            {
                GUI.Box(new Rect(10, 210, 200, 100), "Wave Manager V3");
                GUI.Label(new Rect(20, 235, 180, 20), $"Wave: {currentWaveNumber}");
                GUI.Label(new Rect(20, 250, 180, 20), $"In Progress: {waveInProgress}");
                GUI.Label(new Rect(20, 265, 180, 20), $"Enemies: {enemiesAlive}/{enemiesSpawned}");
                GUI.Label(new Rect(20, 280, 180, 20), $"MapLoader: {(mapLoader != null ? "OK" : "MISSING")}");

                if (GUI.Button(new Rect(20, 290, 80, 15), "Wave 1") && NetworkManager.Instance?.CurrentMode == NetworkMode.Host)
                {
                    StartWave(1);
                }
            }
        }
    }
}