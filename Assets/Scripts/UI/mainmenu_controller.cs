using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BaboonTower.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button joinLobbyButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TextMeshProUGUI versionText;
        
        [Header("Button Hover Effects")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(0.8f, 0.8f, 1f, 1f);
        [SerializeField] private float hoverScale = 1.05f;
        
        private void Start()
        {
            InitializeButtons();
            DisplayVersion();
        }
        
        private void InitializeButtons()
        {
            // Configurer les événements des boutons
            joinLobbyButton.onClick.AddListener(OnJoinLobbyClick);
            optionsButton.onClick.AddListener(OnOptionsClick);
            quitButton.onClick.AddListener(OnQuitClick);
            
            // Ajouter les effets de survol
            AddHoverEffect(joinLobbyButton);
            AddHoverEffect(optionsButton);
            AddHoverEffect(quitButton);
        }
        
        private void AddHoverEffect(Button button)
        {
            var buttonImage = button.GetComponent<Image>();
            var buttonTransform = button.transform;
            
            // Créer les triggers d'événements
            var eventTrigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }
            
            // Effet d'entrée de la souris
            var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((data) => {
                buttonImage.color = hoverColor;
                buttonTransform.localScale = Vector3.one * hoverScale;
            });
            
            // Effet de sortie de la souris
            var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((data) => {
                buttonImage.color = normalColor;
                buttonTransform.localScale = Vector3.one * hoverScale;
            });
            
            eventTrigger.triggers.Add(pointerEnter);
            eventTrigger.triggers.Add(pointerExit);
        }
        
        private void DisplayVersion()
        {
            string version = VersionManager.GetVersion();
            versionText.text = $"Version {version}";
        }
        
        #region Button Events
        
        private void OnJoinLobbyClick()
        {
            Debug.Log("Join Lobby clicked - TODO: Implement lobby connection logic");
            // TODO: Implémenter la logique de connexion au lobby
            // Possiblement charger une scène "LobbyConnection" ou ouvrir un panel de connexion IP
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
            

                Application.Quit();

        }
        
        #endregion
    }
}