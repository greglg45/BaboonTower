using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System;
using System.Linq;
using BaboonTower.Game;

namespace BaboonTower.Network
{
    public enum NetworkMode
    {
        None,
        Host,
        Client
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Failed
    }

    [System.Serializable]
    public class PlayerData
    {
        public string playerName;
        public string PlayerName => playerName;
        public int playerId;
        public bool isReady;
        public bool isHost;

        public PlayerData(string name, int id, bool host = false)
        {
            playerName = name;
            playerId = id;
            isReady = false;
            isHost = host;
        }
    }

    [System.Serializable]
    public class NetworkMessage
    {
        public string messageType;
        public string data;

        public NetworkMessage(string type, string data)
        {
            this.messageType = type;
            this.data = data;
        }
    }

    public class ConnectedClient
    {
        public TcpClient tcpClient;
        public NetworkStream stream;
        public Thread clientThread;
        public bool isActive;

        public PlayerData playerData;

        public ConnectedClient(TcpClient client, int playerId)
        {
            tcpClient = client;
            stream = client.GetStream();
            isActive = true;
            playerData = new PlayerData("Player" + playerId, playerId);
        }
    }

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;

        [Header("Network Settings")]
        public int defaultPort = 7777;
        public int maxPlayers = 16;
        public int CurrentPort { get; private set; }

        public NetworkMode CurrentMode { get; private set; } = NetworkMode.None;
        public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;
 /// <summary>
        /// Vrai uniquement si ce process tient réellement le serveur (socket + thread actifs).
        /// Permet de différencier un "faux host" forcé par du debug/reflection.
        /// </summary>
        public bool IsAuthoritativeHost
        {
            get
            {
                return CurrentMode == NetworkMode.Host
                       && server != null
                       && serverThread != null
                       && serverThread.IsAlive;
            }
        }

        [Header("Player")]
        [SerializeField] private string playerName = "Player";

        public event Action<ConnectionState> OnConnectionStateChanged;
        public event Action<List<PlayerData>> OnPlayersUpdated;
        public event Action<string> OnServerMessage;
        public event Action OnGameStarted;
        public event Action<string, string> OnChatMessage;
        public event Action<string, string> OnGameMessage; // messageType, data

        private TcpListener server;
        private List<ConnectedClient> connectedClients = new List<ConnectedClient>();
        private int nextPlayerId = 1;

        private TcpClient client;
        private NetworkStream clientStream;
        private Thread clientThread;

        private Thread serverThread;

        public List<PlayerData> ConnectedPlayers { get; private set; } = new List<PlayerData>();

        private bool isRunning = false;
        
        // Configuration des vagues
        private WaveConfigurationMessage currentWaveConfig;

        #region Public API

        /// <summary>
        /// BroadcastMessage - Diffuse un message à tous les clients (Host only)
        /// </summary>
public void BroadcastMessage(string messageType, string data)
{
    if (CurrentMode != NetworkMode.Host) return;

    NetworkMessage message = new NetworkMessage(messageType, data);
    string json = JsonUtility.ToJson(message);
    byte[] bytes = Encoding.UTF8.GetBytes(json);

    Debug.Log($"[NetworkManager Host] Broadcasting: {messageType} to {connectedClients.Count} clients");

    var clientsToRemove = new List<ConnectedClient>();

    foreach (var client in connectedClients)
    {
        try
        {
            if (client.tcpClient.Connected)
            {
                client.stream.Write(bytes, 0, bytes.Length);
                client.stream.Flush();
                Debug.Log($"[NetworkManager Host] Sent {messageType} to client {client.playerData.playerId}");
            }
            else
            {
                clientsToRemove.Add(client);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkManager Host] Error sending to client: {e.Message}");
            clientsToRemove.Add(client);
        }
    }

    foreach (var client in clientsToRemove)
    {
        RemoveClient(client);
    }
}

        /// <summary>
        /// BroadcastGameMessage - Diffuse un message de jeu à tous les clients (Host only)
        /// </summary>
        public void BroadcastGameMessage(string messageType, string data)
        {
            if (CurrentMode != NetworkMode.Host) return;

            BroadcastMessage(messageType, data);
            Debug.Log($"[NetworkManager] Broadcasting game message: {messageType}");
        }

