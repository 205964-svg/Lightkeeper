using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Dá aparência própria aos inimigos sem mexer na lógica (vida, velocidade, dano...).
/// Desenha sprites por código (Assets/Sprites/Inimigos) e reorganiza os prefabs:
///
///   Sombra/Colosso (raiz: Enemy + collider, movida pelo Enemy)
///    ├─ SombraChao   elipse escura no chão
///    ├─ Visual       animado pelo EnemyVisual (flutuar/pisar)
///    │   ├─ Aura     brilho pulsante (unlit)
///    │   ├─ Corpo    sprite principal (piscar de dano e tom de lentidão usam este)
///    │   └─ Olhos    olhos brilhantes (unlit, visíveis no escuro)
///    └─ HealthBar    (a do Gabriel, intocada)
///
/// Menu: Lightkeeper > Estilizar inimigos. Pode rodar de novo sem duplicar nada.
/// </summary>
public static class EnemyStyler
{
    private const string SpriteDir = "Assets/Sprites/Inimigos";
    private const string EffectDir = "Assets/Prefabs/Effects";
    private const string AudioDir = "Assets/Audio";
    private const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    private const int Size = 128;

    private class Style
    {
        public string prefab, bodySprite, eyesSprite;
        public Sprite body, eyes;
        public Color bodyColor, auraColor, eyeColor, deathColor;
        public Vector2 eyesPos;
        public float eyesScale, shadowWidth, visualScale = 1f;
        public bool stomp;
        public float bobFrequency, bobHeight, squash, sway;
        public string deathClip;
    }

    [MenuItem("Lightkeeper/Estilizar inimigos")]
    public static void StyleEnemies()
    {
        CreateFolder("Assets/Sprites", "Inimigos");
        CreateFolder("Assets/Prefabs", "Effects");
        CreateFolder("Assets", "Audio");

        Sprite ghost = MakeSprite("Sombra_Corpo", GhostBody);
        Sprite golem = MakeSprite("Colosso_Corpo", GolemBody);
        Sprite ghostEyes = MakeSprite("Sombra_Olhos", (u, v) => Eyes(u, v, 0.17f, 0.11f, 0.075f));
        Sprite golemEyes = MakeSprite("Colosso_Olhos", (u, v) => Eyes(u, v, 0.2f, 0.12f, 0.055f));
        Sprite hydra = MakeSprite("Hidra_Corpo", HydraBody);
        Sprite hydraEyes = MakeSprite("Hidra_Olhos", HydraEyes);
        Sprite glow = MakeSprite("Aura", Glow);
        Sprite shadowSprite = MakeSprite("SombraChao", (u, v) => new Color(0f, 0f, 0f, Ellipse(u, v)));

        MakeClip("morte_sombra", 0.45f, t =>
            (Noise() * 0.35f + Sine(Mathf.Lerp(700f, 160f, t / 0.45f), t) * 0.5f) * Mathf.Exp(-t * 8f) * 0.7f);
        MakeClip("morte_hidra", 0.8f, t =>
            (Noise() * 0.4f + Sine(Mathf.Lerp(260f, 60f, t / 0.8f), t) * 0.5f + Sine(Mathf.Lerp(390f, 90f, t / 0.8f), t) * 0.4f)
            * Mathf.Exp(-t * 4.5f) * 0.75f);
        MakeClip("morte_colosso", 0.7f, t =>
            (Noise() * 0.5f + Sine(Mathf.Lerp(160f, 40f, t / 0.7f), t) * 0.8f) * Mathf.Exp(-t * 5f) * 0.8f);

        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial);

        var styles = new[]
        {
            new Style
            {
                prefab = "Assets/Prefabs/Enemies/Sombra.prefab", bodySprite = "ghost", eyesSprite = "ghost",
                visualScale = 1.25f,
                bodyColor = new Color(0.42f, 0.28f, 0.72f), auraColor = new Color(0.55f, 0.25f, 1f, 0.45f),
                eyeColor = new Color(1f, 0.9f, 0.35f), deathColor = new Color(0.7f, 0.45f, 1f, 0.9f),
                eyesPos = new Vector2(0f, 0.12f), eyesScale = 1f, shadowWidth = 0.7f,
                stomp = false, bobFrequency = 3f, bobHeight = 0.12f, squash = 0.04f, sway = 6f,
                deathClip = "morte_sombra"
            },
            new Style
            {
                prefab = "Assets/Prefabs/Enemies/Colosso.prefab", bodySprite = "golem", eyesSprite = "golem",
                visualScale = 1.15f,
                bodyColor = new Color(0.55f, 0.3f, 0.27f), auraColor = new Color(0.9f, 0.2f, 0.1f, 0.4f),
                eyeColor = new Color(1f, 0.3f, 0.15f), deathColor = new Color(1f, 0.45f, 0.15f, 0.9f),
                eyesPos = new Vector2(0f, 0.05f), eyesScale = 1f, shadowWidth = 0.95f,
                stomp = true, bobFrequency = 4f, bobHeight = 0.06f, squash = 0.07f, sway = 2f,
                deathClip = "morte_colosso"
            },
            new Style
            {
                prefab = "Assets/Prefabs/Enemies/Hidra.prefab", body = hydra, eyes = hydraEyes,
                visualScale = 1.2f,
                bodyColor = new Color(0.78f, 0.16f, 0.14f), auraColor = new Color(1f, 0.15f, 0.1f, 0.45f),
                eyeColor = new Color(1f, 0.85f, 0.2f), deathColor = new Color(1f, 0.25f, 0.1f, 0.9f),
                eyesPos = Vector2.zero, eyesScale = 1f, shadowWidth = 0.9f,
                stomp = false, bobFrequency = 4.5f, bobHeight = 0.08f, squash = 0.05f, sway = 8f,
                deathClip = "morte_hidra"
            }
        };

