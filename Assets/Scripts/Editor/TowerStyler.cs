using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Visual das torres e dos pedestais (sem mexer em custo, alcance, dano...):
/// - desenha sprites por código em Assets/Sprites/Torres;
/// - Lanterna: base de pedra + lanterna de latão com chama tremulante + luz quente;
/// - Prisma: base de pedra azulada + cristal facetado com brilho pulsante + luz fria;
/// - anel de alcance colorido;
/// - pedestais redondos de pedra com runas, e reposiciona/adiciona pedestais na GameScene.
/// Menu: Lightkeeper > Estilizar torres e pedestais. Pode rodar de novo sem duplicar nada.
/// </summary>
public static class TowerStyler
{
    private const string SpriteDir = "Assets/Sprites/Torres";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    private const int Size = 128;

    // Posições dos pedestais (mundo). Os 8 originais ficam, exceto Spot_02, que é movido;
    // Spot_09..11 são novos. Escolhidos pela cobertura do caminho e por estarem em grama/rocha.
    private static readonly (string name, Vector2 pos)[] SpotLayout =
    {
        ("Spot_02", new Vector2(-7.25f, -0.25f)), // curva antes da caveira (antes: colado no Spot_01)
        ("Spot_09", new Vector2(0.75f, 1.0f)),    // rocha no meio do segundo laço
        ("Spot_10", new Vector2(3.75f, 1.75f)),   // perto da ponte, guarda o trecho final
        ("Spot_11", new Vector2(-7.75f, -2.0f)),  // subida depois da clareira
    };

