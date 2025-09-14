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
            Debug.Log($"[PlayerGameState] Creating new player state for {name} (ID: {id}) with {startGold} gold and {startHP} HP");
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
            Debug.Log("[GameStateData] Initializing new game state data");
            currentState = GameState.WaitingForPlayers;
            currentWave = 0;
            waveTimer = 0f;
            preparationTime = 10f; // 10 secondes entre les vagues
            playersStates = new List<PlayerGameState>();
            alivePlayers = 0;
        }
    }

    // Messages de synchronisation rÃ©seau
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
        [SerializeField] private float initialPreparationTime = 10f; // Compte Ã  rebours initial

[Header("Player Synchronization")]
[SerializeField] private bool waitingForAllPlayers = true;
[SerializeField] private int expectedPlayersCount = 0;
[SerializeField] private int readyPlayersCount = 0;
[SerializeField] private Text syncStatusText; // RÃ©fÃ©rence UI pour afficher le statut de sync

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
[Header("Sync Timeout")]
[SerializeField] private float syncTimeout = 15f; // 15 secondes de timeout
private float syncStartTime = 0f;
        // Ã‰tat de jeu
        private GameStateData gameState;
        public GameStateData GameStateData => gameState;
        private NetworkManager networkManager;
        private WaveManager waveManager;
        private bool isHost;
        private PlayerGameState localPlayerState;
        
        // Configuration des vagues
        private WaveConfigurationMessage waveConfiguration;

        // Synchronisation rÃ©seau
        private float lastSyncTime = 0f;
        private const float SYNC_INTERVAL = 1f; // Synchroniser toutes les secondes
        private const float TIMER_SYNC_INTERVAL = 0.5f; // Timer plus frÃ©quent

        // Events pour la logique de jeu
        public System.Action<GameState> OnGameStateChanged;
        public System.Action<int> OnWaveStarted;
        public System.Action<PlayerGameState> OnPlayerEliminated;
        public System.Action<PlayerGameState> OnGameWinner;

        // PropriÃ©tÃ© publique pour que le NetworkManager puisse vÃ©rifier le mode debug
        public bool IsDebugSinglePlayerAllowed => allowSinglePlayerDebug;
// Events pour la synchronisation
public System.Action<int, int> OnSyncStatusUpdate; // (ready, expected)
public System.Action OnAllPlayersReady;

private void Start()
{
    InitializeGame();
    SetupNetworkEvents();
	if (networkManager != null && networkManager.GameHasStarted)
{
    Debug.Log("[GameController] Game already started, calling OnNetworkGameStarted");
    OnNetworkGameStarted();
}
    SetupUI();
    SetupWaveManagerEvents();
    
    if (networkManager != null)
    {
        networkManager.NotifyGameSceneLoaded();
    }
    // NE PAS ajouter CheckAndStartGame() ici
}
/// <summary>
/// CheckAndStartGame - VÃ©rifie et dÃ©marre le jeu si nÃ©cessaire
/// </summary>
private void CheckAndStartGame()
{
    Debug.Log("[GameController] Checking if game can start");
    if (networkManager == null)
    {
        Debug.LogError("[GameController] NetworkManager not found!");
        return;
    }
    
    // Si le flag GameHasStarted est true, on dÃ©marre
    if (networkManager.GameHasStarted)
    {
        Debug.Log($"[GameController] Game already started flag detected - Mode: {networkManager.CurrentMode}");
        
        // Pour l'hÃ´te ET les clients
        OnNetworkGameStarted();
    }
    else
    {
        Debug.LogWarning("[GameController] Waiting for game start signal...");
        
        // OPTIONNEL : Ajouter un timeout de sÃ©curitÃ©
        StartCoroutine(GameStartTimeout());
    }
}

