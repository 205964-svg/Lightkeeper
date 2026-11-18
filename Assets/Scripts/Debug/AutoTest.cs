using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Teste automático (ferramenta de desenvolvimento). Só é ativado quando o executável é
/// iniciado com o argumento "-autotest", então não afeta o jogo normal.
///
/// Exemplo: Lightkeeper.exe -autotest C:\pasta\saida -autotest-difficulty 0
///
/// O que ele faz: tira print do menu, inicia a partida, compra torres com a luz disponível
/// nos pedestais livres, acelera o tempo, tira prints periódicos e registra o resultado
/// em autotest.log. Código de saída: 0 = vitória, 2 = derrota, 1 = erro.
/// </summary>
public class AutoTest : MonoBehaviour
{
    private static string outputDir;
    private static int difficultyIndex;
    private const float Speed = 4f;
    private const float MaxRealSeconds = 400f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        bool enabled = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-autotest")
            {
                enabled = true;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-")) outputDir = args[i + 1];
            }
            else if (args[i] == "-autotest-difficulty" && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out difficultyIndex);
            }
        }
        if (!enabled) return;

        if (string.IsNullOrEmpty(outputDir)) outputDir = Application.persistentDataPath;
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "autotest.log"), "");

        var go = new GameObject("AutoTest");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTest>();
    }

    private void Log(string message)
    {
        Debug.Log("[AutoTest] " + message);
        File.AppendAllText(Path.Combine(outputDir, "autotest.log"), message + "\n");
    }

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        yield return Shot("01_menu");

        MainMenu menu = FindAnyObjectByType<MainMenu>();
        if (menu == null)
        {
            Log("ERRO: MainMenu não encontrado na primeira cena.");
            Application.Quit(1);
            yield break;
        }
        menu.StartGame(difficultyIndex);

        float wait = 0f;
        while (GameManager.Instance == null && wait < 10f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Log("ERRO: GameManager não apareceu após carregar a cena.");
            Application.Quit(1);
            yield break;
        }

        Log($"Partida iniciada: {gm.Difficulty.displayName}, {gm.Difficulty.waves.Count} ondas, " +
            $"{gm.Difficulty.TotalEnemies} inimigos, luz inicial {gm.Currency}");
        yield return new WaitForSecondsRealtime(0.5f);
        BuyTowers(gm);
        yield return Shot("02_inicio");

        float real = 0f, nextShot = 6f, nextBuy = 2f;
        int shotIndex = 3;
        while (!gm.IsGameOver && real < MaxRealSeconds)
        {
            if (gm.IsPlaying) Time.timeScale = Speed;
            real += Time.unscaledDeltaTime;

            if (real >= nextBuy)
            {
                BuyTowers(gm);
                nextBuy += 2f;
            }
            if (real >= nextShot && shotIndex < 12)
            {
                yield return Shot($"{shotIndex++:00}_partida");
                Log(Stats(gm));
                nextShot += 6f;
            }
            yield return null;
        }

        Log($"FIM: {gm.State} — {gm.EndReason}");
        Log(Stats(gm));
        yield return new WaitForSecondsRealtime(1f);
        yield return Shot($"{shotIndex:00}_fim");
        Application.Quit(gm.State == GameState.Victory ? 0 : 2);
    }

    private static string Stats(GameManager gm)
    {
        return $"t={gm.Difficulty.timeLimitSeconds - gm.TimeRemaining:0}s farol={gm.BaseHealth}/{gm.MaxBaseHealth} " +
               $"luz={gm.Currency} abates={gm.Kills} chegaram={gm.EnemiesReachedBase} vivos={Enemy.Active.Count} pontos={gm.Score}";
    }

    /// <summary>Compra torres nos pedestais livres, alternando os tipos, enquanto houver luz.</summary>
    private void BuyTowers(GameManager gm)
    {
        BuildManager build = BuildManager.Instance;
        if (build == null) return;

        BuildSpot[] spots = FindObjectsByType<BuildSpot>(FindObjectsSortMode.InstanceID);
        int built = 0;
        foreach (BuildSpot spot in spots)
        {
            if (spot.IsOccupied) continue;
            Tower tower = build.TowerPrefabs[CountTowers(spots) % build.TowerPrefabs.Length];
            if (tower == null)
            {
                Log("ERRO: BuildManager com prefab de torre vazio.");
                return;
            }
            if (!gm.TrySpend(tower.Cost)) break;
            spot.Build(tower, null);
            built++;
            Log($"Construiu {tower.DisplayName} em {spot.name} (luz restante {gm.Currency})");
        }
    }

    private static int CountTowers(BuildSpot[] spots)
    {
        int n = 0;
        foreach (BuildSpot s in spots) if (s.IsOccupied) n++;
        return n;
    }

    private IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(outputDir, name + ".png"));
        yield return null;
    }
}
