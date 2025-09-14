using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using BaboonTower.Network;
using System.Linq;
using BaboonTower.Core;  // AJOUT DE CETTE LIGNE

namespace BaboonTower.Game
{
    public enum GameState
    {
        WaitingForPlayers,
        PreparationPhase,    // Phase d'achat entre les vagues
        WaveActive,          // Vague en cours
        GameOver
    }

    [System.Serializable]
    public class PlayerGameState
    {
        public int playerId;
        public string playerName;
        public int gold;
        public int castleHP;
        public int maxCastleHP;
        public bool isAlive;
        public bool isEliminated;

        public PlayerGameState(int id, string name, int startGold = 10, int startHP = 100)
        {
            playerId = id;
            playerName = name;
            gold = startGold;
            castleHP = startHP;
            maxCastleHP = startHP;
            isAlive = true;
            isEliminated = false;
        }
    }

    [System.Serializable]
    public class GameStateData
    {
        public GameState currentState;
        public int currentWave;
        public float waveTimer;
        public float preparationTime;
        public List<PlayerGameState> playersStates;
        public int alivePlayers;

        public GameStateData()
        {
            currentState = GameState.WaitingForPlayers;
            currentWave = 0;
            waveTimer = 0f;
            preparationTime = 10f; // 10 secondes entre les vagues
            playersStates = new List<PlayerGameState>();
            alivePlayers = 0;
        }
    }

    // Messages de synchronisation réseau
    [System.Serializable]
    public class GameStateMessage
    {
        public string gameState;
        public int currentWave;
        public float waveTimer;
        public int alivePlayers;
    }

    [System.Serializable]
    public class PlayerStatesMessage
    {
        public List<PlayerGameState> playersStates;
    }

    [System.Serializable]
    public class GameTimerMessage
    {
        public float timer;
        public string phase; // "preparation" ou "wave"
    }

    public class GameController : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField] private int startingGold = 10;
        [SerializeField] private int startingCastleHP = 100;
        [SerializeField] private float preparationTime = 10f;
        [SerializeField] private float waveStartDelay = 5f;
        [SerializeField] private float initialPreparationTime = 10f; // Compte à rebours initial

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI castleHPText;
        [SerializeField] private TextMeshProUGUI gameStateText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Button backToLobbyButton;
        
        [Header("Notification UI")]
        [SerializeField] private GameObject notificationPanel;
        [SerializeField] private TextMeshProUGUI notificationText;

        [Header("Players UI")]
        [SerializeField] private Transform playersUIParent;
        [SerializeField] private GameObject playerUIItemPrefab;

        [Header("Game Grid")]
        [SerializeField] private Transform gameGrid;
        [SerializeField] private Vector2Int gridSize = new Vector2Int(30, 16);
        [SerializeField] private float tileSize = 64f;

        [Header("Debug Settings")]
        [SerializeField] private bool allowSinglePlayerDebug = true;
        [SerializeField] private bool autoStartGameAfterDelay = false;
        [SerializeField] private float autoStartDelay = 3f;
        [SerializeField] private KeyCode forceStartGameKey = KeyCode.F1;

        // État de jeu
        private GameStateData gameState;
        public GameStateData GameStateData => gameState;
        private NetworkManager networkManager;
        private WaveManager waveManager;
        private bool isHost;
        private PlayerGameState localPlayerState;
        
        // Configuration des vagues
        private WaveConfigurationMessage waveConfiguration;

        // Synchronisation réseau
        private float lastSyncTime = 0f;
        private const float SYNC_INTERVAL = 1f; // Synchroniser toutes les secondes
        private const float TIMER_SYNC_INTERVAL = 0.5f; // Timer plus fréquent

        // Events pour la logique de jeu
        public System.Action<GameState> OnGameStateChanged;
        public System.Action<int> OnWaveStarted;
        public System.Action<PlayerGameState> OnPlayerEliminated;
        public System.Action<PlayerGameState> OnGameWinner;

        // Propriété publique pour que le NetworkManager puisse vérifier le mode debug
        public bool IsDebugSinglePlayerAllowed => allowSinglePlayerDebug;

        private void Start()
        {
            InitializeGame();
            SetupNetworkEvents();
            SetupUI();
            SetupWaveManagerEvents();
			
    // AJOUTER : Notifier le NetworkManager qu'on est dans GameScene
    if (networkManager != null)
    {
        networkManager.NotifyGameSceneLoaded();
    }
    
    // AJOUTER : Vérifier si le jeu doit démarrer
    CheckAndStartGame();
        }

/// <summary>
/// CheckAndStartGame - Vérifie et démarre le jeu si nécessaire
/// </summary>
private void CheckAndStartGame()
{
    if (networkManager == null)
    {
        Debug.LogError("[GameController] NetworkManager not found!");
        return;
    }
    
    // Si le flag GameHasStarted est true, on démarre
    if (networkManager.GameHasStarted)
    {
        Debug.Log($"[GameController] Game already started flag detected - Mode: {networkManager.CurrentMode}");
        
        // Pour l'hôte ET les clients
        OnNetworkGameStarted();
    }
    else
    {
        Debug.LogWarning("[GameController] Waiting for game start signal...");
        
        // OPTIONNEL : Ajouter un timeout de sécurité
        StartCoroutine(GameStartTimeout());
    }
}

