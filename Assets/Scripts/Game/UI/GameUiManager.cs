using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using BaboonTower.Network;

namespace BaboonTower.Game.UI
{
    /// <summary>
    /// Gestionnaire principal du menu latéral droit dans la GameScene
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        [Header("Main UI Components")]
        [SerializeField] private RectTransform sidePanel;
        [SerializeField] private float panelWidth = 350f;
        
        [Header("Player Info Panel")]
        [SerializeField] private RectTransform playerInfoPanel;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI castleHPText;
        [SerializeField] private Slider castleHPBar;
        [SerializeField] private TextMeshProUGUI enemiesAliveText;
        [SerializeField] private TextMeshProUGUI waveNumberText;
        [SerializeField] private TextMeshProUGUI waveTimerText;
        [SerializeField] private TextMeshProUGUI gameStateText;
        
        [Header("Shop Tabs")]
        [SerializeField] private GameObject defenseTab;
        [SerializeField] private GameObject attackTab;
        [SerializeField] private Button defenseTabButton;
        [SerializeField] private Button attackTabButton;
        
        [Header("Tower Shop")]
        [SerializeField] private Transform towerShopContent;
        [SerializeField] private GameObject towerShopItemPrefab;
        
        [Header("Mercenary Shop")]
        [SerializeField] private Transform mercenaryShopContent;
        [SerializeField] private GameObject mercenaryShopItemPrefab;
        
        [Header("Target Selection Modal")]
        [SerializeField] private GameObject targetSelectionModal;
        [SerializeField] private Transform targetListContent;
        [SerializeField] private GameObject targetItemPrefab;
        [SerializeField] private Button cancelTargetButton;
        
        [Header("Visual Settings")]
        [SerializeField] private Color activeTabColor = new Color(0.6f, 0.4f, 0.2f);
        [SerializeField] private Color inactiveTabColor = new Color(0.4f, 0.3f, 0.15f);
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = new Color(0.5f, 0.5f, 0.5f);
        
        [Header("Wood Theme Sprites")]
        [SerializeField] private Sprite woodPanelSprite;
        [SerializeField] private Sprite woodButtonSprite;
        [SerializeField] private Sprite goldIconSprite;
        
        // Configuration data
        private TowerConfig towerConfig;
        private MercenaryConfig mercenaryConfig;
        
        // References
        private GameController gameController;
        private TowerPlacementSystem placementSystem;
        private NetworkManager networkManager;
        
        // State
        private bool isDefenseTabActive = true;
        private MercenaryData selectedMercenary;
        private Dictionary<string, float> mercenaryCooldowns = new Dictionary<string, float>();
        
        // UI Elements cache
        private List<TowerShopItem> towerShopItems = new List<TowerShopItem>();
        private List<MercenaryShopItem> mercenaryShopItems = new List<MercenaryShopItem>();
        
        private void Awake()
        {
            // Get references
            gameController = FindObjectOfType<GameController>();
            placementSystem = FindObjectOfType<TowerPlacementSystem>();
            networkManager = NetworkManager.Instance;
            
            if (placementSystem == null)
            {
                GameObject placementGO = new GameObject("TowerPlacementSystem");
                placementSystem = placementGO.AddComponent<TowerPlacementSystem>();
            }
            
            SetupUI();
            LoadConfigurations();
        }
        
        private void Start()
        {
            InitializeShops();
            SetupEventListeners();
            SwitchToDefenseTab();
            
            // Position panel on the right side
            PositionSidePanel();
        }
        
        private void Update()
        {
            UpdatePlayerInfo();
            UpdateCooldowns();
        }
        
        private void OnDestroy()
        {
            RemoveEventListeners();
        }
        
        #region UI Setup
        
        private void SetupUI()
        {
            // Configure tab buttons
            if (defenseTabButton != null)
            {
                defenseTabButton.onClick.AddListener(SwitchToDefenseTab);
            }
            
            if (attackTabButton != null)
            {
                attackTabButton.onClick.AddListener(SwitchToAttackTab);
            }
            
            if (cancelTargetButton != null)
            {
                cancelTargetButton.onClick.AddListener(CloseTargetSelection);
            }
            
            // Hide target selection modal by default
            if (targetSelectionModal != null)
            {
                targetSelectionModal.SetActive(false);
            }
        }
        