        /// <summary>
        /// SendGameMessageToServer - Envoie un message de jeu au serveur (Client only)
        /// </summary>
public void SendGameMessageToServer(string messageType, string data)
        {
            // Autoriser l'envoi si on a une vraie connexion client vivante
            // (cas où CurrentMode a été forcé à Host via debug/reflection).
            bool hasLiveClient =
                client != null &&
                clientThread != null && clientThread.IsAlive &&
                CurrentState == ConnectionState.Connected;

            if (CurrentMode != NetworkMode.Client && !hasLiveClient)
            {
                Debug.LogWarning($"[NetworkManager] Ignored SendGameMessageToServer({messageType}) in mode {CurrentMode} (no live client connection).");
                return;
            }

            SendMessageToServer(messageType, data);
        }

        /// <summary>
        /// RequestSpendGold - Envoie une demande de dépense d'or au serveur (Client only)
        /// </summary>
        public void RequestSpendGold(int playerId, int amount)
        {
            if (CurrentMode != NetworkMode.Client) return;

            string data = $"{playerId}|{amount}";
            SendMessageToServer("SPEND_GOLD_REQUEST", data);
        }

        /// <summary>
        /// SendPlayerAction - Envoie une action de joueur au serveur (Client only)
        /// </summary>
        public void SendPlayerAction(string actionType, string actionData)
        {
            if (CurrentMode != NetworkMode.Client) return;

            string payload = $"{actionType}|{actionData}";
            SendMessageToServer("PLAYER_ACTION", payload);
        }

        #endregion

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureApplicationFocusManager();
            playerName = PlayerPrefs.GetString("PlayerName", playerName);
            
            // Charger la configuration des vagues si elle existe
            LoadWaveConfiguration();
        }
        
        private void EnsureApplicationFocusManager()
        {
            if (FindObjectOfType<BaboonTower.Core.ApplicationFocusManager>() == null)
            {
                GameObject focusManager = new GameObject("ApplicationFocusManager");
                focusManager.AddComponent<BaboonTower.Core.ApplicationFocusManager>();
                DontDestroyOnLoad(focusManager);
                Debug.Log("[NetworkManager] ApplicationFocusManager créé");
            }
        }
        
        private void LoadWaveConfiguration()
        {
            string configJson = PlayerPrefs.GetString("WaveConfiguration", "");
            if (!string.IsNullOrEmpty(configJson))
            {
                try
                {
                    currentWaveConfig = JsonUtility.FromJson<WaveConfigurationMessage>(configJson);
                    Debug.Log("[NetworkManager] Wave configuration loaded from PlayerPrefs");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[NetworkManager] Error loading wave configuration: {e.Message}");
                }
            }
        }
        
        private void OnDestroy()
        {
            StopNetworking();
        }

        private void OnApplicationQuit()
        {
            Debug.Log("Application quitting - stopping networking");
            StopNetworking();
        }

        public void StartServer()
        {
            if (CurrentMode != NetworkMode.None || CurrentState != ConnectionState.Disconnected)
            {
                Debug.LogWarning("Network déjà actif");
                return;
            }

            CurrentPort = PlayerPrefs.GetInt("ServerPort", defaultPort);
            StartCoroutine(StartServerCoroutine());
        }

        public void ConnectToServer(string serverIP)
        {
            if (CurrentState != ConnectionState.Disconnected)
            {
                Debug.LogWarning("Network déjà actif");
                return;
            }

            CurrentPort = PlayerPrefs.GetInt("ServerPort", defaultPort);
            StartCoroutine(ConnectToServerCoroutine(serverIP));
        }

        public void StopNetworking()
        {
            isRunning = false;

            if (CurrentMode == NetworkMode.Host)
            {
                StopServer();
            }
            else if (CurrentMode == NetworkMode.Client)
            {
                DisconnectClient();
            }

            CurrentMode = NetworkMode.None;
            SetConnectionState(ConnectionState.Disconnected);
        }

        public void SetPlayerName(string name)
        {
            playerName = name;
            PlayerPrefs.SetString("PlayerName", playerName);
        }

        /// <summary>
        /// SendChatMessage - Envoi de message chat
        /// </summary>
        public void SendChatMessage(string text)
        {
            string clean = SanitizeChat(text);
            if (string.IsNullOrEmpty(clean)) return;

            if (CurrentMode == NetworkMode.Host && CurrentState == ConnectionState.Connected)
            {
                string payload = $"{playerName}|{clean}";
                BroadcastMessage("CHAT", payload);
                OnChatMessage?.Invoke(playerName, clean); // feedback local host
            }
            else if (CurrentMode == NetworkMode.Client && CurrentState == ConnectionState.Connected)
            {
                SendMessageToServer("CHAT", clean);
                OnChatMessage?.Invoke(playerName, clean); // écho local client
            }
        }

        public void SetLocalReady(bool ready)
        {
            if (CurrentState != ConnectionState.Connected) return;

            if (CurrentMode == NetworkMode.Client)
            {
                SendMessageToServer("PLAYER_READY", ready.ToString());
            }
            else if (CurrentMode == NetworkMode.Host)
            {
                var me = ConnectedPlayers.FirstOrDefault(p => p.isHost);
                if (me != null)
                {
                    me.isReady = ready;
                    BroadcastPlayersUpdate();
                }
            }
        }

