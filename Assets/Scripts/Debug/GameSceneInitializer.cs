using UnityEngine;

namespace BaboonTower.Game
{
    /// <summary>
    /// S'assure que tous les managers n�cessaires sont pr�sents dans la sc�ne Game
    /// </summary>
    public class GameSceneInitializer : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("GameSceneInitializer - Checking for required managers...");

            // V�rifier et cr�er MapManager si n�cessaire

            // V�rifier et cr�er WaveManager si n�cessaire
            if (FindObjectOfType<WaveManager>() == null)
            {
                GameObject waveManagerObj = new GameObject("WaveManager");
                waveManagerObj.AddComponent<WaveManager>();
                Debug.Log("Created WaveManager");
            }

            // V�rifier GameController (devrait d�j� exister)
            if (FindObjectOfType<GameController>() == null)
            {
                Debug.LogError("GameController not found! This is required in the GameScene!");
            }

            Debug.Log("GameSceneInitializer - All managers checked");
        }
    }
}