    [MenuItem("Lightkeeper/Estilizar torres e pedestais")]
    public static void StyleTowers()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder(SpriteDir)) AssetDatabase.CreateFolder("Assets/Sprites", "Torres");

        Sprite baseSprite = MakeSprite("Torre_Base", TowerBase);
        Sprite lanternHead = MakeSprite("Lanterna_Cabeca", LanternHead);
        Sprite crystal = MakeSprite("Prisma_Cristal", Crystal);
        Sprite glow = MakeSprite("Torre_Brilho", Glow);
        Sprite ring = MakeSprite("Alcance_Anel", Ring);
        Sprite pedestal = MakeSprite("Pedestal", Pedestal);
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial);

        StyleTower("Assets/Prefabs/Towers/Torre_Lanterna.prefab", baseSprite, lanternHead, glow, ring, unlit,
            stone: new Color(0.68f, 0.62f, 0.54f), head: new Color(1f, 0.78f, 0.4f), headUnlit: false, headScale: 0.75f,
            light: new Color(1f, 0.75f, 0.4f), glowSpeed: 7f, glowAmount: 0.2f);
        StyleTower("Assets/Prefabs/Towers/Torre_Prisma.prefab", baseSprite, crystal, glow, ring, unlit,
            stone: new Color(0.45f, 0.5f, 0.64f), head: new Color(0.55f, 0.95f, 1f), headUnlit: true, headScale: 0.85f,
            light: new Color(0.5f, 0.85f, 1f), glowSpeed: 1.2f, glowAmount: 0.25f);

        int spots = StyleSpots(pedestal, unlit);
        AssetDatabase.SaveAssets();
        Debug.Log($"Lightkeeper: torres estilizadas, {spots} pedestais na GameScene.");
    }

    // =====================================================================================
    // Torres
    // =====================================================================================

    private static void StyleTower(string path, Sprite baseSprite, Sprite headSprite, Sprite glowSprite, Sprite ring,
        Material unlit, Color stone, Color head, bool headUnlit, float headScale, Color light, float glowSpeed, float glowAmount)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Lightkeeper: prefab não encontrado: {path}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var tower = root.GetComponent<Tower>();
            float range = tower != null ? new SerializedObject(tower).FindProperty("range").floatValue : 3f;

            // Base
            Transform baseT = root.transform.Find("Base");
            if (baseT != null && baseT.TryGetComponent(out SpriteRenderer baseSr))
            {
                baseSr.sprite = baseSprite;
                baseSr.color = stone;
                baseT.localScale = Vector3.one * 0.85f;
            }

            // Cabeça (a parte que gira). O "Canhao" antigo vira invisível — o FirePoint continua igual.
            Transform cabeca = root.transform.Find("Cabeca");
            if (cabeca != null)
            {
                Transform canhao = cabeca.Find("Canhao");
                if (canhao != null && canhao.TryGetComponent(out SpriteRenderer cr)) cr.enabled = false;
                foreach (string old in new[] { "Corpo", "Brilho" })
                {
                    Transform t = cabeca.Find(old);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
            }
            Transform headParent = cabeca != null ? cabeca : root.transform;

            Transform glowT = Child(headParent, "Brilho", Vector3.zero, Vector3.one * 1.1f);
            SpriteRenderer glowSr = AddRenderer(glowT, glowSprite, new Color(light.r, light.g, light.b, 0.55f), 8, unlit);
            Transform bodyT = Child(headParent, "Corpo", Vector3.zero, Vector3.one * headScale);
            Material litMaterial = baseT != null && baseT.TryGetComponent(out SpriteRenderer bsr) ? bsr.sharedMaterial : null;
            AddRenderer(bodyT, headSprite, head, 9, headUnlit ? unlit : litMaterial);

            // Anel de alcance (o Tower ajusta a escala para o alcance).
            Transform alcance = root.transform.Find("Alcance");
            if (alcance != null && alcance.TryGetComponent(out SpriteRenderer ar))
            {
                ar.sprite = ring;
                ar.color = new Color(light.r, light.g, light.b, 0.7f);
                if (unlit != null) ar.sharedMaterial = unlit;
            }

            // Luz 2D de verdade, iluminando o mapa escuro em volta da torre.
            Light2D l = root.GetComponent<Light2D>();
            if (l == null) l = root.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = light;
            l.intensity = 0.8f;
            l.pointLightOuterRadius = range * 0.75f;
            l.pointLightInnerRadius = 0.3f;
            ApplyToAllSortingLayers(l);

            var anim = root.GetComponent<TowerGlow>();
            if (anim == null) anim = root.AddComponent<TowerGlow>();
            var so = new SerializedObject(anim);
            so.FindProperty("glow").objectReferenceValue = glowSr;
            so.FindProperty("light2D").objectReferenceValue = l;
            so.FindProperty("speed").floatValue = glowSpeed;
            so.FindProperty("amount").floatValue = glowAmount;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =====================================================================================
    // Pedestais (cena)
    // =====================================================================================

    private static int StyleSpots(Sprite pedestal, Material unlit)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameScenePath) scene = EditorSceneManager.OpenScene(GameScenePath);

        GameObject parent = GameObject.Find("BuildSpots");
        if (parent == null)
        {
            Debug.LogWarning("Lightkeeper: objeto BuildSpots não encontrado na GameScene.");
            return 0;
        }

        Transform template = parent.transform.Find("Spot_01");
        foreach (var (name, pos) in SpotLayout)
        {
            Transform spot = parent.transform.Find(name);
            if (spot == null && template != null)
            {
                spot = Object.Instantiate(template.gameObject, parent.transform).transform;
                spot.name = name;
            }
            if (spot != null) spot.position = new Vector3(pos.x, pos.y, spot.position.z);
        }

        int count = 0;
        foreach (BuildSpot bs in parent.GetComponentsInChildren<BuildSpot>(true))
        {
            if (!bs.TryGetComponent(out SpriteRenderer sr)) sr = bs.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = pedestal;
            sr.color = Color.white;
            if (unlit != null) sr.sharedMaterial = unlit;
            bs.transform.localScale = Vector3.one * 0.9f;

            var so = new SerializedObject(bs);
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            so.FindProperty("normalColor").colorValue = new Color(0.85f, 0.8f, 0.72f, 0.8f);
            so.FindProperty("hoverColor").colorValue = new Color(1f, 0.82f, 0.35f, 1f);
            so.FindProperty("occupiedColor").colorValue = new Color(0.45f, 0.45f, 0.5f, 0.85f);
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return count;
    }

    // =====================================================================================
    // Desenho (u, v de 0 a 1; v para cima). Em tons de cinza: a cor vem do SpriteRenderer.
    // =====================================================================================

    /// <summary>Base octogonal de pedra com anel externo e juntas de tijolo.</summary>
    private static Color TowerBase(float u, float v)
    {
        float x = u - 0.5f, y = v - 0.5f;
        float r = Octagon(x, y);
        float alpha = Mathf.Clamp01((0.48f - r) * Size);
        float shade = 0.85f + 0.15f * v;
        if (r > 0.38f) shade *= 0.75f;                                  // anel externo
        float ang = Mathf.Atan2(y, x) / (Mathf.PI * 2f) * 16f;
        if (r > 0.38f && Mathf.Abs(ang - Mathf.Round(ang)) < 0.06f) shade *= 0.6f; // juntas
        if (Mathf.Abs(r - 0.38f) < 0.008f) shade *= 0.6f;              // divisão
        return new Color(shade, shade, shade, alpha);
    }

    /// <summary>Lanterna vista de cima: corpo redondo com aro e bico apontando para a direita (+x).</summary>
    private static Color LanternHead(float u, float v)
    {
        float alpha = Coverage(u, v, (x, y) =>
            Sq(x - 0.42f) + Sq(y - 0.5f) < Sq(0.3f) ||
            (x > 0.55f && x < 0.95f && Mathf.Abs(y - 0.5f) < 0.09f + (0.95f - x) * 0.1f));
        float d = Mathf.Sqrt(Sq(u - 0.42f) + Sq(v - 0.5f));
        float shade = 0.9f;
        if (d < 0.3f && d > 0.24f) shade = 0.6f;          // aro
        if (d < 0.16f) shade = 1f;                        // vidro claro
        if (u > 0.55f) shade = 0.75f + 0.25f * (1f - Mathf.Abs(v - 0.5f) * 6f); // bico com volume
        shade += 0.1f * (v - 0.5f);
        return new Color(shade, shade, shade, alpha);
    }

    /// <summary>Cristal hexagonal alongado, facetado, apontando para a direita (+x).</summary>
    private static Color Crystal(float u, float v)
    {
        Vector2[] poly =
        {
            new Vector2(0.06f, 0.5f), new Vector2(0.28f, 0.7f), new Vector2(0.8f, 0.64f),
            new Vector2(0.97f, 0.5f), new Vector2(0.8f, 0.36f), new Vector2(0.28f, 0.3f)
        };
        float alpha = Coverage(u, v, (x, y) => InPolygon(x, y, poly));
        float shade = v > 0.5f ? 1f : 0.62f;                       // face de cima clara, de baixo escura
        if (Mathf.Abs(v - 0.5f) < 0.012f) shade = 1f;              // aresta central brilhante
        if (u > 0.8f) shade *= 0.85f;                              // ponta
        if (u < 0.28f) shade *= 0.8f;                              // base
        return new Color(shade, shade, shade, alpha);
    }

    private static Color Glow(float u, float v)
    {
        float d = Mathf.Sqrt(Sq(u - 0.5f) + Sq(v - 0.5f)) / 0.5f;
        return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
    }

    private static Color Ring(float u, float v)
    {
        float d = Mathf.Sqrt(Sq(u - 0.5f) + Sq(v - 0.5f)) / 0.5f;
        float edge = Mathf.Clamp01(1f - Mathf.Abs(d - 0.97f) / 0.025f);
        float fill = d < 0.97f ? 0.1f : 0f;
        return new Color(1f, 1f, 1f, Mathf.Max(edge, fill));
    }

    /// <summary>Plataforma redonda de pedra com anel de runas.</summary>
    private static Color Pedestal(float u, float v)
    {
        float x = u - 0.5f, y = v - 0.5f, d = Mathf.Sqrt(x * x + y * y);
        float alpha = Mathf.Clamp01((0.47f - d) * Size);
        float shade = 0.55f + 0.2f * v;
        if (d > 0.4f) shade *= 0.7f;                                   // borda
        if (Mathf.Abs(d - 0.3f) < 0.012f) shade = 1f;                  // anel de runas
        float ang = Mathf.Atan2(y, x);
        for (int i = 0; i < 4; i++)                                    // 4 runas
        {
            float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
            if (Sq(x - Mathf.Cos(a) * 0.3f) + Sq(y - Mathf.Sin(a) * 0.3f) < Sq(0.035f)) shade = 1f;
        }
        return new Color(shade, shade, shade, alpha);
    }

    private static float Octagon(float x, float y)
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        return Mathf.Max(Mathf.Max(ax, ay), (ax + ay) * 0.7071f);
    }

    private static bool InPolygon(float x, float y, Vector2[] p)
    {
        bool inside = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                inside = !inside;
        return inside;
    }

    private static float Coverage(float u, float v, Func<float, float, bool> inside)
    {
        const int n = 4;
        float step = 1f / (Size * n);
        int hits = 0;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (inside(u + (i - 1.5f) * step, v + (j - 1.5f) * step)) hits++;
        return hits / (float)(n * n);
    }

    private static float Sq(float x) => x * x;

    // =====================================================================================
    // Auxiliares
    // =====================================================================================

    private static Sprite MakeSprite(string name, Func<float, float, Color> pixel)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = pixel((x + 0.5f) / Size, (y + 0.5f) / Size);
        tex.SetPixels(pixels);
        tex.Apply();

        string path = $"{SpriteDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Size;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Transform Child(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        return go.transform;
    }

    private static SpriteRenderer AddRenderer(Transform t, Sprite sprite, Color color, int order, Material material)
    {
        var sr = t.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (material != null) sr.sharedMaterial = material;
        return sr;
    }

    private static void ApplyToAllSortingLayers(Light2D light)
    {
        var so = new SerializedObject(light);
        SerializedProperty prop = so.FindProperty("m_ApplyToSortingLayers");
        if (prop == null) return;
        SortingLayer[] layers = SortingLayer.layers;
        prop.arraySize = layers.Length;
        for (int i = 0; i < layers.Length; i++) prop.GetArrayElementAtIndex(i).intValue = layers[i].id;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