        int done = 0;
        foreach (Style s in styles)
        {
            if (!File.Exists(s.prefab))
            {
                Debug.LogWarning($"Lightkeeper: prefab não encontrado: {s.prefab}");
                continue;
            }
            GameObject deathFx = MakeDeathEffect(Path.GetFileNameWithoutExtension(s.prefab), glow, s.deathColor, unlit);
            Sprite bodySprite = s.body != null ? s.body : (s.bodySprite == "ghost" ? ghost : golem);
            Sprite eyesSprite = s.eyes != null ? s.eyes : (s.eyesSprite == "ghost" ? ghostEyes : golemEyes);
            ApplyStyle(s, bodySprite, eyesSprite,
                glow, shadowSprite, unlit, deathFx, AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{s.deathClip}.wav"));
            done++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Lightkeeper: {done} inimigos estilizados.");
    }

    // =====================================================================================
    // Prefabs
    // =====================================================================================

    private static void ApplyStyle(Style s, Sprite body, Sprite eyes, Sprite glow, Sprite shadow,
        Material unlit, GameObject deathFx, AudioClip deathSfx)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(s.prefab);
        try
        {
            // O corpo antigo (círculo) ficava na raiz: removemos esse renderer.
            Material litMaterial = null;
            if (root.TryGetComponent(out SpriteRenderer oldRenderer))
            {
                litMaterial = oldRenderer.sharedMaterial;
                Object.DestroyImmediate(oldRenderer);
            }
            else
            {
                Transform oldBody = root.transform.Find("Visual/Corpo");
                if (oldBody != null && oldBody.TryGetComponent(out SpriteRenderer r)) litMaterial = r.sharedMaterial;
            }
            foreach (string old in new[] { "Visual", "SombraChao" })
            {
                Transform existing = root.transform.Find(old);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
            }

            Transform shadowT = Child(root.transform, "SombraChao", new Vector3(0f, -0.42f, 0f),
                new Vector3(s.shadowWidth, s.shadowWidth * 0.3f, 1f));
            AddRenderer(shadowT, shadow, new Color(1f, 1f, 1f, 0.45f), 3, litMaterial);

            Transform visual = Child(root.transform, "Visual", Vector3.zero, Vector3.one * s.visualScale);
            Transform auraT = Child(visual, "Aura", Vector3.zero, Vector3.one * 1.9f);
            SpriteRenderer auraSr = AddRenderer(auraT, glow, s.auraColor, 4, unlit);
            Transform bodyT = Child(visual, "Corpo", Vector3.zero, Vector3.one);
            // Unlit: o mapa é escuro e, iluminado, o corpo virava uma mancha preta sem forma.
            SpriteRenderer bodySr = AddRenderer(bodyT, body, s.bodyColor, 5, unlit != null ? unlit : litMaterial);
            Transform eyesT = Child(visual, "Olhos", new Vector3(s.eyesPos.x, s.eyesPos.y, 0f), Vector3.one * s.eyesScale);
            AddRenderer(eyesT, eyes, s.eyeColor, 6, unlit);

            var anim = root.GetComponent<EnemyVisual>();
            if (anim == null) anim = root.AddComponent<EnemyVisual>();
            Set(anim, "visual", visual);
            Set(anim, "shadow", shadowT);
            Set(anim, "aura", auraSr);
            Set(anim, "stomp", s.stomp);
            Set(anim, "bobFrequency", s.bobFrequency);
            Set(anim, "bobHeight", s.bobHeight);
            Set(anim, "squash", s.squash);
            Set(anim, "swayDegrees", s.sway);

            var enemy = root.GetComponent<Enemy>();
            if (enemy != null)
            {
                Set(enemy, "spriteRenderer", bodySr);
                Set(enemy, "hitFlashColor", Color.white);
                Set(enemy, "deathEffectPrefab", deathFx);
                Set(enemy, "deathSfx", deathSfx);
            }

            PrefabUtility.SaveAsPrefabAsset(root, s.prefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject MakeDeathEffect(string enemyName, Sprite glow, Color color, Material unlit)
    {
        var go = new GameObject($"Morte_{enemyName}");
        AddRenderer(go.transform, glow, color, 15, unlit);
        var fx = go.AddComponent<FadeEffect>();
        Set(fx, "spriteRenderer", go.GetComponent<SpriteRenderer>());
        Set(fx, "duration", 0.45f);
        Set(fx, "startScale", 0.3f);
        Set(fx, "endScale", enemyName == "Colosso" ? 2.2f : 1.5f);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{EffectDir}/{go.name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
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

    private static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning($"Lightkeeper: campo '{field}' não existe em {target.GetType().Name}");
            return;
        }
        switch (value)
        {
            case float f: p.floatValue = f; break;
            case bool b: p.boolValue = b; break;
            case Color c: p.colorValue = c; break;
            case Object o: p.objectReferenceValue = o; break;
            case null: p.objectReferenceValue = null; break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================================
    // Desenho dos sprites (u, v de 0 a 1; v cresce para cima)
    // =====================================================================================

    /// <summary>Fantasma: cabeça redonda + "saia" com barra ondulada que vira fumaça.</summary>
    private static Color GhostBody(float u, float v)
    {
        float alpha = Coverage(u, v, (x, y) =>
        {
            bool head = Sq(x - 0.5f) + Sq(y - 0.6f) < Sq(0.33f);
            float hem = 0.1f + 0.05f * Mathf.Sin(x * Mathf.PI * 7f);
            bool skirt = y < 0.6f && y > hem && Mathf.Abs(x - 0.5f) < 0.33f - (0.6f - y) * 0.05f;
            return head || skirt;
        });
        // Parte de baixo se desfaz em fumaça.
        alpha *= Mathf.SmoothStep(0.08f, 0.35f, v);
        // Sombreamento: mais claro em cima, com brilho no canto superior esquerdo.
        float shade = Mathf.Lerp(0.55f, 1f, v);
        shade += 0.25f * Mathf.Clamp01(1f - (Sq(u - 0.38f) + Sq(v - 0.75f)) / 0.02f);
        return new Color(shade, shade, shade, alpha);
    }

    /// <summary>Golem: corpo maciço, ombros, chifres e rachaduras.</summary>
    private static Color GolemBody(float u, float v)
    {
        float alpha = Coverage(u, v, (x, y) =>
        {
            bool body = Mathf.Pow(Mathf.Abs(x - 0.5f) / 0.34f, 3f) + Mathf.Pow(Mathf.Abs(y - 0.42f) / 0.34f, 3f) < 1f;
            bool shoulderL = Sq(x - 0.2f) + Sq(y - 0.55f) < Sq(0.15f);
            bool shoulderR = Sq(x - 0.8f) + Sq(y - 0.55f) < Sq(0.15f);
            bool hornL = InTriangle(x, y, 0.28f, 0.68f, 0.4f, 0.7f, 0.2f, 0.95f);
            bool hornR = InTriangle(x, y, 0.72f, 0.68f, 0.6f, 0.7f, 0.8f, 0.95f);
            return body || shoulderL || shoulderR || hornL || hornR;
        });
        float shade = Mathf.Lerp(0.5f, 1f, v);
        // Rachaduras mais escuras.
        float crack = Mathf.Min(
            SegmentDistance(u, v, 0.35f, 0.3f, 0.45f, 0.18f),
            Mathf.Min(SegmentDistance(u, v, 0.6f, 0.62f, 0.68f, 0.45f), SegmentDistance(u, v, 0.68f, 0.45f, 0.62f, 0.3f)));
        if (crack < 0.012f) shade *= 0.45f;
        // Borda inferior mais escura (volume).
        shade *= Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(v * 3f));
        return new Color(shade, shade, shade, alpha);
    }

    // Centros das duas cabeças da Hidra (usados no corpo e nos olhos).
    private static readonly Vector2 HeadL = new Vector2(0.27f, 0.74f), HeadR = new Vector2(0.73f, 0.74f);

    /// <summary>Hidra: corpo oval, dois pescoços em V e duas cabeças com focinho.</summary>
    private static Color HydraBody(float u, float v)
    {
        float alpha = Coverage(u, v, (x, y) =>
        {
            bool body = Sq((x - 0.5f) / 0.3f) + Sq((y - 0.3f) / 0.24f) < 1f;
            bool neckL = SegmentDistance(x, y, 0.42f, 0.38f, HeadL.x, HeadL.y) < 0.075f;
            bool neckR = SegmentDistance(x, y, 0.58f, 0.38f, HeadR.x, HeadR.y) < 0.075f;
            bool headL = Sq(x - HeadL.x) + Sq(y - HeadL.y) < Sq(0.13f);
            bool headR = Sq(x - HeadR.x) + Sq(y - HeadR.y) < Sq(0.13f);
            bool snoutL = SegmentDistance(x, y, HeadL.x, HeadL.y, HeadL.x - 0.13f, HeadL.y + 0.1f) < 0.06f;
            bool snoutR = SegmentDistance(x, y, HeadR.x, HeadR.y, HeadR.x + 0.13f, HeadR.y + 0.1f) < 0.06f;
            bool tail = SegmentDistance(x, y, 0.5f, 0.1f, 0.5f, 0.02f) < 0.05f;
            return body || neckL || neckR || headL || headR || snoutL || snoutR || tail;
        });
        float shade = Mathf.Lerp(0.55f, 1f, v);
        // Escamas: faixas mais escuras na barriga.
        if (v < 0.45f && Mathf.Abs(Mathf.Sin(v * 32f)) < 0.12f) shade *= 0.88f;
        return new Color(shade, shade, shade, alpha);
    }

    /// <summary>Dois pares de olhos, um em cada cabeça.</summary>
    private static Color HydraEyes(float u, float v)
    {
        float best = 0f;
        foreach (Vector2 h in new[] { HeadL, HeadR })
            foreach (float dx in new[] { -0.05f, 0.05f })
            {
                float d = Sq((u - (h.x + dx)) / 0.035f) + Sq((v - (h.y + 0.01f)) / 0.028f);
                float core = Mathf.Clamp01((1f - d) * 4f);
                float halo = Mathf.Clamp01(1f - Mathf.Sqrt(d) / 2.2f) * 0.5f;
                best = Mathf.Max(best, Mathf.Max(core, halo));
            }
        return new Color(1f, 1f, 1f, best);
    }

    /// <summary>Dois olhos brilhantes (amêndoas) com halo suave.</summary>
    private static Color Eyes(float u, float v, float spacing, float width, float height)
    {
        float best = 0f;
        foreach (float cx in new[] { 0.5f - spacing, 0.5f + spacing })
        {
            float d = Sq((u - cx) / width) + Sq((v - 0.5f) / height);
            float core = Mathf.Clamp01((1f - d) * 4f);
            float halo = Mathf.Clamp01(1f - Mathf.Sqrt(d) / 2.2f) * 0.5f;
            best = Mathf.Max(best, Mathf.Max(core, halo));
        }
        return new Color(1f, 1f, 1f, best);
    }

    private static Color Glow(float u, float v)
    {
        float d = Mathf.Sqrt(Sq(u - 0.5f) + Sq(v - 0.5f)) / 0.5f;
        float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
        return new Color(1f, 1f, 1f, a);
    }

    private static float Ellipse(float u, float v)
    {
        float d = Mathf.Sqrt(Sq(u - 0.5f) + Sq(v - 0.5f)) / 0.5f;
        return Mathf.Clamp01((1f - d) * 2.5f) * 0.8f;
    }

    private static float Coverage(float u, float v, Func<float, float, bool> inside)
    {
        // Supersampling 4x4 para bordas suaves.
        const int n = 4;
        float step = 1f / (Size * n);
        int hits = 0;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (inside(u + (i - 1.5f) * step, v + (j - 1.5f) * step)) hits++;
        return hits / (float)(n * n);
    }

    private static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
        float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
        float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
    {
        Vector2 p = new Vector2(px, py), a = new Vector2(ax, ay), b = new Vector2(bx, by);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float Sq(float x) => x * x;

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
        importer.spritePixelsPerUnit = Size; // sprite inteiro = 1 unidade, igual ao círculo antigo
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // =====================================================================================
    // Sons sintetizados
    // =====================================================================================

    private static readonly System.Random rng = new System.Random(11);
    private static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
    private static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

    private static void MakeClip(string name, float seconds, Func<float, float> sample)
    {
        const int rate = 22050;
        int count = Mathf.CeilToInt(seconds * rate);
        string path = $"{AudioDir}/{name}.wav";
        using (var w = new BinaryWriter(new FileStream(path, FileMode.Create)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + count * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(count * 2);
            for (int i = 0; i < count; i++)
                w.Write((short)(Mathf.Clamp(sample(i / (float)rate), -1f, 1f) * short.MaxValue));
        }
        AssetDatabase.ImportAsset(path);
    }

    private static void CreateFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }
}
