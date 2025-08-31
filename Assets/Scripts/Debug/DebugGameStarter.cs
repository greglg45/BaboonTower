using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BaboonTower.Network;
using BaboonTower.Game;

/// <summary>
/// Force le démarrage du jeu en mode debug avec création automatique d'un joueur
/// </summary>
public class DebugGameStarter : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool autoStartAsHost = true;
    [SerializeField] private float autoStartDelay = 1f;
    [SerializeField] private string debugPlayerName = "DebugPlayer";
    [SerializeField] private int startingGold = 50;
    [SerializeField] private int startingHP = 100;

    private NetworkManager networkManager;
    private GameController gameController;
    private bool gameStarted = false;

    private void Start()
    {
        Debug.Log("[DEBUG STARTER] Initializing...");
        
        if (autoStartAsHost)
        {
            SetupDebugEnvironment();
            StartCoroutine(ForceStartAfterDelay());
        }
    }

    private void Update()
    {
        // Debug commands
        if (Input.GetKeyDown(KeyCode.F1) && !gameStarted)
        {
            ForceStartGame();
        }
        
        if (Input.GetKeyDown(KeyCode.F2))
        {
            AddDebugGold();
        }
        
        if (Input.GetKeyDown(KeyCode.F3))
        {
            DamageDebugCastle();
        }
        
        if (Input.GetKeyDown(KeyCode.F4))
        {
            ForceNextWave();
        }
        
        if (Input.GetKeyDown(KeyCode.F5))
        {
            PrintGameState();
        }
        
        if (Input.GetKeyDown(KeyCode.F9))
        {
            PrintDebugInfo();
        }
    }

    private void SetupDebugEnvironment()
    {
        Debug.Log("[DEBUG STARTER] Creating NetworkManager...");
        
        // Create NetworkManager if not exists
        networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager == null)
        {
            GameObject nmGo = new GameObject("NetworkManager");
            networkManager = nmGo.AddComponent<NetworkManager>();
        }
        
        gameController = FindObjectOfType<GameController>();
        
        // Configure NetworkManager
        StartCoroutine(ConfigureNetworkManagerNextFrame());
    }

    private IEnumerator ConfigureNetworkManagerNextFrame()
    {
        yield return null; // Wait one frame
        
        ConfigureNetworkManager();
        yield return null;
        
        // Create debug player
        CreateDebugPlayer();
    }

    private void ConfigureNetworkManager()
    {
        if (networkManager == null) return;
        
        Debug.Log("[DEBUG STARTER] Configuring NetworkManager as HOST using reflection...");
        
        System.Type nmType = networkManager.GetType();
        
        // Set CurrentMode to Host
        PropertyInfo modeProperty = nmType.GetProperty("CurrentMode");
        if (modeProperty != null)
        {
            modeProperty.SetValue(networkManager, NetworkMode.Host);
            Debug.Log("[DEBUG STARTER] CurrentMode set to Host via reflection");
        }
        
        // Set CurrentState to Connected
        PropertyInfo stateProperty = nmType.GetProperty("CurrentState");
        if (stateProperty != null)
        {
            stateProperty.SetValue(networkManager, NetworkState.Connected);
            Debug.Log("[DEBUG STARTER] CurrentState set to Connected via reflection");
        }
        
        // Create a debug player in ConnectedPlayers
        Debug.Log("[DEBUG STARTER] Setting up ConnectedPlayers...");
        
        FieldInfo connectedPlayersField = nmType.GetField("connectedPlayers", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (connectedPlayersField != null)
        {
            var playerData = new NetworkPlayerData
            {
                playerId = 1,
                playerName = debugPlayerName,
                isHost = true,
                isReady = true
            };
            
            var playersList = new List<NetworkPlayerData> { playerData };
            connectedPlayersField.SetValue(networkManager, playersList);
            
            Debug.Log("[DEBUG STARTER] ConnectedPlayers configured via reflection");
        }
        
        // Force GameController to recognize host mode
        ForceGameControllerHostMode();
        
        Debug.Log("[DEBUG STARTER] Configuration complete:");
        Debug.Log($"  - Mode: {networkManager.CurrentMode}");
        Debug.Log($"  - State: {networkManager.CurrentState}");
        Debug.Log($"  - Players: {networkManager.GetPlayerCount()}");
    }

    private void CreateDebugPlayer()
    {
        if (gameController == null)
        {
            gameController = FindObjectOfType<GameController>();
        }
        
        if (gameController == null) return;
        
        Debug.Log("[DEBUG STARTER] Creating debug player in GameController...");
        
        // Access GameStateData
        var gameStateDataProp = gameController.GetType().GetProperty("GameStateData");
        if (gameStateDataProp != null)
        {
            var gameStateData = gameStateDataProp.GetValue(gameController) as GameStateData;
            if (gameStateData != null)
            {
                // Create a player
                var player = new PlayerGameState(1, debugPlayerName, startingGold, startingHP);
                
                if (gameStateData.playersStates == null)
                {
                    gameStateData.playersStates = new List<PlayerGameState>();
                }
                
                gameStateData.playersStates.Clear();
                gameStateData.playersStates.Add(player);
                gameStateData.alivePlayers = 1;
                
                Debug.Log($"[DEBUG STARTER] Created player: {debugPlayerName} with {startingGold} gold and {startingHP} HP");
                
                // Force localPlayerState
                var localPlayerField = gameController.GetType().GetField("localPlayerState", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (localPlayerField != null)
                {
                    localPlayerField.SetValue(gameController, player);
                    Debug.Log("[DEBUG STARTER] Set localPlayerState");
                }
            }
        }
    }

    private void ForceGameControllerHostMode()
    {
        if (gameController == null)
        {
            gameController = FindObjectOfType<GameController>();
        }
        
        if (gameController == null) return;
        
        System.Type gcType = gameController.GetType();
        
        // Set isHost to true
        FieldInfo isHostField = gcType.GetField("isHost", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (isHostField != null)
        {
            isHostField.SetValue(gameController, true);
            Debug.Log("[DEBUG STARTER] GameController.isHost forced to true");
        }
    }

    private IEnumerator ForceStartAfterDelay()
    {
        Debug.Log($"[DEBUG STARTER] Waiting {autoStartDelay} seconds before auto-start...");
        yield return new WaitForSeconds(autoStartDelay);
        
        // Re-create player if needed
        CreateDebugPlayer();
        
        ForceStartGame();
    }

    private void ForceStartGame()
    {
        if (gameStarted) return;
        
        ForceGameControllerHostMode();
        CreateDebugPlayer(); // Ensure player exists
        
        Debug.Log("[DEBUG STARTER] Force starting game as HOST...");
        
        if (gameController != null)
        {
            // Change game state directly
            var gameStateDataProp = gameController.GetType().GetProperty("GameStateData");
            if (gameStateDataProp != null)
            {
                var gameStateData = gameStateDataProp.GetValue(gameController) as GameStateData;
                if (gameStateData != null)
                {
                    // Ensure we have a player
                    if (gameStateData.playersStates == null || gameStateData.playersStates.Count == 0)
                    {
                        CreateDebugPlayer();
                    }
                    
                    // Start with preparation phase
                    gameStateData.currentState = GameState.PreparationPhase;
                    gameStateData.currentWave = 0;
                    gameStateData.preparationTime = 5f; // 5 seconds before first wave
                    gameStateData.waveTimer = 5f;
                    
                    Debug.Log("[DEBUG STARTER] Game state set to PreparationPhase");
                }
            }
            
            // Trigger game start
            gameController.SendMessage("OnNetworkGameStarted", SendMessageOptions.DontRequireReceiver);
        }
        
        gameStarted = true;
        
        Debug.Log("[DEBUG STARTER] Game started! Debug commands available:");
        Debug.Log("  F1: Force Start (already done)");
        Debug.Log("  F2: Add 50 Gold");
        Debug.Log("  F3: Damage Castle 25 HP");
        Debug.Log("  F4: Force Next Wave");
        Debug.Log("  F5: Print Game State");
        Debug.Log("  F9: Print Debug Info");
    }

    private void AddDebugGold()
    {
        if (gameController != null)
        {
            gameController.SendMessage("DebugAddGold", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[DEBUG] Added 50 gold");
        }
    }

    private void DamageDebugCastle()
    {
        if (gameController != null)
        {
            gameController.SendMessage("DebugDamageCastle", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[DEBUG] Damaged castle by 25 HP");
        }
    }

    private void ForceNextWave()
    {
        if (gameController != null)
        {
            gameController.SendMessage("DebugForceNextWave", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[DEBUG] Forced next wave");
        }
    }

    private void PrintGameState()
    {
        if (gameController != null)
        {
            gameController.SendMessage("DebugPrintGameState", SendMessageOptions.DontRequireReceiver);
        }
    }

    private void PrintDebugInfo()
    {
        Debug.Log("=== DEBUG INFO ===");
        
        if (networkManager != null)
        {
            Debug.Log($"NetworkManager Mode: {networkManager.CurrentMode}");
            Debug.Log($"NetworkManager State: {networkManager.CurrentState}");
            Debug.Log($"Connected Players: {networkManager.GetPlayerCount()}");
        }
        
        if (gameController != null)
        {
            var gameStateDataProp = gameController.GetType().GetProperty("GameStateData");
            if (gameStateDataProp != null)
            {
                var gameStateData = gameStateDataProp.GetValue(gameController) as GameStateData;
                if (gameStateData != null)
                {
                    Debug.Log($"Game State: {gameStateData.currentState}");
                    Debug.Log($"Current Wave: {gameStateData.currentWave}");
                    Debug.Log($"Wave Timer: {gameStateData.waveTimer}");
                    Debug.Log($"Alive Players: {gameStateData.alivePlayers}");
                    
                    if (gameStateData.playersStates != null)
                    {
                        foreach (var player in gameStateData.playersStates)
                        {
                            Debug.Log($"  Player {player.playerId}: {player.playerName} - Gold: {player.gold}, HP: {player.castleHP}/{player.maxCastleHP}");
                        }
                    }
                }
            }
        }
        
        var waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
        {
            Debug.Log($"WaveManager active: {waveManager.enabled}");
        }
        
        Debug.Log("==================");
    }
}