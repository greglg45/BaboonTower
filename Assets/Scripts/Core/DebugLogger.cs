using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text;
using BaboonTower.Network;

namespace BaboonTower.Core
{
    /// <summary>
    /// DebugLogger - Système de logs persistants pour debug en build
    /// </summary>
    public class DebugLogger : MonoBehaviour
    {
        private static DebugLogger instance;
        private string logFilePath;
        private StreamWriter logWriter;
        private Queue<string> recentLogs = new Queue<string>();
        private const int MAX_RECENT_LOGS = 100;
        
        [Header("Settings")]
        [SerializeField] private bool enableFileLogging = true;
        [SerializeField] private bool showInGameConsole = true;
        [SerializeField] private KeyCode consoleToggleKey = KeyCode.F9;
        
        [Header("UI")]
        [SerializeField] private bool showConsole = false;
        private Vector2 scrollPosition;
        private Rect windowRect = new Rect(20, 20, 600, 400);
        private string logText = "";
        
        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeLogger();
            
            // S'abonner aux logs Unity
            Application.logMessageReceived += HandleUnityLog;
        }
        
        private void InitializeLogger()
        {
            // Créer le dossier de logs
            string logsFolder = Path.Combine(Application.persistentDataPath, "Logs");
            if (!Directory.Exists(logsFolder))
            {
                Directory.CreateDirectory(logsFolder);
            }
            
            // Créer le fichier de log avec timestamp
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string playerType = NetworkManager.Instance?.CurrentMode.ToString() ?? "Unknown";
            logFilePath = Path.Combine(logsFolder, $"BaboonTower_{playerType}_{timestamp}.txt");
            
            if (enableFileLogging)
            {
                try
                {
                    logWriter = new StreamWriter(logFilePath, true);
                    logWriter.AutoFlush = true;
                    
                    WriteLog("=== BABOON TOWER DEBUG LOG ===");
                    WriteLog($"Date: {DateTime.Now}");
                    WriteLog($"Unity Version: {Application.unityVersion}");
                    WriteLog($"Platform: {Application.platform}");
                    WriteLog($"Network Mode: {playerType}");
                    WriteLog($"Log Path: {logFilePath}");
                    WriteLog("================================\n");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to create log file: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// HandleUnityLog - Capture tous les logs Unity
        /// </summary>
        private void HandleUnityLog(string logString, string stackTrace, LogType type)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string playerInfo = GetPlayerInfo();
            string formattedLog = $"[{timestamp}] [{type}] {playerInfo} {logString}";
            
            // Écrire dans le fichier
            if (enableFileLogging && logWriter != null)
            {
                WriteLog(formattedLog);
                
                // Ajouter la stack trace pour les erreurs
                if (type == LogType.Error || type == LogType.Exception)
                {
                    WriteLog($"Stack Trace:\n{stackTrace}");
                }
            }
            
            // Ajouter aux logs récents pour la console
            AddToRecentLogs(formattedLog);
            
            // Mettre à jour le texte de la console
            UpdateConsoleText();
        }
        
        private string GetPlayerInfo()
        {
            var net = NetworkManager.Instance;
            if (net == null) return "[No Network]";
            
            string mode = net.CurrentMode.ToString();
            string playerName = "Unknown";
            
            if (net.ConnectedPlayers != null && net.ConnectedPlayers.Count > 0)
            {
                if (net.CurrentMode == NetworkMode.Host)
                {
                    var host = net.ConnectedPlayers.Find(p => p.isHost);
                    playerName = host?.PlayerName ?? "Host";
                }
                else if (net.CurrentMode == NetworkMode.Client)
                {
                    var client = net.ConnectedPlayers.Find(p => !p.isHost);
                    playerName = client?.PlayerName ?? "Client";
                }
            }
            
            return $"[{mode}:{playerName}]";
        }
        
        private void WriteLog(string message)
        {
            try
            {
                logWriter?.WriteLine(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to write log: {e.Message}");
            }
        }
        
        private void AddToRecentLogs(string log)
        {
            recentLogs.Enqueue(log);
            
            while (recentLogs.Count > MAX_RECENT_LOGS)
            {
                recentLogs.Dequeue();
            }
        }
        
        private void UpdateConsoleText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string log in recentLogs)
            {
                sb.AppendLine(log);
            }
            logText = sb.ToString();
        }
        
        private void Update()
        {
            // Toggle console avec F9
            if (Input.GetKeyDown(consoleToggleKey))
            {
                showConsole = !showConsole;
            }
            
            // Ouvrir le dossier de logs avec F10
            if (Input.GetKeyDown(KeyCode.F10))
            {
                OpenLogsFolder();
            }
        }
        
        private void OnGUI()
        {
            if (!showInGameConsole || !showConsole) return;
            
            // Style pour la fenêtre
            GUI.skin.window.fontSize = 12;
            GUI.skin.label.fontSize = 11;
            
            windowRect = GUI.Window(999, windowRect, DrawConsoleWindow, $"Debug Console - {GetPlayerInfo()} (F9 to toggle, F10 for logs folder)");
        }
        
        private void DrawConsoleWindow(int windowID)
        {
            // Zone de scroll pour les logs
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            
            // Afficher les logs avec couleurs selon le type
            string[] lines = logText.Split('\n');
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                
                // Couleur selon le type de log
                if (line.Contains("[Error]") || line.Contains("[Exception]"))
                    GUI.contentColor = Color.red;
                else if (line.Contains("[Warning]"))
                    GUI.contentColor = Color.yellow;
                else if (line.Contains("[WaveManager]") || line.Contains("[GameController]"))
                    GUI.contentColor = Color.cyan;
                else if (line.Contains("[NetworkManager]"))
                    GUI.contentColor = Color.green;
                else
                    GUI.contentColor = Color.white;
                
                GUILayout.Label(line);
            }
            
            GUILayout.EndScrollView();
            
            // Boutons en bas
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Clear", GUILayout.Width(100)))
            {
                recentLogs.Clear();
                logText = "";
            }
            
            if (GUILayout.Button("Open Logs Folder", GUILayout.Width(120)))
            {
                OpenLogsFolder();
            }
            
            if (GUILayout.Button("Copy Path", GUILayout.Width(100)))
            {
                GUIUtility.systemCopyBuffer = logFilePath;
            }
            
            GUILayout.EndHorizontal();
            
            // Permettre de déplacer la fenêtre
            GUI.DragWindow();
        }
        