public void HostTryStartGame()
{
    // Ne fonctionne que pour l'hôte connecté
    if (CurrentMode != NetworkMode.Host || CurrentState != ConnectionState.Connected) return;

    // Vérifier qu'il y a au moins 1 joueur (pour debug) ou 2 (normal)
    bool allowSinglePlayer = false;

    // Chercher si un GameController existe et s'il autorise le debug single player
    var gameController = FindObjectOfType<BaboonTower.Game.GameController>();
    if (gameController != null)
    {
        allowSinglePlayer = gameController.IsDebugSinglePlayerAllowed;
    }

    int minPlayers = allowSinglePlayer ? 1 : 2;
    if (ConnectedPlayers.Count < minPlayers)
    {
        OnServerMessage?.Invoke($"⚠ Il faut au moins {minPlayers} joueur(s) pour démarrer !");
        return;
    }

    // Considérer l'hôte comme prêt au moment de lancer
    var host = ConnectedPlayers.FirstOrDefault(p => p.isHost);
    if (host != null) host.isReady = true;

    // Si quelqu'un n'est pas prêt -> avertir tout le monde dans le chat (broadcast)
    if (ConnectedPlayers.Count > 1 && ConnectedPlayers.Any(p => !p.isReady))
    {
        BroadcastMessage("SERVER_MESSAGE", "⚠️ Tous les joueurs ne sont pas prêts !");
        OnServerMessage?.Invoke("⚠️ Tous les joueurs ne sont pas prêts !");
        return;
    }

    // IMPORTANT : Log pour debug
    Debug.Log("[NetworkManager] Starting game - Broadcasting GAME_START to all clients");
    
    // Sinon: démarrer la partie (broadcast + callback local)
    BroadcastMessage("GAME_START", "START_NOW");  // Ajout d'un payload pour s'assurer que le message n'est pas vide
    
    // IMPORTANT : Petit délai avant de déclencher localement pour s'assurer que le message est envoyé
    StartCoroutine(DelayedGameStart());
}

