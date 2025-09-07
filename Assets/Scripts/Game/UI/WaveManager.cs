using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BaboonTower.Network;
using System.Linq;
using System.IO;

namespace BaboonTower.Game
{
    // Ajoutez ces nouvelles classes de message pour la synchronisation des ennemis
    [System.Serializable]
    public class EnemySpawnMessage
    {
        public int enemyId;
        public string enemyType;
        public float spawnTime;
        public Vector3 spawnPosition;
    }

    [System.Serializable]
    public class EnemySyncMessage
    {
        public int enemyId;
        public Vector3 position;
        public int currentHealth;
        public int waypointIndex;
    }

    [System.Serializable]
    public class EnemyDeathMessage
    {
        public int enemyId;
        public bool reachedCastle;
    }

    public class WaveManager : MonoBehaviour
    {
    /// <summary>
    /// WaveManager - Gère les vagues d'ennemis avec synchronisation réseau complète
    /// Gestion du premier joueur à terminer et déclenchement synchronisé des vagues suivantes
    /// </summary>

        [Header("Configuration")]
        [SerializeField] private WaveConfiguration waveConfig;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform enemiesContainer;
        
        [Header("State")]
        [SerializeField] private int currentWave = 0;
        [SerializeField] private List<Enemy> activeEnemies = new List<Enemy>();
        [SerializeField] private bool waveInProgress = false;
        [SerializeField] private float waveCompletionTime;
		[SerializeField] private int firstFinisherPlayerId = -1;
		[SerializeField] private string firstFinisherName = "";
		// True quand TOUT le spawn de la vague est terminé (évite les complétions prématurées)
		[SerializeField] private bool hasSpawnedAllEnemies = false;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool autoSpawnEnemies = true;
        
        private GameController gameController;
        private NetworkManager networkManager;
        private MapLoaderV3 mapLoader;
        //private bool IsHost;
		private bool IsHost => networkManager != null && networkManager.IsAuthoritativeHost;
        private Coroutine nextWaveTimerCoroutine;
        private Dictionary<int, Enemy> enemiesByID = new Dictionary<int, Enemy>();
        private int nextEnemyId = 1;
        private float lastSyncTime = 0f;
        private const float ENEMY_SYNC_INTERVAL = 0.1f; // Synchroniser 10 fois par seconde
        // Events
        public System.Action<int> OnWaveStarted;
        public System.Action<int> OnWaveCompleted;
        public System.Action<int, string> OnPlayerFinishedFirst;
        public System.Action<float> OnNextWaveTimerUpdate;
        
        private void Awake()
        {
            gameController = FindObjectOfType<GameController>();
            networkManager = NetworkManager.Instance;
            mapLoader = FindObjectOfType<MapLoaderV3>();
            
            //IsHost = networkManager?.CurrentMode == NetworkMode.Host;
            
            LoadWaveConfiguration();
            CreateEnemiesContainer();
            
            // S'inscrire aux messages réseau
            if (networkManager != null)
            {
                networkManager.OnGameMessage += ProcessNetworkMessage;
            }
        }
        
        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnGameMessage -= ProcessNetworkMessage;
            }
            
