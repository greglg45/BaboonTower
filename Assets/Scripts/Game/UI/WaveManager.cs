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

        [Header("Wave Completion")]
        [SerializeField] private int totalEnemiesInWave = 0;
        [SerializeField] private int enemiesKilledInWave = 0;
        [SerializeField] private bool waveCompleteMessageSent = false;

        // Références pour MapLoaderV3
        private MapLoaderV3 mapLoader;
        private GameController gameController;
        private NetworkManager networkManager;
        private Coroutine currentWaveCoroutine;
        private List<Enemy> activeEnemies = new List<Enemy>();

        // Events
        public System.Action<int> OnWaveCompleted;
        public System.Action<int, int> OnEnemyKilledInWave; // current killed, total

        private void Awake()
        {
            Debug.Log("WaveManager Awake - MapLoaderV3 Compatible");
        }

        private void Start()
        {
            // Chercher MapLoaderV3 directement
            mapLoader = FindObjectOfType<MapLoaderV3>();
            gameController = FindObjectOfType<GameController>();
            networkManager = NetworkManager.Instance;

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
            // Seulement l'host gère le spawn des ennemis
            if (NetworkManager.Instance?.CurrentMode != NetworkMode.Host)
            {
                Debug.Log("Not host, skipping wave spawn");
                return;
            }

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

            if (mapLoader.WorldPath == null || mapLoader.WorldPath.Count < 2)
            {
                Debug.LogError("Cannot start wave: Map path not loaded!");
                return;
            }

            Debug.Log($"Starting wave {waveNumber} with MapLoaderV3 system");
            currentWaveNumber = waveNumber;
            waveInProgress = true;
            enemiesSpawned = 0;
            enemiesAlive = 0;
            enemiesKilledInWave = 0;
            waveCompleteMessageSent = false;
            activeEnemies.Clear();

            if (currentWaveCoroutine != null)
            {
                StopCoroutine(currentWaveCoroutine);
            }

            currentWaveCoroutine = StartCoroutine(SpawnWaveCoroutine(waveNumber));
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
        private IEnumerator SpawnWaveCoroutine(int waveNumber)
        {
            // Calculer le nombre d'ennemis pour cette vague
            int totalEnemies = Mathf.RoundToInt(baseEnemiesPerWave * Mathf.Pow(difficultyMultiplier, waveNumber - 1));
            totalEnemies = Mathf.Max(3, totalEnemies);
            totalEnemiesInWave = totalEnemies; // Stocker le total pour la comparaison

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

            // Si ce n'est pas déjà fait, envoyer le message de complétion
            if (!waveCompleteMessageSent)
            {
                SendWaveCompleteMessage();
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
			Debug.Log($"[WaveManager] Enemy created at world position: {spawnWorldPos}");
			Debug.Log($"[WaveManager] Enemy GameObject active: {enemyObj.activeInHierarchy}");
			Debug.Log($"[WaveManager] Enemy component added: {enemy != null}");
        }

        /// <summary>
        /// Appelé quand un ennemi est tué
        /// </summary>
        private void OnEnemyKilled(Enemy enemy, int goldReward)
        {
            enemiesAlive--;
            enemiesKilledInWave++;
            activeEnemies.Remove(enemy);

            Debug.Log($"Enemy killed! Reward: {goldReward} gold. Enemies remaining: {enemiesAlive}. Progress: {enemiesKilledInWave}/{totalEnemiesInWave}");

            // Déclencher l'event de progression
            OnEnemyKilledInWave?.Invoke(enemiesKilledInWave, totalEnemiesInWave);

            // Vérifier si tous les ennemis de la vague ont été tués
            CheckWaveCompletion();
        }

        /// <summary>
        /// Appelé quand un ennemi atteint le château
        /// </summary>
        private void OnEnemyReachedEnd(Enemy enemy)
        {
            enemiesAlive--;
            activeEnemies.Remove(enemy);
            Debug.Log($"Enemy reached castle! Enemies remaining: {enemiesAlive}");

            // Un ennemi qui atteint le château compte comme non-tué
            // donc on ne vérifie pas la complétion parfaite ici
        }

        /// <summary>
        /// Vérifie si la vague a été complétée (tous les ennemis tués)
        /// </summary>
        private void CheckWaveCompletion()
        {
            // Vérifier si tous les ennemis ont été tués (pas juste disparus)
            if (enemiesKilledInWave >= totalEnemiesInWave && !waveCompleteMessageSent)
            {
                Debug.Log($"[WaveManager] PERFECT WAVE CLEAR! All {totalEnemiesInWave} enemies killed!");
                SendWaveCompleteMessage();

                }
        }

        private int GetLocalPlayerId()
        {
            if (networkManager != null && networkManager.ConnectedPlayers.Count > 0)
            {
                if (networkManager.CurrentMode == NetworkMode.Host)
                {
                    var hostPlayer = networkManager.ConnectedPlayers.Find(p => p.isHost);
                    return hostPlayer?.playerId ?? 0;
                }
                else
                {
                    var clientPlayer = networkManager.ConnectedPlayers.Find(p => !p.isHost);
                    return clientPlayer?.playerId ?? 1;
                }
            }
            return 0;
        }

        /// <summary>
        /// Envoie un message à tous les joueurs pour annoncer la complétion de la vague
        /// </summary>
        private void SendWaveCompleteMessage()
        {
            if (waveCompleteMessageSent) return;
            waveCompleteMessageSent = true;

            // Obtenir le nom du joueur local
            string playerName = "Un joueur";
            if (networkManager != null && networkManager.ConnectedPlayers.Count > 0)
            {
                if (networkManager.CurrentMode == NetworkMode.Host)
                {
                    var hostPlayer = networkManager.ConnectedPlayers.Find(p => p.isHost);
                    if (hostPlayer != null)
                    {
                        playerName = hostPlayer.playerName;
                    }
                }
            }

            // Créer le message
            string message = $"{playerName} a éliminé tous les ennemis de la vague {currentWaveNumber}! ({enemiesKilledInWave} ennemis)";

            // Envoyer via le NetworkManager
            if (networkManager != null)
            {
                // Si on est host, broadcaster directement
                if (networkManager.CurrentMode == NetworkMode.Host)
                {
                    networkManager.BroadcastGameMessage("WAVE_PERFECT_CLEAR", message);

                    // Afficher aussi localement
                    if (gameController != null)
                    {
                        // Vous pouvez ajouter une méthode dans GameController pour afficher ce message
                        Debug.Log($"[ANNOUNCEMENT] {message}");
                    }
                }
                // Si on est client, envoyer au serveur
                else if (networkManager.CurrentMode == NetworkMode.Client)
                {
                    networkManager.SendGameMessageToServer("WAVE_PERFECT_CLEAR", message);
                }
            }

            // Déclencher l'event local
            OnWaveCompleted?.Invoke(currentWaveNumber);
        }

        /// <summary>
        /// Obtient les statistiques de la vague actuelle
        /// </summary>
        public WaveStats GetCurrentWaveStats()
        {
            return new WaveStats
            {
                waveNumber = currentWaveNumber,
                totalEnemies = totalEnemiesInWave,
                enemiesKilled = enemiesKilledInWave,
                enemiesAlive = enemiesAlive,
                isPerfectClear = enemiesKilledInWave >= totalEnemiesInWave
            };
        }

        /// <summary>
        /// Méthodes de debug
        /// </summary>
        [ContextMenu("Force Start Wave 1")]
        private void DebugStartWave1()
        {
            if (NetworkManager.Instance?.CurrentMode == NetworkMode.Host)
            {
                StartWave(1);
            }
            else
            {
                Debug.LogWarning("Must be host to start wave");
            }
        }

        [ContextMenu("Print Wave Status")]
        private void DebugPrintStatus()
        {
            Debug.Log($"=== WAVE MANAGER STATUS ===");
            Debug.Log($"Current Wave: {currentWaveNumber}");
            Debug.Log($"Wave In Progress: {waveInProgress}");
            Debug.Log($"Enemies Spawned: {enemiesSpawned}");
            Debug.Log($"Enemies Alive: {enemiesAlive}");
            Debug.Log($"Enemies Killed: {enemiesKilledInWave}/{totalEnemiesInWave}");
            Debug.Log($"Perfect Clear: {enemiesKilledInWave >= totalEnemiesInWave}");
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
                GUI.Box(new Rect(10, 210, 200, 120), "Wave Manager V3");
                GUI.Label(new Rect(20, 235, 180, 20), $"Wave: {currentWaveNumber}");
                GUI.Label(new Rect(20, 250, 180, 20), $"In Progress: {waveInProgress}");
                GUI.Label(new Rect(20, 265, 180, 20), $"Enemies: {enemiesAlive}/{enemiesSpawned}");
                GUI.Label(new Rect(20, 280, 180, 20), $"Killed: {enemiesKilledInWave}/{totalEnemiesInWave}");
                GUI.Label(new Rect(20, 295, 180, 20), $"Perfect: {(enemiesKilledInWave >= totalEnemiesInWave ? "YES" : "NO")}");
                GUI.Label(new Rect(20, 310, 180, 20), $"MapLoader: {(mapLoader != null ? "OK" : "MISSING")}");

                if (GUI.Button(new Rect(20, 325, 80, 15), "Wave 1") && NetworkManager.Instance?.CurrentMode == NetworkMode.Host)
                {
                    StartWave(1);
                }
            }
        }
    }

    /// <summary>
    /// Structure pour les statistiques de vague
    /// </summary>
    [System.Serializable]
    public struct WaveStats
    {
        public int waveNumber;
        public int totalEnemies;
        public int enemiesKilled;
        public int enemiesAlive;
        public bool isPerfectClear;
    }
}