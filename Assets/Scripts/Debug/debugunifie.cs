using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using BaboonTower.Game;
using BaboonTower.Network;

public class GameDebugBootstrapper : MonoBehaviour
{
    [Header("Startup")]
    public bool autoStart = true;
    public float startDelay = 1f;
    public KeyCode manualStartKey = KeyCode.F1;

    [Tooltip("Vérifie le nom de scène avant bootstrap (ex: GameScene)")]
    public bool requireSceneMatch = true;
    public string requiredSceneName = "GameScene";

    [Header("Network")]
    [Tooltip("Crée un NetworkManager si absent")]
    public bool ensureNetworkManager = true;
    public string debugPlayerName = "DebugPlayer";

    [Header("Overlay & Logs")]
    public bool showOverlay = true;
    public bool verbose = true;

    private GameController gameController;
    private bool started = false;

    void Start()
    {
        if (requireSceneMatch && SceneManager.GetActiveScene().name != requiredSceneName)
        {
            if (verbose) Debug.Log($"[BOOT] Scene '{SceneManager.GetActiveScene().name}' != '{requiredSceneName}', bootstrap ignoré.");
            return;
        }

        if (autoStart) StartCoroutine(DelayedBootstrap());
    }

    IEnumerator DelayedBootstrap()
    {
        if (verbose) Debug.Log($"[BOOT] Attente {startDelay:0.###}s avant bootstrap…");
        yield return new WaitForSeconds(startDelay);
        Bootstrap();
    }

    void Update()
    {
        if (!started && Input.GetKeyDown(manualStartKey)) Bootstrap();
        if (Input.GetKeyDown(KeyCode.F9)) PrintDebugInfo();
    }

    void Bootstrap()
    {
        if (started) { if (verbose) Debug.Log("[BOOT] Déjà démarré."); return; }

        // 1) GameController
        gameController = FindObjectOfType<GameController>();
        if (!gameController)
        {
            Debug.LogError("[BOOT] GameController introuvable.");
            return;
        }

        // 2) NetworkManager (+ création optionnelle)
        var nm = NetworkManager.Instance;
        if (!nm && ensureNetworkManager)
        {
            if (verbose) Debug.Log("[BOOT] Création d’un NetworkManager_DEBUG…");
            var go = new GameObject("NetworkManager_DEBUG");
            DontDestroyOnLoad(go);
            nm = go.AddComponent<NetworkManager>();
        }

        // 3) Forcer Host côté GC
        ForceGameControllerHost(gameController);

        // 4) Config réseau (mode, state, players, nom)
        if (nm) ConfigureNetworkManager(nm, debugPlayerName);

        // 5) Démarrer le jeu
        if (verbose) Debug.Log("[BOOT] Démarrage du jeu…");
        gameController.SendMessage("ForceStartGame", SendMessageOptions.DontRequireReceiver);

        started = true;
        if (verbose)
        {
            Debug.Log("[BOOT] OK. Raccourcis: F1 start (si pas lancé), F9 debug.");
            Debug.Log("      Autres commandes F2/F3/F4/F5 gérées par GameController si existantes.");
        }
    }

    static void ForceGameControllerHost(GameController gc)
    {
        var t = typeof(GameController);
        var hostField = GetFieldOrBackingField(t, "isHost");
        if (hostField != null)
        {
            hostField.SetValue(gc, true);
            Debug.Log("[BOOT] ✓ GameController.isHost = true");
        }
        else
        {
            Debug.LogWarning("[BOOT] isHost introuvable dans GameController.");
        }
    }

