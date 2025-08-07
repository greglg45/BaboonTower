using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using BaboonTower.Network;

namespace BaboonTower.UI
{
    public class LobbyController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI lobbyTitleText;
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        [SerializeField] private Transform playersListParent;
        [SerializeField] private GameObject playerItemPrefab;
        
        [Header("Buttons")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button backToMenuButton;
        
        [Header("Connection Panel")]
        [SerializeField] private GameObject connectionPanel;
        [SerializeField] private TMP_InputField serverIPInput;
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button cancelButton;
        
        [Header("Colors")]
        [SerializeField] private Color connectedColor = Color.green;
        [SerializeField] private Color connectingColor = Color.yellow;
        [SerializeField] private Color disconnectedColor = Color.red;
        [SerializeField] private Color readyColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color notReadyColor = Color.white;

        // State
        private bool isPlayerReady = false;
        private List<GameObject> playerItems = new List<GameObject>();

        private void Start()
        {
            InitializeLobby();
            SetupEventListeners();
            
            // Si pas connecté, afficher le panel de connexion
            if (NetworkManager.Instance.CurrentState == ConnectionState.Disconnected)
            {
                ShowConnectionPanel();
            }
            else
            {
                ShowLobbyPanel();
            }
        }

        private void OnDestroy()
        {
            RemoveEventListeners();
        }

        #region Initialization

        private void InitializeLobby()
        {
            // Configuration initiale des boutons
            readyButton.onClick.AddListener(ToggleReady);
            startGameButton.onClick.AddListener(StartGame);
            disconnectButton.onClick.AddListener(Disconnect);
            backToMenuButton.onClick.AddListener(BackToMenu);
            
            // Panel de connexion
            connectButton.onClick.AddListener(AttemptConnection);
            cancelButton.onClick.AddListener(BackToMenu);
            
            // Charger les paramètres sauvegardés
            LoadSettings();
            
            // État initial
            UpdateUI();
        }

        private void LoadSettings()
        {
            // Charger l'IP du serveur depuis les PlayerPrefs (configurée dans Options)
            string savedIP = PlayerPrefs.GetString("ServerIP", "127.0.0.1");
            serverIPInput.text = savedIP;
            
            // Charger le nom du joueur
            string savedName = PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
            playerNameInput.text = savedName;
            
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SetPlayerName(savedName);
            }
        }

        private void SetupEventListeners()
        {
            // Écouter les événements réseau
            NetworkManager.OnConnectionStateChanged += OnConnectionStateChanged;
            NetworkManager.OnPlayersUpdated += OnPlayersUpdated;
            NetworkManager.OnServerMessage += OnServerMessage;
            NetworkManager.OnGameStarted += OnGameStarted;
        }