private System.Collections.IEnumerator DelayedGameStart()
{
    yield return new WaitForSeconds(0.1f);
    Debug.Log("[NetworkManager] Invoking OnGameStarted for host");
    OnGameStarted?.Invoke();
}


        #region Server Methods (Host)

        private System.Collections.IEnumerator StartServerCoroutine()
        {
            SetConnectionState(ConnectionState.Connecting);

            yield return null;

            try
            {
                IPAddress ip = IPAddress.Any;
                server = new TcpListener(ip, CurrentPort);
                server.Start();
                Application.runInBackground = true;
                Time.timeScale = 1f;
                BaboonTower.Core.ApplicationFocusManager.EnsureServerSettings();
                isRunning = true;

                ConnectedPlayers.Clear();
                ConnectedPlayers.Add(new PlayerData(playerName, 0, true));

                serverThread = new Thread(ServerLoop)
                {
                    IsBackground = false,
                    Priority = System.Threading.ThreadPriority.AboveNormal
                };
                serverThread.Start();

                CurrentMode = NetworkMode.Host;
                SetConnectionState(ConnectionState.Connected);

                BroadcastPlayersUpdate();
                OnServerMessage?.Invoke($"Serveur démarré sur le port {CurrentPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur démarrage serveur: {e.Message}");
                SetConnectionState(ConnectionState.Failed);
            }
        }

        private void ServerLoop()
        {
            while (isRunning && server != null)
            {
                try
                {
                    if (server.Pending())
                    {
                        TcpClient newClient = server.AcceptTcpClient();

                        if (connectedClients.Count < maxPlayers)
                        {
                            var connectedClient = new ConnectedClient(newClient, nextPlayerId++);
                            connectedClients.Add(connectedClient);

                            connectedClient.clientThread = new Thread(() => HandleClient(connectedClient));
                            connectedClient.clientThread.Start();

                            Debug.Log($"Nouveau client connecté. Total: {connectedClients.Count}");
                        }
                        else
                        {
                            newClient.Close();
                        }
                    }

                    Thread.Sleep(8);
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"Erreur serveur: {e.Message}");
                    }
                }
            }
        }

        private void HandleClient(ConnectedClient connectedClient)
        {
            try
            {
                NetworkStream stream = connectedClient.stream;
                byte[] buffer = new byte[4096];

                while (isRunning && connectedClient.isActive)
                {
                    if (connectedClient.tcpClient.Client.Poll(0, SelectMode.SelectRead) && connectedClient.tcpClient.Available == 0)
                    {
                        RemoveClient(connectedClient);
                        break;
                    }

                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            ProcessClientMessage(connectedClient, message);
                        }
                    }

                    Thread.Sleep(16);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur client: {e.Message}");
            }
            finally
            {
                RemoveClient(connectedClient);
            }
        }

        private void ProcessClientMessage(ConnectedClient client, string rawMessage)
        {
            try
            {
                NetworkMessage message = JsonUtility.FromJson<NetworkMessage>(rawMessage);
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    HandleClientMessage(client, message);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur parsing message client: {e.Message}");
            }
        }

        private void HandleClientMessage(ConnectedClient client, NetworkMessage message)
        {
            switch (message.messageType)
            {
                case "JOIN_LOBBY":
                    client.playerData.playerName = message.data;
                    {
                        var playerData = new PlayerData(message.data, client.playerData.playerId);
                        ConnectedPlayers.Add(playerData);
                        BroadcastPlayersUpdate();
                        
                        // Envoyer la configuration des vagues au nouveau client
                        if (currentWaveConfig != null)
                        {
                            string configJson = JsonUtility.ToJson(currentWaveConfig);
                            SendMessageToClient(client, "WAVE_CONFIG_UPDATE", configJson);
                        }
                    }
                    break;
				case "REQUEST_GAME_STATE":
					{
        // Un client demande l'état du jeu
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene")
        {
            SendMessageToClient(client, "FORCE_GAME_START", "");
        }
        break;
    }
                case "PLAYER_READY":
                    {
                        var player = ConnectedPlayers.FirstOrDefault(p => p.playerId == client.playerData.playerId);
                        if (player != null)
                        {
                            player.isReady = bool.Parse(message.data);
                            BroadcastPlayersUpdate();
                        }
                        break;
                    }

                case "CHAT":
                    {
                        string clean = SanitizeChat(message.data);
                        string author = string.IsNullOrEmpty(client.playerData.playerName)
                            ? "Player" + client.playerData.playerId
                            : client.playerData.playerName;

                        string payload = $"{author}|{clean}";
                        BroadcastMessageExcept(client, "CHAT", payload);
                        OnChatMessage?.Invoke(author, clean);
                        break;
                    }

                case "SPEND_GOLD_REQUEST":
                    {
                        // Transférer la demande au GameController
                        OnGameMessage?.Invoke("SPEND_GOLD_REQUEST", message.data);
                        break;
                    }

                case "PLAYER_ACTION":
                    {
                        // Actions du joueur (placement de tours, etc.)
                        string payload = $"{client.playerData.playerId}|{message.data}";
                        OnGameMessage?.Invoke("PLAYER_ACTION", payload);
                        break;
                    }
                    
				case "WAVE_COMPLETED":  // AJOUT IMPORTANT !
					{
						// Un client a terminé sa vague
						Debug.Log($"[NetworkManager Host] Client {client.playerData.playerName} sent WAVE_COMPLETED: {message.data}");
						
						// Transférer au GameController/WaveManager
						OnGameMessage?.Invoke("WAVE_COMPLETED", message.data);
						break;
					}
				case "GOLD_REQUEST":
				case "DAMAGE_REQUEST":
					// Transférer ces demandes au GameController
				OnGameMessage?.Invoke(message.messageType, message.data);
				break;
                case "DISCONNECT":
                    RemoveClient(client);
                    break;
            }
        }

        private void RemoveClient(ConnectedClient client)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                ConnectedPlayers.RemoveAll(p => p.playerId == client.playerData.playerId);
                connectedClients.Remove(client);

                try { client.stream?.Close(); } catch { }
                try { client.tcpClient?.Close(); } catch { }

                client.isActive = false;

                BroadcastPlayersUpdate();

                Debug.Log($"Client déconnecté. Total: {connectedClients.Count}");
            });
        }
        
        private void SendMessageToClient(ConnectedClient client, string messageType, string data)
        {
            NetworkMessage message = new NetworkMessage(messageType, data);
            string json = JsonUtility.ToJson(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            
            try
            {
                if (client.tcpClient.Connected)
                {
                    client.stream.Write(bytes, 0, bytes.Length);
                    client.stream.Flush();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending message to client: {e.Message}");
            }
        }

        // Broadcast à tous SAUF 'except'
        private void BroadcastMessageExcept(ConnectedClient except, string messageType, string data)
        {
            NetworkMessage message = new NetworkMessage(messageType, data);
            string json = JsonUtility.ToJson(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            var clientsToRemove = new List<ConnectedClient>();

            foreach (var c in connectedClients)
            {
                if (c == except) continue;
                try
                {
                    if (c.tcpClient.Connected)
                    {
                        c.stream.Write(bytes, 0, bytes.Length);
                        c.stream.Flush();
                    }
                    else
                    {
                        clientsToRemove.Add(c);
                    }
                }
                catch
                {
                    clientsToRemove.Add(c);
                }
            }

            foreach (var c in clientsToRemove)
            {
                RemoveClient(c);
            }
        }

        private void BroadcastPlayersUpdate()
        {
            string playersJson = JsonUtility.ToJson(new SerializableList<PlayerData>(ConnectedPlayers));
            BroadcastMessage("PLAYERS_UPDATE", playersJson);
            OnPlayersUpdated?.Invoke(ConnectedPlayers);
        }

        private void StopServer()
        {
            try
            {
                foreach (var c in connectedClients)
                {
                    c.isActive = false;
                    try { c.stream?.Close(); } catch { }
                    try { c.tcpClient?.Close(); } catch { }
                }
                connectedClients.Clear();

                try { server?.Stop(); } catch { }
                server = null;

                if (serverThread != null && serverThread.IsAlive)
                {
                    serverThread.Join(1000);
                }
                serverThread = null;

                ConnectedPlayers.Clear();
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur arrêt serveur: {e.Message}");
            }
        }

        #endregion

        #region Client Methods

        private System.Collections.IEnumerator ConnectToServerCoroutine(string serverIP)
        {
            SetConnectionState(ConnectionState.Connecting);

            yield return null;

            try
            {
                client = new TcpClient();
                client.Connect(IPAddress.Parse(serverIP), CurrentPort);
                clientStream = client.GetStream();

                isRunning = true;

                clientThread = new Thread(ClientLoop);
                clientThread.Start();

                CurrentMode = NetworkMode.Client;
                SetConnectionState(ConnectionState.Connected);

                SendMessageToServer("JOIN_LOBBY", playerName);
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur connexion: {e.Message}");
                SetConnectionState(ConnectionState.Failed);
            }
        }

private void ClientLoop()
{
    byte[] buffer = new byte[4096];
    float lastStateCheck = 0;

    while (isRunning && client != null && client.Connected)
    {
        try
        {
            // Vérification périodique de l'état (toutes les 5 secondes)
            if (Time.time - lastStateCheck > 5f)
            {
                lastStateCheck = Time.time;
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lobby")
                {
                    SendMessageToServer("REQUEST_GAME_STATE", "");
                }
            }

            if (clientStream.DataAvailable)
            {
                int bytesRead = clientStream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ProcessServerMessage(message);
                }
            }

            Thread.Sleep(16);
        }
        catch (Exception e)
        {
                    if (isRunning)
                    {
                        Debug.LogError($"Erreur client: {e.Message}");
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            SetConnectionState(ConnectionState.Failed);
                        });
                        break;
                    }
                }
            }
        }

        private void ProcessServerMessage(string message)
        {
            try
            {
                NetworkMessage netMsg = JsonUtility.FromJson<NetworkMessage>(message);

                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    HandleServerMessage(netMsg);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur parsing message serveur: {e.Message}");
            }
        }