/// <summary>
/// GameStartTimeout - Timeout de sécurité si pas de signal reçu
/// </summary>
private IEnumerator GameStartTimeout()
{
    float timeout = 5f;
    float elapsed = 0f;
    
    while (!networkManager.GameHasStarted && elapsed < timeout)
    {
        yield return new WaitForSeconds(0.5f);
        elapsed += 0.5f;
    }
    
    if (!networkManager.GameHasStarted)
    {
        Debug.LogError("[GameController] Game start timeout! Returning to lobby...");
        SceneManager.LoadScene("MainMenu");
    }
    else
    {
        Debug.Log("[GameController] Game start signal received during wait");
        OnNetworkGameStarted();
    }
}
        private void Update()
        {
            // Synchronisation périodique pour l'host
            if (isHost && Time.time - lastSyncTime > SYNC_INTERVAL)
            {
                SyncGameState();
                lastSyncTime = Time.time;
            }

            // Raccourcis clavier de debug (seulement en mode debug)
            if (allowSinglePlayerDebug)
            {
                // F1 : Force start game
                if (Input.GetKeyDown(forceStartGameKey))
                {
                    ForceStartGame();
                }

                // F2 : Add gold
                if (Input.GetKeyDown(KeyCode.F2))
                {
                    DebugAddGold();
                }

                // F3 : Damage castle
                if (Input.GetKeyDown(KeyCode.F3))
                {
                    DebugDamageCastle();
                }

                // F4 : Force next wave
                if (Input.GetKeyDown(KeyCode.F4))
                {
                    DebugForceNextWave();
                }

                // F5 : Print game state
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    DebugPrintGameState();
                }
            }
        }

        private void OnDestroy()
        {
            RemoveNetworkEvents();
            RemoveWaveManagerEvents();
        }

        #region Initialization

        private void InitializeGame()
        {
            networkManager = NetworkManager.Instance;
            waveManager = FindObjectOfType<WaveManager>();
            
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager not found! Returning to main menu.");
                SceneManager.LoadScene("MainMenu");
                return;
            }

            isHost = networkManager.IsAuthoritativeHost;
            gameState = new GameStateData();
            
            // Charger la configuration des vagues
            LoadWaveConfiguration();

            // Initialiser l'état des joueurs basé sur les joueurs connectés
            InitializePlayersStates();

            // Trouver l'état du joueur local
            FindLocalPlayerState();

            Debug.Log($"Game initialized - Host: {isHost}, Players: {gameState.playersStates.Count}");
        }
        
        private void LoadWaveConfiguration()
        {
            string waveConfigJson = PlayerPrefs.GetString("WaveConfiguration", "");
            
            if (!string.IsNullOrEmpty(waveConfigJson))
            {
                try
                {
                    waveConfiguration = JsonUtility.FromJson<WaveConfigurationMessage>(waveConfigJson);
                    initialPreparationTime = waveConfiguration.initialPreparationTime;
                    preparationTime = waveConfiguration.preparationTimeBetweenWaves;
                    
                    Debug.Log($"[GameController] Wave configuration loaded: Initial={initialPreparationTime}s, Prep={preparationTime}s");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameController] Error loading wave configuration: {e.Message}");
                    LoadDefaultWaveConfiguration();
                }
            }
            else
            {
                LoadDefaultWaveConfiguration();
            }
        }
        
        private void LoadDefaultWaveConfiguration()
        {
            waveConfiguration = new WaveConfigurationMessage
            {
                initialPreparationTime = 10f,
                delayAfterFirstFinish = 15f,
                preparationTimeBetweenWaves = 5f
            };
            
            initialPreparationTime = waveConfiguration.initialPreparationTime;
            preparationTime = waveConfiguration.preparationTimeBetweenWaves;
        }

        private void InitializePlayersStates()
        {
            gameState.playersStates.Clear();

            foreach (var player in networkManager.ConnectedPlayers)
            {
                var playerState = new PlayerGameState(
                    player.playerId,
                    player.playerName,
                    startingGold,
                    startingCastleHP
                );
                gameState.playersStates.Add(playerState);
                Debug.Log($"Initialized player: {player.playerName} (ID: {player.playerId})");
            }

            gameState.alivePlayers = gameState.playersStates.Count;
        }

        private void FindLocalPlayerState()
        {
            // Pour l'host, chercher le joueur avec isHost = true
            if (isHost)
            {
                var hostPlayer = networkManager.ConnectedPlayers.Find(p => p.isHost);
                if (hostPlayer != null)
                {
                    localPlayerState = gameState.playersStates.Find(p => p.playerId == hostPlayer.playerId);
                    Debug.Log($"Host player found: {hostPlayer.playerName} (ID: {hostPlayer.playerId})");
                }
            }
            else
            {
                // Pour un client, on prend le premier joueur non-host
                var clientPlayer = networkManager.ConnectedPlayers.Find(p => !p.isHost);
                if (clientPlayer != null)
                {
                    localPlayerState = gameState.playersStates.Find(p => p.playerId == clientPlayer.playerId);
                    Debug.Log($"Client player found: {clientPlayer.playerName} (ID: {clientPlayer.playerId})");
                }
            }

            if (localPlayerState == null)
            {
                Debug.LogError("Could not find local player state!");
                
                // Fallback: prendre le premier joueur disponible
                if (gameState.playersStates.Count > 0)
                {
                    localPlayerState = gameState.playersStates[0];
                    Debug.LogWarning($"Using fallback: {localPlayerState.playerName} as local player");
                }
            }
            else
            {
                Debug.Log($"Local player state set: {localPlayerState.playerName} (Gold: {localPlayerState.gold}, HP: {localPlayerState.castleHP})");
            }
        }

        private void SetupNetworkEvents()
        {
            if (networkManager != null)
            {
                // Écouter les événements réseau
                networkManager.OnConnectionStateChanged += OnNetworkStateChanged;

                // IMPORTANT : Écouter le démarrage du jeu depuis le lobby
                networkManager.OnGameStarted += OnNetworkGameStarted;

                // Écouter les messages de jeu spécifiques
                networkManager.OnGameMessage += OnGameMessageReceived;
            }
        }

        private void RemoveNetworkEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnConnectionStateChanged -= OnNetworkStateChanged;
                networkManager.OnGameStarted -= OnNetworkGameStarted;
                networkManager.OnGameMessage -= OnGameMessageReceived;
            }
        }
        
        private void SetupWaveManagerEvents()
        {
            if (waveManager != null)
            {
                waveManager.OnPlayerFinishedFirst += OnPlayerFinishedFirst;
                waveManager.OnNextWaveTimerUpdate += OnNextWaveTimerUpdate;
                waveManager.OnWaveStarted += OnWaveStartedByManager;
                waveManager.OnWaveCompleted += OnWaveCompletedByManager;
            }
        }
        
        private void RemoveWaveManagerEvents()
        {
            if (waveManager != null)
            {
                waveManager.OnPlayerFinishedFirst -= OnPlayerFinishedFirst;
                waveManager.OnNextWaveTimerUpdate -= OnNextWaveTimerUpdate;
                waveManager.OnWaveStarted -= OnWaveStartedByManager;
                waveManager.OnWaveCompleted -= OnWaveCompletedByManager;
            }
        }

        private void SetupUI()
        {
            if (backToLobbyButton != null)
            {
                backToLobbyButton.onClick.AddListener(BackToLobby);
            }

            // Initialiser l'état de jeu pour tout le monde
            SetGameState(GameState.WaitingForPlayers);
            UpdateUI(); // Mettre à jour l'UI immédiatement

            Debug.Log("UI Setup complete, waiting for OnGameStarted signal...");
            if (isHost && allowSinglePlayerDebug)
            {
                Debug.Log($"[DEBUG] Press {forceStartGameKey} to force start game (Host only)");
            }
        }

        #endregion

        #region Wave Manager Events
        
        private void OnPlayerFinishedFirst(int playerId, string message)
        {
            ShowNotification(message, 5f);
            
            if (gameStateText != null)
            {
                gameStateText.text = $"Joueur {playerId} a fini en premier !";
            }
        }
        
        private void OnNextWaveTimerUpdate(float remainingTime)
        {
            if (timerText != null)
            {
                timerText.text = $"Prochaine vague dans: {Mathf.Ceil(remainingTime)}s";
                timerText.color = remainingTime <= 5 ? Color.red : Color.white;
            }
        }
        
        private void OnWaveStartedByManager(int waveNumber)
        {
            gameState.currentWave = waveNumber;
            UpdateUI();
        }
        
        private void OnWaveCompletedByManager(int waveNumber)
        {
            Debug.Log($"[GameController] Wave {waveNumber} completed");
        }
        
        #endregion

        #region Debug Methods

        private IEnumerator AutoStartGameDebug()
        {
            yield return new WaitForSeconds(autoStartDelay);

            if (isHost && gameState.currentState == GameState.WaitingForPlayers)
            {
                Debug.Log("[DEBUG] Auto-starting game in single player mode");
                ForceStartGame();
            }
        }

        [ContextMenu("Force Start Game (Debug)")]
        private void ForceStartGame()
        {
            if (!isHost)
            {
                Debug.LogWarning("[DEBUG] Only host can force start game");
                return;
            }

            if (gameState.currentState != GameState.WaitingForPlayers)
            {
                Debug.LogWarning("[DEBUG] Game already started or finished");
                return;
            }

            Debug.Log("[DEBUG] Force starting game...");

            // Simuler OnGameStarted pour bypass la logique normale
            OnNetworkGameStarted();
        }

        [ContextMenu("Add 50 Gold (Debug)")]
        private void DebugAddGold()
        {
            if (localPlayerState != null)
            {
                if (isHost)
                {
                    AddGoldToPlayer(localPlayerState.playerId, 50);
                }
                Debug.Log($"[DEBUG] Added 50 gold to {localPlayerState.playerName}");
            }
        }

        [ContextMenu("Damage Castle 25 HP (Debug)")]
        private void DebugDamageCastle()
        {
            if (localPlayerState != null)
            {
                if (isHost)
                {
                    DamageCastle(localPlayerState.playerId, 25);
                }
                Debug.Log($"[DEBUG] Damaged {localPlayerState.playerName}'s castle for 25 HP");
            }
        }

        [ContextMenu("Force Next Wave (Debug)")]
        private void DebugForceNextWave()
        {
            if (isHost && waveManager != null)
            {
                StartNextWave();
                Debug.Log("[DEBUG] Forced next wave");
            }
        }

        [ContextMenu("Print Game State (Debug)")]
        private void DebugPrintGameState()
        {
            Debug.Log("=== GAME STATE DEBUG ===");
            Debug.Log($"Current State: {gameState.currentState}");
            Debug.Log($"Current Wave: {gameState.currentWave}");
            Debug.Log($"Wave Timer: {gameState.waveTimer:F1}s");
            Debug.Log($"Alive Players: {gameState.alivePlayers}");
            Debug.Log($"Is Host: {isHost}");

            if (localPlayerState != null)
            {
                Debug.Log($"Local Player: {localPlayerState.playerName} (ID: {localPlayerState.playerId})");
                Debug.Log($"Gold: {localPlayerState.gold}");
                Debug.Log($"Castle HP: {localPlayerState.castleHP}/{localPlayerState.maxCastleHP}");
                Debug.Log($"Is Alive: {localPlayerState.isAlive}");
            }
            else
            {
                Debug.LogWarning("Local player state is NULL");
            }

            Debug.Log($"All Players ({gameState.playersStates.Count}):");
            foreach (var player in gameState.playersStates)
            {
                Debug.Log($"  - {player.playerName} (ID:{player.playerId}) Gold:{player.gold} HP:{player.castleHP} Alive:{player.isAlive}");
            }
            
            if (waveManager != null)
            {
                Debug.Log($"Wave Manager - Current Wave: {waveManager.GetCurrentWave()}, Enemies: {waveManager.GetActiveEnemiesCount()}");
            }
            
            Debug.Log("=======================");
        }

        #endregion

        #region Game Loop

        private IEnumerator StartGameSequence()
        {
            if (!isHost)
            {
                Debug.LogWarning("Only host should start game sequence!");
                yield break;
            }

            Debug.Log("Host starting game sequence...");

            // Vérifier le nombre de joueurs (avec exception pour le debug solo)
            int requiredPlayers = allowSinglePlayerDebug ? 1 : 2;
            if (gameState.alivePlayers < requiredPlayers)
            {
                Debug.LogWarning($"Not enough players to start game. Required: {requiredPlayers}, Current: {gameState.alivePlayers}");
                yield break;
            }

            if (allowSinglePlayerDebug && gameState.alivePlayers == 1)
            {
                Debug.Log("[DEBUG] Starting game in single player debug mode");
            }

    // Phase d'attente avec compte à rebours initial
    BroadcastGameState(GameState.WaitingForPlayers);
    SetGameState(GameState.WaitingForPlayers);
    
    // IMPORTANT : S'assurer que tous les clients sont synchronisés
    yield return new WaitForSeconds(1f);
            
float countdownTimer = initialPreparationTime;
    while (countdownTimer > 0)
    {
        BroadcastGameTimer(countdownTimer, "initial");
        
        if (timerText != null)
        {
            timerText.text = $"Début dans: {Mathf.Ceil(countdownTimer)}s";
        }
        
        yield return new WaitForSeconds(1f);
        countdownTimer -= 1f;
    }

// Phase de préparation initiale
    BroadcastGameState(GameState.PreparationPhase);
    SetGameState(GameState.PreparationPhase);
    
    ShowNotification("Phase d'achat - Placez vos tours !", 3f);
    
    // Timer avant la première vague
    float prepTimer = waveStartDelay;
    while (prepTimer > 0)
    {
        BroadcastGameTimer(prepTimer, "preparation");
        
        if (timerText != null)
        {
            timerText.text = $"Première vague dans: {Mathf.Ceil(prepTimer)}s";
        }
        
        yield return new WaitForSeconds(1f);
        prepTimer -= 1f;
    }

    // IMPORTANT : Petit délai pour s'assurer que tout le monde est prêt
    yield return new WaitForSeconds(0.5f);

    // Démarrer la première vague
    StartFirstWave();
}
        
