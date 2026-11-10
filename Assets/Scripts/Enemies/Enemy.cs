using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Criatura sombria que percorre o EnemyPath até o farol.
/// - Movimento: translação ponto a ponto com Vector3.MoveTowards.
/// - Vida: recebe dano de projéteis (TakeDamage) e é destruída ao chegar a zero.
/// - Luz e sombra: fora do alcance de qualquer LightSource, anda mais rápido.
/// - Lentidão: projéteis de luz podem desacelerar (ApplySlow).
/// - Ao alcançar o farol (colisão com o Lighthouse), causa dano à base e some.
///
/// Requisitos no prefab: um Collider2D (marcado como trigger automaticamente) e um SpriteRenderer.
/// O Rigidbody2D cinemático é adicionado sozinho se faltar — ele é necessário para gerar
/// eventos de colisão (OnTriggerEnter2D) com projéteis e com o farol.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour
{
    // Lista de todos os inimigos vivos na cena (usada pelo WaveSpawner para saber se a onda acabou).
    private static readonly List<Enemy> active = new List<Enemy>();
    public static IReadOnlyList<Enemy> Active => active;

    [Header("Atributos")]
    [SerializeField] private string displayName = "Sombra";
    [SerializeField] private float maxHealth = 5f;
    [SerializeField] private float speed = 2f;
    [Tooltip("Quanto de vida o farol perde quando este inimigo o alcança.")]
    [SerializeField] private int baseDamage = 1;
    [Tooltip("Luz (recurso) ganha ao eliminar.")]
    [SerializeField] private int reward = 10;
    [SerializeField] private int scoreValue = 10;

    [Header("Luz e sombra")]
    [Tooltip("Multiplicador de velocidade quando o inimigo está fora de qualquer luz.")]
    [SerializeField] private float darkSpeedMultiplier = 1.3f;

    [Header("Visual e som")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private float hitFlashDuration = 0.08f;
    [SerializeField] private Color slowedTint = new Color(0.6f, 0.8f, 1f);
    [Tooltip("Espelha o sprite horizontalmente conforme a direção do movimento.")]
    [SerializeField] private bool flipSpriteByDirection = true;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private AudioClip deathSfx;

    public string DisplayName => displayName;
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public int BaseDamage => baseDamage;
    public int Reward => reward;
    public int ScoreValue => scoreValue;
    /// <summary>Distância já percorrida no caminho (usada pela mira "Primeiro").</summary>
    public float DistanceTravelled { get; private set; }
    public bool IsAlive => !finished;
    public bool IsSlowed => slowMultiplier < 1f;

    /// <summary>(vidaAtual, vidaMáxima) — usado pela barra de vida.</summary>
    public event Action<float, float> HealthChanged;

    private EnemyPath path;
    private int waypointIndex;
    private float currentSpeed;
    private float slowMultiplier = 1f;
    private float slowTimer;
    private float flashTimer;
    private Color baseColor = Color.white;
    private bool finished;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;

        GetComponent<Collider2D>().isTrigger = true;
        PhysicsUtil.EnsureKinematicBody(gameObject);

        MaxHealth = maxHealth;
        Health = maxHealth;
        currentSpeed = speed;
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    /// <summary>Chamado pelo WaveSpawner logo após criar o inimigo.</summary>
    public void Init(EnemyPath enemyPath, float healthMultiplier, float speedMultiplier)
    {
        path = enemyPath;
        MaxHealth = maxHealth * healthMultiplier;
        Health = MaxHealth;
        currentSpeed = speed * speedMultiplier;

        if (path != null && path.Count > 0)
        {
            transform.position = path.GetWaypoint(0);
            waypointIndex = 1;
        }
        HealthChanged?.Invoke(Health, MaxHealth);
    }

    private void Update()
    {
        if (finished || path == null) return;
        UpdateStatusEffects();
        MoveAlongPath();
    }

    private void MoveAlongPath()
    {
        // Chegou ao fim do caminho sem tocar no collider do farol? Conta como chegada mesmo assim.
        if (waypointIndex >= path.Count)
        {
            ReachBase();
            return;
        }

        Vector3 current = transform.position;
        Vector3 target = path.GetWaypoint(waypointIndex);
        target.z = current.z;

        float lightFactor = LightSource.IsLit(current) ? 1f : darkSpeedMultiplier;
        float step = currentSpeed * slowMultiplier * lightFactor * Time.deltaTime;

        Vector3 next = Vector3.MoveTowards(current, target, step);
        DistanceTravelled += Vector3.Distance(current, next);
        transform.position = next;

        if (flipSpriteByDirection && spriteRenderer != null && Mathf.Abs(target.x - current.x) > 0.001f)
            spriteRenderer.flipX = target.x < current.x;

        if ((next - target).sqrMagnitude < 0.0001f)
            waypointIndex++;
    }

    private void UpdateStatusEffects()
    {
        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f) slowMultiplier = 1f;
        }
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;

        if (spriteRenderer != null)
        {
            if (flashTimer > 0f) spriteRenderer.color = hitFlashColor;
            else if (IsSlowed) spriteRenderer.color = baseColor * slowedTint;
            else spriteRenderer.color = baseColor;
        }
    }

    // ---------------------------------------------------------------- Interações

    public void TakeDamage(float amount)
    {
        if (finished) return;

        Health -= amount;
        flashTimer = hitFlashDuration;
        HealthChanged?.Invoke(Mathf.Max(Health, 0f), MaxHealth);

        if (Health <= 0f) Die();
    }

    /// <summary>Reduz a velocidade. multiplier = 0.5 significa metade da velocidade.</summary>
    public void ApplySlow(float multiplier, float duration)
    {
        if (finished || duration <= 0f || multiplier >= 1f) return;
        slowMultiplier = Mathf.Min(slowMultiplier, multiplier);
        slowTimer = Mathf.Max(slowTimer, duration);
    }

    private void Die()
    {
        finished = true;
        if (GameManager.Instance != null) GameManager.Instance.RegisterKill(this);
        AudioManager.Play(deathSfx);
        if (deathEffectPrefab != null) Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    /// <summary>Chamado pelo Lighthouse ao detectar a colisão (ou no fim do caminho).</summary>
    public void ReachBase()
    {
        if (finished) return;
        finished = true;
        if (GameManager.Instance != null) GameManager.Instance.RegisterEnemyAtBase(this);
        Destroy(gameObject);
    }
}
