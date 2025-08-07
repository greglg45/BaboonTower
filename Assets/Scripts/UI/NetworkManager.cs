using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace BaboonTower.Network
{
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
        
        public PlayerData(string name, int id)
        {
            playerName = name;
            playerId = id;
            isReady = false;
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

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Network Configuration")]
        [SerializeField] private int defaultPort = 7777;
        [SerializeField] private float heartbeatInterval = 5f;
        [SerializeField] private float connectionTimeout = 10f;

        [Header("Player Settings")]
        [SerializeField] private string playerName = "Player";

        // Events
        public static event Action<ConnectionState> OnConnectionStateChanged;
        public static event Action<List<PlayerData>> OnPlayersUpdated;
        public static event Action<string> OnServerMessage;
        public static event Action OnGameStarted;

        // Network properties
        public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;
        public List<PlayerData> ConnectedPlayers { get; private set; } = new List<PlayerData>();
        public bool IsHost { get; private set; } = false;

        // Network components
        private TcpClient client;
        private NetworkStream stream;
        private Thread networkThread;
        private bool isRunning = false;

        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
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
            DisconnectFromServer();
        }

        private void OnApplicationQuit()
        {
            DisconnectFromServer();
        }

        #region Public Methods

        /// <summary>
        /// Tente de se connecter au serveur avec l'IP spécifiée
        /// </summary>
        public void ConnectToServer(string serverIP)
        {
            if (CurrentState == ConnectionState.Connecting || CurrentState == ConnectionState.Connected)
            {
                Debug.LogWarning("Déjà connecté ou en cours de connexion");
                return;
            }

            StartCoroutine(ConnectCoroutine(serverIP, defaultPort));
        }

        /// <summary>
        /// Déconnexion du serveur
        /// </summary>
        public void DisconnectFromServer()
        {
            isRunning = false;

            try
            {
                // Envoyer message de déconnexion si connecté
                if (CurrentState == ConnectionState.Connected)
                {
                    SendMessage("DISCONNECT", playerName);
                }

                // Fermer les connexions
                stream?.Close();
                client?.Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur lors de la déconnexion: {e.Message}");
            }

            // Attendre la fin du thread
            if (networkThread != null && networkThread.IsAlive)
            {
                networkThread.Join(1000);
            }

            SetConnectionState(ConnectionState.Disconnected);
        }

        /// <summary>
        /// Marquer le joueur comme prêt/pas prêt
        /// </summary>
        public void SetPlayerReady(bool ready)
        {
            if (CurrentState != ConnectionState.Connected) return;

            string readyStatus = ready ? "true" : "false";
            SendMessage("PLAYER_READY", readyStatus);
        }

        /// <summary>
        /// Définir le nom du joueur
        /// </summary>
        public void SetPlayerName(string name)
        {
            playerName = name;
            PlayerPrefs.SetString("PlayerName", playerName);
        }

        /// <summary>
        /// Demander le lancement de la partie (host seulement)
        /// </summary>
        public void StartGame()
        {
            if (CurrentState != ConnectionState.Connected) return;
            
            SendMessage("START_GAME", "");
        }

        #endregion

        #region Private Methods

        private IEnumerator ConnectCoroutine(string serverIP, int port)
        {
            SetConnectionState(ConnectionState.Connecting);
            yield return new WaitForSeconds(0.1f);

            // Exécution de la logique bloquante dans une Task
            bool success = false;
            Exception error = null;

            var thread = new Thread(() =>
            {
                try
                {
                    client = new TcpClient();
                    client.ReceiveTimeout = (int)(connectionTimeout * 1000);
                    client.SendTimeout = (int)(connectionTimeout * 1000);

                    var result = client.BeginConnect(serverIP, port, null, null);
                    bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(connectionTimeout));

                    if (!connected || !client.Connected)
                        throw new Exception("Connexion timeout");

                    stream = client.GetStream();
                    isRunning = true;
                    networkThread = new Thread(NetworkLoop);
                    networkThread.Start();

                    success = true;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            thread.Start();

            // Attente que le thread ait terminé
            while (thread.IsAlive)
                yield return null;

            if (success)
            {
                SetConnectionState(ConnectionState.Connected);
                SendMessage("JOIN_LOBBY", playerName);
                Debug.Log($"Connecté au serveur {serverIP}:{port}");
            }
            else
            {
                Debug.LogError($"Erreur de connexion: {error?.Message}");
                SetConnectionState(ConnectionState.Failed);

                try { stream?.Close(); client?.Close(); } catch { }
            }
        }


        private void NetworkLoop()
        {
            byte[] buffer = new byte[4096];

            while (isRunning && client != null && client.Connected)
            {
                try
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            ProcessServerMessage(message);
                        }
                    }

                    Thread.Sleep(16); // ~60 FPS
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"Erreur réseau: {e.Message}");
                        UnityMainThreadDispatcher.Instance().Enqueue(() => {
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

                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    HandleServerMessage(netMsg);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur parsing message: {e.Message}");
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

                default:
                    Debug.Log($"Message non géré: {message.messageType}");
                    break;
            }
        }

        private void UpdatePlayersList(string playersData)
        {
            try
            {
                ConnectedPlayers = JsonUtility.FromJson<List<PlayerData>>(playersData);
                OnPlayersUpdated?.Invoke(ConnectedPlayers);
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur mise à jour joueurs: {e.Message}");
            }
        }

        private void SendMessage(string messageType, string data)
        {
            if (CurrentState != ConnectionState.Connected || stream == null)
                return;

            try
            {
                NetworkMessage message = new NetworkMessage(messageType, data);
                string json = JsonUtility.ToJson(message);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            catch (Exception e)
            {
                Debug.LogError($"Erreur envoi message: {e.Message}");
                SetConnectionState(ConnectionState.Failed);
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

    /// <summary>
    /// Utilitaire pour exécuter du code sur le thread principal depuis d'autres threads
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