        private void PositionSidePanel()
        {
            if (sidePanel == null) return;
            
            // Position on the right side of screen
            sidePanel.anchorMin = new Vector2(1, 0);
            sidePanel.anchorMax = new Vector2(1, 1);
            sidePanel.pivot = new Vector2(1, 0.5f);
            sidePanel.sizeDelta = new Vector2(panelWidth, 0);
            sidePanel.anchoredPosition = Vector2.zero;
        }
        
        #endregion
        
        #region Configuration Loading
        
        private void LoadConfigurations()
        {
            // Load tower configuration
            string towerConfigPath = Path.Combine(Application.streamingAssetsPath, "TowersConfig.json");
            if (File.Exists(towerConfigPath))
            {
                string json = File.ReadAllText(towerConfigPath);
                towerConfig = JsonUtility.FromJson<TowerConfig>(json);
                Debug.Log($"[GameUIManager] Loaded {towerConfig.towers.Count} towers from config");
            }
            else
            {
                Debug.LogError($"[GameUIManager] TowersConfig.json not found at {towerConfigPath}");
                CreateDefaultTowerConfig();
            }
            
            // Load mercenary configuration
            string mercConfigPath = Path.Combine(Application.streamingAssetsPath, "MercenariesConfig.json");
            if (File.Exists(mercConfigPath))
            {
                string json = File.ReadAllText(mercConfigPath);
                mercenaryConfig = JsonUtility.FromJson<MercenaryConfig>(json);
                Debug.Log($"[GameUIManager] Loaded {mercenaryConfig.mercenaries.Count} mercenaries from config");
            }
            else
            {
                Debug.LogError($"[GameUIManager] MercenariesConfig.json not found at {mercConfigPath}");
                CreateDefaultMercenaryConfig();
            }
        }
        
        private void CreateDefaultTowerConfig()
        {
            // Create a minimal default config if file not found
            towerConfig = new TowerConfig();
            towerConfig.towers = new List<TowerData>();
            
            // Add one basic tower
            TowerData basicTower = new TowerData();
            basicTower.id = "basic_tower";
            basicTower.name = "Tour Basique";
            basicTower.description = "Une tour simple";
            basicTower.cost = new TowerCost { gold = 20 };
            basicTower.stats = new TowerStats { damage = 5, range = 3, fireRate = 1 };
            towerConfig.towers.Add(basicTower);
        }
        
        private void CreateDefaultMercenaryConfig()
        {
            // Create a minimal default config if file not found
            mercenaryConfig = new MercenaryConfig();
            mercenaryConfig.mercenaries = new List<MercenaryData>();
            
            // Add one basic mercenary
            MercenaryData basicMerc = new MercenaryData();
            basicMerc.id = "basic_merc";
            basicMerc.name = "Mercenaire";
            basicMerc.description = "Un mercenaire basique";
            basicMerc.cost = new MercenaryCost { gold = 30 };
            mercenaryConfig.mercenaries.Add(basicMerc);
        }
        
        #endregion
        
        #region Shop Initialization
        
        private void InitializeShops()
        {
            CreateTowerShopItems();
            CreateMercenaryShopItems();
        }
        
        private void CreateTowerShopItems()
        {
            if (towerConfig == null || towerShopContent == null) return;
            
            // Clear existing items
            foreach (Transform child in towerShopContent)
            {
                Destroy(child.gameObject);
            }
            towerShopItems.Clear();
            
            // Create shop items for each tower
            foreach (var towerData in towerConfig.towers)
            {
                GameObject itemGO = CreateShopItemUI(towerShopContent, true);
                TowerShopItem shopItem = itemGO.AddComponent<TowerShopItem>();
                shopItem.Initialize(towerData, this);
                towerShopItems.Add(shopItem);
            }
        }
        