/// <summary>
/// GameStartTimeout - Timeout de sÃ©curitÃ© si pas de signal reÃ§u
/// </summary>
private IEnumerator GameStartTimeout()
{
    Debug.Log("[GameController] Starting game start timeout coroutine");
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
            // Synchronisation pÃ©riodique pour l'host
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
            Debug.Log("[GameController] OnDestroy called - cleaning up events");
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

            // Initialiser l'Ã©tat des joueurs basÃ© sur les joueurs connectÃ©s
            InitializePlayersStates();

            // Trouver l'Ã©tat du joueur local
            FindLocalPlayerState();

            Debug.Log($"Game initialized - Host: {isHost}, Players: {gameState.playersStates.Count}");
        }
        
        private void LoadWaveConfiguration()
        {
            Debug.Log("[GameController] Loading wave configuration from PlayerPrefs");
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
                Debug.Log("[GameController] No wave configuration found, loading defaults");
                LoadDefaultWaveConfiguration();
            }
        }
        
        private void LoadDefaultWaveConfiguration()
        {
            Debug.Log("[GameController] Loading default wave configuration");
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
            Debug.Log("[GameController] Initializing players states from NetworkManager");
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
            Debug.Log("[GameController] Finding local player state");
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
    Debug.Log("[GameController] Setting up network events");
    if (networkManager != null)
    {
        // DÃ‰SABONNER d'abord pour Ã©viter les doubles abonnements
		Debug.Log($"DÃ‰SABONNER dabord pour Ã©viter les doubles abonnements");
        networkManager.OnConnectionStateChanged -= OnNetworkStateChanged;
        networkManager.OnGameStarted -= OnNetworkGameStarted;
        networkManager.OnGameMessage -= OnGameMessageReceived;
        
        // RÃ‰ABONNER
		Debug.Log($"RÃ‰ABONNER");
        networkManager.OnConnectionStateChanged += OnNetworkStateChanged;
        networkManager.OnGameStarted += OnNetworkGameStarted;
        networkManager.OnGameMessage += OnGameMessageReceived;
    }
}

        private void RemoveNetworkEvents()
        {
            Debug.Log("[GameController] Removing network events");
            if (networkManager != null)
            {
                networkManager.OnConnectionStateChanged -= OnNetworkStateChanged;
                networkManager.OnGameStarted -= OnNetworkGameStarted;
                networkManager.OnGameMessage -= OnGameMessageReceived;
				Debug.Log($"RemoveNetworkEvents");
            }
        }
        
        private void SetupWaveManagerEvents()
        {
            Debug.Log("[GameController] Setting up wave manager events");
            if (waveManager != null)
            {
                waveManager.OnPlayerFinishedFirst += OnPlayerFinishedFirst;
                waveManager.OnNextWaveTimerUpdate += OnNextWaveTimerUpdate;
                waveManager.OnWaveStarted += OnWaveStartedByManager;
                waveManager.OnWaveCompleted += OnWaveCompletedByManager;
				Debug.Log($"SetupWaveManagerEvents");
            }
        }
        
        private void RemoveWaveManagerEvents()
        {
            Debug.Log("[GameController] Removing wave manager events");
            if (waveManager != null)
            {
                waveManager.OnPlayerFinishedFirst -= OnPlayerFinishedFirst;
                waveManager.OnNextWaveTimerUpdate -= OnNextWaveTimerUpdate;
                waveManager.OnWaveStarted -= OnWaveStartedByManager;
                waveManager.OnWaveCompleted -= OnWaveCompletedByManager;
				Debug.Log($"RemoveWaveManagerEvents");
            }
        }

        private void SetupUI()
        {
            Debug.Log("[GameController] Setting up UI components");
            if (backToLobbyButton != null)
            {
                backToLobbyButton.onClick.AddListener(BackToLobby);
            }

            // Initialiser l'Ã©tat de jeu pour tout le monde
            SetGameState(GameState.WaitingForPlayers);
            UpdateUI(); // Mettre Ã  jour l'UI immÃ©diatement

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
            Debug.Log($"[GameController] Player {playerId} finished first: {message}");
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
        
/// <summary>
/// OnWaveStartedByManager - Event handler quand WaveManager dÃ©marre une vague
/// </summary>
private void OnWaveStartedByManager(int waveNumber)
{
    // NOUVEAU : Mettre Ã  jour l'Ã©tat du jeu seulement, pas de double dÃ©marrage
    if (gameState.currentWave != waveNumber)
    {
        gameState.currentWave = waveNumber;
        Debug.Log($"[GameController] Wave {waveNumber} started by WaveManager");
    }
    
    // S'assurer que le GameState est correct
    if (gameState.currentState != GameState.WaveActive)
    {
        SetGameState(GameState.WaveActive);
        BroadcastGameState(GameState.WaveActive);
    }
    
    UpdateUI();
}
        
        private void OnWaveCompletedByManager(int waveNumber)
        {
            Debug.Log($"[GameController] Wave {waveNumber} completed by WaveManager");
        }
        
        #endregion

        #region Debug Methods

        private IEnumerator AutoStartGameDebug()
        {
            Debug.Log("[GameController] Starting auto start game debug coroutine");
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
            Debug.Log("[GameController] Force start game debug command triggered");
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
            Debug.Log("[GameController] Debug add gold command triggered");
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
            Debug.Log("[GameController] Debug damage castle command triggered");
            if (localPlayerState != null)
            {
                if (isHost)
                {
                    DamageCastle(localPlayerState.playerId, 25);
                }
                Debug.Log($"[DEBUG] Damaged {localPlayerState.playerName}'s castle for 25 HP");
            }
        }

/// <summary>
/// DebugForceNextWave - Version corrigÃ©e pour le debug
/// </summary>
[ContextMenu("Force Next Wave (Debug)")]
private void DebugForceNextWave()
{
    if (!isHost || waveManager == null)
    {
        Debug.LogWarning("[DEBUG] Only host can force next wave and WaveManager must exist");
        return;
    }
    
    // NOUVEAU : ArrÃªter proprement la vague courante avant de dÃ©marrer la suivante
    Debug.Log("[DEBUG] Force stopping current wave and starting next one");
    waveManager.StopCurrentWave();
    
    // Petit dÃ©lai pour laisser le temps au nettoyage
    StartCoroutine(DelayedForceNextWave());
}

/// <summary>
/// DelayedForceNextWave - Coroutine pour forcer la vague suivante aprÃ¨s nettoyage
/// </summary>
private IEnumerator DelayedForceNextWave()
{
    Debug.Log("[GameController] Starting delayed force next wave coroutine");
    yield return new WaitForSeconds(0.5f);
    
    int nextWave = gameState.currentWave + 1;
    Debug.Log($"[DEBUG] Starting wave {nextWave}");
    waveManager.StartWave(nextWave);
}
        [ContextMenu("Print Game State (Debug)")]
        private void DebugPrintGameState()
        {
            Debug.Log("[GameController] Debug print game state command triggered");
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

/// <summary>
/// StartGameSequence - MODIFIÃ‰ pour dÃ©marrer seulement aprÃ¨s synchronisation
/// </summary>
private IEnumerator StartGameSequence()
{
    Debug.Log("[GameController] Starting game sequence coroutine");
    if (!isHost)
    {
        Debug.LogWarning("Only host should start game sequence!");
        yield break;
    }

    // NOUVEAU : Ne dÃ©marrer que si tous les joueurs sont synchronisÃ©s
    if (waitingForAllPlayers)
    {
        Debug.LogWarning("Cannot start game sequence - still waiting for players");
        yield break;
    }

    Debug.Log($"Starting game sequence with {expectedPlayersCount} synchronized players");

    // Initialiser l'Ã©tat de jeu
    SetGameState(GameState.PreparationPhase);
    BroadcastGameState(GameState.PreparationPhase);

    ShowNotification("Phase d'achat - Placez vos tours !", 3f);

    // Timer avant la premiÃ¨re vague (maintenant tous les joueurs sont prÃªts)
    float prepTimer = waveStartDelay;
    while (prepTimer > 0)
    {
        BroadcastGameTimer(prepTimer, "preparation");
        
        if (timerText != null)
        {
            timerText.text = $"PremiÃ¨re vague dans: {Mathf.Ceil(prepTimer)}s";
        }
        
        yield return new WaitForSeconds(1f);
        prepTimer -= 1f;
    }

    // DÃ©marrer la premiÃ¨re vague (maintenant synchronisÃ©)
    StartFirstWave();
}
        
// <summary>
/// StartFirstWave - VERSION SIMPLIFIÃ‰E maintenant que tous les joueurs sont synchronisÃ©s
/// </summary>
private void StartFirstWave()
{
    Debug.Log("[GameController] Starting first wave");
    if (!isHost) return;
    
    if (waveManager != null && waveManager.IsWaveInProgress())
    {
        Debug.LogWarning("[GameController] First wave already in progress, skipping StartFirstWave");
        return;
    }
    
    gameState.currentWave = 1;
    SetGameState(GameState.WaveActive);
    BroadcastGameState(GameState.WaveActive);
    
    // Maintenant tous les clients sont synchronisÃ©s, pas besoin de dÃ©lai
    Debug.Log("[GameController] Starting first wave immediately (all players synchronized)");
    if (waveManager != null)
    {
        waveManager.StartWave(1);
    }
    else
    {
        Debug.LogError("[GameController] WaveManager not found!");
    }
}
 /// <summary>
/// Debug method pour forcer la synchronisation (testing)
/// </summary>
 #if UNITY_EDITOR
[ContextMenu("Debug - Force All Players Ready")]
private void DebugForceAllPlayersReady()
{
    if (isHost)
    {
        Debug.Log("[DEBUG] Forcing all players ready status");
        HandleAllPlayersReadyMessage(expectedPlayersCount.ToString());
    }
}
#endif
/// <summary>
/// Debug method pour afficher le statut de sync
/// </summary>

[ContextMenu("Debug - Print Sync Status")]
private void DebugPrintSyncStatus()
{
    Debug.Log("=== SYNC STATUS DEBUG ===");
    Debug.Log($"Waiting for all players: {waitingForAllPlayers}");
    Debug.Log($"Expected players: {expectedPlayersCount}");
    Debug.Log($"Ready players: {readyPlayersCount}");
    Debug.Log($"NetworkManager ready count: {networkManager.SceneReadyPlayersCount}");
    Debug.Log($"NetworkManager expected count: {networkManager.ExpectedPlayersCount}");
    Debug.Log($"All ready: {networkManager.AllPlayersSceneReady}");
    Debug.Log("========================");
}


        #endregion

        #region Game Logic

        public void AddGoldToPlayer(int playerId, int amount)
        {
            Debug.Log($"[GameController] Adding {amount} gold to player {playerId}");
            if (!isHost) return; // Seul l'host gÃ¨re la logique mÃ©tier

            var player = gameState.playersStates.Find(p => p.playerId == playerId);
            if (player != null && player.isAlive)
            {
                player.gold += amount;
                BroadcastPlayerStates(); // Synchroniser immÃ©diatement
                UpdateUI();
            }
        }

        public void DamageCastle(int playerId, int damage)
        {
            Debug.Log($"[GameController] Damaging castle of player {playerId} for {damage} damage");
            if (!isHost) return; // Seul l'host gÃ¨re la logique mÃ©tier

            var player = gameState.playersStates.Find(p => p.playerId == playerId);
            if (player != null && player.isAlive)
            {
                player.castleHP = Mathf.Max(0, player.castleHP - damage);

                if (player.castleHP <= 0)
                {
                    EliminatePlayer(player);
                }

                BroadcastPlayerStates(); // Synchroniser immÃ©diatement
                UpdateUI();
            }
        }
        
        private void SetGameState(GameState newState)
        {
            Debug.Log($"[GameController] Setting game state to {newState}");
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
            Debug.Log($"[GameController] Eliminating player {player.playerName} (ID: {player.playerId})");
            if (player.isEliminated) return;

            player.isEliminated = true;
            player.isAlive = false;
            gameState.alivePlayers--;

            OnPlayerEliminated?.Invoke(player);
            Debug.Log($"Player {player.playerName} eliminated! Remaining: {gameState.alivePlayers}");

            // Synchroniser l'Ã©limination
            BroadcastPlayerElimination(player);
            BroadcastPlayerStates();

            CheckGameEndConditions();
        }

        private void CheckGameEndConditions()
        {
            Debug.Log("[GameController] Checking game end conditions");
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
                    ShowNotification($"ðŸ† {winner.playerName} remporte la partie !", 10f);
                    Debug.Log($"Game Over! Winner: {winner.playerName}");
                }
                else if (allowSinglePlayerDebug)
                {
                    ShowNotification("Game Over !", 10f);
                    Debug.Log("[DEBUG] Game Over in single player debug mode");
                }
                
                // ArrÃªter les vagues
                if (waveManager != null)
                {
                    waveManager.StopCurrentWave();
                }
            }
        }

        #endregion

        #region Network Synchronization

        /// <summary>
        /// Synchronisation gÃ©nÃ©rale de l'Ã©tat de jeu (appelÃ©e pÃ©riodiquement)
        /// </summary>
        private void SyncGameState()
        {
            Debug.Log("[GameController] Syncing game state");
            if (!isHost) return;

            BroadcastGameState(gameState.currentState);
            BroadcastPlayerStates();
        }

        /// <summary>
        /// Diffuse l'Ã©tat de jeu Ã  tous les clients (Host only)
        /// </summary>
        private void BroadcastGameState(GameState state)
        {
            Debug.Log($"[GameController] Broadcasting game state: {state}");
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
        /// Diffuse le numÃ©ro de vague Ã  tous les clients (Host only)
        /// </summary>
        private void BroadcastWaveNumber(int waveNumber)
        {
            Debug.Log($"[GameController] Broadcasting wave number: {waveNumber}");
            if (!isHost || networkManager == null) return;

            networkManager.BroadcastGameMessage("WAVE_STARTED", waveNumber.ToString());
            Debug.Log($"Broadcasting wave number: {waveNumber}");
        }

        /// <summary>
        /// Diffuse le timer Ã  tous les clients (Host only)
        /// </summary>
        private void BroadcastGameTimer(float timer, string phase)
        {
            Debug.Log($"[GameController] Broadcasting game timer: {timer}s, phase: {phase}");
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
        /// Diffuse l'Ã©tat de tous les joueurs (Host only)
        /// </summary>
        private void BroadcastPlayerStates()
        {
            Debug.Log("[GameController] Broadcasting player states");
            if (!isHost || networkManager == null) return;

            var playersMsg = new PlayerStatesMessage
            {
                playersStates = gameState.playersStates
            };

            string json = JsonUtility.ToJson(playersMsg);
            networkManager.BroadcastGameMessage("PLAYERS_STATES", json);
        }

        /// <summary>
        /// Diffuse qu'un joueur a Ã©tÃ© Ã©liminÃ© (Host only)
        /// </summary>
        private void BroadcastPlayerElimination(PlayerGameState player)
        {
            Debug.Log($"[GameController] Broadcasting player elimination: {player.playerName}");
            if (!isHost || networkManager == null) return;

            string json = JsonUtility.ToJson(player);
            networkManager.BroadcastGameMessage("PLAYER_ELIMINATED", json);
        }

        /// <summary>
        /// Diffuse le gagnant de la partie (Host only)
        /// </summary>
        private void BroadcastGameWinner(PlayerGameState winner)
        {
            Debug.Log($"[GameController] Broadcasting game winner: {winner.playerName}");
            if (!isHost || networkManager == null) return;

            string json = JsonUtility.ToJson(winner);
            networkManager.BroadcastGameMessage("GAME_WINNER", json);
        }
private void Awake()
{
    Debug.Log("[GameController] Awake called - initializing DebugLogger");
    // CrÃ©er le DebugLogger s'il n'existe pas
    if (FindObjectOfType<DebugLogger>() == null)
    {
        GameObject loggerGO = new GameObject("DebugLogger");
        loggerGO.AddComponent<DebugLogger>();
    }
}
/// <summary>
/// OnGameMessageReceived - ReÃ§oit les messages de jeu du NetworkManager (CORRIGÃ‰ pour first finisher client)
/// </summary>
private void OnGameMessageReceived(string messageType, string data)
{
    Debug.Log($"[GameController] Received game message: {messageType}");
	    switch (messageType)
    {
        case "GAME_START":
            HandleGameStartSyncMessage(data);
            return;
            
        case "SYNC_STATUS":
            HandleSyncStatusMessage(data);
            return;
            
        case "ALL_PLAYERS_READY":
            HandleAllPlayersReadyMessage(data);
            return;
    }
    // Messages qui doivent TOUJOURS aller au WaveManager indÃ©pendamment du mode
    switch (messageType)
    {
        case "WAVE_START_SYNC":
        case "FIRST_FINISHER":
        case "NEXT_WAVE_TIMER":
            // Ces messages vont TOUJOURS au WaveManager
            if (waveManager != null)
            {
                Debug.Log($"[GameController] Forwarding {messageType} to WaveManager");
                waveManager.ProcessNetworkMessage(messageType, data);
            }
            return;
    }
    
    // IMPORTANT : WAVE_COMPLETED doit suivre la logique Host/Client normale !
    // Il ne doit PAS Ãªtre dans le switch ci-dessus
    
    // Traitement normal pour les autres messages (incluant WAVE_COMPLETED)
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
/// HandleSyncStatusMessage - Met Ã  jour le statut de synchronisation
/// </summary>
private void HandleSyncStatusMessage(string data)
{
    Debug.Log("[GameController] Handling sync status message");
    try
    {
        var syncMessage = JsonUtility.FromJson<SyncStatusMessage>(data);
        readyPlayersCount = syncMessage.readyCount;
        expectedPlayersCount = syncMessage.expectedCount;
        
        Debug.Log($"[GameController] Sync status update: {readyPlayersCount}/{expectedPlayersCount} players ready");
        
        UpdateSyncUI();
        OnSyncStatusUpdate?.Invoke(readyPlayersCount, expectedPlayersCount);
        
        if (isHost)
        {
            CheckAllPlayersReady();
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error parsing SYNC_STATUS message: {e.Message}");
    }
}
/// <summary>
/// HandleAllPlayersReadyMessage - Tous les joueurs sont prÃªts, dÃ©marrer le jeu
/// </summary>
private void HandleAllPlayersReadyMessage(string data)
{
    Debug.Log("[GameController] Handling all players ready message");
    Debug.Log("[GameController] All players are ready! Starting game sequence...");
    
    waitingForAllPlayers = false;
    UpdateSyncUI();
    
    OnAllPlayersReady?.Invoke();
    
    // DÃ©marrer la sÃ©quence de jeu normale
    if (isHost)
    {
        StartCoroutine(StartGameSequence());
    }
}

/// <summary>
/// CheckAllPlayersReady - VÃ©rifie si tous les joueurs sont prÃªts (Host seulement)
/// </summary>
private void CheckAllPlayersReady()
{
    Debug.Log("[GameController] Checking if all players are ready");
    if (!isHost) return;
    
    bool allReady = networkManager.AllPlayersSceneReady;
    
    Debug.Log($"[GameController Host] Checking ready status: {networkManager.SceneReadyPlayersCount}/{networkManager.ExpectedPlayersCount} - All ready: {allReady}");
    
    if (allReady && waitingForAllPlayers)
    {
        Debug.Log("[GameController Host] All players confirmed ready - starting game!");
        HandleAllPlayersReadyMessage(networkManager.ExpectedPlayersCount.ToString());
    }
}

/// <summary>
/// UpdateSyncUI - Met Ã  jour l'interface de synchronisation
/// </summary>
private void UpdateSyncUI()
{
    Debug.Log("[GameController] Updating sync UI");
    if (waitingForAllPlayers)
    {
        string statusMessage = $"Synchronisation: {readyPlayersCount}/{expectedPlayersCount} joueurs prÃªts";
        
        if (syncStatusText != null)
        {
            syncStatusText.text = statusMessage;
        }
        
        if (gameStateText != null)
        {
            gameStateText.text = statusMessage;
        }
        
        Debug.Log($"[GameController UI] {statusMessage}");
    }
    else
    {
        if (syncStatusText != null)
        {
            syncStatusText.text = "";
        }
    }
}
/// <summary>
/// HandleGameStartSyncMessage - Traite le message de dÃ©marrage avec info de sync
/// </summary>
private void HandleGameStartSyncMessage(string data)
{
    Debug.Log("[GameController] Handling game start sync message");
    try
    {
        var startMessage = JsonUtility.FromJson<GameStartMessage>(data);
        expectedPlayersCount = startMessage.expectedPlayers;
        
        Debug.Log($"[GameController] Game start sync - expecting {expectedPlayersCount} players");
        Debug.Log($"Expected player IDs: [{string.Join(", ", startMessage.playerIds)}]");
        
        waitingForAllPlayers = true;
        UpdateSyncUI();
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error parsing GAME_START message: {e.Message}");
    }
}
        /// <summary>
        /// Traite les messages de jeu reÃ§us du serveur (clients uniquement)
        /// </summary>
private void ProcessServerGameMessage(string messageType, string data)
{
    Debug.Log($"[GameController] Processing server game message: {messageType}");
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
                
            // TransfÃ©rer seulement les messages de synchronisation de vagues

        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error processing server game message {messageType}: {e.Message}");
    }
}

        /// <summary>
        /// Traite les demandes de jeu reÃ§ues des clients (host uniquement)
        /// </summary>
private void ProcessClientGameMessage(string messageType, string data)
{
    Debug.Log($"[GameController] Processing client game message: {messageType}");
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
                // Un client demande Ã  gagner de l'or
                parts = data.Split('|');
                if (parts.Length == 2 && int.TryParse(parts[0], out playerId) && int.TryParse(parts[1], out int gold))
                {
                    AddGoldToPlayer(playerId, gold);
                }
                break;
            case "WAVE_COMPLETED":
                // TransfÃ©rer au WaveManager SEULEMENT quand on est Host
                if (waveManager != null)
                {
                    Debug.Log($"[GameController Host] Client completed wave - Forwarding to WaveManager: {data}");
                    waveManager.ProcessNetworkMessage("WAVE_COMPLETED", data);
                }
                break;
            case "DAMAGE_REQUEST":
                // Un client demande Ã  prendre des dÃ©gÃ¢ts
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
            Debug.Log("[GameController] Handling game state message");
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
            Debug.Log("[GameController] Handling game timer message");
            try
            {
                var timerMsg = JsonUtility.FromJson<GameTimerMessage>(data);
                gameState.waveTimer = timerMsg.timer;
                
                if (timerMsg.phase == "initial")
                {
                    if (timerText != null)
                    {
                        timerText.text = $"DÃ©but dans: {Mathf.Ceil(timerMsg.timer)}s";
                    }
                }
                else if (timerMsg.phase == "preparation")
                {
                    if (timerText != null)
                    {
                        timerText.text = $"PremiÃ¨re vague dans: {Mathf.Ceil(timerMsg.timer)}s";
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
            Debug.Log("[GameController] Handling player states message");
            try
            {
                var playersMsg = JsonUtility.FromJson<PlayerStatesMessage>(data);
                gameState.playersStates = playersMsg.playersStates;

                // Remettre Ã  jour la rÃ©fÃ©rence du joueur local
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
            Debug.Log("[GameController] Handling player eliminated message");
            try
            {
                var eliminatedPlayer = JsonUtility.FromJson<PlayerGameState>(data);
                OnPlayerEliminated?.Invoke(eliminatedPlayer);
                ShowNotification($"â˜ ï¸ {eliminatedPlayer.playerName} a Ã©tÃ© Ã©liminÃ© !", 3f);
                Debug.Log($"Player {eliminatedPlayer.playerName} was eliminated!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing player eliminated message: {e.Message}");
            }
        }

        private void HandleGameWinnerMessage(string data)
        {
            Debug.Log("[GameController] Handling game winner message");
            try
            {
                var winner = JsonUtility.FromJson<PlayerGameState>(data);
                OnGameWinner?.Invoke(winner);
                ShowNotification($"ðŸ† {winner.playerName} remporte la partie !", 10f);
                Debug.Log($"Game Over! Winner: {winner.playerName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing game winner message: {e.Message}");
            }
        }

        #endregion

        #region UI Updates
        
/// <summary>
/// ShowNotification - Affiche une notification Ã  l'utilisateur
/// </summary>
private void ShowNotification(string message, float duration)
{
    Debug.Log($"[GameController] Showing notification: {message} for {duration}s");
    Debug.Log($"[GameController] Notification: {message}");
    
    // Afficher dans l'UI si disponible
    if (gameStateText != null)
    {
        gameStateText.text = message;
        gameStateText.color = Color.yellow;
        
        // Remettre la couleur normale aprÃ¨s la durÃ©e
        StartCoroutine(ResetNotificationColor(duration));
    }
}
  private IEnumerator ResetNotificationColor(float delay)
{
    Debug.Log($"[GameController] Starting reset notification color coroutine for {delay}s");
    yield return new WaitForSeconds(delay);
    if (gameStateText != null)
    {
        gameStateText.color = Color.white;
    }
}      
        private void HideNotification()
        {
            Debug.Log("[GameController] Hiding notification");
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
        }

        private void UpdateUI()
        {
            Debug.Log("[GameController] Updating UI elements");
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
                    castleHPText.text = $"ChÃ¢teau: {localPlayerState.castleHP}/{localPlayerState.maxCastleHP}";
            }
            else
            {
                // Valeurs par dÃ©faut si pas de joueur local trouvÃ©
                if (goldText != null)
                    goldText.text = $"Or: {startingGold}";

                if (castleHPText != null)
                    castleHPText.text = $"ChÃ¢teau: {startingCastleHP}/{startingCastleHP}";
            }

            // Ã‰tat du jeu et timer
            if (gameStateText != null)
            {
                string stateText = gameState.currentState switch
                {
                    GameState.WaitingForPlayers => "En attente...",
                    GameState.PreparationPhase => "Phase d'achat",
                    GameState.WaveActive => "Vague en cours",
                    GameState.GameOver => "Partie terminÃ©e",
                    _ => "Ã‰tat inconnu"
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

                // Afficher les infos de debug si activÃ©
                if (allowSinglePlayerDebug && isHost)
                {
                    string debugText = timerText.text;
                    if (!string.IsNullOrEmpty(debugText)) debugText += "\n";
                    debugText += "[DEBUG] F1:Start F2:+Gold F3:Damage F4:Wave F5:Info";
                    timerText.text = debugText;
                }
            }

            // Mettre Ã  jour l'UI des joueurs
            UpdatePlayersUI();
        }

        private void UpdatePlayersUI()
        {
            Debug.Log("[GameController] Updating players UI");
            // TODO: Mettre Ã  jour l'affichage des joueurs (vie, statut)
            // Similar to LobbyController's UpdatePlayersList but for game state
        }

        #endregion

        #region Network Events

        private void OnNetworkStateChanged(ConnectionState newState)
        {
            Debug.Log($"[GameController] Network state changed to: {newState}");
            if (newState == ConnectionState.Disconnected)
            {
                Debug.Log("Network disconnected, returning to main menu");
                BackToMainMenu();
            }
        }

// Modifiez OnNetworkGameStarted pour dÃ©marrer le timeout :
private void OnNetworkGameStarted()
{
    Debug.Log("Game started signal received from NetworkManager");
    
    if (isHost)
    {
        expectedPlayersCount = networkManager.ExpectedPlayersCount;
        readyPlayersCount = networkManager.SceneReadyPlayersCount;
        
        Debug.Log($"Host expecting {expectedPlayersCount} players for synchronization");
        
        waitingForAllPlayers = true;
        syncStartTime = Time.time; // NOUVEAU : DÃ©marrer le timer de timeout
        UpdateSyncUI();
        
        StartCoroutine(HostConfirmReady());
        StartCoroutine(SyncTimeoutHandler()); // NOUVEAU : DÃ©marrer le gestionnaire de timeout
    }
    else
    {
        Debug.Log("Client ready and waiting for sync...");
        waitingForAllPlayers = true;
        
        StartCoroutine(ClientConfirmReady());
    }
}
/// <summary>
/// SyncTimeoutHandler - Gestionnaire de timeout automatique pour la synchronisation
/// </summary>
private IEnumerator SyncTimeoutHandler()
{
    Debug.Log($"[GameController Host] Starting sync timeout handler ({syncTimeout}s)");
    
    while (waitingForAllPlayers && Time.time - syncStartTime < syncTimeout)
    {
        float remainingTime = syncTimeout - (Time.time - syncStartTime);
        
        // Mettre Ã  jour l'UI avec le temps restant
        if (syncStatusText != null)
        {
            syncStatusText.text = $"Synchronisation: {readyPlayersCount}/{expectedPlayersCount} joueurs prÃªts\nTimeout dans: {Mathf.Ceil(remainingTime)}s";
        }
        
        yield return new WaitForSeconds(0.5f);
    }
    
    // Si on arrive ici et qu'on attend encore, c'est un timeout
    if (waitingForAllPlayers)
    {
        Debug.LogWarning($"[GameController Host] Sync timeout aprÃ¨s {syncTimeout}s - force start!");
        
        // Forcer l'ajout des joueurs connectÃ©s
        if (networkManager != null)
        {
            foreach (var player in networkManager.ConnectedPlayers)
            {
                if (networkManager.SceneReadyPlayersCount < networkManager.ExpectedPlayersCount)
                {
                    // Utiliser la rÃ©flexion pour ajouter les joueurs manquants
                    var sceneReadyPlayersField = typeof(NetworkManager).GetField("sceneReadyPlayers", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (sceneReadyPlayersField != null)
                    {
                        var sceneReadyPlayers = (List<int>)sceneReadyPlayersField.GetValue(networkManager);
                        if (!sceneReadyPlayers.Contains(player.playerId))
                        {
                            sceneReadyPlayers.Add(player.playerId);
                            Debug.LogWarning($"[GameController Host] Timeout - force added player {player.playerName} (ID: {player.playerId})");
                        }
                    }
                }
            }
        }
        
        // DÃ©marrer le jeu
        waitingForAllPlayers = false;
        ShowNotification("Timeout de synchronisation - DÃ©marrage forcÃ© du jeu !", 5f);
        StartCoroutine(StartGameSequence());
    }
}
/// <summary>
/// HostConfirmReady - L'host se confirme comme prÃªt aprÃ¨s initialisation - CORRIGÃ‰
/// </summary>
private IEnumerator HostConfirmReady()
{
    Debug.Log("[GameController] Starting host confirm ready coroutine");
    // Attendre que tout soit initialisÃ© cÃ´tÃ© host
    yield return new WaitForSeconds(0.5f);
    
    Debug.Log("Host confirming ready status...");
    
    // CORRECTION : L'host doit s'ajouter explicitement aux joueurs prÃªts
    if (networkManager != null)
    {
        // RÃ©cupÃ©rer l'ID de l'hÃ´te
        var hostPlayer = networkManager.ConnectedPlayers.FirstOrDefault(p => p.isHost);
        if (hostPlayer != null)
        {
            // Appeler directement la mÃ©thode pour ajouter l'hÃ´te aux joueurs prÃªts
            networkManager.AddHostToReadyPlayers(hostPlayer.playerId);
            Debug.Log($"Host player ID {hostPlayer.playerId} added to ready players list");
        }
        else
        {
            Debug.LogError("Host player not found in ConnectedPlayers list!");
        }
    }
    
    CheckAllPlayersReady();
}
/// <summary>
/// DebugSyncDiagnostic - Diagnostic dÃ©taillÃ© du systÃ¨me de synchronisation
/// </summary>
[ContextMenu("Debug - Sync Diagnostic")]
private void DebugSyncDiagnostic()
{
    Debug.Log("=== DIAGNOSTIC SYNCHRONISATION ===");
    Debug.Log($"waitingForAllPlayers: {waitingForAllPlayers}");
    Debug.Log($"expectedPlayersCount: {expectedPlayersCount}");
    Debug.Log($"readyPlayersCount: {readyPlayersCount}");
    Debug.Log($"isHost: {isHost}");
    Debug.Log($"GameHasStarted: {networkManager?.GameHasStarted}");
    Debug.Log($"GameState: {gameState?.currentState}");
    
    if (networkManager != null)
    {
        Debug.Log($"NetworkManager.ExpectedPlayersCount: {networkManager.ExpectedPlayersCount}");
        Debug.Log($"NetworkManager.SceneReadyPlayersCount: {networkManager.SceneReadyPlayersCount}");
        Debug.Log($"NetworkManager.AllPlayersSceneReady: {networkManager.AllPlayersSceneReady}");
        
        // Afficher la liste des joueurs connectÃ©s
        var connectedPlayers = networkManager.ConnectedPlayers;
        Debug.Log($"ConnectedPlayers count: {connectedPlayers.Count}");
        for (int i = 0; i < connectedPlayers.Count; i++)
        {
            var player = connectedPlayers[i];
            Debug.Log($"  Player {i}: ID={player.playerId}, Name={player.playerName}, IsHost={player.isHost}, IsReady={player.isReady}");
        }
        
        // Afficher la liste des joueurs prÃªts sur la scÃ¨ne (via rÃ©flexion si nÃ©cessaire)
        Debug.Log($"Scene ready players: {networkManager.SceneReadyPlayersCount}");
    }
    else
    {
        Debug.LogError("NetworkManager is null!");
    }
    
    // VÃ©rifier si WaveManager est prÃ©sent
    if (waveManager != null)
    {
        Debug.Log($"WaveManager found: {waveManager.name}");
    }
    else
    {
        Debug.LogError("WaveManager is null!");
    }
    
    Debug.Log("================================");
}
/// <summary>
/// ClientConfirmReady - Le client confirme sa prÃ©sence aprÃ¨s chargement
/// </summary>
private IEnumerator ClientConfirmReady()
{
    Debug.Log("[GameController] Starting client confirm ready coroutine");
    // Attendre que l'initialisation du client soit complÃ¨te
    yield return new WaitForSeconds(1f);
    
    Debug.Log("Client confirming ready status to host...");
    
    // Envoyer la confirmation au host
    networkManager.SendSceneReadyConfirmation();
}

        #endregion

        #region UI Handlers

        private void BackToLobby()
        {
            Debug.Log("[GameController] Back to lobby requested");
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
            Debug.Log("[GameController] Back to main menu requested");
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
            Debug.Log($"[GameController] Checking if local player can afford {cost} gold");
            return localPlayerState != null && localPlayerState.gold >= cost;
        }

        public bool SpendGold(int amount)
        {
            Debug.Log($"[GameController] Spending {amount} gold for local player");
            if (CanAfford(amount) && isHost)
            {
                localPlayerState.gold -= amount;
                BroadcastPlayerStates(); // Synchroniser immÃ©diatement
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
            Debug.Log($"[GameController] Requesting to spend {amount} gold from server");
            if (networkManager != null && localPlayerState != null)
            {
                networkManager.RequestSpendGold(localPlayerState.playerId, amount);
            }
        }

        public Vector3 GetWorldPositionFromGrid(Vector2Int gridPos)
        {
            Debug.Log($"[GameController] Converting grid position {gridPos} to world position");
            return new Vector3(
                gridPos.x * tileSize + tileSize * 0.5f,
                gridPos.y * tileSize + tileSize * 0.5f,
                0
            );
        }

        public Vector2Int GetGridPositionFromWorld(Vector3 worldPos)
        {
            Debug.Log($"[GameController] Converting world position {worldPos} to grid position");
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / tileSize),
                Mathf.FloorToInt(worldPos.y / tileSize)
            );
        }

        public bool IsValidGridPosition(Vector2Int gridPos)
        {
            Debug.Log($"[GameController] Checking if grid position {gridPos} is valid");
            return gridPos.x >= 0 && gridPos.x < gridSize.x &&
                   gridPos.y >= 0 && gridPos.y < gridSize.y;
        }

        #endregion
    }
}