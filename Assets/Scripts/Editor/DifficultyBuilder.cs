using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cria a Hidra (inimigo de duas cabeças), a dificuldade Média, endurece a Difícil,
/// equilibra a luz por abate (rewardMultiplier) e adiciona o botão "Jogar - Médio" no menu.
/// Menu: Lightkeeper > Criar dificuldades e Hidra. Pode rodar de novo: atualiza em vez de duplicar.
///
/// Recompensa base por abate: Sombra 10, Colosso 25, Hidra 35 (no prefab).
/// Multiplicador de luz: Fácil 1.0 | Médio 0.85 | Difícil 0.7 — mais inimigos, menos luz por abate.
/// </summary>
public static class DifficultyBuilder
{
    private const string EnemiesDir = "Assets/Prefabs/Enemies";
    private const string DataDir = "Assets/Data";
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Lightkeeper/Criar dificuldades e Hidra")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 1) Hidra: cópia do Colosso com atributos próprios.
        string hydraPath = $"{EnemiesDir}/Hidra.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(hydraPath) == null)
            AssetDatabase.CopyAsset($"{EnemiesDir}/Colosso.prefab", hydraPath);

        GameObject root = PrefabUtility.LoadPrefabContents(hydraPath);
        try
        {
            root.name = "Hidra";
            var so = new SerializedObject(root.GetComponent<Enemy>());
            so.FindProperty("displayName").stringValue = "Hidra";
            so.FindProperty("maxHealth").floatValue = 24f;   // Colosso: 14
            so.FindProperty("speed").floatValue = 1.6f;      // Colosso: 1.1
            so.FindProperty("baseDamage").intValue = 4;
            so.FindProperty("reward").intValue = 35;
            so.FindProperty("scoreValue").intValue = 40;
            so.FindProperty("darkSpeedMultiplier").floatValue = 1.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, hydraPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 2) Visual (inclui a Hidra).
        EnemyStyler.StyleEnemies();

        Enemy sombra = Load("Sombra"), colosso = Load("Colosso"), hidra = Load("Hidra");

        // 3) Dificuldades.
        var facil = AssetDatabase.LoadAssetAtPath<DifficultySettings>($"{DataDir}/Facil.asset");
        if (facil != null)
        {
            facil.rewardMultiplier = 1f;
            EditorUtility.SetDirty(facil);
        }

        DifficultySettings medio = LoadOrCreate("Medio");
        medio.displayName = "Médio";
        medio.description = "5 ondas. A Hidra de duas cabeças aparece a partir da 4ª onda.";
        medio.baseMaxHealth = 16;
        medio.maxEnemiesAtBase = 12;
        medio.timeLimitSeconds = 330f;
        medio.minKillsToWin = 20;
        medio.startingCurrency = 130;
        medio.rewardMultiplier = 0.85f;
        medio.enemyHealthMultiplier = 1.2f;
        medio.enemySpeedMultiplier = 1.1f;
        medio.firstWaveDelay = 5f;
        medio.timeBetweenWaves = 7f;
        medio.waitForWaveCleared = true;
        medio.waves = new List<Wave>
        {
            W("Onda 1", G(sombra, 8, 1.0f)),
            W("Onda 2", G(sombra, 10, 0.8f), G(colosso, 3, 2.2f, 1.5f)),
            W("Onda 3", G(sombra, 12, 0.7f), G(colosso, 4, 2.0f, 1.5f)),
            W("Onda 4", G(sombra, 10, 0.6f), G(colosso, 3, 2.0f, 1f), G(hidra, 2, 3.0f, 1.5f)),
            W("Onda 5", G(sombra, 14, 0.5f), G(colosso, 4, 1.8f, 1f), G(hidra, 4, 2.5f, 1.5f)),
        };
        EditorUtility.SetDirty(medio);

        DifficultySettings dificil = LoadOrCreate("Dificil");
        dificil.displayName = "Difícil";
        dificil.description = "6 ondas, Hidras desde a 2ª onda. O farol aguenta pouco.";
        dificil.baseMaxHealth = 10;
        dificil.maxEnemiesAtBase = 6;
        dificil.timeLimitSeconds = 360f;
        dificil.minKillsToWin = 30;
        dificil.startingCurrency = 110;
        dificil.rewardMultiplier = 0.7f;
        dificil.enemyHealthMultiplier = 1.6f;
        dificil.enemySpeedMultiplier = 1.25f;
        dificil.firstWaveDelay = 5f;
        dificil.timeBetweenWaves = 5f;
        dificil.waitForWaveCleared = true;
        dificil.waves = new List<Wave>
        {
            W("Onda 1", G(sombra, 10, 0.7f)),
            W("Onda 2", G(sombra, 12, 0.6f), G(colosso, 3, 1.8f, 1f), G(hidra, 1, 3.0f, 1f)),
            W("Onda 3", G(sombra, 14, 0.5f), G(colosso, 4, 1.6f, 1f), G(hidra, 2, 2.5f, 1f)),
            W("Onda 4", G(sombra, 16, 0.45f), G(colosso, 5, 1.5f, 1f), G(hidra, 3, 2.2f, 1f)),
            W("Onda 5", G(sombra, 18, 0.4f), G(colosso, 6, 1.4f, 1f), G(hidra, 4, 2.0f, 1f)),
            W("Onda 6", G(sombra, 20, 0.35f), G(colosso, 6, 1.2f, 1f), G(hidra, 6, 1.8f, 1f)),
        };
        EditorUtility.SetDirty(dificil);
        AssetDatabase.SaveAssets();

