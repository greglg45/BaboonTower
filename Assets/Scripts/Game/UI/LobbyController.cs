using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using BaboonTower.Network;
using BaboonTower.Game;
using System.IO;

namespace BaboonTower.UI
{
    public class LobbyController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI lobbyTitleText;
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        [SerializeField] private TextMeshProUGUI serverInfoText;
        [SerializeField] private Transform playersListParent;
        [SerializeField] private GameObject playerItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button stopServerButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button backToMenuButton;

        [Header("Wave Configuration Panel")]
        [SerializeField] private GameObject waveConfigPanel;
        [SerializeField] private TMP_InputField initialPrepTimeInput;
        [SerializeField] private TMP_InputField delayAfterFirstInput;
        [SerializeField] private TMP_InputField prepBetweenWavesInput;
        [SerializeField] private Button applyConfigButton;
        [SerializeField] private Button toggleConfigButton;
        [SerializeField] private TextMeshProUGUI configStatusText;
        [SerializeField] private GameObject waveConfigSection;

        [Header("Colors")]
        [SerializeField] private Color connectedColor = Color.green;
        [SerializeField] private Color listeningColor = Color.blue;
        [SerializeField] private Color disconnectedColor = Color.red;
        [SerializeField] private Color readyColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color notReadyColor = Color.white;
        [SerializeField] private Color hostColor = new Color(1f, 0.8f, 0.2f);

        [Header("Chat")]
        [SerializeField] private TMP_InputField chatInput;
        [SerializeField] private Button chatSendButton;
        [SerializeField] private TextMeshProUGUI chatLogText;
        [SerializeField] private ScrollRect chatScrollRect;

        // Configuration des vagues
        private WaveConfigurationMessage currentWaveConfig;
        private const string WAVE_CONFIG_KEY = "WaveConfiguration";

        // State local UI
        private bool isPlayerReady = false;
        private List<GameObject> playerItems = new List<GameObject>();

        private NetworkManager Net => NetworkManager.Instance;

        private void Start()
        {
            InitializeLobby();
            SetupEventListeners();
            InitializeWaveConfig();
            UpdateUI();
        }

        private void OnDestroy()
        {
            RemoveEventListeners();
        }

        #region Init & Events

        private void InitializeLobby()
        {
            // Boutons existants
            if (readyButton) readyButton.onClick.AddListener(ToggleReady);
            if (startGameButton) startGameButton.onClick.AddListener(StartGame);
            if (stopServerButton) stopServerButton.onClick.AddListener(StopServer);
            if (disconnectButton) disconnectButton.onClick.AddListener(Disconnect);
            if (backToMenuButton) backToMenuButton.onClick.AddListener(BackToMenu);

            // Bouton de configuration des vagues
            if (toggleConfigButton) toggleConfigButton.onClick.AddListener(ToggleWaveConfigPanel);
            if (applyConfigButton) applyConfigButton.onClick.AddListener(ApplyWaveConfiguration);

            // Chat
            if (chatSendButton) chatSendButton.onClick.AddListener(SendChat);
            if (chatInput) chatInput.onSubmit.AddListener(_ => { SendChat(); });

            UpdateUI();
        }

private void SetupEventListeners()
{
    if (Net == null) 
    {
        Debug.LogError("[LobbyController] NetworkManager is null in SetupEventListeners!");
        return;
    }

    Debug.Log("[LobbyController] Setting up event listeners");

    // Abonnements **d'instance** (pas statiques)
    Net.OnConnectionStateChanged += OnConnectionStateChanged;
    Net.OnPlayersUpdated += OnPlayersUpdated;
    Net.OnServerMessage += OnServerMessage;
    Net.OnGameStarted += OnGameStarted;  // IMPORTANT : Vérifier que ceci est bien présent
    Net.OnChatMessage += OnChatMessageReceived;
    Net.OnGameMessage += OnGameMessageReceived;
    
    Debug.Log("[LobbyController] Event listeners setup complete");
}

