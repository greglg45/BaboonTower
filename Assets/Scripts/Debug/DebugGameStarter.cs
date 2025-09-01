using UnityEngine;
using UnityEngine.SceneManagement;
using BaboonTower.Network;
using BaboonTower.Game;
using System.Collections.Generic;
using System.Reflection;

<<<<<<< Updated upstream
public class DebugGameStarter : MonoBehaviour
=======
/// <summary>
/// Script unifié de debug qui combine DebugGameStarter et SimpleGameForcer
/// Corrige les erreurs de types et simplifie le démarrage du jeu
/// </summary>
public class UnifiedDebugStarter : MonoBehaviour
>>>>>>> Stashed changes
{
    [Header("Debug Settings")]
    public bool autoStartOnSceneLoad = true;
    public bool simulateHost = true;
    public float startDelay = 1f;

    [Header("Debug Controls")]
    public bool showDebugInfo = true;
    public KeyCode manualStartKey = KeyCode.F1;

    private NetworkManager networkManager;
    private GameController gameController;
    private bool gameStarted = false;
    private bool showDebugGUI = true;

    void Start()
    {
<<<<<<< Updated upstream
        Debug.Log("[DEBUG STARTER] Initializing...");

        // Si on est dans GameScene
        if (SceneManager.GetActiveScene().name == "GameScene")
=======
        Debug.Log("[UNIFIED DEBUG] Initializing debug starter...");
        
        if (autoStartAsHost)
>>>>>>> Stashed changes
        {
            SetupDebugEnvironment();

            if (autoStartOnSceneLoad)
            {
                StartCoroutine(ForceStartAfterDelay());
            }
        }
    }

    void Update()
    {
<<<<<<< Updated upstream
        // Permettre le démarrage manuel avec F1
        if (!gameStarted && Input.GetKeyDown(manualStartKey))
        {
            ForceStartGame();
        }

        // Afficher les infos de debug
        if (showDebugInfo && Input.GetKeyDown(KeyCode.F9))
=======
        // F1: Force start game
        if (!gameStarted && Input.GetKeyDown(KeyCode.F1))
        {
            ForceStartGame();
        }
        
        // F2: Add gold
        if (Input.GetKeyDown(KeyCode.F2))
        {
            AddDebugGold();
        }
        
        // F3: Damage castle
        if (Input.GetKeyDown(KeyCode.F3))
        {
            DamageDebugCastle();
        }
        
        // F4: Force next wave
        if (Input.GetKeyDown(KeyCode.F4))
        {
            ForceNextWave();
        }
        
        // F5: Print game state
        if (Input.GetKeyDown(KeyCode.F5))
        {
            PrintGameState();
        }
        
        // F9: Toggle debug info
        if (Input.GetKeyDown(KeyCode.F9))
>>>>>>> Stashed changes
        {
            showDebugGUI = !showDebugGUI;
            PrintDebugInfo();
        }
    }

    void SetupDebugEnvironment()
    {
<<<<<<< Updated upstream
        networkManager = NetworkManager.Instance;

        // Si le NetworkManager n'existe pas
        if (networkManager == null)
        {
            Debug.Log("[DEBUG STARTER] Creating NetworkManager...");

            // Créer un NetworkManager
            GameObject netObj = new GameObject("NetworkManager_DEBUG");
            DontDestroyOnLoad(netObj);
            networkManager = netObj.AddComponent<NetworkManager>();

            // Attendre un frame pour que le singleton s'initialise
            StartCoroutine(ConfigureNetworkManagerNextFrame());
        }
        else
        {
            // NetworkManager existe, le configurer
            ConfigureNetworkManager();
        }

        // Trouver le GameController
        gameController = FindObjectOfType<GameController>();
        if (gameController == null)
        {
            Debug.LogError("[DEBUG STARTER] GameController not found!");
        }
    }

    System.Collections.IEnumerator ConfigureNetworkManagerNextFrame()
    {
        yield return null; // Attendre un frame
        networkManager = NetworkManager.Instance;
        ConfigureNetworkManager();
    }

    void ConfigureNetworkManager()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[DEBUG STARTER] NetworkManager.Instance is still null!");
            return;
        }

        Debug.Log("[DEBUG STARTER] Configuring NetworkManager as HOST using reflection...");

        // Utiliser la réflexion pour forcer les propriétés privées
        System.Type networkManagerType = typeof(NetworkManager);

        // Forcer CurrentMode à Host
        FieldInfo modeField = networkManagerType.GetField("CurrentMode", BindingFlags.NonPublic | BindingFlags.Instance);
        if (modeField == null)
        {
            // Si c'est une propriété avec backing field
            PropertyInfo modeProp = networkManagerType.GetProperty("CurrentMode");
            if (modeProp != null)
            {
                // Essayer de trouver le backing field
                modeField = networkManagerType.GetField("<CurrentMode>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        if (modeField != null)
        {
            modeField.SetValue(NetworkManager.Instance, NetworkMode.Host);
            Debug.Log("[DEBUG STARTER] CurrentMode set to Host via reflection");
        }
        else
        {
            Debug.LogWarning("[DEBUG STARTER] Could not find CurrentMode field");
        }

        // Forcer CurrentState à Connected
        FieldInfo stateField = networkManagerType.GetField("CurrentState", BindingFlags.NonPublic | BindingFlags.Instance);
        if (stateField == null)
        {
            PropertyInfo stateProp = networkManagerType.GetProperty("CurrentState");
            if (stateProp != null)
            {
                stateField = networkManagerType.GetField("<CurrentState>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        if (stateField != null)
        {
            stateField.SetValue(NetworkManager.Instance, ConnectionState.Connected);
            Debug.Log("[DEBUG STARTER] CurrentState set to Connected via reflection");
        }
        else
        {
            Debug.LogWarning("[DEBUG STARTER] Could not find CurrentState field");
        }

        // Gérer ConnectedPlayers
        if (NetworkManager.Instance.ConnectedPlayers == null || NetworkManager.Instance.ConnectedPlayers.Count == 0)
        {
            Debug.Log("[DEBUG STARTER] Setting up ConnectedPlayers...");

            // Essayer de trouver le field ConnectedPlayers
            FieldInfo playersField = networkManagerType.GetField("ConnectedPlayers", BindingFlags.NonPublic | BindingFlags.Instance);
            if (playersField == null)
            {
                PropertyInfo playersProp = networkManagerType.GetProperty("ConnectedPlayers");
                if (playersProp != null)
                {
                    playersField = networkManagerType.GetField("<ConnectedPlayers>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                }
            }

            if (playersField != null)
            {
                var playersList = new List<PlayerData>();
                var debugPlayer = new PlayerData("DebugPlayer", 0, true); // isHost = true
                playersList.Add(debugPlayer);
                playersField.SetValue(NetworkManager.Instance, playersList);
                Debug.Log("[DEBUG STARTER] ConnectedPlayers configured via reflection");
            }
            else
            {
                // Si on ne peut pas utiliser la réflexion, essayer d'ajouter directement
                if (NetworkManager.Instance.ConnectedPlayers != null)
                {
                    NetworkManager.Instance.ConnectedPlayers.Clear();
                    var debugPlayer = new PlayerData("DebugPlayer", 0, true);
                    NetworkManager.Instance.ConnectedPlayers.Add(debugPlayer);
                    Debug.Log("[DEBUG STARTER] Added player to existing ConnectedPlayers list");
                }
                else
                {
                    Debug.LogError("[DEBUG STARTER] Could not configure ConnectedPlayers");
                }
            }
        }

        // Configurer le nom du joueur
        NetworkManager.Instance.SetPlayerName("DebugPlayer");

        // Forcer isHost dans GameController aussi
        ForceGameControllerHostMode();

        Debug.Log($"[DEBUG STARTER] Configuration complete:");
        Debug.Log($"  - Mode: {NetworkManager.Instance.CurrentMode}");
        Debug.Log($"  - State: {NetworkManager.Instance.CurrentState}");
        Debug.Log($"  - Players: {NetworkManager.Instance.ConnectedPlayers?.Count ?? 0}");
=======
        Debug.Log("[UNIFIED DEBUG] Setting up debug environment...");
        
        // Find or create NetworkManager
        networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager == null)
        {
            GameObject nmGo = new GameObject("NetworkManager");
            networkManager = nmGo.AddComponent<NetworkManager>();
            Debug.Log("[UNIFIED DEBUG] Created NetworkManager");
        }
        
        // Find GameController
        gameController = FindObjectOfType<GameController>();
        if (gameController == null)
        {
            Debug.LogError("[UNIFIED DEBUG] GameController not found!");
        }
        
        // Configure after one frame
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
        
        Debug.Log("[UNIFIED DEBUG] Configuring NetworkManager as HOST...");
        
        System.Type nmType = networkManager.GetType();
        
        // Set CurrentMode to Host using property or backing field
        var modeField = GetFieldOrBackingField(nmType, "CurrentMode");
        if (modeField != null)
        {
            modeField.SetValue(networkManager, NetworkMode.Host);
            Debug.Log("[UNIFIED DEBUG] CurrentMode set to Host");
        }
        
        // Set CurrentState to Connected using ConnectionState enum
        var stateField = GetFieldOrBackingField(nmType, "CurrentState");
        if (stateField != null)
        {
            stateField.SetValue(networkManager, ConnectionState.Connected);
            Debug.Log("[UNIFIED DEBUG] CurrentState set to Connected");
        }
        
        // Create ConnectedPlayers list if needed
        if (networkManager.ConnectedPlayers == null || networkManager.ConnectedPlayers.Count == 0)
        {
            var playersField = GetFieldOrBackingField(nmType, "ConnectedPlayers");
            if (playersField != null)
            {
                var playersList = new List<PlayerData>();
                var debugPlayer = new PlayerData(debugPlayerName, 1, true); // Use correct constructor
                playersList.Add(debugPlayer);
                playersField.SetValue(networkManager, playersList);
                Debug.Log("[UNIFIED DEBUG] Added debug player to ConnectedPlayers");
            }
        }
        
        // Force GameController to host mode
        ForceGameControllerHostMode();
    }

    private FieldInfo GetFieldOrBackingField(System.Type type, string propertyName)
    {
        // Try direct field
        var field = type.GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (field != null) return field;
        
        // Try auto-generated backing field
        field = type.GetField($"<{propertyName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) return field;
        
        // Try lowercase version
        field = type.GetField(propertyName.ToLower(), BindingFlags.NonPublic | BindingFlags.Instance);
        return field;
>>>>>>> Stashed changes
    }

    void ForceGameControllerHostMode()
    {
        if (gameController == null)
        {
            gameController = FindObjectOfType<GameController>();
        }
<<<<<<< Updated upstream

        if (gameController != null)
=======
        
        if (gameController == null) return;
        
        Debug.Log("[UNIFIED DEBUG] Creating debug player in GameController...");
        
        // Access GameStateData
        var gameStateDataProp = gameController.GetType().GetProperty("GameStateData");
        if (gameStateDataProp != null)
>>>>>>> Stashed changes
        {
            // Utiliser la réflexion pour forcer isHost à true
            System.Type controllerType = typeof(GameController);
            FieldInfo hostField = controllerType.GetField("isHost", BindingFlags.NonPublic | BindingFlags.Instance);

            if (hostField != null)
            {
<<<<<<< Updated upstream
                hostField.SetValue(gameController, true);
                Debug.Log("[DEBUG STARTER] GameController.isHost forced to true");
            }
            else
            {
                Debug.LogWarning("[DEBUG STARTER] Could not find isHost field in GameController");
=======
                // Create a player
                var player = new PlayerGameState(1, debugPlayerName, startingGold, startingHP);
                
                if (gameStateData.playersStates == null)
                {
                    gameStateData.playersStates = new List<PlayerGameState>();
                }
                
                gameStateData.playersStates.Clear();
                gameStateData.playersStates.Add(player);
                gameStateData.alivePlayers = 1;
                
                Debug.Log($"[UNIFIED DEBUG] Created player: {debugPlayerName} with {startingGold} gold and {startingHP} HP");
                
                // Set localPlayerState
                var localPlayerField = gameController.GetType().GetField("localPlayerState", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (localPlayerField != null)
                {
                    localPlayerField.SetValue(gameController, player);
                    Debug.Log("[UNIFIED DEBUG] Set localPlayerState");
                }
>>>>>>> Stashed changes
            }
        }
    }

    System.Collections.IEnumerator ForceStartAfterDelay()
    {
<<<<<<< Updated upstream
        Debug.Log($"[DEBUG STARTER] Waiting {startDelay} seconds before auto-start...");
        yield return new WaitForSeconds(startDelay);

=======
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
            Debug.Log("[UNIFIED DEBUG] GameController.isHost forced to true");
        }
    }

    private IEnumerator ForceStartAfterDelay()
    {
        Debug.Log($"[UNIFIED DEBUG] Waiting {autoStartDelay} seconds before auto-start...");
        yield return new WaitForSeconds(autoStartDelay);
        
>>>>>>> Stashed changes
        ForceStartGame();
    }

    void ForceStartGame()
    {
<<<<<<< Updated upstream
        if (gameStarted)
        {
            Debug.LogWarning("[DEBUG STARTER] Game already started!");
            return;
=======
        if (gameStarted) return;
        
        Debug.Log("[UNIFIED DEBUG] === FORCING GAME START ===");
        
        // Ensure everything is configured
        ForceGameControllerHostMode();
        CreateDebugPlayer();
        
        if (gameController != null)
        {
            // Change game state directly to PreparationPhase
            var gameStateDataProp = gameController.GetType().GetProperty("GameStateData");
            if (gameStateDataProp != null)
            {
                var gameStateData = gameStateDataProp.GetValue(gameController) as GameStateData;
                if (gameStateData != null)
                {
                    // Ensure player exists
                    if (gameStateData.playersStates == null || gameStateData.playersStates.Count == 0)
                    {
                        CreateDebugPlayer();
                    }
                    
                    // Start with preparation phase
                    gameStateData.currentState = GameState.PreparationPhase;
                    gameStateData.currentWave = 0;
                    gameStateData.preparationTime = 5f;
                    gameStateData.waveTimer = 5f;
                    
                    Debug.Log("[UNIFIED DEBUG] Game state set to PreparationPhase");
                }
            }
            
            // Trigger game start
            gameController.SendMessage("OnNetworkGameStarted", SendMessageOptions.DontRequireReceiver);
            
            // Also try ForceStartGame if it exists
            gameController.SendMessage("ForceStartGame", SendMessageOptions.DontRequireReceiver);
>>>>>>> Stashed changes
        }

        gameController = FindObjectOfType<GameController>();
        if (gameController == null)
        {
            Debug.LogError("[DEBUG STARTER] GameController not found! Cannot start game.");
            return;
        }

        // S'assurer que tout est configuré
        if (NetworkManager.Instance != null && NetworkManager.Instance.CurrentMode != NetworkMode.Host)
        {
            Debug.LogWarning("[DEBUG STARTER] Not in Host mode! Reconfiguring...");
            ConfigureNetworkManager();
        }

        // Forcer isHost dans GameController une dernière fois
        ForceGameControllerHostMode();

        Debug.Log("[DEBUG STARTER] Force starting game as HOST...");

        // Utiliser SendMessage pour appeler les méthodes privées
        gameController.SendMessage("ForceStartGame", SendMessageOptions.DontRequireReceiver);

        gameStarted = true;
<<<<<<< Updated upstream

        Debug.Log("[DEBUG STARTER] Game started! Debug commands available:");
        Debug.Log("  F1: Force Start (already done)");
=======
        
        Debug.Log("[UNIFIED DEBUG] === GAME STARTED ===");
        Debug.Log("Debug commands:");
        Debug.Log("  F1: Force Start (done)");
>>>>>>> Stashed changes
        Debug.Log("  F2: Add 50 Gold");
        Debug.Log("  F3: Damage Castle 25 HP");
        Debug.Log("  F4: Force Next Wave");
        Debug.Log("  F5: Print Game State");
<<<<<<< Updated upstream
        Debug.Log("  F9: Print Debug Info (from DebugStarter)");
=======
        Debug.Log("  F9: Toggle Debug GUI");
>>>>>>> Stashed changes
    }

    void PrintDebugInfo()
    {
<<<<<<< Updated upstream
        Debug.Log("=== DEBUG STARTER INFO ===");
=======
        if (gameController != null)
        {
            gameController.SendMessage("DebugAddGold", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[UNIFIED DEBUG] Added 50 gold");
        }
    }
>>>>>>> Stashed changes

        if (NetworkManager.Instance != null)
        {
<<<<<<< Updated upstream
            Debug.Log($"NetworkManager:");
            Debug.Log($"  - Mode: {NetworkManager.Instance.CurrentMode}");
            Debug.Log($"  - State: {NetworkManager.Instance.CurrentState}");
            Debug.Log($"  - Players: {NetworkManager.Instance.ConnectedPlayers?.Count ?? 0}");
        }
        else
        {
            Debug.Log("NetworkManager: NULL");
=======
            gameController.SendMessage("DebugDamageCastle", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[UNIFIED DEBUG] Damaged castle by 25 HP");
>>>>>>> Stashed changes
        }

        if (gameController != null)
        {
<<<<<<< Updated upstream
            // Utiliser la réflexion pour lire isHost
            System.Type controllerType = typeof(GameController);
            FieldInfo hostField = controllerType.GetField("isHost", BindingFlags.NonPublic | BindingFlags.Instance);
            bool isHost = false;
            if (hostField != null)
            {
                isHost = (bool)hostField.GetValue(gameController);
            }

            Debug.Log($"GameController:");
            Debug.Log($"  - Found: Yes");
            Debug.Log($"  - isHost (private): {isHost}");
            Debug.Log($"  - Debug Mode Allowed: {gameController.IsDebugSinglePlayerAllowed}");

            if (gameController.GameStateData != null)
            {
                Debug.Log($"  - Current State: {gameController.GameStateData.currentState}");
                Debug.Log($"  - Current Wave: {gameController.GameStateData.currentWave}");
                Debug.Log($"  - Alive Players: {gameController.GameStateData.alivePlayers}");
            }
        }
        else
=======
            gameController.SendMessage("DebugForceNextWave", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[UNIFIED DEBUG] Forced next wave");
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
        
        // Network info
        if (networkManager != null)
        {
            Debug.Log($"NetworkManager Mode: {networkManager.CurrentMode}");
            Debug.Log($"NetworkManager State: {networkManager.CurrentState}");
            Debug.Log($"Connected Players: {networkManager.ConnectedPlayers?.Count ?? 0}");
            
            if (networkManager.ConnectedPlayers != null)
            {
                foreach (var player in networkManager.ConnectedPlayers)
                {
                    Debug.Log($"  - {player.playerName} (ID: {player.playerId}, Host: {player.isHost})");
                }
            }
        }
        
        // GameController info
        if (gameController != null)
        {
            // Check isHost via reflection
            System.Type gcType = gameController.GetType();
            FieldInfo hostField = gcType.GetField("isHost", BindingFlags.NonPublic | BindingFlags.Instance);
            if (hostField != null)
            {
                bool isHost = (bool)hostField.GetValue(gameController);
                Debug.Log($"GameController.isHost = {isHost}");
            }
            
            // Game state info
            var gameStateDataProp = gameController.GetType().GetProperty("GameStateData");
            if (gameStateDataProp != null)
            {
                var gameStateData = gameStateDataProp.GetValue(gameController) as GameStateData;
                if (gameStateData != null)
                {
                    Debug.Log($"Game State: {gameStateData.currentState}");
                    Debug.Log($"Current Wave: {gameStateData.currentWave}");
                    Debug.Log($"Wave Timer: {gameStateData.waveTimer:F1}");
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
        
        // WaveManager info
        var waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
>>>>>>> Stashed changes
        {
            Debug.Log("GameController: NOT FOUND");
        }

        // Vérifier les managers
        Debug.Log($"WaveManager: {(FindObjectOfType<WaveManager>() != null ? "Found" : "NOT FOUND")}");

        // Compter les ennemis
        var enemies = FindObjectsOfType<Enemy>();
        Debug.Log($"Enemies in scene: {enemies.Length}");
        foreach (var enemy in enemies)
        {
            Debug.Log($"  - {enemy.name} at {enemy.transform.position}");
        }

        Debug.Log("=========================");
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        // Afficher les infos de debug à l'écran
        GUI.Box(new Rect(10, 10, 250, 140), "Debug Game Starter");

        string modeText = NetworkManager.Instance != null ?
            $"Mode: {NetworkManager.Instance.CurrentMode}" : "Mode: No NetworkManager";
        GUI.Label(new Rect(20, 40, 230, 20), modeText);

        string stateText = gameController != null && gameController.GameStateData != null ?
            $"State: {gameController.GameStateData.currentState}" : "State: Not initialized";
        GUI.Label(new Rect(20, 60, 230, 20), stateText);

        if (gameController != null)
        {
            // Utiliser la réflexion pour lire isHost
            System.Type controllerType = typeof(GameController);
            FieldInfo hostField = controllerType.GetField("isHost", BindingFlags.NonPublic | BindingFlags.Instance);
            if (hostField != null)
            {
                bool isHost = (bool)hostField.GetValue(gameController);
                GUI.Label(new Rect(20, 80, 230, 20), $"isHost: {isHost}");
            }
        }

        if (!gameStarted)
        {
            if (GUI.Button(new Rect(20, 100, 220, 25), "Start Game (F1)"))
            {
                ForceStartGame();
            }
        }
        else
        {
            GUI.Label(new Rect(20, 100, 230, 20), "Game Started - Use F1-F5");
        }
<<<<<<< Updated upstream
=======
        
        // Enemy count
        var enemies = FindObjectsOfType<Enemy>();
        Debug.Log($"Enemies in scene: {enemies.Length}");
        
        Debug.Log("==================");
>>>>>>> Stashed changes
    }

    private void OnGUI()
    {
        if (!showDebugGUI) return;
        
        // Debug box
        GUI.Box(new Rect(10, 10, 250, 150), "Unity Debug Mode");
        
        int y = 30;
        GUI.Label(new Rect(20, y, 220, 20), gameStarted ? "? GAME STARTED (HOST MODE)" : "Press F1 to start");
        
        y += 25;
        if (gameController != null)
        {
            var gameStateDataProp = gameController.GetType().GetProperty("GameStateData");
            if (gameStateDataProp != null)
            {
                var gameStateData = gameStateDataProp.GetValue(gameController) as GameStateData;
                if (gameStateData != null)
                {
                    GUI.Label(new Rect(20, y, 220, 20), $"State: {gameStateData.currentState}");
                    y += 20;
                    GUI.Label(new Rect(20, y, 220, 20), $"Wave: {gameStateData.currentWave}");
                    y += 20;
                    
                    if (gameStateData.playersStates != null && gameStateData.playersStates.Count > 0)
                    {
                        var player = gameStateData.playersStates[0];
                        GUI.Label(new Rect(20, y, 220, 20), $"Gold: {player.gold} | HP: {player.castleHP}");
                    }
                }
            }
        }
        
        y += 25;
        GUI.Label(new Rect(20, y, 220, 20), "F9: Toggle this GUI");
    }
}