            if (nextWaveTimerCoroutine != null)
            {
                StopCoroutine(nextWaveTimerCoroutine);
            }
        }
        
        /// <summary>
        /// LoadWaveConfiguration - Charge la configuration des vagues depuis le fichier JSON ou PlayerPrefs
        /// </summary>
        private void LoadWaveConfiguration()
        {
            // D'abord essayer de charger depuis PlayerPrefs (configuration envoyée par l'hôte)
            string savedConfig = PlayerPrefs.GetString("WaveConfiguration", "");
            
            if (!string.IsNullOrEmpty(savedConfig))
            {
                try
                {
                    var simpleConfig = JsonUtility.FromJson<WaveConfigurationMessage>(savedConfig);
                    ApplySimpleConfiguration(simpleConfig);
                    Debug.Log($"[WaveManager] Configuration loaded from PlayerPrefs");
                    return;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[WaveManager] Error loading saved configuration: {e.Message}");
                }
            }
            
            // Sinon charger depuis le fichier
            string configPath = Path.Combine(Application.streamingAssetsPath, "WaveConfig.json");
            
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                waveConfig = JsonUtility.FromJson<WaveConfiguration>(json);
                Debug.Log($"[WaveManager] Configuration loaded from file: {waveConfig.waves.Count} waves defined");
            }
            else
            {
                Debug.LogError($"[WaveManager] Configuration file not found at {configPath}");
                CreateDefaultConfiguration();
            }
        }
        
        /// <summary>
        /// ApplySimpleConfiguration - Applique la configuration simplifiée reçue du lobby
        /// </summary>
        private void ApplySimpleConfiguration(WaveConfigurationMessage config)
        {
            if (waveConfig == null)
            {
                CreateDefaultConfiguration();
            }
            
            waveConfig.initialPreparationTime = config.initialPreparationTime;
            waveConfig.delayAfterFirstFinish = config.delayAfterFirstFinish;
            waveConfig.preparationTimeBetweenWaves = config.preparationTimeBetweenWaves;
            
            Debug.Log($"[WaveManager] Applied configuration: Initial={config.initialPreparationTime}s, " +
                     $"Delay={config.delayAfterFirstFinish}s, Prep={config.preparationTimeBetweenWaves}s");
        }
        
 /// <summary>
/// StartWave - Démarre une vague (synchronisé réseau) - CORRIGÉ
/// </summary>
public void StartWave(int waveNumber)
{
    if (!IsHost)
    {
        Debug.LogWarning("[WaveManager] Only host can start waves");
        return;
    }
    
		currentWave = waveNumber;
		waveInProgress = true;
		hasSpawnedAllEnemies = false;
		// Nettoyer un éventuel timer hérité
		if (nextWaveTimerCoroutine != null) { StopCoroutine(nextWaveTimerCoroutine); nextWaveTimerCoroutine = null; }

    
    // IMPORTANT : Réinitialiser le premier finisseur pour chaque vague
    firstFinisherPlayerId = -1;
    firstFinisherName = "";
    
    Debug.Log($"[WaveManager Host] Starting wave {waveNumber} - Reset first finisher");
    
    // Diffuser le début de vague à tous les clients
    BroadcastWaveStart(waveNumber);
    
    // L'hôte démarre son spawn local
    if (autoSpawnEnemies)
    {
        StartCoroutine(SpawnWaveCoroutine(waveNumber));
    }
    
    OnWaveStarted?.Invoke(waveNumber);
}
        
        /// <summary>
        /// SpawnWaveCoroutine - Coroutine de spawn des ennemis pour une vague
        /// </summary>
<<<<<<< Updated upstream
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
=======
        private IEnumerator SpawnWaveCoroutine(int waveNumber)
        {
            WaveData waveData = GetWaveData(waveNumber);
            if (waveData == null)
            {
                Debug.LogError($"[WaveManager] No wave data for wave {waveNumber}");
                yield break;
            }
            
				Debug.Log($"[WaveManager] Spawning {waveData.GetTotalEnemies()} enemies for wave {waveNumber}");
				hasSpawnedAllEnemies = false;
            // Spawn des ennemis selon la configuration
            foreach (var enemyGroup in waveData.enemyGroups)
            {
                for (int i = 0; i < enemyGroup.count; i++)
                {
                    SpawnEnemy(enemyGroup.enemyType, waveNumber);
                    yield return new WaitForSeconds(enemyGroup.spawnInterval);
                }
                
                if (enemyGroup != waveData.enemyGroups.Last())
                {
                    yield return new WaitForSeconds(waveData.groupInterval);
                }
            }
			hasSpawnedAllEnemies = true;
			Debug.Log($"[WaveManager] All enemies spawned for wave {waveNumber}");

        }
        
        #region Network Message Handling
