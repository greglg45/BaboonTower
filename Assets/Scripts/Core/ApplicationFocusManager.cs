using UnityEngine;
using BaboonTower.Network;

namespace BaboonTower.Core
{
    /// <summary>
    /// Gère le comportement de l'application quand elle perd/gagne le focus
    /// Empêche le serveur de se mettre en pause
    /// </summary>
    public class ApplicationFocusManager : MonoBehaviour
    {
        private static ApplicationFocusManager instance;

        [Header("Settings")]
        [SerializeField] private bool allowBackgroundRunning = true;
        [SerializeField] private int targetFrameRateInBackground = 30;
        [SerializeField] private int targetFrameRateInForeground = 60;

        private bool wasHostWhenFocusLost = false;

        private void Awake()
        {
            // Singleton pattern
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Configuration initiale
            ConfigureApplicationSettings();
        }

        private void Start()
        {
            // S'assurer que ces paramètres sont appliqués au démarrage
            ConfigureApplicationSettings();
        }

        private void ConfigureApplicationSettings()
        {
            // IMPORTANT : Permet à l'application de continuer à tourner en arrière-plan
            Application.runInBackground = allowBackgroundRunning;

            // Définir le framerate cible
            Application.targetFrameRate = targetFrameRateInForeground;

            // Désactiver la pause automatique (pour WebGL aussi si besoin)
#if !UNITY_EDITOR
            QualitySettings.vSyncCount = 0; // Désactiver VSync pour mieux contrôler le framerate
#endif

            Debug.Log($"[ApplicationFocusManager] Configuration appliquée - RunInBackground: {Application.runInBackground}");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Debug.Log($"[ApplicationFocusManager] Focus changed: {hasFocus}");

            // Vérifier si on est en mode Host
            bool isHost = NetworkManager.Instance != null &&
                         NetworkManager.Instance.CurrentMode == NetworkMode.Host;

            if (hasFocus)
            {
                // L'application reprend le focus
                Application.targetFrameRate = targetFrameRateInForeground;
                Time.timeScale = 1f; // S'assurer que le temps n'est pas en pause

                if (isHost || wasHostWhenFocusLost)
                {
                    Debug.Log("[ApplicationFocusManager] Host regained focus - ensuring server continues");
                    wasHostWhenFocusLost = false;
                }
            }
            else
            {
                // L'application perd le focus
                if (isHost)
                {
                    // Pour le serveur, on réduit le framerate mais on continue
                    Application.targetFrameRate = targetFrameRateInBackground;
                    wasHostWhenFocusLost = true;

                    Debug.Log($"[ApplicationFocusManager] Host lost focus - keeping server running at {targetFrameRateInBackground} FPS");

                    // S'assurer que l'application continue en arrière-plan
                    Application.runInBackground = true;
                    Time.timeScale = 1f; // Important : ne pas mettre en pause le temps
                }
                else
                {
                    // Pour les clients, comportement normal
                    Application.targetFrameRate = 5; // Très bas framerate pour économiser les ressources
                }
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            Debug.Log($"[ApplicationFocusManager] Pause status: {pauseStatus}");

            bool isHost = NetworkManager.Instance != null &&
                         NetworkManager.Instance.CurrentMode == NetworkMode.Host;

            if (isHost && pauseStatus)
            {
                // Empêcher la pause pour le serveur
                Debug.Log("[ApplicationFocusManager] Preventing pause for Host");
                Time.timeScale = 1f;

                // Forcer la continuation
                Application.runInBackground = true;
            }
        }

        /// <summary>
        /// Méthode publique pour forcer les paramètres du serveur
        /// </summary>
        public static void EnsureServerSettings()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;

            // S'assurer que les threads continuent
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30; // Framerate stable pour le serveur

            Debug.Log("[ApplicationFocusManager] Server settings enforced");
        }

        /// <summary>
        /// Méthode pour vérifier l'état
        /// </summary>
        [ContextMenu("Debug Application State")]
        public void DebugApplicationState()
        {
            Debug.Log("=== Application State ===");
            Debug.Log($"Run In Background: {Application.runInBackground}");
            Debug.Log($"Target Frame Rate: {Application.targetFrameRate}");
            Debug.Log($"Time Scale: {Time.timeScale}");
            Debug.Log($"VSync Count: {QualitySettings.vSyncCount}");
            Debug.Log($"Has Focus: {Application.isFocused}");
            Debug.Log($"Is Playing: {Application.isPlaying}");
            Debug.Log("========================");
        }
    }
}