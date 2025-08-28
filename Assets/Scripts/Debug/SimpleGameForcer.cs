using UnityEngine;
using BaboonTower.Game;
using System.Reflection;

/// <summary>
/// Script ultra-simple pour forcer le mode Host et démarrer le jeu
/// Pas d'erreurs de compilation, juste de la réflexion
/// </summary>
public class SimpleGameForcer : MonoBehaviour
{
    [Header("Settings")]
    public bool autoStart = true;
    public float startDelay = 1f;

    private GameController gameController;
    private bool started = false;

    void Start()
    {
        Debug.Log("[FORCER] Forcing Host mode for debug...");

        if (autoStart)
        {
            Invoke(nameof(ForceEverything), startDelay);
        }
    }

    void Update()
    {
        // F1 pour démarrer manuellement
        if (!started && Input.GetKeyDown(KeyCode.F1))
        {
            ForceEverything();
        }

        // F9 pour debug info
        if (Input.GetKeyDown(KeyCode.F9))
        {
            ShowDebugInfo();
        }
    }

    void ForceEverything()
    {
        if (started) return;

        Debug.Log("[FORCER] === FORCING HOST MODE ===");

        // 1. Trouver le GameController
        gameController = FindObjectOfType<GameController>();
        if (gameController == null)
        {
            Debug.LogError("[FORCER] GameController not found!");
            return;
        }

        // 2. Forcer isHost à true via réflexion
        System.Type gcType = typeof(GameController);
        FieldInfo hostField = gcType.GetField("isHost", BindingFlags.NonPublic | BindingFlags.Instance);
        if (hostField != null)
        {
            hostField.SetValue(gameController, true);
            Debug.Log("[FORCER] ✓ GameController.isHost = true");
        }

        // 3. Forcer le NetworkManager si possible
        var netManager = BaboonTower.Network.NetworkManager.Instance;
        if (netManager != null)
        {
            System.Type nmType = netManager.GetType();

            // Forcer CurrentMode
            var modeField = GetFieldOrBackingField(nmType, "CurrentMode");
            if (modeField != null)
            {
                modeField.SetValue(netManager, BaboonTower.Network.NetworkMode.Host);
                Debug.Log("[FORCER] ✓ NetworkManager.CurrentMode = Host");
            }

            // Forcer CurrentState
            var stateField = GetFieldOrBackingField(nmType, "CurrentState");
            if (stateField != null)
            {
                stateField.SetValue(netManager, BaboonTower.Network.ConnectionState.Connected);
                Debug.Log("[FORCER] ✓ NetworkManager.CurrentState = Connected");
            }

            // Ajouter un joueur si nécessaire
            if (netManager.ConnectedPlayers == null || netManager.ConnectedPlayers.Count == 0)
            {
                var playersField = GetFieldOrBackingField(nmType, "ConnectedPlayers");
                if (playersField != null)
                {
                    var list = new System.Collections.Generic.List<BaboonTower.Network.PlayerData>();
                    list.Add(new BaboonTower.Network.PlayerData("DebugPlayer", 0, true));
                    playersField.SetValue(netManager, list);
                    Debug.Log("[FORCER] ✓ Added debug player");
                }
            }
        }

        // 4. Démarrer le jeu
        Debug.Log("[FORCER] Starting game...");
        gameController.SendMessage("ForceStartGame", SendMessageOptions.DontRequireReceiver);

        started = true;

        Debug.Log("[FORCER] === DONE! Debug commands now available ===");
        Debug.Log("  F2: Add Gold");
        Debug.Log("  F3: Damage Castle");
        Debug.Log("  F4: Next Wave");
        Debug.Log("  F5: Game State");
        Debug.Log("  F9: Debug Info");
    }

    FieldInfo GetFieldOrBackingField(System.Type type, string propertyName)
    {
        // Essayer le field direct
        var field = type.GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) return field;

        // Essayer le backing field auto-généré
        field = type.GetField($"<{propertyName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        return field;
    }

    void ShowDebugInfo()
    {
        Debug.Log("=== DEBUG INFO ===");

        if (gameController != null)
        {
            // Lire isHost via réflexion
            System.Type gcType = typeof(GameController);
            FieldInfo hostField = gcType.GetField("isHost", BindingFlags.NonPublic | BindingFlags.Instance);
            if (hostField != null)
            {
                bool isHost = (bool)hostField.GetValue(gameController);
                Debug.Log($"GameController.isHost = {isHost}");
            }

            if (gameController.GameStateData != null)
            {
                Debug.Log($"State: {gameController.GameStateData.currentState}");
                Debug.Log($"Wave: {gameController.GameStateData.currentWave}");
            }
        }

        var netManager = BaboonTower.Network.NetworkManager.Instance;
        if (netManager != null)
        {
            Debug.Log($"NetworkManager.CurrentMode = {netManager.CurrentMode}");
            Debug.Log($"NetworkManager.CurrentState = {netManager.CurrentState}");
        }

        var enemies = FindObjectsOfType<Enemy>();
        Debug.Log($"Enemies in scene: {enemies.Length}");

        Debug.Log("==================");
    }

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 200, 100), "Game Forcer");
        GUI.Label(new Rect(20, 30, 180, 20), started ? "✓ HOST MODE" : "Press F1 to start");

        if (gameController != null && gameController.GameStateData != null)
        {
            GUI.Label(new Rect(20, 50, 180, 20), $"Wave: {gameController.GameStateData.currentWave}");
            GUI.Label(new Rect(20, 70, 180, 20), $"State: {gameController.GameStateData.currentState}");
        }
    }
}