    static void ConfigureNetworkManager(NetworkManager nm, string playerName)
    {
        var t = nm.GetType();

        // Mode Host
        var modeField = GetFieldOrBackingField(t, "CurrentMode");
        if (modeField != null)
        {
            modeField.SetValue(nm, NetworkMode.Host);
            Debug.Log("[BOOT] ✓ NetworkManager.CurrentMode = Host");
        }

        // State Connected
        var stateField = GetFieldOrBackingField(t, "CurrentState");
        if (stateField != null)
        {
            stateField.SetValue(nm, ConnectionState.Connected);
            Debug.Log("[BOOT] ✓ NetworkManager.CurrentState = Connected");
        }

        // Players (au moins 1 host)
        if (nm.ConnectedPlayers == null || nm.ConnectedPlayers.Count == 0)
        {
            var playersField = GetFieldOrBackingField(t, "ConnectedPlayers");
            if (playersField != null)
            {
                var list = new List<PlayerData> { new PlayerData(playerName, 0, true) };
                playersField.SetValue(nm, list);
                Debug.Log("[BOOT] ✓ Added debug player");
            }
            else
            {
                // Si propriété accessible
                if (nm.ConnectedPlayers != null)
                {
                    nm.ConnectedPlayers.Clear();
                    nm.ConnectedPlayers.Add(new PlayerData(playerName, 0, true));
                    Debug.Log("[BOOT] ✓ Added debug player (fallback)");
                }
                else
                {
                    Debug.LogWarning("[BOOT] Impossible d’initialiser ConnectedPlayers.");
                }
            }
        }

        // Nom joueur (API publique)
        try { nm.SetPlayerName(playerName); } catch { /* no-op */ }
    }

    static FieldInfo GetFieldOrBackingField(System.Type type, string propertyOrFieldName)
    {
        var field = type.GetField(propertyOrFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) return field;
        return type.GetField($"<{propertyOrFieldName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    void PrintDebugInfo()
    {
        Debug.Log("=== DEBUG INFO ===");
        if (gameController)
        {
            var t = typeof(GameController);
            var hostField = GetFieldOrBackingField(t, "isHost");
            if (hostField != null)
            {
                bool isHost = (bool)hostField.GetValue(gameController);
                Debug.Log($"GC.isHost = {isHost}");
            }

            if (gameController.GameStateData != null)
            {
                Debug.Log($"State: {gameController.GameStateData.currentState}");
                Debug.Log($"Wave : {gameController.GameStateData.currentWave}");
                Debug.Log($"Alive: {gameController.GameStateData.alivePlayers}");
            }
        }
        else Debug.Log("GameController: NULL");

        var nm = NetworkManager.Instance;
        if (nm)
        {
            Debug.Log($"NM.Mode  = {nm.CurrentMode}");
            Debug.Log($"NM.State = {nm.CurrentState}");
            Debug.Log($"NM.Players = {nm.ConnectedPlayers?.Count ?? 0}");
        }
        else Debug.Log("NetworkManager: NULL");

        // Managers / Ennemis
        Debug.Log($"WaveManager: {(FindObjectOfType<WaveManager>() ? "Found" : "NOT FOUND")}");
        var enemies = FindObjectsOfType<Enemy>();
        Debug.Log($"Enemies in scene: {enemies.Length}");
        foreach (var e in enemies) Debug.Log($" - {e.name} @ {e.transform.position}");

        Debug.Log("==================");
    }

    void OnGUI()
    {
        if (!showOverlay) return;

        GUI.Box(new Rect(10, 10, 270, 140), "Game Debug Bootstrapper");

        var nm = NetworkManager.Instance;
        var mode = nm ? nm.CurrentMode.ToString() : "No NM";
        GUI.Label(new Rect(20, 35, 240, 20), $"Mode: {mode}");

        string state = "Not initialized";
        if (gameController && gameController.GameStateData != null)
            state = gameController.GameStateData.currentState.ToString();
        GUI.Label(new Rect(20, 55, 240, 20), $"State: {state}");

        if (!started)
        {
            if (GUI.Button(new Rect(20, 80, 230, 25), $"Start ( {manualStartKey} )"))
                Bootstrap();
        }
        else
        {
            GUI.Label(new Rect(20, 80, 240, 20), "Started - F9: Debug dump");
        }

        if (gameController)
        {
            var hostField = GetFieldOrBackingField(typeof(GameController), "isHost");
            if (hostField != null)
                GUI.Label(new Rect(20, 100, 240, 20), $"isHost: {(bool)hostField.GetValue(gameController)}");
        }
    }
}
