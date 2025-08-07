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
        Starting,
        Listening,
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

        public NetworkMessage(string type, string content)
        {
            messageType = type;
            data = content;
        }
    }

    public class ConnectedClient
    {
        public TcpClient tcpClient;
        public NetworkStream stream;
        public PlayerData playerData;
        public Thread clientThread;
        public bool isActive = true;

        public ConnectedClient(TcpClient client, int playerId)
        {
            tcpClient = client;
            stream = client.GetStream();
            playerData = new PlayerData("", playerId);
        }
    }

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Network Configuration")]
        [SerializeField] private int defaultPort = 7777;
        [SerializeField] private float heartbeatInterval = 5f;
        [SerializeField] private float connectionTimeout = 10f;
        [SerializeField] private int maxPlayers = 8;

        [Header("Player Settings")]
        [SerializeField] private string playerName = "Player";

        // Events
        public static event Action<ConnectionState> OnConnectionStateChanged;
        public static event Action<List<PlayerData>> OnPlayersUpdated;
        public static event Action<string> OnServerMessage;
        public static event Action OnGameStarted;

        // Network properties
        public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;
        public NetworkMode CurrentMode { get; private set; } = NetworkMode.None;
        public List<PlayerData> ConnectedPlayers { get; private set; } = new List<PlayerData>();
        public bool IsHost => CurrentMode == NetworkMode.Host;
        public string LocalIPAddress { get; private set; }

        // Server components (Host mode)
        private TcpListener server;
        private Thread serverThread;
        private List<ConnectedClient> connectedClients = new List<ConnectedClient>();
        private int nextPlayerId = 1;

        // Client components (Client mode)
        private TcpClient client;
        private NetworkStream clientStream;
        private Thread clientThread;

        private bool isRunning = false;

        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                GetLocalIPAddress();
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Load player name from PlayerPrefs
            playerName = PlayerPrefs.GetString("PlayerName", "Player" + UnityEngine.Random.Range(1000, 9999));
        }

        private void OnDestroy()
        {
            StopNetworking();
        }

        private void OnApplicationQuit()
        {
            StopNetworking();
        }

        #region Public Methods

        /// <summary>
        /// Démarre un serveur (mode Host)
        /// </summary>
        public void StartServer()
        {
            if (CurrentState != ConnectionState.Disconnected)
            {
                Debug.LogWarning("Network déjà actif");
                return;
            }

            StartCoroutine(StartServerCoroutine());
        }

        /// <summary>
        /// Se connecte à un serveur (mode Client)
        /// </summary>
        public void ConnectToServer(string serverIP)
        {
            if (CurrentState != ConnectionState.Disconnected)
            {
                Debug.LogWarning("Network déjà actif");
                return;
            }

            StartCoroutine(ConnectToServerCoroutine(serverIP));
        }

        /// <summary>
        /// Arrête le networking (serveur ou client)
        /// </summary>
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

        /// <summary>
        /// Marquer le joueur comme prêt/pas prêt
        /// </summary>
        public void SetPlayerReady(bool ready)
        {
            if (CurrentState != ConnectionState.Connected) return;

            if (IsHost)
            {
                // Host local
                var hostPlayer = ConnectedPlayers.FirstOrDefault(p => p.isHost);
                if (hostPlayer != null)
                {
                    hostPlayer.isReady = ready;
                    BroadcastPlayersUpdate();
                }
            }
            else
            {
                // Client
                SendMessageToServer("PLAYER_READY", ready.ToString());
            }
        }

        /// <summary>
        /// Démarrer la partie (host seulement)
        /// </summary>
        public void StartGame()
        {
            if (!IsHost || CurrentState != ConnectionState.Connected) return;

            // Vérifier que tous les joueurs sont prêts
            if (ConnectedPlayers.All(p => p.isReady))
            {
                BroadcastMessage("GAME_START", "");
                OnGameStarted?.Invoke();
            }
            else
            {
                BroadcastMessage("SERVER_MESSAGE", "Tous les joueurs doivent être prêts !");
            }
        }

        /// <summary>
        /// Définir le nom du joueur
        /// </summary>
        public void SetPlayerName(string name)
        {
            playerName = name;
            PlayerPrefs.SetString("PlayerName", playerName);
        }

        #endregion

        #region Server Methods (Host)

        private System.Collections.IEnumerator StartServerCoroutine()
        {
            SetConnectionState(ConnectionState.Starting);
            CurrentMode = NetworkMode.Host;

            yield return new WaitForSeconds(0.1f);

            try
            {
                // Créer le serveur TCP
                server = new TcpListener(IPAddress.Any, defaultPort);
                server.Start();

                SetConnectionState(ConnectionState.Listening);

                // Ajouter le host à la liste des joueurs
                var hostPlayer = new PlayerData(playerName, 0, true);
                ConnectedPlayers.Add(hostPlayer);
                OnPlayersUpdated?.Invoke(ConnectedPlayers);

                // Démarrer le thread serveur
                isRunning = true;
                serverThread = new Thread(ServerLoop);
                serverThread.Start();

                SetConnectionState(ConnectionState.Connected);
                Debug.Log($"Serveur démarré sur {LocalIPAddress}:{defaultPort}");
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
                    // Accepter de nouvelles connexions
                    if (server.Pending())
                    {
                        TcpClient newClient = server.AcceptTcpClient();

                        if (connectedClients.Count < maxPlayers)
                        {
                            var connectedClient = new ConnectedClient(newClient, nextPlayerId++);
                            connectedClients.Add(connectedClient);

                            // Démarrer le thread pour ce client
                            connectedClient.clientThread = new Thread(() => HandleClient(connectedClient));
                            connectedClient.clientThread.Start();

                            Debug.Log($"Nouveau client connecté. Total: {connectedClients.Count}");
                        }
                        else
                        {
                            // Refuser la connexion (serveur plein)
                            newClient.Close();
                        }
                    }

                    Thread.Sleep(100);
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
            byte[] buffer = new byte[4096];

            try
            {
                while (isRunning && connectedClient.isActive && connectedClient.tcpClient.Connected)
                {
                    if (connectedClient.stream.DataAvailable)
                    {
                        int bytesRead = connectedClient.stream.Read(buffer, 0, buffer.Length);
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

        private void ProcessClientMessage(ConnectedClient client, string message)
        {
            try
            {
                NetworkMessage netMsg = JsonUtility.FromJson<NetworkMessage>(message);

                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    HandleClientMessage(client, netMsg);
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

                    // Ajouter à la liste des joueurs
                    var playerData = new PlayerData(message.data, client.playerData.playerId);
                    ConnectedPlayers.Add(playerData);

                    // Envoyer la liste mise à jour à tous
                    BroadcastPlayersUpdate();
                    break;

                case "PLAYER_READY":
                    var player = ConnectedPlayers.FirstOrDefault(p => p.playerId == client.playerData.playerId);
                    if (player != null)
                    {
                        player.isReady = bool.Parse(message.data);
                        BroadcastPlayersUpdate();
                    }
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
                // Supprimer de la liste des joueurs
                ConnectedPlayers.RemoveAll(p => p.playerId == client.playerData.playerId);

                // Supprimer de la liste des clients connectés
                connectedClients.Remove(client);

                // Fermer la connexion
                try
                {
                    client.stream?.Close();
                    client.tcpClient?.Close();
                }
                catch { }

                client.isActive = false;

                // Mettre à jour la liste des joueurs
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

            // Nettoyer les clients déconnectés
            foreach (var client in clientsToRemove)
            {
                RemoveClient(client);
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
                // Fermer toutes les connexions clients
                foreach (var client in connectedClients)
                {
                    client.isActive = false;
                    client.stream?.Close();
                    client.tcpClient?.Close();
                }
                connectedClients.Clear();

                // Fermer le serveur
                server?.Stop();

                // Attendre la fin du thread serveur
                if (serverThread != null && serverThread.IsAlive)
                {
                    serverThread.Join(1000);
                }

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
            CurrentMode = NetworkMode.Client;

            yield return new WaitForSeconds(0.1f);

            client = new TcpClient();
            client.ReceiveTimeout = (int)(connectionTimeout * 1000);
            client.SendTimeout = (int)(connectionTimeout * 1000);

            var connectTask = client.ConnectAsync(serverIP, defaultPort);
            float timer = 0f;

            while (!connectTask.IsCompleted && timer < connectionTimeout)
            {
                timer += Time.deltaTime;
                yield return null;  // <--- en dehors du try
            }

            try
            {
                if (!connectTask.IsCompleted || !client.Connected)
                    throw new Exception("Connexion timeout");

                clientStream = client.GetStream();
                SetConnectionState(ConnectionState.Connected);

                isRunning = true;
                clientThread = new Thread(ClientLoop);
                clientThread.Start();

                SendMessageToServer("JOIN_LOBBY", playerName);

                Debug.Log($"Connecté au serveur {serverIP}:{defaultPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur connexion client: {e.Message}");
                SetConnectionState(ConnectionState.Failed);

                try
                {
                    clientStream?.Close();
                    client?.Close();
                }
                catch { }
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
                Debug.LogError($"Erreur mise à jour joueurs: {e.Message}");
            }
        }

        private void SendMessageToServer(string messageType, string data)
        {
            if (CurrentState != ConnectionState.Connected || clientStream == null)
                return;

            try
            {
                NetworkMessage message = new NetworkMessage(messageType, data);
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

                clientStream?.Close();
                client?.Close();

                if (clientThread != null && clientThread.IsAlive)
                {
                    clientThread.Join(1000);
                }

                ConnectedPlayers.Clear();
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur déconnexion client: {e.Message}");
            }
        }

        #endregion

        #region Utility Methods

        private void GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                LocalIPAddress = host.AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    ?.ToString() ?? "127.0.0.1";
            }
            catch
            {
                LocalIPAddress = "127.0.0.1";
            }
        }

        private void SetConnectionState(ConnectionState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                OnConnectionStateChanged?.Invoke(CurrentState);
            }
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
                instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
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