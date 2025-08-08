using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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

        [Header("Chat")]
        [SerializeField] private TMP_InputField chatInput;
        [SerializeField] private Button chatSendButton;
        [SerializeField] private TextMeshProUGUI chatLogText;
        [SerializeField] private ScrollRect chatScrollRect;

        // State local UI
        private bool isPlayerReady = false;
        private List<GameObject> playerItems = new List<GameObject>();

        private NetworkManager Net => NetworkManager.Instance;

        private void Start()
        {
            InitializeLobby();
            SetupEventListeners();
            Net?.HostTryStartGame();
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

            // Chat
            if (chatSendButton) chatSendButton.onClick.AddListener(SendChat);
            if (chatInput) chatInput.onSubmit.AddListener(_ => { SendChat(); });

            UpdateUI();
        }

        private void SetupEventListeners()
        {
            if (Net == null) return;

            // Abonnements **d’instance** (pas statiques)
            Net.OnConnectionStateChanged += OnConnectionStateChanged;
            Net.OnPlayersUpdated += OnPlayersUpdated;
            Net.OnServerMessage += OnServerMessage;
            Net.OnGameStarted += OnGameStarted;
            Net.OnChatMessage += OnChatMessageReceived;
        }

        private void RemoveEventListeners()
        {
            if (Net == null) return;

            Net.OnConnectionStateChanged -= OnConnectionStateChanged;
            Net.OnPlayersUpdated -= OnPlayersUpdated;
            Net.OnServerMessage -= OnServerMessage;
            Net.OnGameStarted -= OnGameStarted;
            Net.OnChatMessage -= OnChatMessageReceived;
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

            if (Net.ConnectedPlayers.Count > 0)
                UpdatePlayersList(Net.ConnectedPlayers);
        }

        private void UpdateConnectionStatus(ConnectionState state, NetworkMode mode)
        {
            string modeText = mode == NetworkMode.Host ? " (Serveur)" : " (Client)";

            switch (state)
            {
                case ConnectionState.Disconnected:
                    if (connectionStatusText) { connectionStatusText.text = "Déconnecté"; connectionStatusText.color = disconnectedColor; }
                    break;

                // Ancien 'Starting'/'Listening' retirés : on affiche quelque chose d’équivalent en 'Connecting'
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

            if (buttonText) buttonText.text = isPlayerReady ? "Prêt ✓" : "Pas prêt";
            if (buttonImage) buttonImage.color = isPlayerReady ? readyColor : notReadyColor;
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
            foreach (var p in players) CreatePlayerItem(p);
            UpdateLobbyInfo(Net.CurrentMode);
        }

        private void ClearPlayersList()
        {
            foreach (var go in playerItems) if (go) Destroy(go);
            playerItems.Clear();
        }

        private void CreatePlayerItem(PlayerData player)
        {
            if (!playerItemPrefab || !playersListParent) return;

            var item = Instantiate(playerItemPrefab, playersListParent);
            playerItems.Add(item);

            var nameText = item.GetComponentInChildren<TextMeshProUGUI>();
            var bg = item.GetComponent<Image>();

            if (nameText)
            {
                string pName = player.playerName;
                if (player.isHost) pName += " (Serveur)";
                if (player.isReady) pName += " ✓";
                nameText.text = pName;
            }

            if (bg)
            {
                if (player.isHost) bg.color = hostColor;
                else if (player.isReady) bg.color = readyColor;
                else bg.color = notReadyColor;
            }
        }

        #endregion

        #region Buttons handlers

        private void ToggleReady()
        {
            isPlayerReady = !isPlayerReady;
            Net?.SetLocalReady(isPlayerReady); // <-- ajout
            UpdateReadyButton();
        }


        private void StartGame()
        {
            // côté host: on délègue la logique au NetworkManager
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
            // Si tu déclenches un event côté NetworkManager, on peut charger ici.
            SceneManager.LoadScene("GameScene");
        }

        #endregion

        #region Helpers

        // Remplace l’ancien appel à NetworkManager.LocalIPAddress
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
    }
}
