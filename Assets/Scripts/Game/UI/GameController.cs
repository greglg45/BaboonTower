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

    [System.Serializable]
    public class WaveCompletedMessage
    {
        public string playerName;
        public float timeUntilNextWave;
        public int currentWave;
    }

    [System.Serializable]
    public class WaveCountdownMessage
    {
        public float timeRemaining;
        public string firstPlayer;
    }

    [System.Serializable]
    public class MapConfigjson
    {
        public float preparationTime = 10f;
        public float timeBetweenWaves = 15f;
        public int startingGold = 10;
        public int startingCastleHP = 100;
    }

    public class GameController : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField] private int startingGold = 10;
        [SerializeField] private int startingCastleHP = 100;
        [SerializeField] private float preparationTime = 10f;
        [SerializeField] private float waveStartDelay = 5f;

        [Header("Wave Synchronization")]
        [SerializeField] private float timeBetweenWaves = 15f; // Temps après qu'un joueur finit avant la prochaine vague
        private float nextWaveCountdown = -1f; // -1 = pas de countdown actif
        private string firstPlayerToFinish = "";
        private bool waveCompletedLocally = false;
        private float lastTimerSync = 0f;

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

            // Gestion du countdown entre vagues
            if (isHost && nextWaveCountdown > 0)
            {
                nextWaveCountdown -= Time.deltaTime;
                
                // Synchroniser le timer toutes les 0.5 secondes
                if (Time.time - lastTimerSync > TIMER_SYNC_INTERVAL)
                {
                    BroadcastWaveCountdown(nextWaveCountdown);
                    lastTimerSync = Time.time;
                }
                
                // Démarrer la prochaine vague quand le timer arrive à 0
                if (nextWaveCountdown <= 0)
                {
                    nextWaveCountdown = -1;
                    firstPlayerToFinish = "";
                    waveCompletedLocally = false;
                    StartCoroutine(WavePhase());
                }
            }

            // Raccourcis clavier de debug (seulement en mode debug et pour l'host)
            if (allowSinglePlayerDebug)
            {
                if (Input.GetKeyDown(forceStartGameKey))
                {
                    ForceStartGame();
                }

                if (Input.GetKeyDown(KeyCode.F2))
                {
                    DebugAddGold();
                }

                if (Input.GetKeyDown(KeyCode.F3))
                {
                    DebugDamageCastle();
                }

                if (Input.GetKeyDown(KeyCode.F4))
                {
                    DebugForceNextWave();
                }

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
            // Charger la configuration depuis le JSON de la map (si disponible)
            string mapConfigPath = "Assets/StreamingAssets/map_config.json";
            if (System.IO.File.Exists(mapConfigPath))
            {
                string json = System.IO.File.ReadAllText(mapConfigPath);
                MapConfigjson config = JsonUtility.FromJson<MapConfigjson>(json);
                preparationTime = config.preparationTime;
                timeBetweenWaves = config.timeBetweenWaves;
                startingGold = config.startingGold;
                startingCastleHP = config.startingCastleHP;
                
                Debug.Log($"Map config loaded: prep={preparationTime}s, between waves={timeBetweenWaves}s");
            }

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

        #region Wave Management

        // Méthode pour signaler qu'un joueur a fini sa vague
        public void OnLocalWaveCompleted()
        {
            if (waveCompletedLocally) return; // Éviter les doublons
            waveCompletedLocally = true;
            
            Debug.Log($"Local player completed wave {gameState.currentWave}");
            
            if (isHost)
            {
                // L'hôte gère le timer global
                OnPlayerCompletedWave(localPlayerState.playerName);
            }
            else
            {
                // Le client informe le serveur
                networkManager.SendGameMessageToServer("WAVE_COMPLETED", localPlayerState.playerName);
            }
        }

        // Méthode appelée quand un joueur termine sa vague (côté serveur uniquement)
        private void OnPlayerCompletedWave(string playerName)
        {
            if (!isHost) return;
            
            // Si c'est le premier joueur à finir cette vague
            if (nextWaveCountdown < 0 && gameState.currentState == GameState.WaveActive)
            {
                firstPlayerToFinish = playerName;
                nextWaveCountdown = timeBetweenWaves;
                
                // Broadcaster à tous les joueurs
                var message = new WaveCompletedMessage
                {
                    playerName = playerName,
                    timeUntilNextWave = timeBetweenWaves,
                    currentWave = gameState.currentWave
                };
                
                string json = JsonUtility.ToJson(message);
                networkManager.BroadcastGameMessage("FIRST_PLAYER_COMPLETED", json);
                
                Debug.Log($"{playerName} finished wave {gameState.currentWave} first! Next wave in {timeBetweenWaves} seconds");
            }
        }

        // Broadcaster le countdown
        private void BroadcastWaveCountdown(float timeRemaining)
        {
            if (!isHost) return;
            
            var message = new WaveCountdownMessage
            {
                timeRemaining = timeRemaining,
                firstPlayer = firstPlayerToFinish
            };
            
            string json = JsonUtility.ToJson(message);
            networkManager.BroadcastGameMessage("WAVE_COUNTDOWN", json);
        }

        // Afficher le message de fin de vague
        private void ShowWaveCompletedMessage(string playerName, float timeUntilNext)
        {
            if (gameStateText != null)
            {
                gameStateText.text = $"{playerName} a fini la vague en premier !!!\nProchaine vague dans {timeUntilNext:F0} secondes!";
            }
        }

        // Mettre à jour l'UI du countdown
        private void UpdateWaveCountdownUI(float timeRemaining, string firstPlayer)
        {
            if (timerText != null && timeRemaining > 0)
            {
                timerText.text = $"Prochaine vague dans: {Mathf.Ceil(timeRemaining)}s";
                timerText.gameObject.SetActive(true);
            }
            else if (timerText != null)
            {
                timerText.gameObject.SetActive(false);
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

            // Informer tous les clients que le jeu commence
            BroadcastGameState(GameState.WaitingForPlayers);
            SetGameState(GameState.WaitingForPlayers);
            yield return new WaitForSeconds(waveStartDelay);

            // Démarrer la première phase de préparation
            BroadcastGameState(GameState.PreparationPhase);
            SetGameState(GameState.PreparationPhase);
            gameState.waveTimer = preparationTime;

            // Condition de boucle modifiée pour le debug solo
            int minPlayersToKeepGoing = allowSinglePlayerDebug ? 1 : 2;
            while (gameState.alivePlayers >= minPlayersToKeepGoing && gameState.currentState != GameState.GameOver)
            {
                yield return StartCoroutine(PreparationPhase());
                yield return StartCoroutine(WavePhase());
            }
        }

        private IEnumerator PreparationPhase()
        {
            if (!isHost) yield break;

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
            waveCompletedLocally = false; // Reset pour la nouvelle vague

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

                // Attendre que la vague soit terminée OU qu'un joueur finisse en premier
                while (waveManager.IsWaveInProgress() && nextWaveCountdown < 0)
                {
                    yield return new WaitForSeconds(0.5f);
                }

                Debug.Log($"Wave {gameState.currentWave} phase completed");
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

        private void SyncGameState()
        {
            if (!isHost) return;

            BroadcastGameState(gameState.currentState);
            BroadcastPlayerStates();
        }

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

        private void BroadcastWaveNumber(int waveNumber)
        {
            if (!isHost || networkManager == null) return;

            networkManager.BroadcastGameMessage("WAVE_STARTED", waveNumber.ToString());
            Debug.Log($"Broadcasting wave number: {waveNumber}");
        }

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

        private void BroadcastPlayerElimination(PlayerGameState player)
        {
            if (!isHost || networkManager == null) return;

            string json = JsonUtility.ToJson(player);
            networkManager.BroadcastGameMessage("PLAYER_ELIMINATED", json);
        }

        private void BroadcastGameWinner(PlayerGameState winner)
        {
            if (!isHost || networkManager == null) return;

            string json = JsonUtility.ToJson(winner);
            networkManager.BroadcastGameMessage("GAME_WINNER", json);
        }

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
                            waveCompletedLocally = false; // Reset pour la nouvelle vague
                            OnWaveStarted?.Invoke(wave);
                            
                            // IMPORTANT: Les clients doivent aussi démarrer leur WaveManager!
                            WaveManager waveManager = FindObjectOfType<WaveManager>();
                            if (waveManager != null)
                            {
                                waveManager.StartWave(wave);
                            }
                            
                            UpdateUI();
                        }
                        break;
                        
                    case "FIRST_PLAYER_COMPLETED":
                        var completedMsg = JsonUtility.FromJson<WaveCompletedMessage>(data);
                        ShowWaveCompletedMessage(completedMsg.playerName, completedMsg.timeUntilNextWave);
                        break;
                        
                    case "WAVE_COUNTDOWN":
                        var countdownMsg = JsonUtility.FromJson<WaveCountdownMessage>(data);
                        UpdateWaveCountdownUI(countdownMsg.timeRemaining, countdownMsg.firstPlayer);
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
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error processing server game message {messageType}: {e.Message}");
            }
        }

        private void ProcessClientGameMessage(string messageType, string data)
        {
            if (!isHost) return;
            
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
                        
                    case "WAVE_COMPLETED":
                        OnPlayerCompletedWave(data); // data contient le nom du joueur
                        break;

                    case "PLAYER_ACTION":
                        // TODO: Traiter les actions des joueurs (placement de tours, etc.)
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