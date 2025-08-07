using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BaboonTower
{
    /// <summary>
    /// Gestionnaire de version pour Baboon Tower
    /// Lit la version depuis un fichier version.txt ou utilise une version par défaut
    /// </summary>
    public static class VersionManager
    {
        private const string VERSION_FILE = "version.txt";
        private const string DEFAULT_VERSION = "1.0.0";

        /// <summary>
        /// Récupère la version actuelle du jeu
        /// </summary>
        /// <returns>Numéro de version au format v1.0.0</returns>
        public static string GetVersion()
        {
            string version = DEFAULT_VERSION;

            try
            {
                // Chercher le fichier version.txt dans StreamingAssets
                string versionPath = Path.Combine(Application.streamingAssetsPath, VERSION_FILE);

                if (File.Exists(versionPath))
                {
                    string fileContent = File.ReadAllText(versionPath).Trim();
                    if (!string.IsNullOrEmpty(fileContent))
                    {
                        version = fileContent;
                    }
                }
                else
                {
                    Debug.LogWarning($"Version file not found at {versionPath}, using default version {DEFAULT_VERSION}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error reading version file: {e.Message}. Using default version {DEFAULT_VERSION}");
            }

            // S'assurer que la version commence par 'v'
            if (!version.StartsWith("v"))
            {
                version = "v" + version;
            }

            return version;
        }

        /// <summary>
        /// Récupère la version sans le préfixe 'v'
        /// </summary>
        /// <returns>Numéro de version sans préfixe</returns>
        public static string GetVersionNumber()
        {
            string version = GetVersion();
            return version.StartsWith("v") ? version.Substring(1) : version;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Méthode utilitaire pour créer/mettre à jour le fichier de version (Editor only)
        /// </summary>
        [MenuItem("Baboon Tower/Update Version")]
        public static void UpdateVersion()
        {
            string currentVersion = GetVersionNumber();
            string newVersion = EditorInputDialog.Show("Update Version", "Enter new version:", currentVersion);

            if (!string.IsNullOrEmpty(newVersion) && newVersion != currentVersion)
            {
                SetVersion(newVersion);
            }
        }

        private static void SetVersion(string newVersion)
        {
            try
            {
                // S'assurer que le dossier StreamingAssets existe
                string streamingAssetsPath = Application.streamingAssetsPath;
                if (!Directory.Exists(streamingAssetsPath))
                {
                    Directory.CreateDirectory(streamingAssetsPath);
                }

                string versionPath = Path.Combine(streamingAssetsPath, VERSION_FILE);

                // Retirer le 'v' s'il existe pour le stockage
                string cleanVersion = newVersion.StartsWith("v") ? newVersion.Substring(1) : newVersion;

                File.WriteAllText(versionPath, cleanVersion);

                Debug.Log($"Version updated to {cleanVersion} in {versionPath}");

                // Rafraîchir l'Asset Database
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error updating version file: {e.Message}");
            }
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Classe utilitaire pour afficher des dialogs d'input en mode éditeur
    /// </summary>
    public static class EditorInputDialog
    {
        public static string Show(string title, string message, string defaultValue = "")
        {
            // Utiliser une fenêtre simple avec EditorUtility.DisplayDialog pour la simplicité
            // Pour un vrai input field, il faudrait créer une EditorWindow personnalisée

            bool result = EditorUtility.DisplayDialog(
                title,
                $"{message}\n\nCurrent: {defaultValue}\n\nClick OK to increment patch version, Cancel to keep current.",
                "Increment Patch",
                "Cancel"
            );

            if (result)
            {
                // Auto-incrémenter la version patch (x.x.X)
                return IncrementPatchVersion(defaultValue);
            }

            return string.Empty;
        }

        private static string IncrementPatchVersion(string version)
        {
            try
            {
                var parts = version.Split('.');
                if (parts.Length == 3)
                {
                    if (int.TryParse(parts[2], out int patch))
                    {
                        return $"{parts[0]}.{parts[1]}.{patch + 1}";
                    }
                }
            }
            catch
            {
                // Si erreur, retourner version par défaut incrémentée
            }

            return "1.0.1";
        }
    }
#endif
}