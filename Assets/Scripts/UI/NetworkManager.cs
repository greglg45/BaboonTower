using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System;
using System.Linq;

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

        // (Pas de [Header] ici : ce sont des propriétés non sérialisées)
        public NetworkMode CurrentMode { get; private set; } = NetworkMode.None;
        public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;

        [Header("Player")]
        [SerializeField] private string playerName = "Player";

        // (Pas de [Header] ici non plus : events != champs sérialisés)
        public event Action<ConnectionState> OnConnectionStateChanged;
        public event Action<List<PlayerData>> OnPlayersUpdated;
        public event Action<string> OnServerMessage;
        public event Action OnGameStarted;
        public event Action<string, string> OnChatMessage;

        private TcpListener server;
        private List<ConnectedClient> connectedClients = new List<ConnectedClient>();
        private int nextPlayerId = 1;

        private TcpClient client;
        private NetworkStream clientStream;
        private Thread clientThread;

        private Thread serverThread;

        public List<PlayerData> ConnectedPlayers { get; private set; } = new List<PlayerData>();

        private bool isRunning = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            playerName = PlayerPrefs.GetString("PlayerName", playerName);
        }

        #region Public API

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
        /// Envoi de message chat.
        /// - Host : broadcast direct + feedback local
        /// - Client : send to server + écho local (le serveur NE renvoie PAS à l’émetteur)
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

        #endregion

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

                isRunning = true;

                ConnectedPlayers.Clear();
                ConnectedPlayers.Add(new PlayerData(playerName, 0, true));

                serverThread = new Thread(ServerLoop);
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
                    }
                    break;

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
                        BroadcastMessageExcept(client, "CHAT", payload); // pas de renvoi à l’émetteur
                        OnChatMessage?.Invoke(author, clean); // feedback host
                        break;
                    }

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

        private void BroadcastMessage(string messageType, string data)
        {
            NetworkMessage message = new NetworkMessage(messageType, data);
            string json = JsonUtility.ToJson(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            var clientsToRemove = new List<ConnectedClient>();

            foreach (var client in connectedClients)
            {
                try
                {
                    if (client.tcpClient.Connected)
                    {
                        client.stream.Write(bytes, 0, bytes.Length);
                        client.stream.Flush();
                    }
                    else
                    {
                        clientsToRemove.Add(client);
                    }
                }
                catch
                {
                    clientsToRemove.Add(client);
                }
            }

            foreach (var client in clientsToRemove)
            {
                RemoveClient(client);
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

            while (isRunning && client != null && client.Connected)
            {
                try
                {
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
            switch (message.messageType)
            {
                case "PLAYERS_UPDATE":
                    UpdatePlayersList(message.data);
                    break;

                case "GAME_START":
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
        public void HostTryStartGame()
        {
            // Ne fonctionne que pour l'hôte connecté
            if (CurrentMode != NetworkMode.Host || CurrentState != ConnectionState.Connected) return;

            // Considérer l'hôte comme prêt au moment de lancer
            var host = ConnectedPlayers.FirstOrDefault(p => p.isHost);
            if (host != null) host.isReady = true;

            // Si quelqu'un n'est pas prêt -> avertir tout le monde dans le chat (broadcast)
            if (ConnectedPlayers.Any(p => !p.isReady))
            {
                // Envoi aux clients + affichage local
                BroadcastMessage("SERVER_MESSAGE", "⚠️ Tous les joueurs ne sont pas prêts !");
                OnServerMessage?.Invoke("⚠️ Tous les joueurs ne sont pas prêts !");
                return;
            }

            // Sinon: démarrer la partie (broadcast + callback local)
            BroadcastMessage("GAME_START", "");
            OnGameStarted?.Invoke();
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

    /// <summary>
    /// Utilitaire pour exécuter du code sur le thread principal
    /// </summary>
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
