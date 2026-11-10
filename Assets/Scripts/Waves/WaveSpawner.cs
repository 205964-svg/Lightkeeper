using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Cria os inimigos onda a onda, seguindo a lista de ondas da dificuldade atual.
/// Usa uma Coroutine: uma função que "pausa" (yield) e continua nos frames seguintes,
/// o que deixa a sequência de tempo (esperar X segundos, criar inimigo, esperar...) bem legível.
/// Quando a última onda termina e não sobra inimigo vivo, avisa o GameManager.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    [Tooltip("Caminhos disponíveis. O WaveGroup.pathIndex escolhe qual usar.")]
    [SerializeField] private EnemyPath[] paths;
    [Tooltip("Objeto pai dos inimigos criados (só para organizar a Hierarchy).")]
    [SerializeField] private Transform enemiesParent;
    [SerializeField] private AudioClip waveStartSfx;

    public int CurrentWave { get; private set; }
    public int TotalWaves => GameManager.Instance != null ? GameManager.Instance.Difficulty.waves.Count : 0;
    /// <summary>Segundos até a próxima onda (0 quando uma onda está em andamento).</summary>
    public float NextWaveCountdown { get; private set; }
    public bool IsSpawning { get; private set; }

    public event Action<int> WaveStarted;

    private void Start()
    {
        if (paths == null || paths.Length == 0)
        {
            Debug.LogError("WaveSpawner: nenhum EnemyPath configurado.");
            return;
        }
        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        GameManager gm = GameManager.Instance;
        DifficultySettings difficulty = gm.Difficulty;

        yield return Countdown(difficulty.firstWaveDelay);

        for (int w = 0; w < difficulty.waves.Count; w++)
        {
            if (gm.IsGameOver) yield break;

            CurrentWave = w + 1;
            WaveStarted?.Invoke(CurrentWave);
            AudioManager.Play(waveStartSfx);

            IsSpawning = true;
            foreach (WaveGroup group in difficulty.waves[w].groups)
            {
                if (group.delayBefore > 0f) yield return new WaitForSeconds(group.delayBefore);

                for (int i = 0; i < group.count; i++)
                {
                    if (gm.IsGameOver) yield break;
                    Spawn(group, difficulty);
                    yield return new WaitForSeconds(group.spawnInterval);
                }
            }
            IsSpawning = false;

            // Espera a onda ser eliminada (ou chegar no farol) antes da próxima.
            if (difficulty.waitForWaveCleared || w == difficulty.waves.Count - 1)
            {
                while (Enemy.Active.Count > 0)
                {
                    if (gm.IsGameOver) yield break;
                    yield return null;
                }
            }

            if (w < difficulty.waves.Count - 1)
                yield return Countdown(difficulty.timeBetweenWaves);
        }

        gm.OnAllWavesFinished();
    }

    private IEnumerator Countdown(float seconds)
    {
        NextWaveCountdown = seconds;
        while (NextWaveCountdown > 0f)
        {
            NextWaveCountdown -= Time.deltaTime;
            yield return null;
        }
        NextWaveCountdown = 0f;
    }

    private void Spawn(WaveGroup group, DifficultySettings difficulty)
    {
        if (group.enemyPrefab == null)
        {
            Debug.LogWarning("WaveSpawner: WaveGroup sem prefab de inimigo.");
            return;
        }

        EnemyPath path = paths[Mathf.Clamp(group.pathIndex, 0, paths.Length - 1)];
        Enemy enemy = Instantiate(group.enemyPrefab, path.GetWaypoint(0), Quaternion.identity, enemiesParent);
        enemy.Init(path, difficulty.enemyHealthMultiplier, difficulty.enemySpeedMultiplier);
    }

    /// <summary>Botão "Chamar próxima onda": pula a contagem regressiva.</summary>
    public void SkipCountdown()
    {
        NextWaveCountdown = 0f;
    }
}
