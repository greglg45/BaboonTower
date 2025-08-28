using UnityEngine;

namespace BaboonTower.Game
{
    /// <summary>
    /// S'assure que tous les managers nécessaires sont présents dans la scène Game
    /// </summary>
    public class GameSceneInitializer : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("GameSceneInitializer - Checking for required managers...");

            // Vérifier et créer MapManager si nécessaire

            // Vérifier et créer WaveManager si nécessaire
            if (FindObjectOfType<WaveManager>() == null)
            {
                GameObject waveManagerObj = new GameObject("WaveManager");
                waveManagerObj.AddComponent<WaveManager>();
                Debug.Log("Created WaveManager");
            }

            // Vérifier GameController (devrait déjà exister)
            if (FindObjectOfType<GameController>() == null)
            {
                Debug.LogError("GameController not found! This is required in the GameScene!");
            }

            Debug.Log("GameSceneInitializer - All managers checked");
        }
    }
}