        private void CreateMercenaryShopItems()
        {
            if (mercenaryConfig == null || mercenaryShopContent == null) return;
            
            // Clear existing items
            foreach (Transform child in mercenaryShopContent)
            {
                Destroy(child.gameObject);
            }
            mercenaryShopItems.Clear();
            
            // Create shop items for each mercenary
            foreach (var mercData in mercenaryConfig.mercenaries)
            {
                GameObject itemGO = CreateShopItemUI(mercenaryShopContent, false);
                MercenaryShopItem shopItem = itemGO.AddComponent<MercenaryShopItem>();
                shopItem.Initialize(mercData, this);
                mercenaryShopItems.Add(shopItem);
                
                // Initialize cooldown
                mercenaryCooldowns[mercData.id] = 0f;
            }
        }
        
        private GameObject CreateShopItemUI(Transform parent, bool isTower)
        {
            GameObject item = new GameObject(isTower ? "TowerShopItem" : "MercenaryShopItem");
            item.transform.SetParent(parent, false);
            
            // Add RectTransform
            RectTransform rect = item.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 80);
            
            // Add Layout Element
            LayoutElement layout = item.AddComponent<LayoutElement>();
            layout.preferredHeight = 80;
            layout.preferredWidth = -1;
            
            // Add Button
            Button button = item.AddComponent<Button>();
            