>>>>>>> Stashed changes



       
/// <summary>
/// HandleWaveStartSync - CORRIGÉ pour réinitialiser le premier finisseur
/// </summary>
private void HandleWaveStartSync(string data)
{
    var message = JsonUtility.FromJson<WaveStartMessage>(data);
	currentWave = message.waveNumber;
	waveInProgress = true;
	hasSpawnedAllEnemies = false;
    
    // IMPORTANT : Réinitialiser aussi côté client
    firstFinisherPlayerId = -1;
    firstFinisherName = "";
    
    Debug.Log($"[WaveManager Client] Received wave start sync for wave {message.waveNumber} - Reset first finisher");
    
    if (autoSpawnEnemies)
    {
        Debug.Log($"[WaveManager Client] Starting spawn coroutine for wave {message.waveNumber}");
        StartCoroutine(SpawnWaveCoroutine(message.waveNumber));
    }
    else
    {
        Debug.LogWarning("[WaveManager Client] autoSpawnEnemies is false, not spawning enemies!");
    }
    
    OnWaveStarted?.Invoke(message.waveNumber);
}

        #endregion
private void SpawnEnemy(string enemyType, int waveNumber)
{
    if (mapLoader == null || mapLoader.WorldPath == null || mapLoader.WorldPath.Count == 0)
    {
        Debug.LogError("[WaveManager] No path available for enemies");
        return;
    }
    
    // Créer l'ennemi LOCALEMENT seulement
    GameObject enemyGO = null;
    
    if (enemyPrefab != null)
    {
        enemyGO = Instantiate(enemyPrefab, enemiesContainer);
    }
    else
    {
        enemyGO = new GameObject($"Enemy_{enemyType}_{activeEnemies.Count}");
        enemyGO.transform.SetParent(enemiesContainer);
    }
    
    Enemy enemy = enemyGO.GetComponent<Enemy>();
    if (enemy == null)
    {
        enemy = enemyGO.AddComponent<Enemy>();
    }
    
    // Convertir le type string en enum
    EnemyType type = ParseEnemyType(enemyType);
    
    // Initialiser l'ennemi avec le chemin
    enemy.InitializeForMapV3(type, new List<Vector3>(mapLoader.WorldPath), gameController);
    
    // S'abonner aux événements
    enemy.OnEnemyKilled += OnEnemyKilled;
    enemy.OnEnemyReachedEnd += OnEnemyReachedEnd;
    
    activeEnemies.Add(enemy);
    
    if (debugMode)
    {
        Debug.Log($"[WaveManager] Spawned {enemyType} enemy locally, total active: {activeEnemies.Count}");
    }
}
        /// <summary>
        /// CreateEnemyGameObject - Crée le GameObject de l'ennemi
        /// </summary>
        private GameObject CreateEnemyGameObject(string enemyType, int enemyId)
        {
            GameObject enemyGO = null;
            
            if (enemyPrefab != null)
            {
                enemyGO = Instantiate(enemyPrefab, enemiesContainer);
            }
            else
            {
                enemyGO = new GameObject($"Enemy_{enemyType}_{enemyId}");
                enemyGO.transform.SetParent(enemiesContainer);
            }
            
            enemyGO.name = $"Enemy_{enemyType}_{enemyId}";
            
            return enemyGO;
        }
        /// <summary>
        /// OnEnemyKilled - Appelé quand un ennemi est tué (modifié pour inclure l'ID)
        /// </summary>
        private void OnEnemyKilled(Enemy enemy, int goldReward)
{
    activeEnemies.Remove(enemy);
    
    if (debugMode)
    {
        Debug.Log($"[WaveManager] Enemy killed locally, {activeEnemies.Count} remaining");
    }
    
    CheckWaveCompletion();
}

        
        /// <summary>
        /// OnEnemyReachedEnd - Appelé quand un ennemi atteint le château (modifié pour inclure l'ID)
        /// </summary>
        private void OnEnemyReachedEnd(Enemy enemy)
{
    activeEnemies.Remove(enemy);
    
    if (debugMode)
    {
        Debug.Log($"[WaveManager] Enemy reached castle locally, {activeEnemies.Count} remaining");
    }
    
    CheckWaveCompletion();
}
/// <summary>
/// CheckWaveCompletion - Vérifie si la vague est terminée
/// </summary>
private void CheckWaveCompletion()
{
    if (waveInProgress && hasSpawnedAllEnemies && activeEnemies.Count == 0)
    {
        waveInProgress = false;
        waveCompletionTime = Time.time;
        
        Debug.Log($"[WaveManager] Wave {currentWave} completed!");
        OnWaveCompleted?.Invoke(currentWave);
        
        // Si on est l'hôte
        if (IsHost)
        {
            HandleWaveCompletionAsHost();
        }
        else
        {
            // IMPORTANT : Les clients DOIVENT notifier l'hôte
            Debug.Log("[WaveManager Client] Notifying host of wave completion");
            NotifyWaveCompletionToHost();
        }
    }
}

