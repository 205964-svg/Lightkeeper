using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Projétil disparado por uma Tower.
/// - Movimento por TRANSLAÇÃO (alteramos transform.position a cada frame).
/// - homing = true: persegue o alvo; false: segue reto na direção inicial.
/// - Colisão: OnTriggerEnter2D com qualquer Enemy causa o impacto.
/// - splashRadius > 0: dano em área (Physics2D.OverlapCircle no ponto do impacto).
/// - slowMultiplier < 1: deixa os inimigos atingidos mais lentos.
/// - É destruído ao acertar, ao exceder o tempo de vida ou ao sair da tela (área válida).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private bool homing = true;
    [SerializeField] private float maxLifetime = 4f;
    [SerializeField] private bool rotateToDirection = true;
    [Tooltip("Correção de ângulo caso o sprite 'aponte' para cima em vez de para a direita.")]
    [SerializeField] private float rotationOffset = 0f;

    [Header("Efeito no impacto")]
    [Tooltip("0 = acerta só um inimigo. > 0 = dano em área com esse raio.")]
    [SerializeField] private float splashRadius = 0f;
    [Tooltip("1 = sem lentidão. 0.5 = inimigo anda na metade da velocidade.")]
    [Range(0.1f, 1f)] [SerializeField] private float slowMultiplier = 1f;
    [SerializeField] private float slowDuration = 0f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Visual e som")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private AudioClip hitSfx;

    private Enemy target;
    private Vector3 targetPosition;
    private Vector3 direction = Vector3.right;
    private float damage;
    private float lifetime;
    private bool hasHit;
    private Camera mainCamera;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        PhysicsUtil.EnsureKinematicBody(gameObject);
        mainCamera = Camera.main;
    }

    /// <summary>Chamado pela torre logo após criar o projétil.</summary>
    public void Launch(Enemy newTarget, float newDamage)
    {
        target = newTarget;
        damage = newDamage;
        targetPosition = newTarget != null ? newTarget.transform.position : transform.position + Vector3.right;
        targetPosition.z = transform.position.z;
        direction = (targetPosition - transform.position).normalized;
        ApplyRotation();
    }

    private void Update()
    {
        lifetime += Time.deltaTime;
        if (lifetime >= maxLifetime || IsOutsidePlayArea())
        {
            Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;

        if (homing)
        {
            // Atualiza o destino enquanto o alvo existir; se ele morrer, vai até a última posição.
            if (target != null && target.IsAlive)
            {
                targetPosition = target.transform.position;
                targetPosition.z = transform.position.z;
            }

            Vector3 toTarget = targetPosition - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f) direction = toTarget.normalized;

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

            // Chegou ao destino: garante o impacto mesmo que a física ainda não tenha detectado.
            if ((transform.position - targetPosition).sqrMagnitude < 0.0001f)
            {
                if (target != null && target.IsAlive) Hit(target);
                else if (splashRadius > 0f) Hit(null);
                else Destroy(gameObject);
                return;
            }
        }
        else
        {
            transform.Translate(direction * step, Space.World);
        }

        ApplyRotation();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null && enemy.IsAlive) Hit(enemy);
    }

    private void Hit(Enemy directHit)
    {
        if (hasHit) return;
        hasHit = true;

        if (splashRadius > 0f)
        {
            // Dano em área: todos os inimigos no raio.
            var filter = new ContactFilter2D();
            filter.SetLayerMask(enemyLayers);
            filter.useTriggers = true;
            var results = new List<Collider2D>();
            int count = Physics2D.OverlapCircle(transform.position, splashRadius, filter, results);

            var damaged = new HashSet<Enemy>();
            for (int i = 0; i < count; i++)
            {
                Enemy enemy = results[i].GetComponentInParent<Enemy>();
                if (enemy != null && enemy.IsAlive && damaged.Add(enemy)) ApplyTo(enemy);
            }
        }
        else if (directHit != null)
        {
            ApplyTo(directHit);
        }

        AudioManager.Play(hitSfx, 0.7f);
        if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    private void ApplyTo(Enemy enemy)
    {
        enemy.ApplySlow(slowMultiplier, slowDuration); // antes do dano, pois o dano pode destruir o inimigo
        enemy.TakeDamage(damage);
    }

    private void ApplyRotation()
    {
        if (!rotateToDirection || direction.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool IsOutsidePlayArea()
    {
        if (mainCamera == null) return false;
        Vector3 viewport = mainCamera.WorldToViewportPoint(transform.position);
        return viewport.x < -0.1f || viewport.x > 1.1f || viewport.y < -0.1f || viewport.y > 1.1f;
    }

    private void OnDrawGizmosSelected()
    {
        if (splashRadius <= 0f) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }
}
