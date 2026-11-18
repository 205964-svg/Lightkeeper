using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Monta o jogo inteiro a partir do código: sprites simples, sons sintetizados, prefabs,
/// dificuldades e as cenas MainMenu e GameScene.
/// Menu: Lightkeeper > Montar jogo completo.
///
/// ATENÇÃO: recria tudo em Assets/Generated e SOBRESCREVE Assets/Scenes/MainMenu.unity
/// e Assets/Scenes/GameScene.unity. Ajustes feitos à mão nessas cenas se perdem ao rodar de novo.
/// Para ajustar números (vida, velocidade, custo...), edite as constantes/valores aqui ou
/// direto nos prefabs/assets gerados.
/// </summary>
public static class LightkeeperBuilder
{
    private const string Root = "Assets/Generated";
    private const string SpritesDir = Root + "/Sprites";
    private const string AudioDir = Root + "/Audio";
    private const string PrefabsDir = Root + "/Prefabs";
    private const string DataDir = Root + "/Data";
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string BackgroundPath = "Assets/Sprites/Fundo.jpeg";

    // Câmera: altura visível = 2 * OrthoSize. O mapa ocupa a altura toda; à direita fica o painel da HUD.
    private const float OrthoSize = 4.48f;
    private const float ReferenceAspect = 16f / 9f;

    // Trajeto dos inimigos em PIXELS da imagem Fundo.jpeg (origem no canto superior esquerdo).
    private static readonly Vector2[] PathPixels =
    {
        new Vector2(-40, 700), new Vector2(180, 650), new Vector2(330, 630), new Vector2(440, 600),
        new Vector2(485, 540), new Vector2(470, 470), new Vector2(420, 400), new Vector2(380, 330),
        new Vector2(375, 275), new Vector2(430, 250), new Vector2(510, 280), new Vector2(545, 350),
        new Vector2(580, 440), new Vector2(640, 500), new Vector2(730, 525), new Vector2(820, 500),
        new Vector2(860, 430), new Vector2(840, 350), new Vector2(830, 310), new Vector2(880, 285),
        new Vector2(950, 240)
    };

    // Pedestais de construção (pixels).
    private static readonly Vector2[] SpotPixels =
    {
        new Vector2(300, 560), new Vector2(560, 560), new Vector2(400, 470), new Vector2(640, 420),
        new Vector2(290, 420), new Vector2(690, 560), new Vector2(905, 400), new Vector2(760, 300)
    };

    private static readonly Vector2 LampPixel = new Vector2(985, 75);

    private static Material spriteUnlit;
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    // =====================================================================================
    // Entradas de menu / linha de comando
    // =====================================================================================

    [MenuItem("Lightkeeper/Montar jogo completo")]
    public static void BuildAllFromMenu() => BuildAll(askConfirmation: true);

    private static void BuildAll(bool askConfirmation)
    {
        if (askConfirmation && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("Lightkeeper",
                "Isso recria Assets/Generated e sobrescreve as cenas MainMenu e GameScene. Continuar?",
                "Montar", "Cancelar"))
            return;

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
        CreateFolder(Root);
        CreateFolder(SpritesDir);
        CreateFolder(AudioDir);
        CreateFolder(PrefabsDir);
        CreateFolder(DataDir);
        sprites.Clear();
        clips.Clear();

        spriteUnlit = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        if (spriteUnlit == null) Debug.LogWarning("Lightkeeper: material Sprite-Unlit-Default não encontrado.");

        CreateSprites();
        CreateAudio();
        var prefabs = CreatePrefabs();
        var difficulties = CreateDifficulties(prefabs);
        BuildGameScene(prefabs, difficulties[0]);
        BuildMenuScene(difficulties);
        ConfigureProject();

        AssetDatabase.SaveAssets();
        Debug.Log("Lightkeeper: jogo montado com sucesso.");
        if (askConfirmation) EditorSceneManager.OpenScene(MenuScenePath);
    }

    [MenuItem("Lightkeeper/Gerar executável Windows")]
    public static void BuildWindowsFromMenu() => BuildWindows();