/// <summary>
/// NotifyWaveCompletionToHost - Un client notifie l'hôte qu'il a terminé sa vague
/// </summary>
private void NotifyWaveCompletionToHost()
{
     if (IsHost || networkManager == null) return;
    
    var message = new WaveCompletionMessage
    {
        playerId = GetLocalPlayerId(),
        playerName = GetPlayerName(GetLocalPlayerId()),
        waveNumber = currentWave,
        completionTime = Time.time
    };
    
    string json = JsonUtility.ToJson(message);
    Debug.Log($"[WaveManager Client] Sending WAVE_COMPLETED to server: {json}");
    networkManager.SendGameMessageToServer("WAVE_COMPLETED", json);
}
        
        /// <summary>
        /// HandleWaveCompletionAsHost - Gère la complétion de vague côté hôte
        /// </summary>
private void HandleWaveCompletionAsHost()
{
    if (!IsHost) return;

    var msg = new WaveCompletionMessage {
        playerId = GetLocalPlayerId(),
        playerName = GetPlayerName(GetLocalPlayerId()),
        waveNumber = currentWave,
        completionTime = Time.time
    };
    ApplyServerSideCompletion(msg);
} 

        
        #region Network Synchronization
        
       
        /// <summary>
        /// BroadcastWaveStart - Diffuse le début d'une vague à tous les clients
        /// </summary>
private void BroadcastWaveStart(int waveNumber)
{
    if (!IsHost || networkManager == null) return;
    
    var message = new WaveStartMessage
    {
        waveNumber = waveNumber,
        timestamp = Time.time
    };
    
    string json = JsonUtility.ToJson(message);
    Debug.Log($"[WaveManager Host] Broadcasting WAVE_START_SYNC for wave {waveNumber}");
    networkManager.BroadcastGameMessage("WAVE_START_SYNC", json);
}
        
        /// <summary>
        /// BroadcastFirstFinisher - Diffuse l'information du premier joueur à finir
        /// </summary>
        private void BroadcastFirstFinisher(int playerId, string playerName)
        {
            if (!IsHost || networkManager == null) return;
            
            var message = new FirstFinisherMessage
            {
                playerId = playerId,
                playerName = playerName,
                waveNumber = currentWave,
                remainingTime = waveConfig.delayAfterFirstFinish
            };
            
            string json = JsonUtility.ToJson(message);
            networkManager.BroadcastGameMessage("FIRST_FINISHER", json);
            
            // Notification locale aussi
            string notification = $"{playerName} a fini la vague en premier !!!\nVite, il te reste {waveConfig.delayAfterFirstFinish} secondes avant la suivante !";
            OnPlayerFinishedFirst?.Invoke(playerId, notification);
        }
        
        /// <summary>
        /// BroadcastNextWaveTimer - Diffuse le timer de la prochaine vague
        /// </summary>
        private void BroadcastNextWaveTimer(float remainingTime)
        {
            if (!IsHost || networkManager == null) return;
            
            networkManager.BroadcastGameMessage("NEXT_WAVE_TIMER", remainingTime.ToString());
        }
        

       
