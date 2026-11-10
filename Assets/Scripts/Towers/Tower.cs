using System.Collections.Generic;
using UnityEngine;

/// <summary>Como a torre escolhe o alvo entre os inimigos dentro do alcance.</summary>
public enum TargetingMode
{
    First,      // o que está mais adiantado no caminho (mais perto do farol)
    Closest,    // o mais próximo da torre
    Strongest,  // o com mais vida
    Weakest     // o com menos vida
}

/// <summary>
/// Estrutura defensiva. Ciclo de cada frame:
///   1. DETECTAR inimigos dentro do raio (Physics2D.OverlapCircle);
///   2. SELECIONAR um alvo conforme o TargetingMode;
///   3. MIRAR (gira a parte móvel na direção do alvo);
///   4. DISPARAR um Projectile quando o tempo de recarga acabar;
///   5. AGUARDAR 1/fireRate segundos até o próximo disparo.
///
/// Os tipos de torre são prefabs diferentes com este mesmo script, mudando alcance, cadência,
/// dano, modo de mira e — principalmente — o prefab do projétil (rápido x área com lentidão).
/// </summary>
public class Tower : MonoBehaviour
{
    [Header("Identificação")]
    [SerializeField] private string displayName = "Torre";
    [TextArea] [SerializeField] private string description = "";
    [SerializeField] private int cost = 50;
    [SerializeField] private Sprite icon;

    [Header("Ataque")]
    [SerializeField] private float range = 3f;
    [Tooltip("Disparos por segundo.")]
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float damage = 1f;
    [SerializeField] private TargetingMode targeting = TargetingMode.First;
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("De onde o projétil sai. Se vazio, sai do centro da torre.")]
    [SerializeField] private Transform firePoint;
    [Tooltip("Camadas consideradas inimigas. Deixe 'Everything' se não usar layers.")]
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Visual e som")]
    [Tooltip("Parte que gira para mirar (canhão, lente...). Opcional.")]
    [SerializeField] private Transform rotatingPart;
    [Tooltip("Correção de ângulo caso o sprite 'aponte' para cima em vez de para a direita.")]
    [SerializeField] private float rotationOffset = 0f;
    [Tooltip("Sprite circular mostrando o alcance (aparece ao passar o mouse). Opcional.")]
    [SerializeField] private SpriteRenderer rangeIndicator;
    [Tooltip("Opcional: Animator com um Trigger chamado 'Shoot'.")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioClip shootSfx;

    public string DisplayName => displayName;
    public string Description => description;
    public int Cost => cost;
    public Sprite Icon => icon;
    public float Range => range;
    public BuildSpot Spot { get; set; }

    private const float ScanInterval = 0.15f;

    private Enemy target;
    private float cooldown;
    private float scanTimer;
    private ContactFilter2D filter;
    private readonly List<Collider2D> hits = new List<Collider2D>();

    private void Awake()
    {
        filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayers);
        filter.useTriggers = true;

        if (rangeIndicator != null)
        {
            // Supõe um sprite circular de 1 unidade de diâmetro.
            rangeIndicator.transform.localScale = Vector3.one * range * 2f;
            rangeIndicator.enabled = false;
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        cooldown -= Time.deltaTime;
        scanTimer -= Time.deltaTime;

        // Reavalia o alvo periodicamente (o "primeiro" da fila muda o tempo todo) ou se o atual sumiu.
        if (scanTimer <= 0f || !IsValidTarget(target))
        {
            target = FindTarget();
            scanTimer = ScanInterval;
        }
        if (target == null) return;

        Aim(target.transform.position);

        if (cooldown <= 0f)
        {
            Fire();
            cooldown = 1f / Mathf.Max(0.01f, fireRate);
        }
    }

    private bool IsValidTarget(Enemy enemy)
    {
        if (enemy == null || !enemy.IsAlive) return false;
        return ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude <= range * range;
    }

    /// <summary>Detecta inimigos no raio e escolhe o melhor segundo o modo de mira.</summary>
    private Enemy FindTarget()
    {
        int count = Physics2D.OverlapCircle(transform.position, range, filter, hits);

        Enemy best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = hits[i].GetComponentInParent<Enemy>();
            if (enemy == null || !enemy.IsAlive) continue;

            float score = targeting switch
            {
                TargetingMode.First => enemy.DistanceTravelled,
                TargetingMode.Closest => -((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude,
                TargetingMode.Strongest => enemy.Health,
                TargetingMode.Weakest => -enemy.Health,
                _ => 0f
            };

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }
        return best;
    }

    private void Aim(Vector3 targetPosition)
    {
        if (rotatingPart == null) return;
        Vector2 dir = targetPosition - rotatingPart.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + rotationOffset;
        rotatingPart.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Fire()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"{name}: sem prefab de projétil.");
            return;
        }

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Projectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
        projectile.Launch(target, damage);

        AudioManager.Play(shootSfx, 0.6f);
        if (animator != null) animator.SetTrigger("Shoot");
    }

    public void ShowRange(bool show)
    {
        if (rangeIndicator != null) rangeIndicator.enabled = show;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