        // 4) Menu: botão Médio e ordem [Fácil, Médio, Difícil].
        UpdateMenu(facil, medio, dificil);
        Debug.Log("Lightkeeper: Hidra, dificuldade Média e Difícil atualizadas; menu com 3 dificuldades.");
    }

    private static void UpdateMenu(DifficultySettings facil, DifficultySettings medio, DifficultySettings dificil)
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath);
        // OpenScene descarrega assets recém-criados da memória: recarrega pelo caminho.
        facil = AssetDatabase.LoadAssetAtPath<DifficultySettings>($"{DataDir}/Facil.asset");
        medio = AssetDatabase.LoadAssetAtPath<DifficultySettings>($"{DataDir}/Medio.asset");
        dificil = AssetDatabase.LoadAssetAtPath<DifficultySettings>($"{DataDir}/Dificil.asset");
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            Debug.LogWarning("Lightkeeper: MainMenu não encontrado na cena.");
            return;
        }

        var so = new SerializedObject(menu);
        SerializedProperty arr = so.FindProperty("difficulties");
        arr.arraySize = 3;
        arr.GetArrayElementAtIndex(0).objectReferenceValue = facil;
        arr.GetArrayElementAtIndex(1).objectReferenceValue = medio;
        arr.GetArrayElementAtIndex(2).objectReferenceValue = dificil;
        so.ApplyModifiedPropertiesWithoutUndo();

        Button facilBtn = FindButton(scene, "BtnFacil");
        Button dificilBtn = FindButton(scene, "BtnDificil");
        Button medioBtn = FindButton(scene, "BtnMedio");

        if (medioBtn == null && facilBtn != null)
        {
            GameObject copy = Object.Instantiate(facilBtn.gameObject, facilBtn.transform.parent);
            copy.name = "BtnMedio";
            copy.transform.SetSiblingIndex(facilBtn.transform.GetSiblingIndex() + 1);
            medioBtn = copy.GetComponent<Button>();
        }

        if (medioBtn != null)
        {
            SetStartGameIndex(medioBtn, 1);
            foreach (TMP_Text t in medioBtn.GetComponentsInChildren<TMP_Text>(true)) t.text = "Jogar - Médio";
            if (medioBtn.TryGetComponent(out Outline o)) o.effectColor = new Color(1f, 0.7f, 0.2f, 0.9f);
        }
        if (dificilBtn != null) SetStartGameIndex(dificilBtn, 2);
        if (facilBtn != null) SetStartGameIndex(facilBtn, 0);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>Ajusta o argumento inteiro de StartGame(int) no OnClick do botão.</summary>
    private static void SetStartGameIndex(Button button, int index)
    {
        var so = new SerializedObject(button);
        SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        for (int i = 0; i < calls.arraySize; i++)
        {
            SerializedProperty call = calls.GetArrayElementAtIndex(i);
            if (call.FindPropertyRelative("m_MethodName").stringValue == "StartGame")
                call.FindPropertyRelative("m_Arguments.m_IntArgument").intValue = index;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button FindButton(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
                if (b.name == name) return b;
        return null;
    }

    private static Enemy Load(string name) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemiesDir}/{name}.prefab").GetComponent<Enemy>();

    private static DifficultySettings LoadOrCreate(string name)
    {
        string path = $"{DataDir}/{name}.asset";
        var d = AssetDatabase.LoadAssetAtPath<DifficultySettings>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<DifficultySettings>();
            AssetDatabase.CreateAsset(d, path);
        }
        return d;
    }

    private static WaveGroup G(Enemy prefab, int count, float interval, float delay = 0f) =>
        new WaveGroup { enemyPrefab = prefab, count = count, spawnInterval = interval, delayBefore = delay };

    private static Wave W(string name, params WaveGroup[] groups) =>
        new Wave { name = name, groups = new List<WaveGroup>(groups) };
}
