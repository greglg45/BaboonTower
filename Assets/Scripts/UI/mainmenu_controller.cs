using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using BaboonTower.Network;

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
            // Configurer les événements des boutons principaux
            hostGameButton.onClick.AddListener(ShowHostPanel);
            joinGameButton.onClick.AddListener(ShowJoinPanel);
            optionsButton.onClick.AddListener(OnOptionsClick);
            quitButton.onClick.AddListener(OnQuitClick);

            // Configurer les panneaux
            startHostButton.onClick.AddListener(StartHostGame);
            cancelHostButton.onClick.AddListener(HideAllPanels);

            connectButton.onClick.AddListener(ConnectToGame);
            cancelJoinButton.onClick.AddListener(HideAllPanels);

            cancelStatusButton.onClick.AddListener(CancelNetworkAction);

            // Ajouter les effets de survol
            AddHoverEffect(hostGameButton);
            AddHoverEffect(joinGameButton);
            AddHoverEffect(optionsButton);
            AddHoverEffect(quitButton);

            // Charger les paramètres sauvegardés
            LoadSettings();

            // Cacher tous les panneaux au départ
            HideAllPanels();
        }

        private void LoadSettings()
        {
            // Charger les paramètres sauvegardés
            string savedPlayerName = PlayerPrefs.GetString("PlayerName", "Player" + UnityEngine.Random.Range(1000, 9999));
            string savedServerIP = PlayerPrefs.GetString("ServerIP", "127.0.0.1");

            if (hostPlayerNameInput != null)
                hostPlayerNameInput.text = savedPlayerName;

            if (joinPlayerNameInput != null)
                joinPlayerNameInput.text = savedPlayerName;

            if (serverIPInput != null)
                serverIPInput.text = savedServerIP;

            // Afficher l'IP locale pour le host
            if (serverIPText != null && NetworkManager.Instance != null)
                serverIPText.text = $"IP du serveur: {NetworkManager.Instance.LocalIPAddress}:7777";
        }

        private void SetupNetworkEvents()
        {
            NetworkManager.OnConnectionStateChanged += OnNetworkStateChanged;
        }

        private void RemoveNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnConnectionStateChanged -= OnNetworkStateChanged;
            }
        }

        #region Panel Management

        private void ShowHostPanel()
        {
            HideAllPanels();
            hostPanel?.SetActive(true);

            // Mettre à jour l'IP du serveur
            if (serverIPText != null && NetworkManager.Instance != null)
                serverIPText.text = $"IP du serveur: {NetworkManager.Instance.LocalIPAddress}:7777";
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
            if (statusText != null)
                statusText.text = message;
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

            // Sauvegarder le nom
            PlayerPrefs.SetString("PlayerName", playerName);
            NetworkManager.Instance.SetPlayerName(playerName);

            // Démarrer le serveur
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

            // Sauvegarder les paramètres
            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.SetString("ServerIP", serverIP);
            NetworkManager.Instance.SetPlayerName(playerName);

            // Se connecter au serveur
            ShowStatusPanel("Connexion au serveur...");
            NetworkManager.Instance.ConnectToServer(serverIP);
        }

        private void CancelNetworkAction()
        {
            NetworkManager.Instance.StopNetworking();
            HideAllPanels();
        }

        #endregion

        #region Network Events

        private void OnNetworkStateChanged(ConnectionState newState)
        {
            switch (newState)
            {
                case ConnectionState.Listening:
                    ShowStatusPanel("Serveur en attente de joueurs...\nLes joueurs peuvent se connecter sur:\n" +
                                  NetworkManager.Instance.LocalIPAddress + ":7777");
                    break;

                case ConnectionState.Connected:
                    // Aller directement au lobby
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
            versionText.text = $"Version {version}";
        }

        private void OnOptionsClick()
        {
            Debug.Log("Options clicked - TODO: Implement options modal");
            // TODO: Ouvrir une fenêtre modale avec les réglages
            // Possiblement instancier un prefab d'options ou activer un panel caché
        }

        private void OnQuitClick()
        {
            Debug.Log("Quit application");

            // S'assurer de fermer le réseau avant de quitter
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
    }
}