        private void RemoveEventListeners()
        {
            if (Net == null) return;

            Net.OnConnectionStateChanged -= OnConnectionStateChanged;
            Net.OnPlayersUpdated -= OnPlayersUpdated;
            Net.OnServerMessage -= OnServerMessage;
            Net.OnGameStarted -= OnGameStarted;
            Net.OnChatMessage -= OnChatMessageReceived;
            Net.OnGameMessage -= OnGameMessageReceived;
        }

        #endregion

        #region Wave Configuration

        /// <summary>
        /// InitializeWaveConfig - Initialise le panel de configuration des vagues
        /// </summary>
        private void InitializeWaveConfig()
        {
            // Créer une configuration par défaut si elle n'existe pas
            currentWaveConfig = new WaveConfigurationMessage
            {
                initialPreparationTime = 10f,
                delayAfterFirstFinish = 15f,
                preparationTimeBetweenWaves = 5f
            };

            // Seulement visible pour l'hôte
            bool isHost = Net?.CurrentMode == NetworkMode.Host;
            
            if (waveConfigPanel != null)
            {
                waveConfigPanel.SetActive(false); // Caché par défaut
            }
            
            if (toggleConfigButton != null)
            {
                toggleConfigButton.gameObject.SetActive(isHost);
            }
            
            if (waveConfigSection != null)
            {
                waveConfigSection.SetActive(isHost);
            }
            
            // Si on est l'hôte, charger la configuration existante
            if (isHost)
            {
                LoadWaveConfiguration();
            }
        }

        /// <summary>
        /// ToggleWaveConfigPanel - Affiche ou masque le panel de configuration
        /// </summary>
        private void ToggleWaveConfigPanel()
        {
            if (waveConfigPanel != null)
            {
                bool newState = !waveConfigPanel.activeSelf;
                waveConfigPanel.SetActive(newState);
                
                // Si on ouvre le panel, charger la config actuelle
                if (newState)
                {
                    LoadWaveConfiguration();
                }
            }
        }

        /// <summary>
        /// LoadWaveConfiguration - Charge la configuration des vagues
        /// </summary>
        private void LoadWaveConfiguration()
        {
            // D'abord essayer de charger depuis PlayerPrefs
            string savedConfig = PlayerPrefs.GetString(WAVE_CONFIG_KEY, "");
            
            if (!string.IsNullOrEmpty(savedConfig))
            {
                try
                {
                    currentWaveConfig = JsonUtility.FromJson<WaveConfigurationMessage>(savedConfig);
                    Debug.Log("[LobbyController] Configuration loaded from PlayerPrefs");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyController] Error loading saved configuration: {e.Message}");
                    LoadWaveConfigurationFromFile();
                }
            }
            else
            {
                // Sinon, essayer de charger depuis le fichier StreamingAssets
                LoadWaveConfigurationFromFile();
            }
            
