using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using BaboonTower.Network;
using System.Net;            // <-- pour GetLocalIPAddress
using System.Net.Sockets;    // <-- pour AddressFamily

namespace BaboonTower.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button hostGameButton;
        [SerializeField] private Button joinGameButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TextMeshProUGUI versionText;

        [Header("Host Panel")]
        [SerializeField] private GameObject hostPanel;
        [SerializeField] private TMP_InputField hostPlayerNameInput;
        [SerializeField] private TextMeshProUGUI serverIPText;
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button cancelHostButton;

        [Header("Join Panel")]
        [SerializeField] private GameObject joinPanel;
        [SerializeField] private TMP_InputField joinPlayerNameInput;
        [SerializeField] private TMP_InputField serverIPInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button cancelJoinButton;

        [Header("Status Panel")]
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button cancelStatusButton;

        [Header("Button Hover Effects")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(0.8f, 0.8f, 1f, 1f);
        [SerializeField] private float hoverScale = 1.05f;

        private void Start()
        {
            InitializeMainMenu();
            DisplayVersion();
            SetupNetworkEvents();
        }

        private void OnDestroy()
        {
            RemoveNetworkEvents();
        }

        private void InitializeMainMenu()
        {
            hostGameButton.onClick.AddListener(ShowHostPanel);
            joinGameButton.onClick.AddListener(ShowJoinPanel);
            optionsButton.onClick.AddListener(OnOptionsClick);
            quitButton.onClick.AddListener(OnQuitClick);

            startHostButton.onClick.AddListener(StartHostGame);
            cancelHostButton.onClick.AddListener(HideAllPanels);

            connectButton.onClick.AddListener(ConnectToGame);
            cancelJoinButton.onClick.AddListener(HideAllPanels);

            cancelStatusButton.onClick.AddListener(CancelNetworkAction);

            AddHoverEffect(hostGameButton);
            AddHoverEffect(joinGameButton);
            AddHoverEffect(optionsButton);
            AddHoverEffect(quitButton);

            LoadSettings();
            HideAllPanels();
        }

        private void LoadSettings()
        {
            string savedPlayerName = PlayerPrefs.GetString("PlayerName", "Player" + UnityEngine.Random.Range(1000, 9999));
            string savedServerIP = PlayerPrefs.GetString("ServerIP", "127.0.0.1");

            if (hostPlayerNameInput != null) hostPlayerNameInput.text = savedPlayerName;
            if (joinPlayerNameInput != null) joinPlayerNameInput.text = savedPlayerName;
            if (serverIPInput != null) serverIPInput.text = savedServerIP;

            // Afficher l’IP locale pour le host (pas de LocalIPAddress dans NetworkManager)
            if (serverIPText != null)
            {
                int port = (NetworkManager.Instance != null)
                    ? NetworkManager.Instance.CurrentPort
                    : PlayerPrefs.GetInt("ServerPort", 7777);

                serverIPText.text = $"IP du serveur: {GetLocalIPAddress()}:{port}";
            }
        }

        private void SetupNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged += OnNetworkStateChanged; // <-- via Instance
            }
        }

        private void RemoveNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged -= OnNetworkStateChanged; // <-- via Instance
            }
        }

        #region Panel Management

        private void ShowHostPanel()
        {
            HideAllPanels();
            hostPanel?.SetActive(true);

            // Mettre à jour l'IP du serveur (sans LocalIPAddress du manager)
            if (serverIPText != null)
            {
                int port = (NetworkManager.Instance != null)
                    ? NetworkManager.Instance.CurrentPort
                    : PlayerPrefs.GetInt("ServerPort", 7777);

                serverIPText.text = $"IP du serveur: {GetLocalIPAddress()}:{port}";
            }
        }

        private void ShowJoinPanel()
        {
            HideAllPanels();
            joinPanel?.SetActive(true);
        }

        private void ShowStatusPanel(string message)
        {
            HideAllPanels();
            statusPanel?.SetActive(true);
            if (statusText != null) statusText.text = message;
        }

        private void HideAllPanels()
        {
            hostPanel?.SetActive(false);
            joinPanel?.SetActive(false);
            statusPanel?.SetActive(false);
        }

        #endregion

        #region Network Actions

        private void StartHostGame()
        {
            string playerName = hostPlayerNameInput?.text?.Trim();

            if (string.IsNullOrEmpty(playerName))
            {
                ShowStatusPanel("Veuillez saisir votre nom de joueur");
                return;
            }

            PlayerPrefs.SetString("PlayerName", playerName);
            NetworkManager.Instance.SetPlayerName(playerName);

            ShowStatusPanel("Démarrage du serveur...");
            NetworkManager.Instance.StartServer();
        }

        private void ConnectToGame()
        {
            string playerName = joinPlayerNameInput?.text?.Trim();
            string serverIP = serverIPInput?.text?.Trim();

            if (string.IsNullOrEmpty(playerName))
            {
                ShowStatusPanel("Veuillez saisir votre nom de joueur");
                return;
            }

            if (string.IsNullOrEmpty(serverIP))
            {
                ShowStatusPanel("Veuillez saisir l'adresse IP du serveur");
                return;
            }

            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.SetString("ServerIP", serverIP);
            NetworkManager.Instance.SetPlayerName(playerName);

            ShowStatusPanel("Connexion au serveur...");
            NetworkManager.Instance.ConnectToServer(serverIP);
        }

        private void CancelNetworkAction()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.StopNetworking();
            }
            HideAllPanels();
        }

        #endregion

        #region Network Events

        private void OnNetworkStateChanged(ConnectionState newState)
        {
            switch (newState)
            {
                // Remplace l’ancien 'Listening' par une logique basée sur l’état existant
                case ConnectionState.Connecting:
                    {
                        // Si on est en host pendant le boot serveur, afficher l’info IP
                        if (NetworkManager.Instance != null && NetworkManager.Instance.CurrentMode == NetworkMode.Host)
                        {
                            int port = NetworkManager.Instance.CurrentPort;
                            ShowStatusPanel(
                                "Serveur en attente de joueurs...\nLes joueurs peuvent se connecter sur:\n" +
                                GetLocalIPAddress() + ":" + port
                            );
                        }
                        else
                        {
                            ShowStatusPanel("Connexion en cours...");
                        }
                        break;
                    }

                case ConnectionState.Connected:
                    // Aller au lobby une fois connecté (host ou client)
                    SceneManager.LoadScene("Lobby");
                    break;

                case ConnectionState.Failed:
                    ShowStatusPanel("Connexion échouée!\nVérifiez l'adresse IP et réessayez.");
                    break;

                case ConnectionState.Disconnected:
                    HideAllPanels();
                    break;
            }
        }

        #endregion

        #region Hover Effects

        private void AddHoverEffect(Button button)
        {
            var buttonImage = button.GetComponent<Image>();
            var buttonTransform = button.transform;

            var eventTrigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }

            var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((data) =>
            {
                buttonImage.color = hoverColor;
                buttonTransform.localScale = Vector3.one * hoverScale;
            });

            var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((data) =>
            {
                buttonImage.color = normalColor;
                buttonTransform.localScale = Vector3.one;
            });

            eventTrigger.triggers.Add(pointerEnter);
            eventTrigger.triggers.Add(pointerExit);
        }

        #endregion

        #region Original Button Events

        private void DisplayVersion()
        {
            string version = VersionManager.GetVersion();
            if (versionText != null)
                versionText.text = $"Version {version}";
        }

        private void OnOptionsClick()
        {
            Debug.Log("Options clicked - TODO: Implement options modal");
        }

        private void OnQuitClick()
        {
            Debug.Log("Quit application");

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.StopNetworking();
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region Helpers

        // Remplace l’appel à NetworkManager.LocalIPAddress (absent)
        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }

        #endregion
    }
}