    private static bool BuildWindows()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { MenuScenePath, GameScenePath },
            locationPathName = "Builds/Windows/Lightkeeper.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Lightkeeper: build {report.summary.result}, {report.summary.totalErrors} erros, " +
                  $"{report.summary.totalSize / (1024 * 1024)} MB em {options.locationPathName}");
        return report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
    }

    /// <summary>
    /// Linha de comando: monta tudo, gera o executável e fecha a Unity.
    /// Ex.: Unity.exe -projectPath . -executeMethod LightkeeperBuilder.BatchBuildAll
    /// </summary>
    public static void BatchBuildAll()
    {
        int code = 1;
        try
        {
            BuildAll(askConfirmation: false);
            code = BuildWindows() ? 0 : 1;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        EditorApplication.Exit(code);
    }

    // =====================================================================================
    // Sprites gerados por código (geometria simples)
    // =====================================================================================

    private static void CreateSprites()
    {
        const int s = 64;
        MakeSprite("Circulo", s, (x, y) => Circle(x, y, s, 0.5f, 1.5f), new Vector2(0.5f, 0.5f));
        MakeSprite("Quadrado", s, (x, y) => RoundedSquare(x, y, s, 0.12f), new Vector2(0.5f, 0.5f));
        MakeSprite("Anel", 128, (x, y) => Ring(x, y, 128), new Vector2(0.5f, 0.5f));
        MakeSprite("Brilho", s, (x, y) => Glow(x, y, s), new Vector2(0.5f, 0.5f));
        MakeSprite("Triangulo", s, (x, y) => Triangle(x, y, s), new Vector2(0.5f, 0.5f));
        MakeSprite("Barra", 8, (x, y) => 1f, new Vector2(0f, 0.5f));
        MakeSprite("Losango", s, (x, y) => Diamond(x, y, s), new Vector2(0.5f, 0.5f));
    }

    private static float Circle(int x, int y, int size, float radius01, float softPx)
    {
        float c = (size - 1) / 2f;
        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
        return Mathf.Clamp01((radius01 * size - d) / softPx);
    }

    private static float RoundedSquare(int x, int y, int size, float corner01)
    {
        float r = corner01 * size, c = (size - 1) / 2f, half = size / 2f - 1f;
        float dx = Mathf.Max(Mathf.Abs(x - c) - (half - r), 0f);
        float dy = Mathf.Max(Mathf.Abs(y - c) - (half - r), 0f);
        return Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
    }

    private static float Ring(int x, int y, int size)
    {
        float c = (size - 1) / 2f;
        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size / 2f);
        float edge = Mathf.Clamp01(1f - Mathf.Abs(d - 0.97f) / 0.03f);
        float fill = d < 0.97f ? 0.18f : 0f;
        return Mathf.Max(edge, fill);
    }

    private static float Glow(int x, int y, int size)
    {
        float c = (size - 1) / 2f;
        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size / 2f);
        return Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
    }

    private static float Triangle(int x, int y, int size)
    {
        // Triângulo apontando para cima.
        float u = x / (float)(size - 1), v = y / (float)(size - 1);
        if (v < 0.08f || v > 0.95f) return 0f;
        float halfWidth = (0.95f - v) * 0.5f;
        return Mathf.Abs(u - 0.5f) <= halfWidth ? 1f : 0f;
    }

    private static float Diamond(int x, int y, int size)
    {
        float c = (size - 1) / 2f;
        float d = (Mathf.Abs(x - c) + Mathf.Abs(y - c)) / (size / 2f);
        return Mathf.Clamp01((1f - d) * size / 3f);
    }

    private static void MakeSprite(string name, int size, Func<int, int, float> alpha, Vector2 pivot)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(x, y));
        tex.SetPixels(pixels);
        tex.Apply();

        string path = $"{SpritesDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = size; // 1 unidade de mundo = sprite inteiro
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        sprites[name] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // =====================================================================================
    // Sons sintetizados (arquivos .wav gerados por código)
    // =====================================================================================

    private static void CreateAudio()
    {
        var rng = new System.Random(7);
        float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        float Env(float t, float d) => Mathf.Exp(-t * 6f / d);

        MakeClip("tiro_lanterna", 0.09f, t => Sine(Mathf.Lerp(1100, 650, t / 0.09f), t) * Env(t, 0.09f) * 0.5f);
        MakeClip("tiro_prisma", 0.28f, t => (Sine(Mathf.Lerp(420, 1250, t / 0.28f), t) * 0.6f + Sine(1800, t) * 0.15f) * Env(t, 0.28f) * 0.5f);
        MakeClip("impacto", 0.1f, t => Noise() * Env(t, 0.1f) * 0.35f);
        MakeClip("impacto_prisma", 0.3f, t => (Noise() * 0.4f + Sine(300, t) * 0.4f) * Env(t, 0.3f) * 0.5f);
        MakeClip("morte", 0.3f, t => (Noise() * 0.3f + Sine(Mathf.Lerp(420, 70, t / 0.3f), t) * 0.6f) * Env(t, 0.3f) * 0.6f);
        MakeClip("farol_atingido", 0.5f, t => (Sine(Mathf.Lerp(140, 45, t / 0.5f), t) * 0.8f + Noise() * 0.25f) * Env(t, 0.5f) * 0.8f);
        MakeClip("construir", 0.25f, t => Sine(t < 0.1f ? 523f : 784f, t) * Env(t, 0.3f) * 0.5f);
        MakeClip("vender", 0.25f, t => Sine(t < 0.1f ? 784f : 523f, t) * Env(t, 0.3f) * 0.5f);
        MakeClip("erro", 0.18f, t => Mathf.Sign(Sine(180, t)) * Env(t, 0.25f) * 0.25f);
        MakeClip("onda", 0.9f, t => (Sine(220, t) + Sine(330, t) * 0.6f) * Mathf.Sin(Mathf.PI * t / 0.9f) * 0.35f);
        MakeClip("vitoria", 1.2f, t =>
        {
            float[] notes = { 523f, 659f, 784f, 1047f };
            int i = Mathf.Min((int)(t / 0.22f), 3);
            float local = t - i * 0.22f;
            return Sine(notes[i], t) * Env(local, i == 3 ? 0.9f : 0.3f) * 0.5f;
        });
        MakeClip("derrota", 1.3f, t => Sine(Mathf.Lerp(330, 90, t / 1.3f), t) * Mathf.Exp(-t * 1.5f) * 0.55f);

        // Música ambiente: acordes graves com variação lenta (loop de 16s).
        MakeClip("musica", 16f, t =>
        {
            float[] roots = { 110f, 98f, 87.31f, 98f };
            float root = roots[(int)(t / 4f) % 4];
            float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / 4f - Mathf.PI / 2f);
            float pad = Sine(root, t) * 0.5f + Sine(root * 1.5f, t) * 0.25f + Sine(root * 2f, t) * 0.15f
                        + Sine(root * 3f + 0.4f * Mathf.Sin(t), t) * 0.05f;
            float fade = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((16f - t) / 0.5f);
            return pad * swell * fade * 0.22f;
        });
    }

    private static void MakeClip(string name, float seconds, Func<float, float> sample)
    {
        const int rate = 22050;
        int count = Mathf.CeilToInt(seconds * rate);
        string path = $"{AudioDir}/{name}.wav";

        using (var stream = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(stream))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + count * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);       // PCM
            w.Write((short)1);       // mono
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(count * 2);
            for (int i = 0; i < count; i++)
            {
                float v = Mathf.Clamp(sample(i / (float)rate), -1f, 1f);
                w.Write((short)(v * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path);
        clips[name] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // =====================================================================================
    // Prefabs
    // =====================================================================================

    private class Prefabs
    {
        public Tower lantern, prism;
        public Enemy shade, colossus;
        public GameObject buildEffect;
    }

    private static Prefabs CreatePrefabs()
    {
        var p = new Prefabs();

        // ---- Efeitos visuais
        GameObject sparkFx = SavePrefab(MakeEffect("Efeito_Faisca", new Color(1f, 0.85f, 0.3f, 0.9f), 0.2f, 0.1f, 0.6f), "Efeito_Faisca");
        GameObject prismFx = SavePrefab(MakeEffect("Efeito_Prisma", new Color(0.4f, 0.9f, 1f, 0.7f), 0.4f, 0.3f, 2.4f), "Efeito_Prisma");
        GameObject deathFx = SavePrefab(MakeEffect("Efeito_Morte", new Color(0.75f, 0.45f, 1f, 0.9f), 0.45f, 0.2f, 1.1f), "Efeito_Morte");
        p.buildEffect = SavePrefab(MakeEffect("Efeito_Construcao", new Color(1f, 0.95f, 0.6f, 0.8f), 0.5f, 0.3f, 1.6f), "Efeito_Construcao");

        // ---- Projéteis
        Projectile spark = MakeProjectile("Projetil_Faisca", "Circulo", new Color(1f, 0.9f, 0.4f), 0.16f,
            speed: 12f, splash: 0f, slow: 1f, slowTime: 0f, fx: sparkFx, sfx: clips["impacto"]);
        Projectile prismBolt = MakeProjectile("Projetil_Prisma", "Losango", new Color(0.5f, 0.95f, 1f), 0.32f,
            speed: 6f, splash: 1.2f, slow: 0.5f, slowTime: 2f, fx: prismFx, sfx: clips["impacto_prisma"]);

        // ---- Torres
        p.lantern = MakeTower("Torre_Lanterna", "Lanterna",
            "Dispara faíscas rápidas num único alvo. Mira no inimigo mais adiantado.",
            cost: 50, range: 3f, fireRate: 2f, damage: 1f, targeting: TargetingMode.First,
            projectile: spark, baseColor: new Color(0.45f, 0.4f, 0.35f), headColor: new Color(1f, 0.8f, 0.35f),
            headSprite: "Circulo", headScale: 0.42f, rotationOffset: 0f,
            lightColor: new Color(1f, 0.8f, 0.45f), sfx: clips["tiro_lanterna"]);

        p.prism = MakeTower("Torre_Prisma", "Prisma",
            "Feixe lento que explode em área e deixa os inimigos lentos. Mira no mais resistente.",
            cost: 90, range: 4.2f, fireRate: 0.55f, damage: 3f, targeting: TargetingMode.Strongest,
            projectile: prismBolt, baseColor: new Color(0.25f, 0.3f, 0.45f), headColor: new Color(0.5f, 0.95f, 1f),
            headSprite: "Triangulo", headScale: 0.5f, rotationOffset: -90f,
            lightColor: new Color(0.55f, 0.85f, 1f), sfx: clips["tiro_prisma"]);

        // ---- Inimigos
        p.shade = MakeEnemy("Inimigo_Sombra", "Sombra", bodyColor: new Color(0.3f, 0.12f, 0.42f),
            eyeColor: new Color(1f, 0.85f, 0.25f), bodySize: 0.42f, health: 4f, speed: 1.0f,
            baseDamage: 1, reward: 8, score: 10, darkMultiplier: 1.35f, deathFx: deathFx);
        p.colossus = MakeEnemy("Inimigo_Colosso", "Colosso", bodyColor: new Color(0.4f, 0.13f, 0.1f),
            eyeColor: new Color(1f, 0.25f, 0.2f), bodySize: 0.68f, health: 14f, speed: 0.55f,
            baseDamage: 3, reward: 20, score: 30, darkMultiplier: 1.15f, deathFx: deathFx);

        return p;
    }

    private static GameObject MakeEffect(string name, Color color, float duration, float startScale, float endScale)
    {
        var go = new GameObject(name);
        var sr = AddSprite(go, "Brilho", color, 15, unlit: true);
        var fx = go.AddComponent<FadeEffect>();
        Set(fx, "spriteRenderer", sr);
        Set(fx, "duration", duration);
        Set(fx, "startScale", startScale);
        Set(fx, "endScale", endScale);
        return go;
    }

    private static Projectile MakeProjectile(string name, string sprite, Color color, float size,
        float speed, float splash, float slow, float slowTime, GameObject fx, AudioClip sfx)
    {
        var go = new GameObject(name);
        var body = Child(go.transform, "Visual", Vector3.zero, Vector3.one * size);
        AddSprite(body.gameObject, sprite, color, 10, unlit: true);
        var glow = Child(go.transform, "Brilho", Vector3.zero, Vector3.one * size * 2.5f);
        AddSprite(glow.gameObject, "Brilho", new Color(color.r, color.g, color.b, 0.5f), 9, unlit: true);

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = size * 0.5f;
        col.isTrigger = true;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var proj = go.AddComponent<Projectile>();
        Set(proj, "speed", speed);
        Set(proj, "homing", true);
        Set(proj, "splashRadius", splash);
        Set(proj, "slowMultiplier", slow);
        Set(proj, "slowDuration", slowTime);
        Set(proj, "hitEffectPrefab", fx);
        Set(proj, "hitSfx", sfx);
        return SavePrefab(go, name).GetComponent<Projectile>();
    }

    private static Tower MakeTower(string assetName, string displayName, string description, int cost, float range,
        float fireRate, float damage, TargetingMode targeting, Projectile projectile, Color baseColor, Color headColor,
        string headSprite, float headScale, float rotationOffset, Color lightColor, AudioClip sfx)
    {
        var go = new GameObject(assetName);

        var baseT = Child(go.transform, "Base", Vector3.zero, Vector3.one * 0.62f);
        AddSprite(baseT.gameObject, "Quadrado", baseColor, 2);

        var head = Child(go.transform, "Cabeca", new Vector3(0f, 0.05f, 0f), Vector3.one);
        var headVisual = Child(head, "Visual", Vector3.zero, Vector3.one * headScale);
        AddSprite(headVisual.gameObject, headSprite, headColor, 4, unlit: true);
        if (headSprite == "Circulo")
        {
            var barrel = Child(head, "Cano", new Vector3(0.25f, 0f, 0f), new Vector3(0.3f, 0.12f, 1f));
            AddSprite(barrel.gameObject, "Quadrado", headColor * 0.8f, 3, unlit: true);
        }
        var firePoint = Child(head, "FirePoint", rotationOffset == 0f ? new Vector3(0.4f, 0f, 0f) : new Vector3(0f, 0.3f, 0f), Vector3.one);
        if (rotationOffset != 0f) head.localRotation = Quaternion.identity;

        var rangeT = Child(go.transform, "Alcance", Vector3.zero, Vector3.one);
        var rangeSr = AddSprite(rangeT.gameObject, "Anel", new Color(lightColor.r, lightColor.g, lightColor.b, 0.6f), 1, unlit: true);

        var lightSource = go.AddComponent<LightSource>();
        Set(lightSource, "radius", range);
        AddPointLight(go, lightColor, range, 0.75f);

        var tower = go.AddComponent<Tower>();
        Set(tower, "displayName", displayName);
        Set(tower, "description", description);
        Set(tower, "cost", cost);
        Set(tower, "range", range);
        Set(tower, "fireRate", fireRate);
        Set(tower, "damage", damage);
        Set(tower, "targeting", (int)targeting);
        Set(tower, "projectilePrefab", projectile);
        Set(tower, "firePoint", firePoint);
        Set(tower, "rotatingPart", head);
        Set(tower, "rotationOffset", rotationOffset);
        Set(tower, "rangeIndicator", rangeSr);
        Set(tower, "shootSfx", sfx);
        return SavePrefab(go, assetName).GetComponent<Tower>();
    }

    private static Enemy MakeEnemy(string assetName, string displayName, Color bodyColor, Color eyeColor, float bodySize,
        float health, float speed, int baseDamage, int reward, int score, float darkMultiplier, GameObject deathFx)
    {
        var go = new GameObject(assetName);

        var aura = Child(go.transform, "Aura", Vector3.zero, Vector3.one * bodySize * 1.8f);
        AddSprite(aura.gameObject, "Brilho", new Color(bodyColor.r * 0.6f, bodyColor.g * 0.6f, bodyColor.b * 0.6f, 0.7f), 4);
        var body = Child(go.transform, "Corpo", Vector3.zero, Vector3.one * bodySize);
        var bodySr = AddSprite(body.gameObject, "Circulo", bodyColor, 5, unlit: true);
        float eyeY = bodySize * 0.1f, eyeX = bodySize * 0.18f, eyeSize = bodySize * 0.2f;
        AddSprite(Child(go.transform, "OlhoE", new Vector3(-eyeX, eyeY, 0f), Vector3.one * eyeSize).gameObject, "Circulo", eyeColor, 6, unlit: true);
        AddSprite(Child(go.transform, "OlhoD", new Vector3(eyeX, eyeY, 0f), Vector3.one * eyeSize).gameObject, "Circulo", eyeColor, 6, unlit: true);

        // Barra de vida
        var bar = Child(go.transform, "HealthBar", new Vector3(0f, bodySize * 0.5f + 0.12f, 0f), Vector3.one);
        float width = Mathf.Max(0.45f, bodySize);
        // O sprite "Barra" mede 1x1 unidade com pivô à esquerda.
        var bg = Child(bar, "Fundo", new Vector3(-width / 2f, 0f, 0f), new Vector3(width, 0.07f, 1f));
        AddSprite(bg.gameObject, "Barra", new Color(0.1f, 0.05f, 0.05f, 0.85f), 20, unlit: true);
        var fill = Child(bar, "Fill", new Vector3(-width / 2f, 0f, 0f), new Vector3(width, 0.07f, 1f));
        AddSprite(fill.gameObject, "Barra", new Color(0.35f, 0.95f, 0.4f), 21, unlit: true);
        var hb = bar.gameObject.AddComponent<HealthBar>();
        Set(hb, "fill", fill);

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = bodySize * 0.5f;
        col.isTrigger = true;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var enemy = go.AddComponent<Enemy>();
        Set(hb, "enemy", enemy);
        Set(enemy, "displayName", displayName);
        Set(enemy, "maxHealth", health);
        Set(enemy, "speed", speed);
        Set(enemy, "baseDamage", baseDamage);
        Set(enemy, "reward", reward);
        Set(enemy, "scoreValue", score);
        Set(enemy, "darkSpeedMultiplier", darkMultiplier);
        Set(enemy, "spriteRenderer", bodySr);
        Set(enemy, "flipSpriteByDirection", false);
        Set(enemy, "deathEffectPrefab", deathFx);
        Set(enemy, "deathSfx", clips["morte"]);
        return SavePrefab(go, assetName).GetComponent<Enemy>();
    }

    // =====================================================================================
    // Dificuldades
    // =====================================================================================

    private static DifficultySettings[] CreateDifficulties(Prefabs p)
    {
        var easy = ScriptableObject.CreateInstance<DifficultySettings>();
        easy.displayName = "Fácil";
        easy.description = "3 ondas, farol resistente e mais luz inicial.";
        easy.baseMaxHealth = 20;
        easy.maxEnemiesAtBase = 0;
        easy.timeLimitSeconds = 300f;
        easy.minKillsToWin = 10;
        easy.startingCurrency = 150;
        easy.enemyHealthMultiplier = 1f;
        easy.enemySpeedMultiplier = 1f;
        easy.firstWaveDelay = 5f;
        easy.timeBetweenWaves = 6f;
        easy.waves = new List<Wave>
        {
            MakeWave("Onda 1", G(p.shade, 6, 1.5f)),
            MakeWave("Onda 2", G(p.shade, 10, 1.0f), G(p.colossus, 2, 3f, 1f)),
            MakeWave("Onda 3", G(p.shade, 12, 0.8f), G(p.colossus, 5, 2.5f, 1f)),
        };
        AssetDatabase.CreateAsset(easy, $"{DataDir}/Dificuldade_Facil.asset");

        var hard = ScriptableObject.CreateInstance<DifficultySettings>();
        hard.displayName = "Difícil";
        hard.description = "5 ondas, criaturas mais fortes e rápidas, farol frágil.";
        hard.baseMaxHealth = 12;
        hard.maxEnemiesAtBase = 8;
        hard.timeLimitSeconds = 300f;
        hard.minKillsToWin = 25;
        hard.startingCurrency = 120;
        hard.enemyHealthMultiplier = 1.4f;
        hard.enemySpeedMultiplier = 1.2f;
        hard.firstWaveDelay = 5f;
        hard.timeBetweenWaves = 5f;
        hard.waves = new List<Wave>
        {
            MakeWave("Onda 1", G(p.shade, 8, 1.0f)),
            MakeWave("Onda 2", G(p.shade, 12, 0.8f), G(p.colossus, 3, 2.5f, 1f)),
            MakeWave("Onda 3", G(p.shade, 16, 0.6f)),
            MakeWave("Onda 4", G(p.colossus, 8, 1.8f), G(p.shade, 10, 0.6f)),
            MakeWave("Onda 5", G(p.shade, 20, 0.5f), G(p.colossus, 8, 1.5f, 1f)),
        };
        AssetDatabase.CreateAsset(hard, $"{DataDir}/Dificuldade_Dificil.asset");
        AssetDatabase.SaveAssets();

        return new[] { easy, hard };
    }

    private static WaveGroup G(Enemy prefab, int count, float interval, float delay = 0f)
    {
        return new WaveGroup { enemyPrefab = prefab, count = count, spawnInterval = interval, delayBefore = delay, pathIndex = 0 };
    }

    private static Wave MakeWave(string name, params WaveGroup[] groups)
    {
        return new Wave { name = name, groups = new List<WaveGroup>(groups) };
    }

    // =====================================================================================
    // Cena do jogo
    // =====================================================================================

    /// <summary>
    /// NewScene descarrega assets "não usados" da memória, invalidando referências criadas antes.
    /// Por isso recarregamos prefabs e dificuldades pelo caminho depois de cada NewScene.
    /// </summary>
    private static T LoadPrefab<T>(string name) where T : Component
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/{name}.prefab");
        if (go == null) throw new Exception($"Lightkeeper: prefab {name} não encontrado.");
        return go.GetComponent<T>();
    }

    private static Prefabs ReloadPrefabs()
    {
        return new Prefabs
        {
            lantern = LoadPrefab<Tower>("Torre_Lanterna"),
            prism = LoadPrefab<Tower>("Torre_Prisma"),
            shade = LoadPrefab<Enemy>("Inimigo_Sombra"),
            colossus = LoadPrefab<Enemy>("Inimigo_Colosso"),
            buildEffect = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/Efeito_Construcao.prefab")
        };
    }

    private static DifficultySettings[] ReloadDifficulties()
    {
        return new[]
        {
            AssetDatabase.LoadAssetAtPath<DifficultySettings>($"{DataDir}/Dificuldade_Facil.asset"),
            AssetDatabase.LoadAssetAtPath<DifficultySettings>($"{DataDir}/Dificuldade_Dificil.asset")
        };
    }

    private static void BuildGameScene(Prefabs ignored, DifficultySettings ignoredDifficulty)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Prefabs p = ReloadPrefabs();
        DifficultySettings defaultDifficulty = ReloadDifficulties()[0];

        CreateCamera(new Vector3(0f, 0f, -10f));
        CreateGlobalLight(0.55f);

        // ---- Mapa
        SpriteRenderer map = CreateMap();

        // ---- Caminho
        var pathGo = new GameObject("Caminho");
        var path = pathGo.AddComponent<EnemyPath>();
        for (int i = 0; i < PathPixels.Length; i++)
        {
            var wp = new GameObject($"Waypoint_{i:00}");
            wp.transform.SetParent(pathGo.transform);
            wp.transform.position = MapPoint(map, PathPixels[i]);
        }

        // ---- Farol (base)
        var lighthouse = new GameObject("Farol");
        lighthouse.transform.position = MapPoint(map, PathPixels[PathPixels.Length - 1]);
        var lhCol = lighthouse.AddComponent<CircleCollider2D>();
        lhCol.radius = 0.4f;
        var lamp = new GameObject("Lampada");
        lamp.transform.SetParent(lighthouse.transform);
        lamp.transform.position = MapPoint(map, LampPixel);
        lamp.transform.localScale = Vector3.one * 0.9f;
        var lampSr = AddSprite(lamp, "Brilho", new Color(1f, 0.85f, 0.4f, 0.85f), 1, unlit: true);
        var lh = lighthouse.AddComponent<Lighthouse>();
        Set(lh, "spriteRenderer", lampSr);
        Set(lh, "damageColor", new Color(1f, 0.25f, 0.2f, 1f));
        var lhLight = lighthouse.AddComponent<LightSource>();
        Set(lhLight, "radius", 2.2f);
        AddPointLight(lamp, new Color(1f, 0.85f, 0.5f), 3f, 1.1f);

        // ---- Pedestais
        var spotsParent = new GameObject("Pedestais");
        for (int i = 0; i < SpotPixels.Length; i++)
        {
            var spot = new GameObject($"Pedestal_{i + 1:00}");
            spot.transform.SetParent(spotsParent.transform);
            spot.transform.position = MapPoint(map, SpotPixels[i]);
            spot.transform.localScale = Vector3.one * 0.7f;
            var sr = AddSprite(spot, "Quadrado", Color.white, -10, unlit: true);
            spot.AddComponent<BoxCollider2D>().size = Vector2.one;
            var bs = spot.AddComponent<BuildSpot>();
            Set(bs, "spriteRenderer", sr);
            Set(bs, "normalColor", new Color(1f, 0.95f, 0.75f, 0.35f));
            Set(bs, "hoverColor", new Color(1f, 0.85f, 0.35f, 0.85f));
            Set(bs, "occupiedColor", new Color(0f, 0f, 0f, 0.35f));
        }

        var enemiesParent = new GameObject("Inimigos").transform;
        var towersParent = new GameObject("Torres").transform;

        // ---- Sistemas
        var systems = new GameObject("Sistemas");
        var gm = systems.AddComponent<GameManager>();
        Set(gm, "defaultDifficulty", defaultDifficulty);
        Set(gm, "menuSceneName", "MainMenu");
        Set(gm, "baseHitSfx", clips["farol_atingido"]);
        Set(gm, "victorySfx", clips["vitoria"]);
        Set(gm, "defeatSfx", clips["derrota"]);
        AddAudioManager(systems);

        var spawner = systems.AddComponent<WaveSpawner>();
        Set(spawner, "paths", new Object[] { path });
        Set(spawner, "enemiesParent", enemiesParent);
        Set(spawner, "waveStartSfx", clips["onda"]);

        var build = systems.AddComponent<BuildManager>();
        Set(build, "towerPrefabs", new Object[] { p.lantern, p.prism });
        Set(build, "towersParent", towersParent);
        Set(build, "buildEffectPrefab", p.buildEffect);
        Set(build, "buildSfx", clips["construir"]);
        Set(build, "sellSfx", clips["vender"]);
        Set(build, "errorSfx", clips["erro"]);

        CreateEventSystem();
        BuildHud(spawner);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), GameScenePath);
    }

    private static void BuildHud(WaveSpawner spawner)
    {
        Canvas canvas = CreateCanvas("HUD");
        var hud = canvas.gameObject.AddComponent<HUD>();
        Transform c = canvas.transform;

        // Painel lateral direito
        var panel = Panel(c, "PainelLateral", new Color(0.05f, 0.06f, 0.1f, 0.92f));
        Anchor(panel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-470f, 0f), Vector2.zero);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 26, 26);
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        Sized(Text(panel, "Titulo", "LIGHTKEEPER", 46, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f), FontStyles.Bold), 60);
        var health = Sized(Text(panel, "Vida", "Farol 20/20", 30, TextAlignmentOptions.Left, Color.white), 40);
        var currency = Sized(Text(panel, "Luz", "Luz 0", 30, TextAlignmentOptions.Left, new Color(1f, 0.9f, 0.5f)), 40);
        var kills = Sized(Text(panel, "Abates", "Abates 0", 30, TextAlignmentOptions.Left, Color.white), 40);
        var wave = Sized(Text(panel, "Onda", "Onda 0/0", 26, TextAlignmentOptions.Left, Color.white), 40);
        var timer = Sized(Text(panel, "Tempo", "Tempo 0:00", 30, TextAlignmentOptions.Left, Color.white), 40);

        Sized(Text(panel, "TituloTorres", "TORRES", 26, TextAlignmentOptions.Left, new Color(0.7f, 0.75f, 0.9f), FontStyles.Bold), 40);
        var b1 = Sized(Button(panel, "BotaoLanterna", "Lanterna", 26, new Color(1f, 1f, 1f)), 84);
        var b2 = Sized(Button(panel, "BotaoPrisma", "Prisma", 26, new Color(1f, 1f, 1f)), 84);
        var sell = Sized(Button(panel, "BotaoVender", "Vender torre", 24, Color.white), 54);

        var row = UI(panel, "Controles");
        Sized(row, 54);
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        var nextWave = Button(row, "BotaoProximaOnda", "Próxima onda", 22, Color.white);
        var pause = Button(row, "BotaoPausa", "Pausar", 22, Color.white);

        var help = Text(panel, "Ajuda",
            "Clique num pedestal para construir.\nClique direito vende (50%).\nTeclas 1/2 trocam a torre, P pausa.\n" +
            "Fora da luz as criaturas ficam mais rápidas!",
            20, TextAlignmentOptions.TopLeft, new Color(0.75f, 0.78f, 0.88f));
        var helpLe = help.gameObject.AddComponent<LayoutElement>();
        helpLe.flexibleHeight = 1f;

        // Mensagem central (sobre o mapa)
        var message = Text(c, "Mensagem", "", 32, TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.75f), FontStyles.Bold);
        Anchor(message.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -90f), new Vector2(-490f, -20f));
        message.outlineWidth = 0.2f;
        message.outlineColor = Color.black;

        // Painel de pausa
        var pausePanel = Panel(c, "PainelPausa", new Color(0f, 0f, 0f, 0.7f));
        Stretch(pausePanel);
        var pauseBox = CenteredBox(pausePanel, new Vector2(520f, 400f));
        Sized(Text(pauseBox, "Titulo", "PAUSADO", 56, TextAlignmentOptions.Center, Color.white, FontStyles.Bold), 90);
        var resume = Sized(Button(pauseBox, "Continuar", "Continuar", 30, Color.white), 70);
        var restart1 = Sized(Button(pauseBox, "Reiniciar", "Reiniciar", 30, Color.white), 70);
        var menu1 = Sized(Button(pauseBox, "Menu", "Menu principal", 30, Color.white), 70);

        // Painel de fim de jogo
        var endPanel = Panel(c, "PainelFim", new Color(0f, 0f, 0f, 0.75f));
        Stretch(endPanel);
        var endBox = CenteredBox(endPanel, new Vector2(700f, 560f));
        var endTitle = Sized(Text(endBox, "Titulo", "VITÓRIA!", 64, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f), FontStyles.Bold), 100);
        var endDetails = Sized(Text(endBox, "Detalhes", "", 28, TextAlignmentOptions.Center, Color.white), 220);
        var restart2 = Sized(Button(endBox, "Reiniciar", "Jogar de novo", 30, Color.white), 70);
        var menu2 = Sized(Button(endBox, "Menu", "Menu principal", 30, Color.white), 70);

        // Ligações
        Set(hud, "spawner", spawner);
        Set(hud, "healthText", health);
        Set(hud, "currencyText", currency);
        Set(hud, "waveText", wave);
        Set(hud, "timerText", timer);
        Set(hud, "killsText", kills);
        Set(hud, "messageText", message);
        Set(hud, "towerButtons", new Object[] { b1, b2 });
        Set(hud, "towerButtonLabels", new Object[] { b1.GetComponentInChildren<TMP_Text>(), b2.GetComponentInChildren<TMP_Text>() });
        Set(hud, "sellButtonImage", sell.image);
        Set(hud, "pausePanel", pausePanel.gameObject);
        Set(hud, "endPanel", endPanel.gameObject);
        Set(hud, "endTitleText", endTitle);
        Set(hud, "endDetailsText", endDetails);

        UnityEventTools.AddPersistentListener(sell.onClick, hud.OnSellModeButton);
        UnityEventTools.AddPersistentListener(nextWave.onClick, hud.OnNextWaveButton);
        UnityEventTools.AddPersistentListener(pause.onClick, hud.OnPauseButton);
        UnityEventTools.AddPersistentListener(resume.onClick, hud.OnPauseButton);
        UnityEventTools.AddPersistentListener(restart1.onClick, hud.OnRestartButton);
        UnityEventTools.AddPersistentListener(menu1.onClick, hud.OnMenuButton);
        UnityEventTools.AddPersistentListener(restart2.onClick, hud.OnRestartButton);
        UnityEventTools.AddPersistentListener(menu2.onClick, hud.OnMenuButton);
    }

    // =====================================================================================
    // Cena do menu
    // =====================================================================================

    private static void BuildMenuScene(DifficultySettings[] ignored)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        DifficultySettings[] difficulties = ReloadDifficulties();
        CreateCamera(new Vector3(0f, 0f, -10f));
        CreateGlobalLight(0.35f);
        SpriteRenderer map = CreateMap();
        map.transform.position = Vector3.zero;
        var lamp = new GameObject("Lampada");
        lamp.transform.position = MapPoint(map, LampPixel);
        AddPointLight(lamp, new Color(1f, 0.85f, 0.5f), 4f, 1.3f);

        var audioGo = new GameObject("Audio");
        AddAudioManager(audioGo);
        CreateEventSystem();

        Canvas canvas = CreateCanvas("Menu");
        var menu = canvas.gameObject.AddComponent<MainMenu>();
        Transform c = canvas.transform;

        // ---- Painel principal
        var main = UI(c, "PainelPrincipal");
        Stretch(main);
        var mainBox = CenteredBox(main, new Vector2(640f, 860f));
        Sized(Text(mainBox, "Titulo", "LIGHTKEEPER", 80, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f), FontStyles.Bold), 100);
        Sized(Text(mainBox, "Subtitulo", "Proteja o farol das criaturas da sombra", 26, TextAlignmentOptions.Center, new Color(0.85f, 0.88f, 1f)), 40);
        Sized(Text(mainBox, "RotuloNome", "Seu nome:", 24, TextAlignmentOptions.Left, Color.white), 32);

        var input = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        input.name = "Nome";
        input.transform.SetParent(mainBox, false);
        var inputField = input.GetComponent<TMP_InputField>();
        inputField.characterLimit = 16;
        inputField.pointSize = 28;
        foreach (var t in input.GetComponentsInChildren<TMP_Text>(true)) { t.font = TMP_Settings.defaultFontAsset; t.fontSize = 28; }
        Sized((RectTransform)input.transform, 56);

        var easy = Sized(Button(mainBox, "JogarFacil", "Jogar — Fácil", 32, new Color(0.75f, 1f, 0.75f)), 74);
        var hard = Sized(Button(mainBox, "JogarDificil", "Jogar — Difícil", 32, new Color(1f, 0.7f, 0.65f)), 74);
        var desc = Sized(Text(mainBox, "Descricao", DifficultyText(difficulties), 22, TextAlignmentOptions.Center, new Color(0.8f, 0.82f, 0.9f)), 70);
        var info = Sized(Button(mainBox, "ComoJogar", "Como jogar", 28, Color.white), 62);
        var ranking = Sized(Button(mainBox, "Ranking", "Ranking", 28, Color.white), 62);
        var quit = Sized(Button(mainBox, "Sair", "Sair", 28, Color.white), 62);

        // ---- Painel "Como jogar"
        var infoPanel = UI(c, "PainelComoJogar");
        Stretch(infoPanel);
        var infoBox = CenteredBox(infoPanel, new Vector2(1100f, 820f));
        Sized(Text(infoBox, "Titulo", "COMO JOGAR", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f), FontStyles.Bold), 80);
        var infoText = Text(infoBox, "Texto",
            "<b>Objetivo:</b> criaturas sombrias saem da clareira e seguem a trilha até o farol. " +
            "Resista a todas as ondas, eliminando o mínimo de criaturas do nível, antes que o tempo acabe.\n\n" +
            "<b>Construir:</b> escolha uma torre no painel (ou teclas 1 e 2) e clique num pedestal livre. " +
            "Eliminar criaturas rende <color=#FFE080>luz</color>, usada para construir mais torres.\n\n" +
            "<b>Lanterna</b> (50): rápida, um alvo, mira no inimigo mais adiantado.\n" +
            "<b>Prisma</b> (90): lenta, dano em área e deixa os inimigos lentos, mira no mais resistente.\n\n" +
            "<b>Luz e sombra:</b> fora do alcance das luzes, as criaturas andam mais rápido.\n\n" +
            "<b>Derrota:</b> a vida do farol chega a zero, criaturas demais chegam ao farol ou o tempo acaba.\n\n" +
            "<b>Controles:</b> clique/toque constrói • clique direito vende (50%) • P/Esc pausa.",
            26, TextAlignmentOptions.TopLeft, Color.white);
        infoText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var back1 = Sized(Button(infoBox, "Voltar", "Voltar", 28, Color.white), 62);

        // ---- Painel ranking
        var rankingPanel = UI(c, "PainelRanking");
        Stretch(rankingPanel);
        var rankBox = CenteredBox(rankingPanel, new Vector2(1000f, 760f));
        Sized(Text(rankBox, "Titulo", "RANKING", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f), FontStyles.Bold), 80);
        var rankText = Text(rankBox, "Lista", "", 26, TextAlignmentOptions.TopLeft, Color.white);
        rankText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var clear = Sized(Button(rankBox, "Limpar", "Limpar ranking", 26, new Color(1f, 0.75f, 0.7f)), 58);
        var back2 = Sized(Button(rankBox, "Voltar", "Voltar", 28, Color.white), 62);

        Set(menu, "gameSceneName", "GameScene");
        Set(menu, "difficulties", difficulties);
        Set(menu, "playerNameInput", inputField);
        Set(menu, "mainPanel", main.gameObject);
        Set(menu, "infoPanel", infoPanel.gameObject);
        Set(menu, "rankingPanel", rankingPanel.gameObject);
        Set(menu, "rankingText", rankText);
        Set(menu, "difficultyDescriptionText", desc);

        UnityEventTools.AddIntPersistentListener(easy.onClick, menu.StartGame, 0);
        UnityEventTools.AddIntPersistentListener(hard.onClick, menu.StartGame, 1);
        UnityEventTools.AddPersistentListener(info.onClick, menu.ShowInfo);
        UnityEventTools.AddPersistentListener(ranking.onClick, menu.ShowRanking);
        UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);
        UnityEventTools.AddPersistentListener(back1.onClick, menu.ShowMain);
        UnityEventTools.AddPersistentListener(back2.onClick, menu.ShowMain);
        UnityEventTools.AddPersistentListener(clear.onClick, menu.ClearRanking);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MenuScenePath);
    }

    private static string DifficultyText(DifficultySettings[] d)
    {
        var parts = new List<string>();
        foreach (var x in d)
            parts.Add($"<b>{x.displayName}:</b> {x.waves.Count} ondas, {x.TotalEnemies} criaturas, mínimo {x.minKillsToWin} abates");
        return string.Join("\n", parts);
    }

    // =====================================================================================
    // Projeto
    // =====================================================================================

    private static void ConfigureProject()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
        PlayerSettings.productName = "Lightkeeper";
        PlayerSettings.companyName = "Lightkeeper";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
    }

    // =====================================================================================
    // Auxiliares de cena
    // =====================================================================================

    private static void CreateCamera(Vector3 position)
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = position;
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = OrthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
        go.AddComponent<AudioListener>();
    }

    private static void CreateGlobalLight(float intensity)
    {
        var go = new GameObject("Global Light 2D");
        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = intensity;
        light.color = new Color(0.75f, 0.8f, 1f);
        ApplyToAllSortingLayers(light);
    }

    private static void AddPointLight(GameObject go, Color color, float radius, float intensity)
    {
        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = radius * 0.2f;
        ApplyToAllSortingLayers(light);
    }

    private static void ApplyToAllSortingLayers(Light2D light)
    {
        var so = new SerializedObject(light);
        var prop = so.FindProperty("m_ApplyToSortingLayers");
        if (prop == null) return;
        var layers = SortingLayer.layers;
        prop.arraySize = layers.Length;
        for (int i = 0; i < layers.Length; i++) prop.GetArrayElementAtIndex(i).intValue = layers[i].id;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SpriteRenderer CreateMap()
    {
        Sprite bg = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(BackgroundPath))
            if (o is Sprite s) { bg = s; break; }
        if (bg == null) throw new Exception("Lightkeeper: sprite do fundo não encontrado em " + BackgroundPath);

        var go = new GameObject("Mapa");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = bg;
        sr.sortingOrder = -100;

        float spriteHeight = bg.rect.height / bg.pixelsPerUnit;
        float scale = OrthoSize * 2f / spriteHeight;
        go.transform.localScale = Vector3.one * scale;

        // Encosta o mapa na esquerda, deixando o painel da HUD à direita.
        float halfWidth = OrthoSize * ReferenceAspect;
        float mapWidth = bg.rect.width / bg.pixelsPerUnit * scale;
        Vector2 pivotOffset = (bg.pivot / bg.pixelsPerUnit - bg.rect.size / bg.pixelsPerUnit / 2f) * scale;
        go.transform.position = new Vector3(-halfWidth + mapWidth / 2f + pivotOffset.x, pivotOffset.y, 0f);
        return sr;
    }

    /// <summary>Converte um pixel da imagem de fundo (origem no topo-esquerdo) em posição do mundo.</summary>
    private static Vector3 MapPoint(SpriteRenderer map, Vector2 pixel)
    {
        Sprite s = map.sprite;
        float texHeight = s.texture.height;
        float localX = (pixel.x - s.rect.x - s.pivot.x) / s.pixelsPerUnit;
        float localY = ((texHeight - pixel.y) - s.rect.y - s.pivot.y) / s.pixelsPerUnit;
        Vector3 world = map.transform.TransformPoint(new Vector3(localX, localY, 0f));
        world.z = 0f;
        return world;
    }

    private static void AddAudioManager(GameObject go)
    {
        var sfx = go.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        var music = go.AddComponent<AudioSource>();
        music.playOnAwake = false;
        var am = go.AddComponent<AudioManager>();
        Set(am, "sfxSource", sfx);
        Set(am, "musicSource", music);
        Set(am, "music", clips["musica"]);
        Set(am, "musicVolume", 0.35f);
    }

    private static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    private static Transform Child(Transform parent, string name, Vector3 localPos, Vector3 localScale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        return go.transform;
    }

    private static SpriteRenderer AddSprite(GameObject go, string sprite, Color color, int order, bool unlit = false)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprites[sprite];
        sr.color = color;
        sr.sortingOrder = order;
        if (unlit && spriteUnlit != null) sr.sharedMaterial = spriteUnlit;
        return sr;
    }

    private static GameObject SavePrefab(GameObject go, string name)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void CreateFolder(string path)
    {
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>Atribui um campo serializado (inclusive private [SerializeField]) pelo nome.</summary>
    private static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"Lightkeeper: campo '{field}' não existe em {target.GetType().Name}");
            return;
        }

        switch (value)
        {
            case null: p.objectReferenceValue = null; break;
            case float f: p.floatValue = f; break;
            case int i:
                if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i;
                else p.intValue = i;
                break;
            case bool b: p.boolValue = b; break;
            case string s: p.stringValue = s; break;
            case Color c: p.colorValue = c; break;
            case Object o: p.objectReferenceValue = o; break;
            case Object[] arr:
                p.arraySize = arr.Length;
                for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                break;
            default:
                Debug.LogError($"Lightkeeper: tipo não suportado para '{field}': {value.GetType()}");
                break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================================
    // Auxiliares de UI
    // =====================================================================================

    private static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static RectTransform UI(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Panel(Transform parent, string name, Color color)
    {
        var rt = UI(parent, name);
        rt.gameObject.AddComponent<Image>().color = color;
        return rt;
    }

    private static RectTransform CenteredBox(Transform parent, Vector2 size)
    {
        var box = Panel(parent, "Caixa", new Color(0.06f, 0.07f, 0.12f, 0.92f));
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.sizeDelta = size;
        var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 30, 30);
        layout.spacing = 14f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        return box;
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private static void Stretch(RectTransform rt) => Anchor(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    private static TextMeshProUGUI Text(Transform parent, string name, string text, float size,
        TextAlignmentOptions align, Color color, FontStyles style = FontStyles.Normal)
    {
        var rt = UI(parent, name);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = TMP_Settings.defaultFontAsset;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    private static Button Button(Transform parent, string name, string label, float size, Color tint)
    {
        var rt = UI(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Color.white;
        var button = rt.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = new Color(0.2f * tint.r + 0.05f, 0.22f * tint.g + 0.05f, 0.3f * tint.b + 0.05f, 1f);
        colors.highlightedColor = new Color(0.35f * tint.r + 0.1f, 0.38f * tint.g + 0.1f, 0.5f * tint.b + 0.1f, 1f);
        colors.pressedColor = new Color(0.5f, 0.45f, 0.25f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        button.colors = colors;

        var t = Text(rt, "Texto", label, size, TextAlignmentOptions.Center, Color.white);
        Stretch(t.rectTransform);
        return button;
    }

    private static T Sized<T>(T component, float height) where T : Component
    {
        var le = component.GetComponent<LayoutElement>();
        if (le == null) le = component.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return component;
    }
}