        private void RemoveEventListeners()
        {
            // Nettoyer les événements
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnConnectionStateChanged -= OnConnectionStateChanged;
                NetworkManager.OnPlayersUpdated -= OnPlayersUpdated;
                NetworkManager.OnServerMessage -= OnServerMessage;
                NetworkManager.OnGameStarted -= OnGameStarted;
            }
        }

        #endregion

        #region UI Management

        private void ShowConnectionPanel()
        {
            connectionPanel.SetActive(true);
            // Cacher les autres éléments du lobby si nécessaire
        }

        private void ShowLobbyPanel()
        {
            connectionPanel.SetActive(false);
            // Afficher les éléments du lobby
        }

        private void UpdateUI()
        {
            if (NetworkManager.Instance == null) return;

            ConnectionState state = NetworkManager.Instance.CurrentState;
            
            // Mise à jour du statut de connexion
            UpdateConnectionStatus(state);
            
            // Mise à jour des boutons
            UpdateButtons(state);
            
            // Mise à jour du titre
            UpdateLobbyTitle();
        }

        private void UpdateConnectionStatus(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.Disconnected:
                    connectionStatusText.text = "Déconnecté";
                    connectionStatusText.color = disconnectedColor;
                    break;
                    
                case ConnectionState.Connecting:
                    connectionStatusText.text = "Connexion en cours...";
                    connectionStatusText.color = connectingColor;
                    break;
                    
                case ConnectionState.Connected:
                    connectionStatusText.text = "Connecté";
                    connectionStatusText.color = connectedColor;
                    break;
                    
                case ConnectionState.Failed:
                    connectionStatusText.text = "Connexion échouée";
                    connectionStatusText.color = disconnectedColor;
                    break;
            }
        }

        private void UpdateButtons(ConnectionState state)
        {
            bool isConnected = (state == ConnectionState.Connected);
            bool isConnecting = (state == ConnectionState.Connecting);
            
            // Boutons du lobby
            readyButton.gameObject.SetActive(isConnected);
            startGameButton.gameObject.SetActive(isConnected && NetworkManager.Instance.IsHost);
            disconnectButton.gameObject.SetActive(isConnected);
            
            // Panel de connexion
            connectButton.interactable = !isConnecting && !isConnected;
            serverIPInput.interactable = !isConnecting && !isConnected;
            playerNameInput.interactable = !isConnecting && !isConnected;
            
            // Mise à jour du bouton Ready
            UpdateReadyButton();
        }

        private void UpdateReadyButton()
        {
            if (NetworkManager.Instance.CurrentState != ConnectionState.Connected) return;

            var buttonImage = readyButton.GetComponent<Image>();
            var buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            
            if (isPlayerReady)
            {
                buttonText.text = "Prêt ✓";
                buttonImage.color = readyColor;
            }
            else
            {
                buttonText.text = "Pas prêt";
                buttonImage.color = notReadyColor;
            }
        }

        private void UpdateLobbyTitle()
        {
            int playerCount = NetworkManager.Instance.ConnectedPlayers.Count;
            lobbyTitleText.text = $"Lobby Baboon Tower ({playerCount} joueurs)";
        }

        #endregion

        #region Players List Management

        private void UpdatePlayersList(List<PlayerData> players)
        {
            // Nettoyer la liste existante
            ClearPlayersList();
            
            // Créer les nouveaux éléments
            foreach (PlayerData player in players)
            {
                CreatePlayerItem(player);
            }
            
            UpdateLobbyTitle();
        }

        private void ClearPlayersList()
        {
            foreach (GameObject item in playerItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            playerItems.Clear();
        }

        private void CreatePlayerItem(PlayerData player)
        {
            if (playerItemPrefab == null || playersListParent == null) return;

            GameObject playerItem = Instantiate(playerItemPrefab, playersListParent);
            playerItems.Add(playerItem);
            
            // Configurer l'affichage du joueur
            var nameText = playerItem.GetComponentInChildren<TextMeshProUGUI>();
            var backgroundImage = playerItem.GetComponent<Image>();
            
            if (nameText != null)
            {
                nameText.text = player.playerName + (player.isReady ? " ✓" : "");
            }
            
            if (backgroundImage != null)
            {
                backgroundImage.color = player.isReady ? readyColor : notReadyColor;
            }
        }

        #endregion

        #region Button Events

        private void ToggleReady()
        {
            isPlayerReady = !isPlayerReady;
            NetworkManager.Instance.SetPlayerReady(isPlayerReady);
            UpdateReadyButton();
        }

        private void StartGame()
        {
            if (NetworkManager.Instance.IsHost)
            {
                NetworkManager.Instance.StartGame();
            }
        }

        private void Disconnect()
        {
            NetworkManager.Instance.DisconnectFromServer();
            ShowConnectionPanel();
        }

        private void BackToMenu()
        {
            if (NetworkManager.Instance.CurrentState == ConnectionState.Connected)
            {
                NetworkManager.Instance.DisconnectFromServer();
            }
            
            SceneManager.LoadScene("MainMenu");
        }

        private void AttemptConnection()
        {
            string serverIP = serverIPInput.text.Trim();
            string playerName = playerNameInput.text.Trim();
            
            // Validation
            if (string.IsNullOrEmpty(serverIP))
            {
                ShowMessage("Veuillez saisir l'adresse IP du serveur");
                return;
            }
            
            if (string.IsNullOrEmpty(playerName))
            {
                ShowMessage("Veuillez saisir votre nom de joueur");
                return;
            }
            
            // Sauvegarder les paramètres
            PlayerPrefs.SetString("ServerIP", serverIP);
            PlayerPrefs.SetString("PlayerName", playerName);
            
            // Définir le nom du joueur
            NetworkManager.Instance.SetPlayerName(playerName);
            
            // Tenter la connexion
            NetworkManager.Instance.ConnectToServer(serverIP);
        }

        private void ShowMessage(string message)
        {
            // TODO: Afficher un message d'erreur à l'utilisateur
            Debug.Log($"Message: {message}");
            // Vous pouvez implémenter un système de toast/popup ici
        }

        #endregion

        #region Network Events

        private void OnConnectionStateChanged(ConnectionState newState)
        {
            UpdateUI();
            
            if (newState == ConnectionState.Connected)
            {
                ShowLobbyPanel();
            }
            else if (newState == ConnectionState.Failed || newState == ConnectionState.Disconnected)
            {
                ShowConnectionPanel();
                isPlayerReady = false;
            }
        }

        private void OnPlayersUpdated(List<PlayerData> players)
        {
            UpdatePlayersList(players);
        }

        private void OnServerMessage(string message)
        {
            ShowMessage($"Serveur: {message}");
        }

        private void OnGameStarted()
        {
            // Charger la scène de jeu
            Debug.Log("Lancement de la partie !");
            // TODO: SceneManager.LoadScene("GameScene");
        }

        #endregion
    }
}