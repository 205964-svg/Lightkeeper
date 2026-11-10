using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuração de um nível de dificuldade (ScriptableObject = arquivo de dados no projeto).
/// Crie com: botão direito na pasta > Create > Lightkeeper > Dificuldade.
/// Cada dificuldade define suas próprias ondas, então "Fácil" e "Difícil" podem ter
/// quantidades, tipos e intervalos de inimigos diferentes.
/// </summary>
[CreateAssetMenu(fileName = "Dificuldade", menuName = "Lightkeeper/Dificuldade")]
public class DifficultySettings : ScriptableObject
{
    [Header("Identificação")]
    public string displayName = "Normal";
    [TextArea] public string description = "";

    [Header("Farol (base)")]
    public int baseMaxHealth = 20;
    [Tooltip("Derrota se essa quantidade de inimigos alcançar o farol. 0 = sem limite (só a vida conta).")]
    public int maxEnemiesAtBase = 0;

    [Header("Missão")]
    [Tooltip("Tempo máximo da partida, em segundos.")]
    public float timeLimitSeconds = 300f;
    [Tooltip("Mínimo de inimigos eliminados para vencer ao fim das ondas.")]
    public int minKillsToWin = 5;

    [Header("Economia")]
    public int startingCurrency = 100;

    [Header("Inimigos")]
    [Tooltip("Multiplica a vida de todos os inimigos.")]
    public float enemyHealthMultiplier = 1f;
    [Tooltip("Multiplica a velocidade de todos os inimigos.")]
    public float enemySpeedMultiplier = 1f;

    [Header("Ondas")]
    public float firstWaveDelay = 5f;
    public float timeBetweenWaves = 8f;
    [Tooltip("Se marcado, a próxima onda só começa quando todos os inimigos da atual sumirem.")]
    public bool waitForWaveCleared = true;
    public List<Wave> waves = new List<Wave>();

    /// <summary>Total de inimigos previstos em todas as ondas.</summary>
    public int TotalEnemies
    {
        get
        {
            int total = 0;
            foreach (var wave in waves)
                foreach (var group in wave.groups)
                    total += group.count;
            return total;
        }
    }
}

/// <summary>Uma onda é uma sequência de grupos de inimigos.</summary>
[Serializable]
public class Wave
{
    public string name = "Onda";
    public List<WaveGroup> groups = new List<WaveGroup>();
}

/// <summary>Um grupo: N inimigos do mesmo tipo, com intervalo fixo entre eles.</summary>
[Serializable]
public class WaveGroup
{
    public Enemy enemyPrefab;
    [Min(1)] public int count = 5;
    [Tooltip("Segundos entre um inimigo e o próximo deste grupo.")]
    public float spawnInterval = 1f;
    [Tooltip("Espera antes de começar este grupo.")]
    public float delayBefore = 0f;
    [Tooltip("Índice do caminho no WaveSpawner (para mapas com mais de uma entrada).")]
    public int pathIndex = 0;
}