/// <summary>
/// StartFirstWave - Démarre la première vague avec synchronisation réseau
/// </summary>
private void StartFirstWave()
{
    if (!isHost) return;
    
    gameState.currentWave = 1;
    SetGameState(GameState.WaveActive);
    BroadcastGameState(GameState.WaveActive);
    
    // IMPORTANT : Petit délai pour s'assurer que tous les clients sont synchronisés
    StartCoroutine(DelayedFirstWave());
}
  
/// <summary>
/// DelayedFirstWave - Lance la première vague avec un délai pour la synchronisation
/// </summary>
private IEnumerator DelayedFirstWave()
{
    // Attendre que tous les clients soient prêts
    yield return new WaitForSeconds(1f);
    
    if (waveManager != null)
    {
        Debug.Log("[GameController] Starting first wave with delay for sync");
        waveManager.StartWave(1);
    }
    else
    {
        Debug.LogError("[GameController] WaveManager not found!");
    }
}      
        /// <summary>
        /// StartNextWave - Démarre la prochaine vague (appelé par WaveManager après le timer)
        /// </summary>
        public void StartNextWave()
{
    // SUPPRIMER cette méthode ou la rendre vide
    // Le WaveManager gère directement le démarrage des vagues
    Debug.Log("[GameController] StartNextWave called but WaveManager handles this now");
}

        #endregion

        #region Game Logic

        public void AddGoldToPlayer(int playerId, int amount)
        {
            if (!isHost) return; // Seul l'host gère la logique métier

            var player = gameState.playersStates.Find(p => p.playerId == playerId);
            if (player != null && player.isAlive)
            {
                player.gold += amount;
                BroadcastPlayerStates(); // Synchroniser immédiatement
                UpdateUI();
            }
        }

        public void DamageCastle(int playerId, int damage)
        {
            if (!isHost) return; // Seul l'host gère la logique métier

            var player = gameState.playersStates.Find(p => p.playerId == playerId);
            if (player != null && player.isAlive)
            {
                player.castleHP = Mathf.Max(0, player.castleHP - damage);

                if (player.castleHP <= 0)
                {
                    EliminatePlayer(player);
                }

                BroadcastPlayerStates(); // Synchroniser immédiatement
                UpdateUI();
            }
        }
        
        private void SetGameState(GameState newState)
        {
            if (gameState.currentState != newState)
            {
                gameState.currentState = newState;
                OnGameStateChanged?.Invoke(newState);
                UpdateUI();
                Debug.Log($"Game State changed to: {newState}");
            }
        }
        
        private void EliminatePlayer(PlayerGameState player)
        {
            if (player.isEliminated) return;

            player.isEliminated = true;
            player.isAlive = false;
            gameState.alivePlayers--;

            OnPlayerEliminated?.Invoke(player);
            Debug.Log($"Player {player.playerName} eliminated! Remaining: {gameState.alivePlayers}");

            // Synchroniser l'élimination
            BroadcastPlayerElimination(player);
            BroadcastPlayerStates();

            CheckGameEndConditions();
        }

        private void CheckGameEndConditions()
        {
            // En mode debug solo, ne pas terminer le jeu avec 1 joueur
            int minPlayersForGameOver = allowSinglePlayerDebug ? 0 : 1;

            if (gameState.alivePlayers <= minPlayersForGameOver)
            {
                SetGameState(GameState.GameOver);
                BroadcastGameState(GameState.GameOver);

                var winner = gameState.playersStates.Find(p => p.isAlive);
                if (winner != null)
                {
                    OnGameWinner?.Invoke(winner);
                    BroadcastGameWinner(winner);
                    ShowNotification($"🏆 {winner.playerName} remporte la partie !", 10f);
                    Debug.Log($"Game Over! Winner: {winner.playerName}");
                }
                else if (allowSinglePlayerDebug)
                {
                    ShowNotification("Game Over !", 10f);
                    Debug.Log("[DEBUG] Game Over in single player debug mode");
                }
                
                // Arrêter les vagues
                if (waveManager != null)
                {
                    waveManager.StopCurrentWave();
                }
            }
        }

        #endregion

        #region Network Synchronization

        /// <summary>
        /// Synchronisation générale de l'état de jeu (appelée périodiquement)
        /// </summary>
        private void SyncGameState()
        {
            if (!isHost) return;

            BroadcastGameState(gameState.currentState);
            BroadcastPlayerStates();
        }

        /// <summary>
        /// Diffuse l'état de jeu à tous les clients (Host only)
        /// </summary>
        private void BroadcastGameState(GameState state)
        {
            if (!isHost || networkManager == null) return;

            var gameStateMsg = new GameStateMessage
            {
                gameState = state.ToString(),
                currentWave = gameState.currentWave,
                waveTimer = gameState.waveTimer,
                alivePlayers = gameState.alivePlayers
            };

            string json = JsonUtility.ToJson(gameStateMsg);
            networkManager.BroadcastGameMessage("GAME_STATE_UPDATE", json);

            Debug.Log($"Broadcasting game state: {state}");
        }

        /// <summary>
        /// Diffuse le numéro de vague à tous les clients (Host only)
        /// </summary>
        private void BroadcastWaveNumber(int waveNumber)
        {
            if (!isHost || networkManager == null) return;

            networkManager.BroadcastGameMessage("WAVE_STARTED", waveNumber.ToString());
            Debug.Log($"Broadcasting wave number: {waveNumber}");
        }

        /// <summary>
        /// Diffuse le timer à tous les clients (Host only)
        /// </summary>
        private void BroadcastGameTimer(float timer, string phase)
        {
            if (!isHost || networkManager == null) return;

            var timerMsg = new GameTimerMessage
            {
                timer = timer,
                phase = phase
            };

            string json = JsonUtility.ToJson(timerMsg);
            networkManager.BroadcastGameMessage("GAME_TIMER", json);
        }

        /// <summary>
        /// Diffuse l'état de tous les joueurs (Host only)
        /// </summary>
        private void BroadcastPlayerStates()
        {
            if (!isHost || networkManager == null) return;

            var playersMsg = new PlayerStatesMessage
            {
                playersStates = gameState.playersStates
            };

            string json = JsonUtility.ToJson(playersMsg);
            networkManager.BroadcastGameMessage("PLAYERS_STATES", json);
        }

        /// <summary>
        /// Diffuse qu'un joueur a été éliminé (Host only)
        /// </summary>
        private void BroadcastPlayerElimination(PlayerGameState player)
        {
            if (!isHost || networkManager == null) return;

            string json = JsonUtility.ToJson(player);
            networkManager.BroadcastGameMessage("PLAYER_ELIMINATED", json);
        }

        /// <summary>
        /// Diffuse le gagnant de la partie (Host only)
        /// </summary>
        private void BroadcastGameWinner(PlayerGameState winner)
        {
            if (!isHost || networkManager == null) return;

            string json = JsonUtility.ToJson(winner);
            networkManager.BroadcastGameMessage("GAME_WINNER", json);
        }