private void HandleFirstFinisherMessage(string data)
{
    var message = JsonUtility.FromJson<FirstFinisherMessage>(data);
    firstFinisherPlayerId = message.playerId;
    firstFinisherName = message.playerName;
    
    string notification = $"{message.playerName} a fini la vague en premier !!!\nProchaine vague dans {message.remainingTime} secondes !";
    OnPlayerFinishedFirst?.Invoke(message.playerId, notification);
    
    Debug.Log($"[WaveManager Client] First finisher notification: {message.playerName}");
    
    // IMPORTANT : Les clients doivent aussi démarrer leur timer LOCAL pour l'affichage
    if (!IsHost)
    {
        if (nextWaveTimerCoroutine != null)
        {
            StopCoroutine(nextWaveTimerCoroutine);
        }
        nextWaveTimerCoroutine = StartCoroutine(ClientNextWaveTimerCoroutine(message.remainingTime));
    }
}
/// <summary>
/// ClientNextWaveTimerCoroutine - Timer côté client pour l'affichage uniquement
/// </summary>
private IEnumerator ClientNextWaveTimerCoroutine(float startTime)
{
    float timer = startTime;
    
    Debug.Log($"[WaveManager Client] Starting next wave timer: {timer} seconds");
    
    while (timer > 0)
    {
        // Juste mettre à jour l'affichage, pas de broadcast
        OnNextWaveTimerUpdate?.Invoke(timer);
        
        yield return new WaitForSeconds(1f);
        timer -= 1f;
    }
    
    // Le client attend que l'hôte lance la vague
    Debug.Log($"[WaveManager Client] Timer finished, waiting for host to start next wave");
}
/// <summary>
/// NextWaveTimerCoroutine - Timer côté hôte qui déclenche la vague suivante
/// </summary>
private IEnumerator NextWaveTimerCoroutine()
{
    float timer = waveConfig.delayAfterFirstFinish;
    
    Debug.Log($"[WaveManager Host] Starting next wave timer: {timer} seconds");
    
    while (timer > 0)
    {
        // Diffuser le timer à tous les clients
        BroadcastNextWaveTimer(timer);
        OnNextWaveTimerUpdate?.Invoke(timer);
        
        yield return new WaitForSeconds(1f);
        timer -= 1f;
    }
    
    // L'HÔTE démarre la prochaine vague pour TOUT LE MONDE
    Debug.Log($"[WaveManager Host] Timer expired, starting wave {currentWave + 1} for everyone!");
    
    // NE PAS passer par GameController, démarrer directement
    StartWave(currentWave + 1);
}

/// <summary>
/// ProcessNetworkMessage - Traite les messages réseau (modifié)
/// </summary>
public void ProcessNetworkMessage(string messageType, string data)
{
    switch (messageType)
    {
        case "WAVE_START_SYNC":
            if (!IsHost) HandleWaveStartSync(data);
            break;
            
        case "FIRST_FINISHER":
            HandleFirstFinisherMessage(data);
            break;
            
        case "NEXT_WAVE_TIMER":
            // Les clients mettent juste à jour l'affichage
            if (!IsHost && float.TryParse(data, out float timer))
            {
                OnNextWaveTimerUpdate?.Invoke(timer);
            }
            break;
            
        case "WAVE_COMPLETED":
            if (IsHost) HandleClientWaveCompletion(data);
            break;
    }
}        

