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
        [SerializeField] private TextMeshProUGUI serverInfoText;
        [SerializeField] private Transform playersListParent;
        [SerializeField] private GameObject playerItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button stopServerButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button backToMenuButton;

        [Header("Colors")]
        [SerializeField] private Color connectedColor = Color.green;
        [SerializeField] private Color listeningColor = Color.blue;
        [SerializeField] private Color disconnectedColor = Color.red;
        [SerializeField] private Color readyColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color notReadyColor = Color.white;
        [SerializeField] private Color hostColor = new Color(1f, 0.8f, 0.2f);

        // State
        private bool isPlayerReady = false;
        private List<GameObject> playerItems = new List<GameObject>();

        private void Start()
        {
            InitializeLobby();
            SetupEventListeners();
            UpdateUI();
        }

        private void OnDestroy()
        {
            RemoveEventListeners();
        }

        #region Initialization

        private void InitializeLobby()
        {
            // Configuration des boutons
            readyButton.onClick.AddListener(ToggleReady);
            startGameButton.onClick.AddListener(StartGame);
            stopServerButton.onClick.AddListener(StopServer);
            disconnectButton.onClick.AddListener(Disconnect);
            backToMenuButton.onClick.AddListener(BackToMenu);

            // État initial
            UpdateUI();
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

        private void UpdateUI()
        {
            if (NetworkManager.Instance == null) return;

            ConnectionState state = NetworkManager.Instance.CurrentState;
            NetworkMode mode = NetworkManager.Instance.CurrentMode;

            // Mise à jour du statut de connexion
            UpdateConnectionStatus(state, mode);

            // Mise à jour des boutons
            UpdateButtons(state, mode);

            // Mise à jour du titre et info serveur
            UpdateLobbyInfo(mode);

            // Mise à jour de la liste des joueurs
            if (NetworkManager.Instance.ConnectedPlayers.Count > 0)
            {
                UpdatePlayersList(NetworkManager.Instance.ConnectedPlayers);
            }
        }

        private void UpdateConnectionStatus(ConnectionState state, NetworkMode mode)
        {
            string modeText = mode == NetworkMode.Host ? " (Serveur)" : " (Client)";

            switch (state)
            {
                case ConnectionState.Disconnected:
                    connectionStatusText.text = "Déconnecté";
                    connectionStatusText.color = disconnectedColor;
                    break;

                case ConnectionState.Starting:
                    connectionStatusText.text = "Démarrage...";
                    connectionStatusText.color = listeningColor;
                    break;

                case ConnectionState.Listening:
                    connectionStatusText.text = "En attente de joueurs" + modeText;
                    connectionStatusText.color = listeningColor;
                    break;

                case ConnectionState.Connecting:
                    connectionStatusText.text = "Connexion en cours...";
                    connectionStatusText.color = listeningColor;
                    break;

                case ConnectionState.Connected:
                    connectionStatusText.text = "Connecté" + modeText;
                    connectionStatusText.color = connectedColor;
                    break;

                case ConnectionState.Failed:
                    connectionStatusText.text = "Connexion échouée";
                    connectionStatusText.color = disconnectedColor;
                    break;
            }
        }

        private void UpdateButtons(ConnectionState state, NetworkMode mode)
        {
            bool isConnected = (state == ConnectionState.Connected || state == ConnectionState.Listening);
            bool isHost = (mode == NetworkMode.Host);

            // Boutons disponibles selon le mode et l'état
            readyButton.gameObject.SetActive(isConnected);
            startGameButton.gameObject.SetActive(isConnected && isHost);
            stopServerButton.gameObject.SetActive(isHost && (state != ConnectionState.Disconnected));
            disconnectButton.gameObject.SetActive(!isHost && isConnected);

            // Mise à jour du bouton Ready
            UpdateReadyButton();
        }

        private void UpdateLobbyInfo(NetworkMode mode)
        {
            int playerCount = NetworkManager.Instance.ConnectedPlayers.Count;

            if (mode == NetworkMode.Host)
            {
                lobbyTitleText.text = $"Lobby Baboon Tower - Serveur ({playerCount} joueurs)";
                serverInfoText.text = $"IP du serveur: {NetworkManager.Instance.LocalIPAddress}:7777\nDonnez cette adresse aux autres joueurs";
            }
            else
            {
                lobbyTitleText.text = $"Lobby Baboon Tower - Client ({playerCount} joueurs)";
                serverInfoText.text = "Connecté au serveur";
            }
        }

        private void UpdateReadyButton()
        {
            if (NetworkManager.Instance.CurrentState != ConnectionState.Connected &&
                NetworkManager.Instance.CurrentState != ConnectionState.Listening) return;

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

            // Mettre à jour le titre
            UpdateLobbyInfo(NetworkManager.Instance.CurrentMode);
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
                string playerName = player.playerName;
                if (player.isHost) playerName += " (Serveur)";
                if (player.isReady) playerName += " ✓";

                nameText.text = playerName;
            }

            if (backgroundImage != null)
            {
                if (player.isHost)
                    backgroundImage.color = hostColor;
                else if (player.isReady)
                    backgroundImage.color = readyColor;
                else
                    backgroundImage.color = notReadyColor;
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

        private void StopServer()
        {
            if (NetworkManager.Instance.IsHost)
            {
                NetworkManager.Instance.StopNetworking();
                BackToMenu();
            }
        }

        private void Disconnect()
        {
            if (!NetworkManager.Instance.IsHost)
            {
                NetworkManager.Instance.StopNetworking();
                BackToMenu();
            }
        }

        private void BackToMenu()
        {
            // Arrêter le réseau s'il est encore actif
            if (NetworkManager.Instance.CurrentState != ConnectionState.Disconnected)
            {
                NetworkManager.Instance.StopNetworking();
            }

            SceneManager.LoadScene("MainMenu");
        }

        private void ShowMessage(string message)
        {
            Debug.Log($"Message: {message}");
            // TODO: Implémenter un système de notification UI
        }

        #endregion

        #region Network Events

        private void OnConnectionStateChanged(ConnectionState newState)
        {
            UpdateUI();

            if (newState == ConnectionState.Failed || newState == ConnectionState.Disconnected)
            {
                isPlayerReady = false;

                // Si on est déconnecté de manière inattendue, revenir au menu
                if (newState == ConnectionState.Disconnected && NetworkManager.Instance.CurrentMode == NetworkMode.Client)
                {
                    ShowMessage("Déconnecté du serveur");
                    Invoke(nameof(BackToMenu), 2f); // Délai pour voir le message
                }
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
            Debug.Log("Lancement de la partie !");
            // TODO: SceneManager.LoadScene("GameScene");
            ShowMessage("Lancement de la partie ! (GameScene pas encore implémentée)");
        }

        #endregion
    }
}