private void Awake()
{
    // Créer le DebugLogger s'il n'existe pas
    if (FindObjectOfType<DebugLogger>() == null)
    {
        GameObject loggerGO = new GameObject("DebugLogger");
        loggerGO.AddComponent<DebugLogger>();
    }
}
 /// <summary>
/// OnGameMessageReceived - Reçoit les messages de jeu du NetworkManager (CORRIGÉ)
/// </summary>
private void OnGameMessageReceived(string messageType, string data)
{
    // CORRECTION : Certains messages doivent être traités par le WaveManager directement
    // indépendamment du fait qu'on soit host ou client
    
    // Messages qui doivent TOUJOURS aller au WaveManager
    switch (messageType)
    {
        case "WAVE_START_SYNC":
        case "FIRST_FINISHER":
        case "NEXT_WAVE_TIMER":
        case "WAVE_COMPLETED":  // AJOUT IMPORTANT
            if (waveManager != null)
            {
                Debug.Log($"[GameController] Forwarding {messageType} to WaveManager");
                waveManager.ProcessNetworkMessage(messageType, data);
            }
            return; // Ne pas continuer le traitement
    }
    
    // Traitement normal pour les autres messages
    if (!isHost)
    {
        // Client : traiter les messages du serveur
        ProcessServerGameMessage(messageType, data);
    }
    else
    {
        // Host : traiter les demandes des clients  
        ProcessClientGameMessage(messageType, data);
    }
}

        /// <summary>
        /// Traite les messages de jeu reçus du serveur (clients uniquement)
        /// </summary>