        private void OpenLogsFolder()
        {
            string logsFolder = Path.Combine(Application.persistentDataPath, "Logs");
            
            #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                System.Diagnostics.Process.Start("explorer.exe", logsFolder.Replace('/', '\\'));
            #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                System.Diagnostics.Process.Start("open", logsFolder);
            #elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
                System.Diagnostics.Process.Start("xdg-open", logsFolder);
            #endif
            
            Debug.Log($"Logs folder: {logsFolder}");
        }
        
        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleUnityLog;
            
            if (logWriter != null)
            {
                WriteLog("\n=== SESSION ENDED ===");
                WriteLog($"End Time: {DateTime.Now}");
                logWriter.Close();
                logWriter.Dispose();
            }
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                WriteLog("=== APPLICATION PAUSED ===");
            }
            else
            {
                WriteLog("=== APPLICATION RESUMED ===");
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            WriteLog($"=== APPLICATION FOCUS: {hasFocus} ===");
        }
        
        /// <summary>
        /// LogCustom - Pour logger des messages custom importants
        /// </summary>
        public static void LogCustom(string category, string message, LogType logType = LogType.Log)
        {
            string formattedMessage = $"[{category}] {message}";
            
            switch (logType)
            {
                case LogType.Error:
                    Debug.LogError(formattedMessage);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;
                default:
                    Debug.Log(formattedMessage);
                    break;
            }
        }
    }
}