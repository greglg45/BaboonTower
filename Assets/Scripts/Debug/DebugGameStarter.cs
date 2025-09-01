using UnityEngine;
using UnityEngine.SceneManagement;
using BaboonTower.Network;
using BaboonTower.Game;
using System.Collections.Generic;
using System.Reflection;

public class DebugGameStarter : MonoBehaviour
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

    void Start()
    {
        Debug.Log("[DEBUG STARTER] Initializing...");

        // Si on est dans GameScene
        if (SceneManager.GetActiveScene().name == "GameScene")
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
        // Permettre le démarrage manuel avec F1
        if (!gameStarted && Input.GetKeyDown(manualStartKey))
        {
            ForceStartGame();
        }

        // Afficher les infos de debug
        if (showDebugInfo && Input.GetKeyDown(KeyCode.F9))
        {
            PrintDebugInfo();
        }
    }

    void SetupDebugEnvironment()
    {
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
    }

    void ForceGameControllerHostMode()
    {
        if (gameController == null)
        {
            gameController = FindObjectOfType<GameController>();
        }

        if (gameController != null)
        {
            // Utiliser la réflexion pour forcer isHost à true
            System.Type controllerType = typeof(GameController);
            FieldInfo hostField = controllerType.GetField("isHost", BindingFlags.NonPublic | BindingFlags.Instance);

            if (hostField != null)
            {
                hostField.SetValue(gameController, true);
                Debug.Log("[DEBUG STARTER] GameController.isHost forced to true");
            }
            else
            {
                Debug.LogWarning("[DEBUG STARTER] Could not find isHost field in GameController");
            }
        }
    }

    System.Collections.IEnumerator ForceStartAfterDelay()
    {
        Debug.Log($"[DEBUG STARTER] Waiting {startDelay} seconds before auto-start...");
        yield return new WaitForSeconds(startDelay);

        ForceStartGame();
    }

    void ForceStartGame()
    {
        if (gameStarted)
        {
            Debug.LogWarning("[DEBUG STARTER] Game already started!");
            return;
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

        Debug.Log("[DEBUG STARTER] Game started! Debug commands available:");
        Debug.Log("  F1: Force Start (already done)");
        Debug.Log("  F2: Add 50 Gold");
        Debug.Log("  F3: Damage Castle 25 HP");
        Debug.Log("  F4: Force Next Wave");
        Debug.Log("  F5: Print Game State");
        Debug.Log("  F9: Print Debug Info (from DebugStarter)");
    }

    void PrintDebugInfo()
    {
        Debug.Log("=== DEBUG STARTER INFO ===");

        if (NetworkManager.Instance != null)
        {
            Debug.Log($"NetworkManager:");
            Debug.Log($"  - Mode: {NetworkManager.Instance.CurrentMode}");
            Debug.Log($"  - State: {NetworkManager.Instance.CurrentState}");
            Debug.Log($"  - Players: {NetworkManager.Instance.ConnectedPlayers?.Count ?? 0}");
        }
        else
        {
            Debug.Log("NetworkManager: NULL");
        }

        if (gameController != null)
        {
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
    }
}