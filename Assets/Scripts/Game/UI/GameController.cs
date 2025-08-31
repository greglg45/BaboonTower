using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using BaboonTower.Network;
using System.Linq;

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
        
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI castleHPText;
        [SerializeField] private TextMeshProUGUI gameStateText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Button backToLobbyButton;

        [Header("Players UI")]
        [SerializeField] private Transform playersUIParent;
        [SerializeField] private GameObject playerUIItemPrefab;

        [Header("Game Grid")]
        [SerializeField] private Transform gameGrid;
        [SerializeField] private Vector2Int gridSize = new Vector2Int(30, 16);
        [SerializeField] private float tileSize = 1f;

        [Header("Debug Settings")]
        [SerializeField] private bool allowSinglePlayerDebug = true;
        [SerializeField] private bool autoStartGameAfterDelay = false;
        [SerializeField] private float autoStartDelay = 3f;
        [SerializeField] private KeyCode forceStartGameKey = KeyCode.F1;

        // État de jeu
        private GameStateData gameState;
        public GameStateData GameStateData => gameState;
        private NetworkManager networkManager;
        private bool isHost;
        private PlayerGameState localPlayerState;

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
        }

        private void Update()
        {
            // Synchronisation périodique pour l'host
            if (isHost && Time.time - lastSyncTime > SYNC_INTERVAL)
            {
                SyncGameState();
                lastSyncTime = Time.time;
            }

            // Raccourcis clavier de debug (seulement en mode debug et pour l'host)
            if (allowSinglePlayerDebug)  // Enlever la vérification isHost
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
        }

        #region Initialization

        private void InitializeGame()
        {
            networkManager = NetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager not found! Returning to main menu.");
                SceneManager.LoadScene("MainMenu");
                return;
            }

            isHost = networkManager.CurrentMode == NetworkMode.Host;
            gameState = new GameStateData();

            // Initialiser l'état des joueurs basé sur les joueurs connectés
            InitializePlayersStates();

            // Trouver l'état du joueur local
            FindLocalPlayerState();

            Debug.Log($"Game initialized - Host: {isHost}, Players: {gameState.playersStates.Count}");
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
                Debug.Log($"Available players: {string.Join(", ", gameState.playersStates.ConvertAll(p => $"{p.playerName} (ID:{p.playerId})"))}");
                Debug.Log($"Connected players: {string.Join(", ", networkManager.ConnectedPlayers.ConvertAll(p => $"{p.playerName} (ID:{p.playerId}, Host:{p.isHost})"))}");

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
            if (isHost && gameState.currentState == GameState.PreparationPhase)
            {
                gameState.waveTimer = 0f;
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

            // Attendre un peu avant de commencer
            yield return new WaitForSeconds(2f);

            // Condition de boucle modifiée pour le debug solo
            int minPlayersToKeepGoing = allowSinglePlayerDebug ? 1 : 2;
            while (gameState.alivePlayers >= minPlayersToKeepGoing && gameState.currentState != GameState.GameOver)
            {
                yield return StartCoroutine(PreparationPhase());
                
                if (gameState.currentState != GameState.GameOver)
                {
                    yield return StartCoroutine(WavePhase());
                }
            }
        }

        private IEnumerator PreparationPhase()
        {
            if (!isHost) yield break;

            // UTILISATION CORRECTE DE L'ENUM
            BroadcastGameState(GameState.PreparationPhase);
            SetGameState(GameState.PreparationPhase);
            gameState.waveTimer = preparationTime;

            float lastTimerSync = Time.time;

            while (gameState.waveTimer > 0)
            {
                gameState.waveTimer -= Time.deltaTime;

                // Synchroniser le timer avec les clients plus fréquemment
                if (Time.time - lastTimerSync > TIMER_SYNC_INTERVAL)
                {
                    BroadcastGameTimer(gameState.waveTimer, "preparation");
                    lastTimerSync = Time.time;
                }

                UpdateUI();
                yield return null;
            }
        }

        private IEnumerator WavePhase()
        {
            if (!isHost) yield break;

            gameState.currentWave++;

            // UTILISATION CORRECTE DE L'ENUM GameState (pas gameState)
            BroadcastGameState(GameState.WaveActive);
            BroadcastWaveNumber(gameState.currentWave);
            SetGameState(GameState.WaveActive);

            OnWaveStarted?.Invoke(gameState.currentWave);

            // Chercher et déclencher le WaveManager
            WaveManager waveManager = FindObjectOfType<WaveManager>();
            if (waveManager != null)
            {
                Debug.Log($"Starting wave {gameState.currentWave} with WaveManager");
                waveManager.StartWave(gameState.currentWave);

                // Attendre que la vague soit terminée
                while (waveManager.IsWaveInProgress())
                {
                    yield return new WaitForSeconds(0.5f);
                }

                Debug.Log($"Wave {gameState.currentWave} completed");
            }
            else
            {
                Debug.LogError("WaveManager not found! Cannot spawn enemies!");
                // Fallback : attendre 5 secondes
                yield return new WaitForSeconds(5f);
            }

            // Vérifier les éliminations et gagnant
            CheckGameEndConditions();
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
                    Debug.Log($"Game Over! Winner: {winner.playerName}");
                }
                else if (allowSinglePlayerDebug)
                {
                    Debug.Log("[DEBUG] Game Over in single player debug mode");
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

        /// <summary>
        /// Reçoit les messages de jeu du NetworkManager
        /// </summary>
        private void OnGameMessageReceived(string messageType, string data)
        {
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
                        
                    case "WAVE_PERFECT_CLEAR":
                        HandleWavePerfectClearMessage(data);
                        break;
                                             
                    case "SERVER_ANNOUNCEMENT":
                        // Afficher directement l'annonce sans la retransmettre
                        Debug.Log($"[SERVER ANNOUNCEMENT] {data}");
                        StartCoroutine(ShowNotificationCoroutine(data, 5f));
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

                    case "PLAYER_ACTION":
                        // TODO: Traiter les actions des joueurs (placement de tours, etc.)
                        Debug.Log($"Received player action: {data}");
                        break;
                        
                    case "WAVE_PERFECT_CLEAR":
                        // Un client annonce qu'il a parfaitement terminé sa vague
                        // Le host retransmet à tous les joueurs
                        BroadcastMessage("WAVE_PERFECT_CLEAR", data);
                        HandleWavePerfectClearMessage(data);
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error processing client game message {messageType}: {e.Message}");
            }
        }
        
        private void BroadcastMessage(string messageType, string data)
        {
            if (!isHost || networkManager == null) return;
            networkManager.BroadcastGameMessage(messageType, data);
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
                Debug.Log($"Game Over! Winner: {winner.playerName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing game winner message: {e.Message}");
            }
        }
        
        private void HandleWavePerfectClearMessage(string data)
        {
            try
            {
                // Le message contient déjà le texte formaté
                Debug.Log($"[WAVE CLEAR ANNOUNCEMENT] {data}");
                
                // Afficher le message dans l'UI (vous pouvez créer un système de notification)
                ShowGameNotification(data, 5f);
                
                // Jouer un son de célébration si disponible
                // AudioManager.PlaySound("perfect_wave_clear");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error handling wave perfect clear message: {e.Message}");
            }
        }
        
        /// <summary>
        /// Affiche une notification dans le jeu
        /// </summary>
        private void ShowGameNotification(string message, float duration)
        {
            // Option 1: Envoyer via le système de messages du NetworkManager si on est host
            if (isHost && networkManager != null)
            {
                // Le host peut broadcaster un message serveur
                networkManager.BroadcastGameMessage("SERVER_ANNOUNCEMENT", message);
            }
            
            // Option 2: Créer un message flottant dans l'UI (pour tous)
            StartCoroutine(ShowNotificationCoroutine(message, duration));
        }
        
        private System.Collections.IEnumerator ShowNotificationCoroutine(string message, float duration)
        {
            // Créer un GameObject temporaire pour afficher le message
            GameObject notificationGO = new GameObject("WaveNotification");
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("No canvas found for notification!");
                yield break;
            }
            
            notificationGO.transform.SetParent(canvas.transform, false);
            
            // Positionner au centre-haut de l'écran
            RectTransform rect = notificationGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.8f);
            rect.anchorMax = new Vector2(0.5f, 0.8f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600, 100);
            rect.anchoredPosition = Vector2.zero;
            
            // Ajouter un background
            Image bg = notificationGO.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            
            // Ajouter le texte
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(notificationGO.transform, false);
            
            TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = message;
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.84f, 0f); // Couleur dorée
            
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            // Animation d'apparition
            float elapsed = 0;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.3f;
                rect.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                yield return null;
            }
            
            // Attendre
            yield return new WaitForSeconds(duration);
            
            // Animation de disparition
            elapsed = 0;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.3f;
                rect.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
                bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 1f - t);
                text.color = new Color(text.color.r, text.color.g, text.color.b, 1f - t);
                yield return null;
            }
            
            // Détruire l'objet
            Destroy(notificationGO);
        }

        #endregion

        #region UI Updates

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

            // État du jeu et timer - UTILISATION CORRECTE DE L'ENUM
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

            if (timerText != null)
            {
                if (gameState.currentState == GameState.PreparationPhase)
                {
                    timerText.text = $"Prochaine vague dans: {Mathf.Ceil(gameState.waveTimer)}s";
                }
                else
                {
                    timerText.text = "";
                }

                // Afficher les infos de debug si activé
                if (allowSinglePlayerDebug && isHost)
                {
                    timerText.text += $"\n[DEBUG] F1:Start F2:+Gold F3:Damage F4:Wave F5:Info";
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
                // L'host démarre immédiatement la séquence de jeu
                Debug.Log("[HOST] Starting game sequence immediately");
                StartCoroutine(StartGameSequence());
            }
            else
            {
                // Le client attend les instructions du serveur
                SetGameState(GameState.WaitingForPlayers);
                Debug.Log("[CLIENT] Ready and waiting for game state from server...");
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