            // Mettre à jour les champs UI
            UpdateWaveConfigUI();
        }

        /// <summary>
        /// LoadWaveConfigurationFromFile - Charge la configuration depuis le fichier JSON
        /// </summary>
        private void LoadWaveConfigurationFromFile()
        {
            string configPath = Path.Combine(Application.streamingAssetsPath, "WaveConfig.json");
            
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    // Parser seulement les champs qui nous intéressent
                    var fullConfig = JsonUtility.FromJson<FullWaveConfiguration>(json);
                    
                    currentWaveConfig = new WaveConfigurationMessage
                    {
                        initialPreparationTime = fullConfig.initialPreparationTime,
                        delayAfterFirstFinish = fullConfig.delayAfterFirstFinish,
                        preparationTimeBetweenWaves = fullConfig.preparationTimeBetweenWaves
                    };
                    
                    Debug.Log($"[LobbyController] Configuration loaded from file: {configPath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyController] Error loading configuration file: {e.Message}");
                    LoadDefaultWaveConfiguration();
                }
            }
            else
            {
                Debug.LogWarning($"[LobbyController] Configuration file not found: {configPath}");
                LoadDefaultWaveConfiguration();
            }
        }

        /// <summary>
        /// LoadDefaultWaveConfiguration - Charge une configuration par défaut
        /// </summary>
        private void LoadDefaultWaveConfiguration()
        {
            currentWaveConfig = new WaveConfigurationMessage
            {
                initialPreparationTime = 10f,
                delayAfterFirstFinish = 15f,
                preparationTimeBetweenWaves = 5f
            };
            
            Debug.Log("[LobbyController] Using default wave configuration");
        }

        /// <summary>
        /// UpdateWaveConfigUI - Met à jour les champs UI avec la configuration actuelle
        /// </summary>
        private void UpdateWaveConfigUI()
        {
            if (initialPrepTimeInput != null)
                initialPrepTimeInput.text = currentWaveConfig.initialPreparationTime.ToString("F1");
                
            if (delayAfterFirstInput != null)
                delayAfterFirstInput.text = currentWaveConfig.delayAfterFirstFinish.ToString("F1");
                
            if (prepBetweenWavesInput != null)
                prepBetweenWavesInput.text = currentWaveConfig.preparationTimeBetweenWaves.ToString("F1");
        }

        /// <summary>
        /// ApplyWaveConfiguration - Applique et diffuse la configuration des vagues
        /// </summary>
        private void ApplyWaveConfiguration()
        {
            if (Net?.CurrentMode != NetworkMode.Host) 
            {
                Debug.LogWarning("[LobbyController] Only host can modify wave configuration");
                UpdateConfigStatus("Seul l'hôte peut modifier la configuration", Color.red);
                return;
            }
            
            // Valider et parser les inputs
            bool isValid = true;
            float initialPrep = 10f;
            float delayAfterFirst = 15f;
            float prepBetweenWaves = 5f;
            
            // Parser avec validation
            if (initialPrepTimeInput != null)
            {
                if (!float.TryParse(initialPrepTimeInput.text, out initialPrep))
                {
                    isValid = false;
                    initialPrepTimeInput.text = "10";
                }
                else
                {
                    initialPrep = Mathf.Clamp(initialPrep, 5f, 60f);
                }
            }
            
            if (delayAfterFirstInput != null)
            {
                if (!float.TryParse(delayAfterFirstInput.text, out delayAfterFirst))
                {
                    isValid = false;
                    delayAfterFirstInput.text = "15";
                }
                else
                {
                    delayAfterFirst = Mathf.Clamp(delayAfterFirst, 5f, 60f);
                }
            }
            
            if (prepBetweenWavesInput != null)
            {
                if (!float.TryParse(prepBetweenWavesInput.text, out prepBetweenWaves))
                {
                    isValid = false;
                    prepBetweenWavesInput.text = "5";
                }
                else
                {
                    prepBetweenWaves = Mathf.Clamp(prepBetweenWaves, 0f, 30f);
                }
            }
            
            if (!isValid)
            {
                UpdateConfigStatus("Erreur: Valeurs invalides corrigées", Color.red);
                return;
            }
            
            // Créer la nouvelle configuration
            currentWaveConfig = new WaveConfigurationMessage
            {
                initialPreparationTime = initialPrep,
                delayAfterFirstFinish = delayAfterFirst,
                preparationTimeBetweenWaves = prepBetweenWaves
            };
            
            // Sauvegarder localement
            SaveWaveConfiguration(currentWaveConfig);
            
            // Diffuser à tous les clients
            if (Net != null)
            {
                string json = JsonUtility.ToJson(currentWaveConfig);
                Net.BroadcastMessage("WAVE_CONFIG_UPDATE", json);
                
                Debug.Log($"[LobbyController] Wave configuration updated and broadcast: " +
                         $"Initial={initialPrep}s, Delay={delayAfterFirst}s, Prep={prepBetweenWaves}s");
            }
            
            UpdateConfigStatus("Configuration appliquée et diffusée", Color.green);
            
            // Informer dans le chat
            AppendChatLine("Serveur", $"Configuration mise à jour: Préparation={initialPrep}s, Délai après premier={delayAfterFirst}s");
        }

        /// <summary>
        /// SaveWaveConfiguration - Sauvegarde la configuration des vagues
        /// </summary>
        private void SaveWaveConfiguration(WaveConfigurationMessage config)
        {
            string json = JsonUtility.ToJson(config);
            PlayerPrefs.SetString(WAVE_CONFIG_KEY, json);
            PlayerPrefs.Save();
            
            Debug.Log("[LobbyController] Wave configuration saved to PlayerPrefs");
        }

        /// <summary>
        /// UpdateConfigStatus - Met à jour le texte de statut de configuration
        /// </summary>
        private void UpdateConfigStatus(string message, Color color)
        {
            if (configStatusText != null)
            {
                configStatusText.text = message;
                configStatusText.color = color;
                
                // Faire disparaître le message après 3 secondes
                CancelInvoke(nameof(ClearConfigStatus));
                Invoke(nameof(ClearConfigStatus), 3f);
            }
        }

        /// <summary>
        /// ClearConfigStatus - Efface le texte de statut
        /// </summary>
        private void ClearConfigStatus()
        {
            if (configStatusText != null)
            {
                configStatusText.text = "";
            }
        }

        /// <summary>
        /// OnWaveConfigurationReceived - Appelé quand un client reçoit une mise à jour de configuration
        /// </summary>
        private void OnWaveConfigurationReceived(string jsonData)
        {
            try
            {
                currentWaveConfig = JsonUtility.FromJson<WaveConfigurationMessage>(jsonData);
                SaveWaveConfiguration(currentWaveConfig);
                
                Debug.Log($"[LobbyController] Wave configuration received from server: " +
                         $"Initial={currentWaveConfig.initialPreparationTime}s, " +
                         $"Delay={currentWaveConfig.delayAfterFirstFinish}s");
                
                // Informer le joueur
                AppendChatLine("Serveur", "La configuration des vagues a été mise à jour par l'hôte");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyController] Error receiving configuration: {e.Message}");
            }
        }

        #endregion

        #region UI refresh

        private void UpdateUI()
        {
            if (Net == null) return;

            var state = Net.CurrentState;
            var mode = Net.CurrentMode;

            UpdateConnectionStatus(state, mode);
            UpdateButtons(state, mode);
            UpdateLobbyInfo(mode);
            UpdateWaveConfigVisibility(mode);

            if (Net.ConnectedPlayers.Count > 0)
                UpdatePlayersList(Net.ConnectedPlayers);
        }
        
        private void UpdateWaveConfigVisibility(NetworkMode mode)
        {
            bool isHost = (mode == NetworkMode.Host);
            
            if (toggleConfigButton != null)
            {
                toggleConfigButton.gameObject.SetActive(isHost);
            }
            
            if (waveConfigSection != null)
            {
                waveConfigSection.SetActive(isHost);
            }
            
            // Si on n'est pas hôte et que le panel est ouvert, le fermer
            if (!isHost && waveConfigPanel != null && waveConfigPanel.activeSelf)
            {
                waveConfigPanel.SetActive(false);
            }
        }

        private void UpdateConnectionStatus(ConnectionState state, NetworkMode mode)
        {
            string modeText = mode == NetworkMode.Host ? " (Serveur)" : " (Client)";

            switch (state)
            {
                case ConnectionState.Disconnected:
                    if (connectionStatusText) { connectionStatusText.text = "Déconnecté"; connectionStatusText.color = disconnectedColor; }
                    break;

                case ConnectionState.Connecting:
                    if (connectionStatusText)
                    {
                        if (mode == NetworkMode.Host)
                        {
                            connectionStatusText.text = "Serveur en attente de joueurs...";
                            connectionStatusText.color = listeningColor;
                        }
                        else
                        {
                            connectionStatusText.text = "Connexion en cours...";
                            connectionStatusText.color = listeningColor;
                        }
                    }
                    break;

                case ConnectionState.Connected:
                    if (connectionStatusText) { connectionStatusText.text = "Connecté" + modeText; connectionStatusText.color = connectedColor; }
                    break;

                case ConnectionState.Failed:
                    if (connectionStatusText) { connectionStatusText.text = "Connexion échouée"; connectionStatusText.color = disconnectedColor; }
                    break;
            }
        }

        private void UpdateButtons(ConnectionState state, NetworkMode mode)
        {
            bool isHost = (mode == NetworkMode.Host);

            if (startGameButton) startGameButton.gameObject.SetActive(isHost);
            if (stopServerButton) stopServerButton.gameObject.SetActive(isHost);
            if (disconnectButton) disconnectButton.gameObject.SetActive(!isHost);

            if (readyButton) readyButton.gameObject.SetActive(!isHost);
            UpdateReadyButton();
        }

        private void UpdateReadyButton()
        {
            if (!readyButton) return;

            var buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            var buttonImage = readyButton.GetComponent<Image>();

            if (buttonText) buttonText.text = isPlayerReady ? "Pas prêt" : "Prêt ✓";
            if (buttonImage) buttonImage.color = isPlayerReady ? notReadyColor : readyColor;
        }

        private void UpdateLobbyInfo(NetworkMode mode)
        {
            if (lobbyTitleText)
                lobbyTitleText.text = mode == NetworkMode.Host ? "Lobby (Serveur)" : "Lobby (Client)";

            if (serverInfoText && Net != null)
            {
                if (mode == NetworkMode.Host)
                {
                    serverInfoText.text = $"IP: {GetLocalIPAddress()}:{Net.CurrentPort}";
                }
                else
                {
                    serverInfoText.text = "";
                }
            }
        }

        #endregion

        #region Players list

        private void UpdatePlayersList(List<PlayerData> players)
        {
            ClearPlayersList();
            foreach (var p in players)
            {
                CreatePlayerItem(p);
            }

            // Forcer la mise à jour du layout après avoir créé tous les items
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(playersListParent as RectTransform);
        }

        private void ClearPlayersList()
        {
            foreach (var go in playerItems)
            {
                if (go) Destroy(go);
            }
            playerItems.Clear();
        }

        private void CreatePlayerItem(PlayerData player)
        {
            if (!playerItemPrefab || !playersListParent) return;

            var item = Instantiate(playerItemPrefab, playersListParent);
            playerItems.Add(item);

            // Chercher spécifiquement le TextMeshPro avec le nom "PlayerNameText"
            var nameTextComponent = item.transform.Find("Background/PlayerNameText");
            TextMeshProUGUI nameText = null;

            if (nameTextComponent != null)
            {
                nameText = nameTextComponent.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                // Fallback: chercher dans tous les enfants
                nameText = item.GetComponentInChildren<TextMeshProUGUI>();
            }

            // Chercher le background spécifiquement
            var backgroundComponent = item.transform.Find("Background");
            Image bg = null;

            if (backgroundComponent != null)
            {
                bg = backgroundComponent.GetComponent<Image>();
            }
            else
            {
                // Fallback: prendre le premier Image trouvé
                bg = item.GetComponent<Image>();
            }

            // Mise à jour du texte et des couleurs
            if (nameText)
            {
                string displayName = player.playerName;
                if (player.isHost) displayName += " (Serveur)";
                if (player.isReady) displayName += " ✓";

                nameText.text = displayName;

                // S'assurer que le texte est visible
                nameText.color = Color.white;
            }

            // Gestion améliorée des couleurs de background
            if (bg)
            {
                if (player.isHost)
                {
                    bg.color = new Color(hostColor.r, hostColor.g, hostColor.b, 1f); // Alpha à 1 pour être visible
                }
                else if (player.isReady)
                {
                    bg.color = new Color(readyColor.r, readyColor.g, readyColor.b, 0.8f);
                }
                else
                {
                    bg.color = new Color(notReadyColor.r, notReadyColor.g, notReadyColor.b, 0.3f);
                }
            }

            // S'assurer que l'item est actif
            item.SetActive(true);

            Debug.Log($"Created player item for: {player.playerName}, Ready: {player.isReady}, Host: {player.isHost}");
        }

        #endregion

        #region Buttons handlers

        private void ToggleReady()
        {
            isPlayerReady = !isPlayerReady;
            Net?.SetLocalReady(isPlayerReady);
            UpdateReadyButton();
        }

        private void StartGame()
        {
            // S'assurer que la configuration est sauvegardée avant de démarrer
            if (currentWaveConfig != null)
            {
                SaveWaveConfiguration(currentWaveConfig);
                
                // Diffuser une dernière fois la configuration
                if (Net != null)
                {
                    string json = JsonUtility.ToJson(currentWaveConfig);
                    Net.BroadcastMessage("WAVE_CONFIG_UPDATE", json);
                }
            }
            
            // Côté host: on délègue la logique au NetworkManager
            Net?.HostTryStartGame();
        }

        private void StopServer()
        {
            if (Net != null && Net.CurrentMode == NetworkMode.Host)
            {
                Net.StopNetworking();
                BackToMenu();
            }
        }

        private void Disconnect()
        {
            if (Net != null && Net.CurrentMode == NetworkMode.Client)
            {
                Net.StopNetworking();
                BackToMenu();
            }
        }

        private void BackToMenu()
        {
            if (Net != null && Net.CurrentState != ConnectionState.Disconnected)
                Net.StopNetworking();

            SceneManager.LoadScene("MainMenu");
        }

        #endregion

        #region Chat

        private void SendChat()
        {
            if (!chatInput) return;
            string text = chatInput.text;
            chatInput.text = string.Empty;
            Net?.SendChatMessage(text);
            chatInput.ActivateInputField();
        }

        private void OnChatMessageReceived(string author, string message)
        {
            AppendChatLine(author, message);
        }

        private void AppendChatLine(string author, string message)
        {
            if (!chatLogText) return;

            string ts = System.DateTime.Now.ToString("HH:mm");
            chatLogText.text += $"[{ts}] <b>{author}</b>: {message}\n";

            if (chatScrollRect)
            {
                Canvas.ForceUpdateCanvases();
                chatScrollRect.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
            }
        }

        #endregion

        #region Net events

        private void OnConnectionStateChanged(ConnectionState newState)
        {
            UpdateUI();

            if (newState == ConnectionState.Failed || newState == ConnectionState.Disconnected)
            {
                isPlayerReady = false;

                if (newState == ConnectionState.Disconnected && Net.CurrentMode == NetworkMode.Client)
                {
                    Debug.Log("Déconnecté du serveur");
                    Invoke(nameof(BackToMenu), 1.5f);
                }
            }
        }

        private void OnPlayersUpdated(List<PlayerData> players) => UpdatePlayersList(players);
        private void OnServerMessage(string message) => AppendChatLine("Serveur", message);