private void ProcessServerGameMessage(string messageType, string data)
{
    try
    {
        switch (messageType)
        {
            case "GAME_STATE_UPDATE":
                HandleGameStateMessage(data);
                break;

            case "WAVE_STARTED":
                if (int.TryParse(data, out int wave))
                {
                    gameState.currentWave = wave;
                    OnWaveStarted?.Invoke(wave);
                    UpdateUI();
                }
                break;

            case "GAME_TIMER":
                HandleGameTimerMessage(data);
                break;

            case "PLAYERS_STATES":
                HandlePlayerStatesMessage(data);
                break;

            case "PLAYER_ELIMINATED":
                HandlePlayerEliminatedMessage(data);
                break;

            case "GAME_WINNER":
                HandleGameWinnerMessage(data);
                break;
                
            // Transférer seulement les messages de synchronisation de vagues
            case "WAVE_START_SYNC":
            case "FIRST_FINISHER":
            case "NEXT_WAVE_TIMER":
                if (waveManager != null)
                {
                    waveManager.ProcessNetworkMessage(messageType, data);
                }
                break;
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error processing server game message {messageType}: {e.Message}");
    }
}

        /// <summary>
        /// Traite les demandes de jeu reçues des clients (host uniquement)
        /// </summary>
private void ProcessClientGameMessage(string messageType, string data)
{
    try
    {
        switch (messageType)
        {
            case "SPEND_GOLD_REQUEST":
                var parts = data.Split('|');
                if (parts.Length == 2 && int.TryParse(parts[0], out int playerId) && int.TryParse(parts[1], out int amount))
                {
                    var player = gameState.playersStates.Find(p => p.playerId == playerId);
                    if (player != null && player.gold >= amount)
                    { 
                        player.gold -= amount;
                        BroadcastPlayerStates();
                        UpdateUI();
                    }
                }
                break;

            case "GOLD_REQUEST":
                // Un client demande à gagner de l'or
                parts = data.Split('|');
                if (parts.Length == 2 && int.TryParse(parts[0], out playerId) && int.TryParse(parts[1], out int gold))
                {
                    AddGoldToPlayer(playerId, gold);
                }
                break;
case "WAVE_COMPLETED":
                // Transférer au WaveManager
                if (waveManager != null)
                {
                    Debug.Log("[GameController Host] Forwarding WAVE_COMPLETED to WaveManager");
                    waveManager.ProcessNetworkMessage("WAVE_COMPLETED", data);
                }
                break;
            case "DAMAGE_REQUEST":
                // Un client demande à prendre des dégâts
                parts = data.Split('|');
                if (parts.Length == 2 && int.TryParse(parts[0], out playerId) && int.TryParse(parts[1], out int damage))
                {
                    DamageCastle(playerId, damage);
                }
                break;

            case "PLAYER_ACTION":
                // Actions du joueur (placement de tours, etc.)
                Debug.Log($"Received player action: {data}");
                break;
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error processing client game message {messageType}: {e.Message}");
    }
}

        private void HandleGameStateMessage(string data)
        {
            try
            {
                var gameStateMsg = JsonUtility.FromJson<GameStateMessage>(data);

                SetGameState((GameState)System.Enum.Parse(typeof(GameState), gameStateMsg.gameState));
                gameState.currentWave = gameStateMsg.currentWave;
                gameState.waveTimer = gameStateMsg.waveTimer;
                gameState.alivePlayers = gameStateMsg.alivePlayers;

                UpdateUI();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing game state message: {e.Message}");
            }
        }

        private void HandleGameTimerMessage(string data)
        {
            try
            {
                var timerMsg = JsonUtility.FromJson<GameTimerMessage>(data);
                gameState.waveTimer = timerMsg.timer;
                
                if (timerMsg.phase == "initial")
                {
                    if (timerText != null)
                    {
                        timerText.text = $"Début dans: {Mathf.Ceil(timerMsg.timer)}s";
                    }
                }
                else if (timerMsg.phase == "preparation")
                {
                    if (timerText != null)
                    {
                        timerText.text = $"Première vague dans: {Mathf.Ceil(timerMsg.timer)}s";
                    }
                }
                
                UpdateUI();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing game timer message: {e.Message}");
            }
        }

        private void HandlePlayerStatesMessage(string data)
        {
            try
            {
                var playersMsg = JsonUtility.FromJson<PlayerStatesMessage>(data);
                gameState.playersStates = playersMsg.playersStates;

                // Remettre à jour la référence du joueur local
                FindLocalPlayerState();
                UpdateUI(); // IMPORTANT !

                Debug.Log($"[CLIENT] Updated player states. Local player: {localPlayerState?.playerName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing player states message: {e.Message}");
            }
        }

        private void HandlePlayerEliminatedMessage(string data)
        {
            try
            {
                var eliminatedPlayer = JsonUtility.FromJson<PlayerGameState>(data);
                OnPlayerEliminated?.Invoke(eliminatedPlayer);
                ShowNotification($"☠️ {eliminatedPlayer.playerName} a été éliminé !", 3f);
                Debug.Log($"Player {eliminatedPlayer.playerName} was eliminated!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing player eliminated message: {e.Message}");
            }
        }

        private void HandleGameWinnerMessage(string data)
        {
            try
            {
                var winner = JsonUtility.FromJson<PlayerGameState>(data);
                OnGameWinner?.Invoke(winner);
                ShowNotification($"🏆 {winner.playerName} remporte la partie !", 10f);
                Debug.Log($"Game Over! Winner: {winner.playerName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing game winner message: {e.Message}");
            }
        }

        #endregion

        #region UI Updates
        
        private void ShowNotification(string message, float duration = 3f)
        {
            if (notificationPanel != null && notificationText != null)
            {
                notificationText.text = message;
                notificationPanel.SetActive(true);
                
                CancelInvoke(nameof(HideNotification));
                Invoke(nameof(HideNotification), duration);
            }
            
            Debug.Log($"[Notification] {message}");
        }
        
        private void HideNotification()
        {
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
        }

        private void UpdateUI()
        {
            // Informations de la vague
            if (waveText != null)
            {
                waveText.text = $"Vague #{gameState.currentWave}";
            }

            // Informations du joueur local
            if (localPlayerState != null)
            {
                if (goldText != null)
                    goldText.text = $"Or: {localPlayerState.gold}";

                if (castleHPText != null)
                    castleHPText.text = $"Château: {localPlayerState.castleHP}/{localPlayerState.maxCastleHP}";
            }
            else
            {
                // Valeurs par défaut si pas de joueur local trouvé
                if (goldText != null)
                    goldText.text = $"Or: {startingGold}";

                if (castleHPText != null)
                    castleHPText.text = $"Château: {startingCastleHP}/{startingCastleHP}";
            }

            // État du jeu et timer
            if (gameStateText != null)
            {
                string stateText = gameState.currentState switch
                {
                    GameState.WaitingForPlayers => "En attente...",
                    GameState.PreparationPhase => "Phase d'achat",
                    GameState.WaveActive => "Vague en cours",
                    GameState.GameOver => "Partie terminée",
                    _ => "État inconnu"
                };
                gameStateText.text = stateText;
            }

            // Timer et infos de debug
            if (timerText != null && gameState.currentState != GameState.WaveActive)
            {
                if (gameState.currentState == GameState.PreparationPhase)
                {
                    timerText.text = $"Prochaine vague dans: {Mathf.Ceil(gameState.waveTimer)}s";
                }

                // Afficher les infos de debug si activé
                if (allowSinglePlayerDebug && isHost)
                {
                    string debugText = timerText.text;
                    if (!string.IsNullOrEmpty(debugText)) debugText += "\n";
                    debugText += "[DEBUG] F1:Start F2:+Gold F3:Damage F4:Wave F5:Info";
                    timerText.text = debugText;
                }
            }

            // Mettre à jour l'UI des joueurs
            UpdatePlayersUI();
        }

        private void UpdatePlayersUI()
        {
            // TODO: Mettre à jour l'affichage des joueurs (vie, statut)
            // Similar to LobbyController's UpdatePlayersList but for game state
        }

        #endregion

        #region Network Events

        private void OnNetworkStateChanged(ConnectionState newState)
        {
            if (newState == ConnectionState.Disconnected)
            {
                Debug.Log("Network disconnected, returning to main menu");
                BackToMainMenu();
            }
        }

        /// <summary>
        /// Appelé quand le NetworkManager déclenche OnGameStarted (depuis le lobby)
        /// </summary>
        private void OnNetworkGameStarted()
        {
            Debug.Log("Game started signal received from NetworkManager");

            if (networkManager.CurrentState != ConnectionState.Connected)
            {
                Debug.LogError("Game started but network not connected!");
                BackToMainMenu();
                return;
            }

            if (isHost)
            {
                // L'host démarre la séquence et synchronise avec les clients
                StartCoroutine(StartGameSequence());
            }
            else
            {
                // IMPORTANT: Le client doit aussi démarrer son état !
                SetGameState(GameState.WaitingForPlayers);
                Debug.Log("Client ready and waiting for game state from server...");

                // Forcer une mise à jour de l'UI
                UpdateUI();
            }
        }

        #endregion

        #region UI Handlers

        private void BackToLobby()
        {
            if (networkManager != null && networkManager.CurrentState == ConnectionState.Connected)
            {
                SceneManager.LoadScene("Lobby");
            }
            else
            {
                BackToMainMenu();
            }
        }

        private void BackToMainMenu()
        {
            if (networkManager != null)
            {
                networkManager.StopNetworking();
            }
            SceneManager.LoadScene("MainMenu");
        }

        #endregion

        #region Public API for Game Systems

        public bool CanAfford(int cost)
        {
            return localPlayerState != null && localPlayerState.gold >= cost;
        }

        public bool SpendGold(int amount)
        {
            if (CanAfford(amount) && isHost)
            {
                localPlayerState.gold -= amount;
                BroadcastPlayerStates(); // Synchroniser immédiatement
                UpdateUI();
                return true;
            }
            else if (CanAfford(amount) && !isHost)
            {
                // Pour les clients, envoyer une demande au serveur
                RequestSpendGold(amount);
                return true; // Optimistic update
            }
            return false;
        }

        private void RequestSpendGold(int amount)
        {
            if (networkManager != null && localPlayerState != null)
            {
                networkManager.RequestSpendGold(localPlayerState.playerId, amount);
            }
        }

        public Vector3 GetWorldPositionFromGrid(Vector2Int gridPos)
        {
            return new Vector3(
                gridPos.x * tileSize + tileSize * 0.5f,
                gridPos.y * tileSize + tileSize * 0.5f,
                0
            );
        }

        public Vector2Int GetGridPositionFromWorld(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / tileSize),
                Mathf.FloorToInt(worldPos.y / tileSize)
            );
        }

        public bool IsValidGridPosition(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < gridSize.x &&
                   gridPos.y >= 0 && gridPos.y < gridSize.y;
        }

        #endregion
    }
}
