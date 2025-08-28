using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BaboonTower.Game;

namespace BaboonTower.UI
{
	public class PlayerUIItem : MonoBehaviour
	{
		[Header("UI References")]
		[SerializeField] private TextMeshProUGUI playerNameText;
		[SerializeField] private TextMeshProUGUI goldText;
		[SerializeField] private Image hpFillImage;
		[SerializeField] private Image statusIcon;
		[SerializeField] private Image backgroundImage;

		[Header("Colors")]
		[SerializeField] private Color aliveColor = new Color(0f, 1f, 0f, 1f); // Vert
		[SerializeField] private Color deadColor = new Color(1f, 0f, 0f, 1f); // Rouge
		[SerializeField] private Color localPlayerColor = new Color(1f, 1f, 0f, 1f); // Jaune
		[SerializeField] private Color normalBackgroundColor = new Color(0.17f, 0.24f, 0.31f, 0.6f);
		[SerializeField] private Color localBackgroundColor = new Color(1f, 1f, 0f, 0.3f);
		[SerializeField] private Color deadBackgroundColor = new Color(1f, 0f, 0f, 0.3f);

		[Header("HP Colors")]
		[SerializeField] private Color hpHighColor = new Color(0f, 1f, 0f, 1f); // Vert > 50%
		[SerializeField] private Color hpMediumColor = new Color(1f, 1f, 0f, 1f); // Jaune 25-50%
		[SerializeField] private Color hpLowColor = new Color(1f, 0f, 0f, 1f); // Rouge < 25%

		private PlayerGameState currentPlayerState;
		private bool isCurrentlyLocalPlayer;

		private void Awake()
		{
			// Vérifier que toutes les références sont assignées
			ValidateReferences();
		}

		private void ValidateReferences()
		{
			if (playerNameText == null)
				Debug.LogError("PlayerNameText not assigned in PlayerUIItem!");
			if (goldText == null)
				Debug.LogError("GoldText not assigned in PlayerUIItem!");
			if (hpFillImage == null)
				Debug.LogError("HPFillImage not assigned in PlayerUIItem!");
			if (statusIcon == null)
				Debug.LogError("StatusIcon not assigned in PlayerUIItem!");
			if (backgroundImage == null)
				Debug.LogError("BackgroundImage not assigned in PlayerUIItem!");
		}

		/// <summary>
		/// Met à jour l'affichage avec les informations du joueur
		/// </summary>
		/// <param name="playerState">État du joueur</param>
		/// <param name="isLocalPlayer">Est-ce le joueur local?</param>
		public void UpdatePlayerInfo(PlayerGameState playerState, bool isLocalPlayer = false)
		{
			if (playerState == null) return;

			currentPlayerState = playerState;
			isCurrentlyLocalPlayer = isLocalPlayer;

			UpdatePlayerName();
			UpdateGold();
			UpdateHP();
			UpdateStatus();
			UpdateBackground();
		}

		private void UpdatePlayerName()
		{
			if (playerNameText == null || currentPlayerState == null) return;

			string displayName = currentPlayerState.playerName;

			// Ajouter des indicateurs visuels
			if (isCurrentlyLocalPlayer)
				displayName += " (Moi)";
			if (currentPlayerState.isEliminated)
				displayName += " ☠️";

			playerNameText.text = displayName;

			// Couleur du texte
			if (currentPlayerState.isEliminated)
				playerNameText.color = deadColor;
			else if (isCurrentlyLocalPlayer)
				playerNameText.color = localPlayerColor;
			else
				playerNameText.color = Color.white;
		}

		private void UpdateGold()
		{
			if (goldText == null || currentPlayerState == null) return;

			goldText.text = $"💰 {currentPlayerState.gold}";

			// Couleur basée sur l'état
			if (currentPlayerState.isEliminated)
				goldText.color = deadColor * 0.7f;
			else
				goldText.color = new Color(1f, 0.84f, 0f, 1f); // Doré
		}

		private void UpdateHP()
		{
			if (hpFillImage == null || currentPlayerState == null) return;

			// Calcul du pourcentage de vie
			float hpPercent = currentPlayerState.maxCastleHP > 0
				? (float)currentPlayerState.castleHP / currentPlayerState.maxCastleHP
				: 0f;

			// Mise à jour de la barre
			hpFillImage.fillAmount = hpPercent;

			// Couleur basée sur le pourcentage de vie
			if (hpPercent > 0.5f)
				hpFillImage.color = hpHighColor;
			else if (hpPercent > 0.25f)
				hpFillImage.color = hpMediumColor;
			else
				hpFillImage.color = hpLowColor;

			// Si éliminé, assombrir
			if (currentPlayerState.isEliminated)
				hpFillImage.color *= 0.5f;
		}

		private void UpdateStatus()
		{
			if (statusIcon == null || currentPlayerState == null) return;

			// Couleur et visibilité de l'icône de statut
			if (currentPlayerState.isEliminated)
			{
				statusIcon.color = deadColor;
				statusIcon.enabled = true;
			}
			else if (currentPlayerState.isAlive)
			{
				statusIcon.color = aliveColor;
				statusIcon.enabled = true;
			}
			else
			{
				statusIcon.enabled = false;
			}
		}

		private void UpdateBackground()
		{
			if (backgroundImage == null || currentPlayerState == null) return;

			// Couleur de fond basée sur l'état
			if (currentPlayerState.isEliminated)
				backgroundImage.color = deadBackgroundColor;
			else if (isCurrentlyLocalPlayer)
				backgroundImage.color = localBackgroundColor;
			else
				backgroundImage.color = normalBackgroundColor;
		}

		/// <summary>
		/// Animation simple pour attirer l'attention (par exemple quand le joueur prend des dégâts)
		/// </summary>
		public void AnimateAttention()
		{
			// Simple flash effect
			StartCoroutine(FlashEffect());
		}

		private System.Collections.IEnumerator FlashEffect()
		{
			if (backgroundImage == null) yield break;

			Color originalColor = backgroundImage.color;
			Color flashColor = new Color(1f, 0f, 0f, 0.8f); // Rouge vif

			// Flash vers rouge
			backgroundImage.color = flashColor;
			yield return new WaitForSeconds(0.1f);

			// Retour à la normale
			backgroundImage.color = originalColor;
		}

		/// <summary>
		/// Pour debug/test - génère des données de test
		/// </summary>
		[ContextMenu("Test with Random Data")]
		private void TestWithRandomData()
		{
			var testPlayer = new PlayerGameState(
				Random.Range(1, 100),
				$"TestPlayer{Random.Range(1, 10)}",
				Random.Range(0, 100),
				100
			);

			testPlayer.castleHP = Random.Range(0, 100);
			testPlayer.isEliminated = testPlayer.castleHP <= 0;
			testPlayer.isAlive = !testPlayer.isEliminated;

			UpdatePlayerInfo(testPlayer, Random.value > 0.5f);
		}
	}
}