private void OnGameStarted()
{
    Debug.Log("[LobbyController] OnGameStarted event received!");
    
    // Sauvegarder la configuration une dernière fois
    if (currentWaveConfig != null)
    {
        SaveWaveConfiguration(currentWaveConfig);
    }
    
    Debug.Log("[LobbyController] Loading GameScene...");
    
    // Charger la scène de jeu avec un petit délai pour s'assurer que tout est synchronisé
    StartCoroutine(LoadGameSceneWithDelay());
}

private System.Collections.IEnumerator LoadGameSceneWithDelay()
{
    yield return new WaitForSeconds(0.5f);
    Debug.Log("[LobbyController] Actually loading GameScene now");
    SceneManager.LoadScene("GameScene");
}
        
        private void OnGameMessageReceived(string messageType, string data)
        {
            if (messageType == "WAVE_CONFIG_UPDATE")
            {
                OnWaveConfigurationReceived(data);
            }
        }

        #endregion

        #region Helpers

        // Remplace l'ancien appel à NetworkManager.LocalIPAddress
        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        #endregion
        
        // Classe helper pour parser le fichier complet (privée, interne à LobbyController)
        [System.Serializable]
        private class FullWaveConfiguration
        {
            public float initialPreparationTime;
            public float delayAfterFirstFinish;
            public float preparationTimeBetweenWaves;
            // Les autres champs du fichier JSON ne sont pas nécessaires ici
        }
    }
}