using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BaboonTower.UI
{
    public class OptionsController : MonoBehaviour
    {
        [Header("Network Settings")]
        [SerializeField] private TMP_InputField serverIPInput;
        [SerializeField] private TMP_InputField serverPortInput;
        [SerializeField] private Button testConnectionButton;

        [Header("Player Settings")]
        [SerializeField] private TMP_InputField playerNameInput;

        [Header("Audio Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle muteToggle;

        [Header("Graphics Settings")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vsyncToggle;

        [Header("Buttons")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button closeButton;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        [SerializeField] private GameObject optionsPanel;

        // Default values
        private const string DEFAULT_SERVER_IP = "127.0.0.1";
        private const int DEFAULT_SERVER_PORT = 7777;
        private const string DEFAULT_PLAYER_NAME = "Player";
        private const float DEFAULT_MASTER_VOLUME = 0.8f;
        private const float DEFAULT_MUSIC_VOLUME = 0.7f;
        private const float DEFAULT_SFX_VOLUME = 0.8f;

        private void Start()
        {
            InitializeOptions();
            LoadSettings();
            SetupEventListeners();
        }

        #region Initialization

        private void InitializeOptions()
        {
            // Initialiser les résolutions disponibles
            InitializeResolutionDropdown();

            // Initialiser les niveaux de qualité
            InitializeQualityDropdown();

            // Configuration initiale
            UpdateConnectionStatus("Non testé", Color.gray);
        }

        private void InitializeResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            resolutionDropdown.ClearOptions();

            // Ajouter les résolutions communes
            var resolutions = new[]
            {
                "1920x1080",
                "1680x1050",
                "1600x900",
                "1440x900",
                "1366x768",
                "1280x720"
            };

            resolutionDropdown.AddOptions(new System.Collections.Generic.List<string>(resolutions));

            // Sélectionner la résolution actuelle
            string currentRes = Screen.width + "x" + Screen.height;
            for (int i = 0; i < resolutions.Length; i++)
            {
                if (resolutions[i] == currentRes)
                {
                    resolutionDropdown.value = i;
                    break;
                }
            }
        }

        private void InitializeQualityDropdown()
        {
            if (qualityDropdown == null) return;

            qualityDropdown.ClearOptions();

            var qualityLevels = new[]
            {
                "Bas",
                "Moyen",
                "Élevé",
                "Ultra"
            };

            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(qualityLevels));
            qualityDropdown.value = QualitySettings.GetQualityLevel();
        }

        private void SetupEventListeners()
        {
            // Network
            testConnectionButton?.onClick.AddListener(TestConnection);

            // Audio sliders
            masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
            musicVolumeSlider?.onValueChanged.AddListener(OnMusicVolumeChanged);
            sfxVolumeSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);
            muteToggle?.onValueChanged.AddListener(OnMuteToggled);

            // Graphics
            resolutionDropdown?.onValueChanged.AddListener(OnResolutionChanged);
            qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
            fullscreenToggle?.onValueChanged.AddListener(OnFullscreenToggled);
            vsyncToggle?.onValueChanged.AddListener(OnVSyncToggled);

            // Buttons
            saveButton?.onClick.AddListener(SaveSettings);
            resetButton?.onClick.AddListener(ResetToDefaults);
            closeButton?.onClick.AddListener(CloseOptions);
        }

        #endregion

        #region Settings Management

        private void LoadSettings()
        {
            // Network Settings
            if (serverIPInput != null)
                serverIPInput.text = PlayerPrefs.GetString("ServerIP", DEFAULT_SERVER_IP);

            if (serverPortInput != null)
                serverPortInput.text = PlayerPrefs.GetInt("ServerPort", DEFAULT_SERVER_PORT).ToString();

            // Player Settings
            if (playerNameInput != null)
                playerNameInput.text = PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);

            // Audio Settings
            if (masterVolumeSlider != null)
                masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", DEFAULT_MASTER_VOLUME);

            if (musicVolumeSlider != null)
                musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", DEFAULT_MUSIC_VOLUME);

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", DEFAULT_SFX_VOLUME);

            if (muteToggle != null)
                muteToggle.isOn = PlayerPrefs.GetInt("MuteAudio", 0) == 1;

            // Graphics Settings
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = Screen.fullScreen;

            if (vsyncToggle != null)
                vsyncToggle.isOn = QualitySettings.vSyncCount > 0;

            // Appliquer les paramètres audio
            ApplyAudioSettings();
        }

        public void SaveSettings()
        {
            // Network Settings
            if (serverIPInput != null)
                PlayerPrefs.SetString("ServerIP", serverIPInput.text);

            if (serverPortInput != null)
            {
                if (int.TryParse(serverPortInput.text, out int port))
                    PlayerPrefs.SetInt("ServerPort", port);
            }

            // Player Settings
            if (playerNameInput != null)
                PlayerPrefs.SetString("PlayerName", playerNameInput.text);

            // Audio Settings
            if (masterVolumeSlider != null)
                PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);

            if (musicVolumeSlider != null)
                PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);

            if (sfxVolumeSlider != null)
                PlayerPrefs.SetFloat("SFXVolume", sfxVolumeSlider.value);

            if (muteToggle != null)
                PlayerPrefs.SetInt("MuteAudio", muteToggle.isOn ? 1 : 0);

            // Sauvegarder PlayerPrefs
            PlayerPrefs.Save();

            // Appliquer les paramètres
            ApplyAudioSettings();

            Debug.Log("Paramètres sauvegardés");
            UpdateConnectionStatus("Paramètres sauvegardés", Color.green);
        }

        private void ResetToDefaults()
        {
            // Network
            if (serverIPInput != null)
                serverIPInput.text = DEFAULT_SERVER_IP;

            if (serverPortInput != null)
                serverPortInput.text = DEFAULT_SERVER_PORT.ToString();

            // Player
            if (playerNameInput != null)
                playerNameInput.text = DEFAULT_PLAYER_NAME;

            // Audio
            if (masterVolumeSlider != null)
                masterVolumeSlider.value = DEFAULT_MASTER_VOLUME;

            if (musicVolumeSlider != null)
                musicVolumeSlider.value = DEFAULT_MUSIC_VOLUME;

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = DEFAULT_SFX_VOLUME;

            if (muteToggle != null)
                muteToggle.isOn = false;

            // Graphics
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = true;

            if (vsyncToggle != null)
                vsyncToggle.isOn = true;

            if (resolutionDropdown != null)
                resolutionDropdown.value = 0; // 1920x1080

            if (qualityDropdown != null)
                qualityDropdown.value = 2; // Élevé

            UpdateConnectionStatus("Valeurs par défaut restaurées", Color.blue);
        }

        #endregion

        #region Audio Settings

        private void ApplyAudioSettings()
        {
            float masterVolume = masterVolumeSlider != null ? masterVolumeSlider.value : DEFAULT_MASTER_VOLUME;
            float musicVolume = musicVolumeSlider != null ? musicVolumeSlider.value : DEFAULT_MUSIC_VOLUME;
            float sfxVolume = sfxVolumeSlider != null ? sfxVolumeSlider.value : DEFAULT_SFX_VOLUME;
            bool isMuted = muteToggle != null ? muteToggle.isOn : false;

            // Appliquer le volume général
            AudioListener.volume = isMuted ? 0f : masterVolume;

            // TODO: Appliquer aux AudioMixers spécifiques si utilisés
            // musicAudioMixer.SetFloat("MusicVolume", Mathf.Log10(musicVolume) * 20);
            // sfxAudioMixer.SetFloat("SFXVolume", Mathf.Log10(sfxVolume) * 20);
        }

        private void OnMasterVolumeChanged(float value)
        {
            ApplyAudioSettings();
        }

        private void OnMusicVolumeChanged(float value)
        {
            ApplyAudioSettings();
        }

        private void OnSFXVolumeChanged(float value)
        {
            ApplyAudioSettings();
        }

        private void OnMuteToggled(bool isMuted)
        {
            ApplyAudioSettings();
        }

        #endregion

        #region Graphics Settings

        private void OnResolutionChanged(int resolutionIndex)
        {
            if (resolutionDropdown == null) return;

            string selectedResolution = resolutionDropdown.options[resolutionIndex].text;
            string[] parts = selectedResolution.Split('x');

            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
                {
                    Screen.SetResolution(width, height, Screen.fullScreen);
                }
            }
        }

        private void OnQualityChanged(int qualityIndex)
        {
            QualitySettings.SetQualityLevel(qualityIndex);
        }

        private void OnFullscreenToggled(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
        }

        private void OnVSyncToggled(bool enableVSync)
        {
            QualitySettings.vSyncCount = enableVSync ? 1 : 0;
        }

        #endregion

        #region Network Testing

        private void TestConnection()
        {
            if (serverIPInput == null)
            {
                UpdateConnectionStatus("Erreur: champ IP manquant", Color.red);
                return;
            }

            string serverIP = serverIPInput.text.Trim();
            int serverPort = DEFAULT_SERVER_PORT;

            if (serverPortInput != null)
            {
                int.TryParse(serverPortInput.text, out serverPort);
            }

            if (string.IsNullOrEmpty(serverIP))
            {
                UpdateConnectionStatus("Erreur: IP vide", Color.red);
                return;
            }

            UpdateConnectionStatus("Test en cours...", Color.yellow);

            // Lancer le test de connexion
            StartCoroutine(TestConnectionCoroutine(serverIP, serverPort));
        }

        private System.Collections.IEnumerator TestConnectionCoroutine(string ip, int port)
        {
            bool? result = null;

            // Lancer la vérification de la connexion dans un thread séparé
            Thread testThread = new Thread(() =>
            {
                try
                {
                    using (var client = new System.Net.Sockets.TcpClient())
                    {
                        var connectTask = client.ConnectAsync(ip, port);
                        bool connected = connectTask.Wait(5000); // 5s timeout

                        result = connected && client.Connected;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Erreur test connexion: {e.Message}");
                    result = false;
                }
            });

            testThread.Start();

            // Attente que le thread se termine
            while (result == null)
                yield return null;

            // Mettre à jour le statut
            if (result == true)
            {
                UpdateConnectionStatus("✓ Connexion réussie", Color.green);
            }
            else
            {
                UpdateConnectionStatus("✗ Connexion échouée", Color.red);
            }
        }


        private void UpdateConnectionStatus(string message, Color color)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = message;
                connectionStatusText.color = color;
            }
        }

        #endregion

        #region Panel Management

        public void ShowOptions()
        {
            if (optionsPanel != null)
            {
                optionsPanel.SetActive(true);
            }

            LoadSettings();
        }

        public void CloseOptions()
        {
            if (optionsPanel != null)
            {
                optionsPanel.SetActive(false);
            }
        }

        #endregion

        #region Public Methods (for MainMenu integration)

        /// <summary>
        /// Récupérer l'IP du serveur configurée
        /// </summary>
        public string GetServerIP()
        {
            return PlayerPrefs.GetString("ServerIP", DEFAULT_SERVER_IP);
        }

        /// <summary>
        /// Récupérer le port du serveur configuré
        /// </summary>
        public int GetServerPort()
        {
            return PlayerPrefs.GetInt("ServerPort", DEFAULT_SERVER_PORT);
        }

        /// <summary>
        /// Récupérer le nom du joueur configuré
        /// </summary>
        public string GetPlayerName()
        {
            return PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);
        }

        #endregion
    }
}       