private void LogNetworkDebug(string message)
{
    #if ENABLE_DETAILED_LOGS
    string fullMessage = $"[NETWORK_DEBUG] [{(IsHost ? "HOST" : "CLIENT")}] {message}";
    Debug.Log(fullMessage);
    
    // Log aussi dans un canal custom si vous avez le DebugLogger
    if (Application.isPlaying)
    {
        DebugLogger.LogCustom("NETWORK_SYNC", message);
    }
    #endif
}
/// <summary>
/// HandleClientWaveCompletion - CORRIGÉ pour permettre aux clients d'être premiers
/// </summary>
private void HandleClientWaveCompletion(string data)
{
    if (!IsHost) return;
    var msg = JsonUtility.FromJson<WaveCompletionMessage>(data);
    ApplyServerSideCompletion(msg);
}
private void ApplyServerSideCompletion(WaveCompletionMessage message)
{
    if (message.waveNumber != currentWave) return;

    if (firstFinisherPlayerId == -1)
    {
        firstFinisherPlayerId = message.playerId;
            // Résolution serveur du nom par playerId
    var resolved = networkManager?.ConnectedPlayers
        ?.FirstOrDefault(p => p.playerId == message.playerId)?.playerName;
    firstFinisherName = string.IsNullOrEmpty(resolved) ? $"Player{message.playerId}" : resolved;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[WaveManager Host] Resolving first finisher name: client='{message.playerName}', server='{firstFinisherName}'");
#endif
        BroadcastFirstFinisher(message.playerId, message.playerName);
        if (nextWaveTimerCoroutine != null) StopCoroutine(nextWaveTimerCoroutine);
        nextWaveTimerCoroutine = StartCoroutine(NextWaveTimerCoroutine());
    }
}        
        #endregion
  // NetworkManager.cs

      
        #region Helper Methods
        
        private WaveData GetWaveData(int waveNumber)
        {
            if (waveConfig == null || waveConfig.waves == null) return null;
            
            // Si on a une définition spécifique pour cette vague
            var specificWave = waveConfig.waves.FirstOrDefault(w => w.waveNumber == waveNumber);
            if (specificWave != null) return specificWave;
            
            // Sinon, utiliser la formule de génération
            return GenerateWaveData(waveNumber);
        }
        
        private WaveData GenerateWaveData(int waveNumber)
        {
            var generated = new WaveData
            {
                waveNumber = waveNumber,
                groupInterval = waveConfig.defaultGroupInterval
            };
            
            // Formule simple d'augmentation de difficulté
            int smallCount = 5 + (waveNumber * 2);
            int mediumCount = waveNumber > 3 ? 3 + waveNumber : 0;
            int highCount = waveNumber > 7 ? waveNumber - 5 : 0;
            
            generated.enemyGroups = new List<EnemyGroup>();
            
            if (smallCount > 0)
            {
                generated.enemyGroups.Add(new EnemyGroup 
                { 
                    enemyType = "Small", 
                    count = smallCount, 
                    spawnInterval = 0.5f 
                });
            }
            
            if (mediumCount > 0)
            {
                generated.enemyGroups.Add(new EnemyGroup 
                { 
                    enemyType = "Medium", 
                    count = mediumCount, 
                    spawnInterval = 0.7f 
                });
            }
            
            if (highCount > 0)
            {
                generated.enemyGroups.Add(new EnemyGroup 
                { 
                    enemyType = "High", 
                    count = highCount, 
                    spawnInterval = 1f 
                });
            }
            
            return generated;
        }
        
        private void CreateEnemiesContainer()
        {
            if (enemiesContainer == null)
            {
                GameObject container = GameObject.Find("Enemies");
                if (container == null)
                {
                    container = new GameObject("Enemies");
                }
                enemiesContainer = container.transform;
            }
        }
        
        private EnemyType ParseEnemyType(string type)
        {
            switch (type.ToLower())
            {
                case "small": return EnemyType.Small;
                case "medium": return EnemyType.Medium;
                case "high": return EnemyType.High;
                default: return EnemyType.Small;
            }
        }
        
        private int GetLocalPlayerId()
        {
            if (networkManager != null && networkManager.ConnectedPlayers.Count > 0)
            {
                if (networkManager.IsAuthoritativeHost)
                {
                    var hostPlayer = networkManager.ConnectedPlayers.FirstOrDefault(p => p.isHost);
                    return hostPlayer?.playerId ?? 0;
                }
                else
                {
                    var clientPlayer = networkManager.ConnectedPlayers.FirstOrDefault(p => !p.isHost);
                    return clientPlayer?.playerId ?? 1;
                }
            }
            return 0;
        }
        
        private string GetPlayerName(int playerId)
        {
            if (networkManager != null)
            {
                var player = networkManager.ConnectedPlayers.FirstOrDefault(p => p.playerId == playerId);
                if (player != null)
                {
                    return player.playerName;
                }
            }
            
            if (gameController != null && gameController.GameStateData != null)
            {
                var gamePlayer = gameController.GameStateData.playersStates.FirstOrDefault(p => p.playerId == playerId);
                return gamePlayer?.playerName ?? $"Player{playerId}";
            }
            
            return $"Player{playerId}";
        }
        
        private void CreateDefaultConfiguration()
        {
            waveConfig = new WaveConfiguration
            {
                version = "1.0.0",
                initialPreparationTime = 10f,
                preparationTimeBetweenWaves = 5f,
                delayAfterFirstFinish = 15f,
                defaultGroupInterval = 2f,
                waves = new List<WaveData>()
            };
            
            // Créer quelques vagues par défaut
            for (int i = 1; i <= 10; i++)
            {
                waveConfig.waves.Add(GenerateWaveData(i));
            }
        }
        
        #endregion
        
        #region Public API
        
        public bool IsWaveInProgress() => waveInProgress;
        
        public void StopCurrentWave()
        {
            waveInProgress = false;
            
            if (nextWaveTimerCoroutine != null)
            {
                StopCoroutine(nextWaveTimerCoroutine);
                nextWaveTimerCoroutine = null;
            }
            
            // Détruire tous les ennemis actifs
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }
            activeEnemies.Clear();
        }