            // Add background image
            Image bg = item.AddComponent<Image>();
            bg.sprite = woodButtonSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.6f, 0.4f, 0.2f);
            
            // Create content container
            GameObject content = new GameObject("Content");
            content.transform.SetParent(item.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;
            
            // Add horizontal layout
            HorizontalLayoutGroup hLayout = content.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(10, 10, 10, 10);
            hLayout.spacing = 10;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;
            
            // Icon
            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(content.transform, false);
            Image iconImg = icon.AddComponent<Image>();
            iconImg.color = Color.white;
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(60, 60);
            
            // Info container
            GameObject info = new GameObject("Info");
            info.transform.SetParent(content.transform, false);
            RectTransform infoRect = info.AddComponent<RectTransform>();
            infoRect.sizeDelta = new Vector2(180, 60);
            
            VerticalLayoutGroup vLayout = info.AddComponent<VerticalLayoutGroup>();
            vLayout.childAlignment = TextAnchor.MiddleLeft;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            
            // Name text
            GameObject nameGO = new GameObject("Name");
            nameGO.transform.SetParent(info.transform, false);
            TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text = "Item Name";
            nameText.fontSize = 16;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            
            // Description text
            GameObject descGO = new GameObject("Description");
            descGO.transform.SetParent(info.transform, false);
            TextMeshProUGUI descText = descGO.AddComponent<TextMeshProUGUI>();
            descText.text = "Description";
            descText.fontSize = 12;
            descText.color = new Color(0.9f, 0.9f, 0.9f);
            
            // Cost text
            GameObject costGO = new GameObject("Cost");
            costGO.transform.SetParent(info.transform, false);
            TextMeshProUGUI costText = costGO.AddComponent<TextMeshProUGUI>();
            costText.text = "💰 0";
            costText.fontSize = 14;
            costText.fontStyle = FontStyles.Bold;
            costText.color = new Color(1f, 0.84f, 0f);
            
            return item;
        }
        
        #endregion
        
        #region Tab Management
        
        private void SwitchToDefenseTab()
        {
            isDefenseTabActive = true;
            
            if (defenseTab != null) defenseTab.SetActive(true);
            if (attackTab != null) attackTab.SetActive(false);
            
            // Update tab button colors
            if (defenseTabButton != null)
            {
                defenseTabButton.GetComponent<Image>().color = activeTabColor;
            }
            if (attackTabButton != null)
            {
                attackTabButton.GetComponent<Image>().color = inactiveTabColor;
            }
            
            UpdateShopItems();
        }
        
        private void SwitchToAttackTab()
        {
            isDefenseTabActive = false;
            
            if (defenseTab != null) defenseTab.SetActive(false);
            if (attackTab != null) attackTab.SetActive(true);
            
            // Update tab button colors
            if (defenseTabButton != null)
            {
                defenseTabButton.GetComponent<Image>().color = inactiveTabColor;
            }
            if (attackTabButton != null)
            {
                attackTabButton.GetComponent<Image>().color = activeTabColor;
            }
            
            UpdateShopItems();
        }
        
        #endregion
        
        #region Player Info Updates
        
        private void UpdatePlayerInfo()
        {
            if (gameController == null || gameController.GameStateData == null) return;
            
            var gameState = gameController.GameStateData;
            
            // Find local player state
            var localPlayer = gameState.playersStates.Find(p => p.playerId == GetLocalPlayerId());
            
            if (localPlayer != null)
            {
                // Update gold
                if (goldText != null)
                {
                    goldText.text = $"💰 {localPlayer.gold}";
                }
                
                // Update castle HP
                if (castleHPText != null)
                {
                    castleHPText.text = $"🏰 {localPlayer.castleHP}/{localPlayer.maxCastleHP}";
                }
                
                if (castleHPBar != null)
                {
                    castleHPBar.maxValue = localPlayer.maxCastleHP;
                    castleHPBar.value = localPlayer.castleHP;
                }
            }
            
            // Update wave info
            if (waveNumberText != null)
            {
                waveNumberText.text = $"Vague #{gameState.currentWave}";
            }
            
            if (waveTimerText != null)
            {
                if (gameState.currentState == GameState.PreparationPhase)
                {
                    waveTimerText.text = $"Prochaine vague: {Mathf.Ceil(gameState.waveTimer)}s";
                }
                else
                {
                    waveTimerText.text = "";
                }
            }
            
            if (gameStateText != null)
            {
                gameStateText.text = GetGameStateText(gameState.currentState);
            }
            
            // Update enemies count
            WaveManager waveManager = FindObjectOfType<WaveManager>();
            if (waveManager != null && enemiesAliveText != null)
            {
                // This would need to be implemented in WaveManager
                enemiesAliveText.text = $"Ennemis: 0"; // Placeholder
            }
            
            // Update shop affordability
            UpdateShopItems();
        }
        
        private string GetGameStateText(GameState state)
        {
            switch (state)
            {
                case GameState.WaitingForPlayers:
                    return "En attente...";
                case GameState.PreparationPhase:
                    return "Phase d'achat";
                case GameState.WaveActive:
                    return "Vague en cours";
                case GameState.GameOver:
                    return "Partie terminée";
                default:
                    return "";
            }
        }
        
        private int GetLocalPlayerId()
        {
            // Get local player ID from NetworkManager
            if (networkManager != null && networkManager.ConnectedPlayers.Count > 0)
            {
                if (networkManager.CurrentMode == NetworkMode.Host)
                {
                    var hostPlayer = networkManager.ConnectedPlayers.Find(p => p.isHost);
                    return hostPlayer?.playerId ?? 0;
                }
                else
                {
                    var clientPlayer = networkManager.ConnectedPlayers.Find(p => !p.isHost);
                    return clientPlayer?.playerId ?? 1;
                }
            }
            return 0;
        }
        
        private int GetLocalPlayerGold()
        {
            if (gameController == null || gameController.GameStateData == null) return 0;
            
            var localPlayer = gameController.GameStateData.playersStates.Find(p => p.playerId == GetLocalPlayerId());
            return localPlayer?.gold ?? 0;
        }
        
        #endregion
        
        #region Shop Updates
        
        private void UpdateShopItems()
        {
            int playerGold = GetLocalPlayerGold();
            
            // Update tower shop items
            foreach (var item in towerShopItems)
            {
                item.UpdateAffordability(playerGold);
            }
            
            // Update mercenary shop items
            foreach (var item in mercenaryShopItems)
            {
                item.UpdateAffordability(playerGold);
                item.UpdateCooldown(GetMercenaryCooldown(item.MercenaryData.id));
            }
        }
        
        private void UpdateCooldowns()
        {
            // Update mercenary cooldowns
            List<string> keys = new List<string>(mercenaryCooldowns.Keys);
            foreach (string key in keys)
            {
                if (mercenaryCooldowns[key] > 0)
                {
                    mercenaryCooldowns[key] -= Time.deltaTime;
                    if (mercenaryCooldowns[key] < 0)
                    {
                        mercenaryCooldowns[key] = 0;
                    }
                }
            }
        }
        
        private float GetMercenaryCooldown(string mercenaryId)
        {
            return mercenaryCooldowns.ContainsKey(mercenaryId) ? mercenaryCooldowns[mercenaryId] : 0f;
        }
        
        #endregion
        
        #region Tower Purchase
        
        public void OnTowerSelected(TowerData towerData)
        {
            if (gameController == null || !gameController.CanAfford(towerData.cost.gold))
            {
                ShowNotification("Or insuffisant!");
                return;
            }
            
            // Close any open UI panels to avoid blocking
            if (targetSelectionModal != null && targetSelectionModal.activeSelf)
            {
                targetSelectionModal.SetActive(false);
            }
            
            // Start placement mode
            if (placementSystem != null)
            {
                placementSystem.StartPlacement(towerData);
            }
            else
            {
                Debug.LogError("[GameUIManager] TowerPlacementSystem not found!");
            }
        }
        
        public void OnTowerPlaced(TowerData towerData, Vector2Int gridPos)
        {
            // Deduct gold
            if (gameController != null)
            {
                gameController.SpendGold(towerData.cost.gold);
            }
            
            ShowNotification($"{towerData.name} placée!");
        }
        
        #endregion
        
        #region Mercenary Purchase
        
        public void OnMercenarySelected(MercenaryData mercenaryData)
        {
            if (gameController == null || !gameController.CanAfford(mercenaryData.cost.gold))
            {
                ShowNotification("Or insuffisant!");
                return;
            }
            
            // Check cooldown
            if (GetMercenaryCooldown(mercenaryData.id) > 0)
            {
                ShowNotification($"Cooldown: {Mathf.Ceil(GetMercenaryCooldown(mercenaryData.id))}s");
                return;
            }
            
            selectedMercenary = mercenaryData;
            
            // Show target selection
            ShowTargetSelection();
        }
        
        private void ShowTargetSelection()
        {
            if (targetSelectionModal == null) return;
            
            targetSelectionModal.SetActive(true);
            
            // Clear existing targets
            foreach (Transform child in targetListContent)
            {
                Destroy(child.gameObject);
            }
            
            // Get enemy players
            var players = gameController.GameStateData.playersStates;
            int localId = GetLocalPlayerId();
            
            foreach (var player in players)
            {
                if (player.playerId != localId && player.isAlive)
                {
                    CreateTargetItem(player);
                }
            }
        }
        
        private void CreateTargetItem(PlayerGameState player)
        {
            GameObject item = new GameObject($"Target_{player.playerName}");
            item.transform.SetParent(targetListContent, false);
            
            // Add components
            RectTransform rect = item.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 60);
            
            Button button = item.AddComponent<Button>();
            Image bg = item.AddComponent<Image>();
            bg.sprite = woodButtonSprite;
            bg.color = new Color(0.5f, 0.3f, 0.1f);
            
            // Add text
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(item.transform, false);
            TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = player.playerName;
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            // Add click handler
            button.onClick.AddListener(() => OnTargetSelected(player));
        }
        
        private void OnTargetSelected(PlayerGameState target)
        {
            if (selectedMercenary == null) return;
            
            // Send mercenary to target
            SendMercenary(selectedMercenary, target.playerId);
            
            // Close modal
            CloseTargetSelection();
        }
        
        private void CloseTargetSelection()
        {
            if (targetSelectionModal != null)
            {
                targetSelectionModal.SetActive(false);
            }
            selectedMercenary = null;
        }
        
        private void SendMercenary(MercenaryData mercenary, int targetPlayerId)
        {
            // Deduct gold
            if (gameController != null)
            {
                gameController.SpendGold(mercenary.cost.gold);
            }
            
            // Set cooldown
            mercenaryCooldowns[mercenary.id] = mercenary.cost.cooldown;
            
            // Send network message
            if (networkManager != null)
            {
                string data = $"{mercenary.id}|{targetPlayerId}";
                networkManager.SendGameMessageToServer("SEND_MERCENARY", data);
            }
            
            ShowNotification($"{mercenary.name} envoyé!");
        }
        
        #endregion
        
        #region Notifications
        
        private void ShowNotification(string message)
        {
            // This could be improved with a proper notification system
            Debug.Log($"[GameUI] {message}");
        }
        
        #endregion
        
        #region Event Listeners
        
        private void SetupEventListeners()
        {
            if (gameController != null)
            {
                gameController.OnGameStateChanged += OnGameStateChanged;
                gameController.OnWaveStarted += OnWaveStarted;
            }
            
            if (placementSystem != null)
            {
                placementSystem.OnTowerPlaced += OnTowerPlaced;
            }
        }
        
        private void RemoveEventListeners()
        {
            if (gameController != null)
            {
                gameController.OnGameStateChanged -= OnGameStateChanged;
                gameController.OnWaveStarted -= OnWaveStarted;
            }
            
            if (placementSystem != null)
            {
                placementSystem.OnTowerPlaced -= OnTowerPlaced;
            }
        }
        
        private void OnGameStateChanged(GameState newState)
        {
            UpdatePlayerInfo();
        }
        
        private void OnWaveStarted(int waveNumber)
        {
            UpdatePlayerInfo();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Component for tower shop items
    /// </summary>
    public class TowerShopItem : MonoBehaviour
    {
        private TowerData towerData;
        private GameUIManager uiManager;
        private Button button;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI descText;
        private TextMeshProUGUI costText;
        private Image iconImage;
        private Image bgImage;
        
        public void Initialize(TowerData data, GameUIManager manager)
        {
            towerData = data;
            uiManager = manager;
            
            // Get components
            button = GetComponent<Button>();
            bgImage = GetComponent<Image>();
            
            Transform content = transform.Find("Content");
            if (content != null)
            {
                iconImage = content.Find("Icon")?.GetComponent<Image>();
                
                Transform info = content.Find("Info");
                if (info != null)
                {
                    nameText = info.Find("Name")?.GetComponent<TextMeshProUGUI>();
                    descText = info.Find("Description")?.GetComponent<TextMeshProUGUI>();
                    costText = info.Find("Cost")?.GetComponent<TextMeshProUGUI>();
                }
            }
            
            // Set data
            if (nameText != null) nameText.text = data.name;
            if (descText != null) descText.text = data.description;
            if (costText != null) costText.text = $"💰 {data.cost.gold}";
            
            // Set icon color based on tower type
            if (iconImage != null)
            {
                Color iconColor = Color.white;
                if (!string.IsNullOrEmpty(data.visual.towerColor))
                {
                    ColorUtility.TryParseHtmlString(data.visual.towerColor, out iconColor);
                }
                iconImage.color = iconColor;
            }
            
            // Add click listener
            if (button != null)
            {
                button.onClick.AddListener(() => uiManager.OnTowerSelected(towerData));
            }
        }
        
        public void UpdateAffordability(int playerGold)
        {
            bool canAfford = playerGold >= towerData.cost.gold;
            
            if (button != null)
            {
                button.interactable = canAfford;
            }
            
            if (bgImage != null)
            {
                bgImage.color = canAfford ? 
                    new Color(0.6f, 0.4f, 0.2f) : 
                    new Color(0.3f, 0.2f, 0.1f);
            }
            
            if (costText != null)
            {
                costText.color = canAfford ? 
                    new Color(1f, 0.84f, 0f) : 
                    new Color(0.5f, 0.42f, 0f);
            }
        }
    }
    
    /// <summary>
    /// Component for mercenary shop items
    /// </summary>
    public class MercenaryShopItem : MonoBehaviour
    {
        private MercenaryData mercenaryData;
        private GameUIManager uiManager;
        private Button button;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI descText;
        private TextMeshProUGUI costText;
        private Image iconImage;
        private Image bgImage;
        private GameObject cooldownOverlay;
        private TextMeshProUGUI cooldownText;
        
        public MercenaryData MercenaryData => mercenaryData;
        
        public void Initialize(MercenaryData data, GameUIManager manager)
        {
            mercenaryData = data;
            uiManager = manager;
            
            // Get components
            button = GetComponent<Button>();
            bgImage = GetComponent<Image>();
            
            Transform content = transform.Find("Content");
            if (content != null)
            {
                iconImage = content.Find("Icon")?.GetComponent<Image>();
                
                Transform info = content.Find("Info");
                if (info != null)
                {
                    nameText = info.Find("Name")?.GetComponent<TextMeshProUGUI>();
                    descText = info.Find("Description")?.GetComponent<TextMeshProUGUI>();
                    costText = info.Find("Cost")?.GetComponent<TextMeshProUGUI>();
                }
            }
            
            // Create cooldown overlay
            CreateCooldownOverlay();
            
            // Set data
            if (nameText != null) nameText.text = $"{data.name} - {data.title}";
            if (descText != null) descText.text = data.description;
            if (costText != null) costText.text = $"💰 {data.cost.gold}";
            
            // Set icon color based on mercenary
            if (iconImage != null)
            {
                Color iconColor = Color.white;
                if (!string.IsNullOrEmpty(data.visual.color))
                {
                    ColorUtility.TryParseHtmlString(data.visual.color, out iconColor);
                }
                iconImage.color = iconColor;
            }
            
            // Add click listener
            if (button != null)
            {
                button.onClick.AddListener(() => uiManager.OnMercenarySelected(mercenaryData));
            }
        }
        
        private void CreateCooldownOverlay()
        {
            cooldownOverlay = new GameObject("CooldownOverlay");
            cooldownOverlay.transform.SetParent(transform, false);
            
            RectTransform rect = cooldownOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            
            Image overlay = cooldownOverlay.AddComponent<Image>();
            overlay.color = new Color(0, 0, 0, 0.7f);
            
            GameObject textGO = new GameObject("CooldownText");
            textGO.transform.SetParent(cooldownOverlay.transform, false);
            
            cooldownText = textGO.AddComponent<TextMeshProUGUI>();
            cooldownText.text = "0s";
            cooldownText.fontSize = 24;
            cooldownText.fontStyle = FontStyles.Bold;
            cooldownText.alignment = TextAlignmentOptions.Center;
            cooldownText.color = Color.white;
            
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            cooldownOverlay.SetActive(false);
        }
        
        public void UpdateAffordability(int playerGold)
        {
            bool canAfford = playerGold >= mercenaryData.cost.gold;
            
            if (button != null)
            {
                button.interactable = canAfford && (cooldownOverlay == null || !cooldownOverlay.activeSelf);
            }
            
            if (bgImage != null && (cooldownOverlay == null || !cooldownOverlay.activeSelf))
            {
                bgImage.color = canAfford ? 
                    new Color(0.6f, 0.4f, 0.2f) : 
                    new Color(0.3f, 0.2f, 0.1f);
            }
            
            if (costText != null)
            {
                costText.color = canAfford ? 
                    new Color(1f, 0.84f, 0f) : 
                    new Color(0.5f, 0.42f, 0f);
            }
        }
        
        public void UpdateCooldown(float cooldown)
        {
            if (cooldownOverlay != null)
            {
                bool onCooldown = cooldown > 0;
                cooldownOverlay.SetActive(onCooldown);
                
                if (onCooldown && cooldownText != null)
                {
                    cooldownText.text = $"{Mathf.Ceil(cooldown)}s";
                }
                
                if (button != null)
                {
                    button.interactable = !onCooldown;
                }
            }
        }
    }
}