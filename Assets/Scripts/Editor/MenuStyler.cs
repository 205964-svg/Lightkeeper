using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Aplica o visual "noite e farol" na cena MainMenu aberta, sem recriar nada:
/// só muda cores, bordas, fontes e adiciona o efeito de hover nos botões.
/// Menu: Lightkeeper > Estilizar menu (com a cena MainMenu aberta).
/// </summary>
public static class MenuStyler
{
    private static readonly Color PanelColor = new Color(0.03f, 0.04f, 0.09f, 0.9f);
    private static readonly Color Gold = new Color(1f, 0.8f, 0.35f, 1f);
    private static readonly Color Cream = new Color(1f, 0.94f, 0.8f, 1f);
    private static readonly Color ButtonNormal = new Color(0.1f, 0.12f, 0.2f, 1f);
    private static readonly Color ButtonHighlight = new Color(0.62f, 0.38f, 0.08f, 1f);
    private static readonly Color ButtonPressed = new Color(0.42f, 0.26f, 0.05f, 1f);
    private static readonly Color BodyText = new Color(0.9f, 0.92f, 1f, 1f);

    private const string DropShadowMaterial =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat";

    [MenuItem("Lightkeeper/Estilizar menu")]
    public static void StyleMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu")
        {
            EditorUtility.DisplayDialog("Lightkeeper", "Abra a cena MainMenu antes de estilizar.", "OK");
            return;
        }

        var all = new Dictionary<string, GameObject>();
        var buttons = new List<Button>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (!all.ContainsKey(t.name)) all[t.name] = t.gameObject;
            buttons.AddRange(root.GetComponentsInChildren<Button>(true));
        }

        // Painéis
        foreach (string name in new[] { "MainPanel", "InfoPanel", "RankingPanel" })
            if (all.TryGetValue(name, out GameObject panel)) StylePanel(panel);

        // Títulos
        foreach (string name in new[] { "TxtTitulo", "TxtInfoTitulo", "TxtRankingTitulo" })
            if (all.TryGetValue(name, out GameObject title)) StyleTitle(title.GetComponent<TMP_Text>(), name == "TxtTitulo");

        // Subtítulo logo abaixo do título principal
        if (all.TryGetValue("TxtTitulo", out GameObject mainTitle) && !all.ContainsKey("TxtSubtitulo"))
            CreateSubtitle(mainTitle.transform);

        // Botões
        foreach (Button b in buttons)
        {
            Color accent = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
            if (b.name == "BtnFacil") accent = new Color(0.4f, 0.9f, 0.45f, 0.9f);
            else if (b.name == "BtnDificil") accent = new Color(1f, 0.4f, 0.35f, 0.9f);
            StyleButton(b, accent);
        }

        // Campo do nome
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
                StyleInput(input);

        // Textos longos
        foreach (string name in new[] { "TxtInfo", "TxtRanking" })
            if (all.TryGetValue(name, out GameObject text) && text.TryGetComponent(out TMP_Text tmp))
            {
                Undo.RecordObject(tmp, "Estilizar menu");
                tmp.color = BodyText;
            }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Lightkeeper: menu estilizado ({buttons.Count} botões).");
    }

    private static void StylePanel(GameObject panel)
    {
        if (panel.TryGetComponent(out Image img))
        {
            Undo.RecordObject(img, "Estilizar menu");
            img.color = PanelColor;
        }
        // Painel translúcido + Outline deixaria o fundo marrom (as cópias douradas aparecem através).
        if (panel.TryGetComponent(out Outline outline)) Undo.DestroyObjectImmediate(outline);
    }

    private static void StyleTitle(TMP_Text t, bool main)
    {
        if (t == null) return;
        Undo.RecordObject(t, "Estilizar menu");
        t.color = Color.white;
        t.enableVertexGradient = true;
        var top = new Color(1f, 0.96f, 0.65f);
        var bottom = new Color(1f, 0.58f, 0.15f);
        t.colorGradient = new VertexGradient(top, top, bottom, bottom);
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = main ? 4f : 3f;
        // Nunca quebrar o título em duas linhas: diminui a fonte se não couber.
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = t.fontSize * 0.5f;
        t.enableAutoSizing = true;

        var shadow = AssetDatabase.LoadAssetAtPath<Material>(DropShadowMaterial);
        if (shadow != null && t.font != null && t.font.name == "LiberationSans SDF")
            t.fontSharedMaterial = shadow;
    }

    private static void CreateSubtitle(Transform title)
    {
        var go = new GameObject("TxtSubtitulo", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Estilizar menu");
        go.transform.SetParent(title.parent, false);
        go.transform.SetSiblingIndex(title.GetSiblingIndex() + 1);

        var t = go.AddComponent<TextMeshProUGUI>();
        TMP_Text titleText = title.GetComponent<TMP_Text>();
        if (titleText != null) t.font = titleText.font;
        t.text = "Proteja o farol das criaturas da sombra";
        t.fontSize = 28;
        t.fontStyle = FontStyles.Italic;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.8f, 0.85f, 1f, 0.9f);
        t.textWrappingMode = TextWrappingModes.Normal;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;
        le.minHeight = 40f;
    }

    private static void StyleButton(Button b, Color accent)
    {
        Undo.RecordObject(b, "Estilizar menu");
        if (b.image != null)
        {
            Undo.RecordObject(b.image, "Estilizar menu");
            b.image.color = Color.white; // a cor final vem do ColorBlock abaixo
        }

        ColorBlock c = b.colors;
        c.normalColor = ButtonNormal;
        c.highlightedColor = ButtonHighlight;
        c.pressedColor = ButtonPressed;
        c.selectedColor = ButtonNormal;
        c.disabledColor = new Color(0.2f, 0.2f, 0.22f, 0.6f);
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        b.colors = c;

        AddOutline(b.gameObject, accent, 1.5f);

        foreach (TMP_Text t in b.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(t, "Estilizar menu");
            t.color = Cream;
            t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            t.characterSpacing = 3f;
        }

        if (!b.TryGetComponent(out ButtonHover _))
            Undo.AddComponent<ButtonHover>(b.gameObject);
    }

    private static void StyleInput(TMP_InputField input)
    {
        Undo.RecordObject(input, "Estilizar menu");
        if (input.targetGraphic is Image img)
        {
            Undo.RecordObject(img, "Estilizar menu");
            img.color = new Color(0.06f, 0.07f, 0.13f, 1f);
        }
        AddOutline(input.gameObject, new Color(Gold.r, Gold.g, Gold.b, 0.45f), 1.5f);

        if (input.textComponent != null)
        {
            Undo.RecordObject(input.textComponent, "Estilizar menu");
            input.textComponent.color = Cream;
        }
        if (input.placeholder is TMP_Text ph)
        {
            Undo.RecordObject(ph, "Estilizar menu");
            ph.color = new Color(0.6f, 0.62f, 0.72f, 0.8f);
            ph.fontStyle = FontStyles.Italic;
        }
        input.customCaretColor = true;
        input.caretColor = Gold;
        input.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
    }

    private static void AddOutline(GameObject go, Color color, float distance)
    {
        if (!go.TryGetComponent(out Outline outline))
            outline = Undo.AddComponent<Outline>(go);
        Undo.RecordObject(outline, "Estilizar menu");
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = false;
    }
}