<<<<<<< Updated upstream

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
=======
        
        public int GetCurrentWave() => currentWave;
        
        public int GetActiveEnemiesCount() => activeEnemies.Count;
        
        #endregion
        
        #region Debug Methods
        
        [ContextMenu("Force Complete Wave")]
        private void DebugForceCompleteWave()
        {
            Debug.Log("[WaveManager] Force completing wave");
            
            // Détruire tous les ennemis
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
>>>>>>> Stashed changes
                {
                    Destroy(enemy.gameObject);
                }
            }
            activeEnemies.Clear();
            
            CheckWaveCompletion();
        }
        
        [ContextMenu("Spawn Test Enemy")]
        private void DebugSpawnTestEnemy()
        {
            SpawnEnemy("Small", currentWave);
        }
        
        #endregion
    }
    
    #region Message Classes
    
    [System.Serializable]
    public class WaveConfiguration
    {
        public string version;
        public float initialPreparationTime;
        public float preparationTimeBetweenWaves;
        public float delayAfterFirstFinish;
        public float defaultGroupInterval;
        public List<WaveData> waves;
    }
    
    [System.Serializable]
    public class WaveConfigurationMessage
    {
        public float initialPreparationTime;
        public float delayAfterFirstFinish;
        public float preparationTimeBetweenWaves;
    }
    [System.Serializable]
    public class SerializableList<T>
    {
        public List<T> items;
        
        public SerializableList(List<T> list)
        {
            items = list;
        }
    }
    [System.Serializable]
    public class WaveData
    {
        public int waveNumber;
        public List<EnemyGroup> enemyGroups;
        public float groupInterval;
        
        public int GetTotalEnemies()
        {
            int total = 0;
            if (enemyGroups != null)
            {
                foreach (var group in enemyGroups)
                {
                    total += group.count;
                }
            }
            return total;
        }
    }
    
    [System.Serializable]
    public class EnemyGroup
    {
        public string enemyType;
        public int count;
        public float spawnInterval;
    }
    
    [System.Serializable]
    public class WaveStartMessage
    {
        public int waveNumber;
        public float timestamp;
    }
    
    [System.Serializable]
    public class FirstFinisherMessage
    {
        public int playerId;
        public string playerName;
        public int waveNumber;
        public float remainingTime;
    }
    
    [System.Serializable]
    public class WaveCompletionMessage
    {
        public int playerId;
        public string playerName;
        public int waveNumber;
        public float completionTime;
    }
    
    #endregion
}