private void HandleServerMessage(NetworkMessage message)
{
    Debug.Log($"[NetworkManager Client] Received message type: {message.messageType}, data: {message.data}");
    
    switch (message.messageType)
    {
        case "PLAYERS_UPDATE":
            UpdatePlayersList(message.data);
            break;

        case "GAME_START":
            Debug.Log("[NetworkManager Client] GAME_START received! Invoking OnGameStarted event");
            OnGameStarted?.Invoke();
            break;
            
        case "FORCE_GAME_START":
            Debug.Log("[NetworkManager Client] Received FORCE_GAME_START - Loading game scene");
            OnGameStarted?.Invoke();
            break;
            
        case "SERVER_MESSAGE":
            OnServerMessage?.Invoke(message.data);
            break;

        case "CHAT":
            {
                var parts = (message.data ?? "").Split(new char[] { '|' }, 2);
                string author = parts.Length > 0 ? parts[0] : "???";
                string text = parts.Length > 1 ? parts[1] : "";
                OnChatMessage?.Invoke(author, text);
                break;
            }
                    
        case "WAVE_CONFIG_UPDATE":
            {
                // Sauvegarder la configuration des vagues
                PlayerPrefs.SetString("WaveConfiguration", message.data);
                PlayerPrefs.Save();
                
                try
                {
                    currentWaveConfig = JsonUtility.FromJson<WaveConfigurationMessage>(message.data);
                    Debug.Log("[NetworkManager] Wave configuration received and saved");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkManager] Error parsing wave configuration: {e.Message}");
                }
                
                // Transférer au LobbyController ou GameController
                OnGameMessage?.Invoke(message.messageType, message.data);
                break;
            }
        case "WAVE_START_SYNC":
            Debug.Log("[NetworkManager Client] Received WAVE_START_SYNC - Forwarding to GameController");
            OnGameMessage?.Invoke(message.messageType, message.data);
            break;
        // IMPORTANT : Ajouter ces messages pour les ennemis
        case "ENEMY_SPAWN":
        case "ENEMIES_SYNC":
        case "ENEMY_DEATH":
        case "FIRST_FINISHER":
        case "NEXT_WAVE_TIMER":
        case "GAME_STATE_UPDATE":
        case "WAVE_STARTED":
        case "GAME_TIMER":
        case "PLAYERS_STATES":
        case "PLAYER_ELIMINATED":
        case "GAME_WINNER":
            {
                // Transférer TOUS ces messages au GameController ou WaveManager
                OnGameMessage?.Invoke(message.messageType, message.data);
                break;
            }
case "WAVE_COMPLETED":
    {
        // Transférer directement au WaveManager via GameController
        OnGameMessage?.Invoke(message.messageType, message.data);
        break;
    }
        case "DISCONNECT":
            SetConnectionState(ConnectionState.Disconnected);
            break;
    }
}

        private void UpdatePlayersList(string playersData)
        {
            try
            {
                var listWrapper = JsonUtility.FromJson<SerializableList<PlayerData>>(playersData);
                ConnectedPlayers = listWrapper.items;

                OnPlayersUpdated?.Invoke(ConnectedPlayers);
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur parsing players list: {e.Message}");
            }
        }

        private void SendMessageToServer(string type, string data)
        {
            try
            {
                NetworkMessage message = new NetworkMessage(type, data);
                string json = JsonUtility.ToJson(message);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                clientStream.Write(bytes, 0, bytes.Length);
                clientStream.Flush();
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur envoi message: {e.Message}");
                SetConnectionState(ConnectionState.Failed);
            }
        }

        private void DisconnectClient()
        {
            try
            {
                if (CurrentState == ConnectionState.Connected)
                {
                    SendMessageToServer("DISCONNECT", playerName);
                }

                try { clientStream?.Close(); } catch { }
                try { client?.Close(); } catch { }

                if (clientThread != null && clientThread.IsAlive)
                {
                    clientThread.Join(1000);
                }
                clientThread = null;

                ConnectedPlayers.Clear();
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur déconnexion client: {e.Message}");
            }
        }

        #endregion

        #region Utils

        private void SetConnectionState(ConnectionState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                OnConnectionStateChanged?.Invoke(CurrentState);
            }
        }

        private string SanitizeChat(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            text = text.Trim();
            if (text.Length > 200) text = text.Substring(0, 200);
            return text;
        }

        #endregion
    }

    [System.Serializable]
    public class SerializableList<T>
    {
        public List<T> items;

        public SerializableList(List<T> list)
        {
            items = list;
        }
    }

    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher instance;
        private Queue<System.Action> actionQueue = new Queue<System.Action>();

        public static UnityMainThreadDispatcher Instance()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("MainThreadDispatcher");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<UnityMainThreadDispatcher>();
            }
            return instance;
        }

        public void Enqueue(System.Action action)
        {
            lock (actionQueue)
            {
                actionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (actionQueue)
            {
                while (actionQueue.Count > 0)
                {
                    actionQueue.Dequeue().Invoke();
                }
            }
        }
    }
}