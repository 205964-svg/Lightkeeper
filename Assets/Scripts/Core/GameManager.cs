using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Estados possíveis de uma partida.</summary>
public enum GameState { Playing, Paused, Victory, Defeat }

/// <summary>
/// Cérebro da partida: guarda vida do farol, recursos (luz), abates, tempo restante e pontuação,
/// e decide vitória/derrota. Existe um único GameManager por cena (padrão Singleton).
/// Os outros scripts avisam o GameManager quando algo acontece (inimigo morreu, chegou no farol...)
/// e a interface (HUD) escuta os eventos StatsChanged / StateChanged para se atualizar.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuração")]
    [Tooltip("Dificuldade usada quando a cena é aberta direto no editor, sem passar pelo menu.")]
    [SerializeField] private DifficultySettings defaultDifficulty;
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("Sons")]
    [SerializeField] private AudioClip baseHitSfx;
    [SerializeField] private AudioClip victorySfx;
    [SerializeField] private AudioClip defeatSfx;

    public DifficultySettings Difficulty { get; private set; }
    public GameState State { get; private set; } = GameState.Playing;
    public bool IsPlaying => State == GameState.Playing;
    public bool IsGameOver => State == GameState.Victory || State == GameState.Defeat;

    public int BaseHealth { get; private set; }
    public int MaxBaseHealth => Difficulty.baseMaxHealth;
    /// <summary>Recurso usado para construir torres ("luz").</summary>
    public int Currency { get; private set; }
    public int Kills { get; private set; }
    public int EnemiesReachedBase { get; private set; }
    public float TimeRemaining { get; private set; }
    public int Score { get; private set; }
    public string EndReason { get; private set; } = "";

    /// <summary>Disparado sempre que vida, recursos, abates ou pontuação mudam.</summary>
    public event Action StatsChanged;
    /// <summary>Disparado quando o estado muda (pausa, vitória, derrota).</summary>
    public event Action<GameState> StateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // A dificuldade escolhida no menu tem prioridade; senão usa a padrão da cena.
        Difficulty = GameSession.SelectedDifficulty != null ? GameSession.SelectedDifficulty : defaultDifficulty;
        if (Difficulty == null)
        {
            Debug.LogError("GameManager: nenhuma DifficultySettings definida. Usando valores padrão.");
            Difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
        }

        BaseHealth = Difficulty.baseMaxHealth;
        Currency = Difficulty.startingCurrency;
        TimeRemaining = Difficulty.timeLimitSeconds;
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsPlaying) return;

        // Tempo máximo da missão.
        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            EndGame(false, "O tempo acabou antes de a missão ser cumprida!");
        }
    }

    // ---------------------------------------------------------------- Recursos

    public void AddCurrency(int amount)
    {
        Currency += amount;
        StatsChanged?.Invoke();
    }

    /// <summary>Tenta gastar recursos. Retorna false se não houver o suficiente.</summary>
    public bool TrySpend(int amount)
    {
        if (Currency < amount) return false;
        Currency -= amount;
        StatsChanged?.Invoke();
        return true;
    }

    // ---------------------------------------------------------------- Eventos dos inimigos

    /// <summary>Chamado pelo Enemy quando a vida dele chega a zero.</summary>
    public void RegisterKill(Enemy enemy)
    {
        if (!IsPlaying) return;
        Kills++;
        Score += enemy.ScoreValue;
        AddCurrency(enemy.Reward); // já dispara StatsChanged
    }

    /// <summary>Chamado quando um inimigo alcança o farol (colisão com o Lighthouse).</summary>
    public void RegisterEnemyAtBase(Enemy enemy)
    {
        if (!IsPlaying) return;

        EnemiesReachedBase++;
        BaseHealth = Mathf.Max(0, BaseHealth - enemy.BaseDamage);
        AudioManager.Play(baseHitSfx);
        StatsChanged?.Invoke();

        if (BaseHealth <= 0)
            EndGame(false, "A luz do farol se apagou!");
        else if (Difficulty.maxEnemiesAtBase > 0 && EnemiesReachedBase >= Difficulty.maxEnemiesAtBase)
            EndGame(false, "Criaturas demais alcançaram o farol!");
    }

    /// <summary>Chamado pelo WaveSpawner quando todas as ondas terminaram e não há inimigos vivos.</summary>
    public void OnAllWavesFinished()
    {
        if (!IsPlaying) return;

        if (Kills >= Difficulty.minKillsToWin)
            EndGame(true, "Todas as ondas foram repelidas!");
        else
            EndGame(false, $"Missão incompleta: {Kills}/{Difficulty.minKillsToWin} criaturas eliminadas.");
    }

    // ---------------------------------------------------------------- Estados

    private void EndGame(bool victory, string reason)
    {
        if (IsGameOver) return;

        State = victory ? GameState.Victory : GameState.Defeat;
        EndReason = reason;

        // Bônus de vitória: vida restante do farol e tempo que sobrou.
        if (victory)
            Score += BaseHealth * 10 + Mathf.RoundToInt(TimeRemaining);

        RankingManager.AddEntry(GameSession.PlayerName, Score, Difficulty.displayName, victory);
        AudioManager.Play(victory ? victorySfx : defeatSfx);

        Time.timeScale = 0f; // congela inimigos, torres e projéteis
        StateChanged?.Invoke(State);
        StatsChanged?.Invoke();
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
        {
            State = GameState.Paused;
            Time.timeScale = 0f;
        }
        else if (State == GameState.Paused)
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
        }
        else return;

        StateChanged?.Invoke(State);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }
}
