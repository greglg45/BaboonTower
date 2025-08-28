using UnityEngine;
using BaboonTower.Network;

namespace BaboonTower.Core
{
    /// <summary>
    /// G�re le comportement de l'application quand elle perd/gagne le focus
    /// Emp�che le serveur de se mettre en pause
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
            // S'assurer que ces param�tres sont appliqu�s au d�marrage
            ConfigureApplicationSettings();
        }

        private void ConfigureApplicationSettings()
        {
            // IMPORTANT : Permet � l'application de continuer � tourner en arri�re-plan
            Application.runInBackground = allowBackgroundRunning;

            // D�finir le framerate cible
            Application.targetFrameRate = targetFrameRateInForeground;

            // D�sactiver la pause automatique (pour WebGL aussi si besoin)
#if !UNITY_EDITOR
            QualitySettings.vSyncCount = 0; // D�sactiver VSync pour mieux contr�ler le framerate
#endif

            Debug.Log($"[ApplicationFocusManager] Configuration appliqu�e - RunInBackground: {Application.runInBackground}");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Debug.Log($"[ApplicationFocusManager] Focus changed: {hasFocus}");

            // V�rifier si on est en mode Host
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
                    // Pour le serveur, on r�duit le framerate mais on continue
                    Application.targetFrameRate = targetFrameRateInBackground;
                    wasHostWhenFocusLost = true;

                    Debug.Log($"[ApplicationFocusManager] Host lost focus - keeping server running at {targetFrameRateInBackground} FPS");

                    // S'assurer que l'application continue en arri�re-plan
                    Application.runInBackground = true;
                    Time.timeScale = 1f; // Important : ne pas mettre en pause le temps
                }
                else
                {
                    // Pour les clients, comportement normal
                    Application.targetFrameRate = 5; // Tr�s bas framerate pour �conomiser les ressources
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
                // Emp�cher la pause pour le serveur
                Debug.Log("[ApplicationFocusManager] Preventing pause for Host");
                Time.timeScale = 1f;

                // Forcer la continuation
                Application.runInBackground = true;
            }
        }

        /// <summary>
        /// M�thode publique pour forcer les param�tres du serveur
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
        /// M�thode pour v�rifier l'�tat
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