using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    void Start()
    {
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        CreateBackground(canvasGO.transform);
        CreateButtons(canvasGO.transform);
        CreateVersionFooter(canvasGO.transform);
    }

    void CreateBackground(Transform parent)
    {
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(parent, false);
        var rect = bgGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = bgGO.AddComponent<Image>();
        image.color = Color.black;
    }

    void CreateButtons(Transform parent)
    {
        CreateButton(parent, "Rejoindre le lobby", new Vector2(0, 40), OnJoinLobby);
        CreateButton(parent, "Options", new Vector2(0, -10), OnOptions);
        CreateButton(parent, "Quitter", new Vector2(0, -60), OnQuit);
    }

    void CreateButton(Transform parent, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var buttonGO = new GameObject(label);
        buttonGO.transform.SetParent(parent, false);
        var rect = buttonGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 40);
        rect.anchoredPosition = anchoredPos;

        var image = buttonGO.AddComponent<Image>();
        image.color = Color.white;

        var button = buttonGO.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textGO.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
    }

    void CreateVersionFooter(Transform parent)
    {
        var textGO = new GameObject("Version");
        textGO.transform.SetParent(parent, false);
        var rect = textGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 20);
        var text = textGO.AddComponent<Text>();
        text.text = VersionInfo.Version;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.gray;
    }

    void OnJoinLobby()
    {
        // Logique à définir
    }

    void OnOptions()
    {
        // Fenêtre d'options à implémenter
    }

    void OnQuit()
    {
        Application.Quit();
    }
}
