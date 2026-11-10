using System.Collections;
using UnityEngine;

/// <summary>
/// O farol — a base a ser defendida. Detecta a chegada de inimigos por colisão (trigger)
/// e repassa ao GameManager, que desconta a vida. Também faz um feedback visual (piscar + tremer).
/// Requisitos: um Collider2D cobrindo o farol (vira trigger automaticamente).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Lighthouse : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private float shakeAmount = 0.08f;
    [SerializeField] private GameObject hitEffectPrefab;

    private Color baseColor = Color.white;
    private Vector3 basePosition;
    private Coroutine feedback;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
        basePosition = transform.position;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || !enemy.IsAlive) return;

        if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, enemy.transform.position, Quaternion.identity);
        enemy.ReachBase();

        if (feedback != null) StopCoroutine(feedback);
        feedback = StartCoroutine(DamageFeedback());
    }

    private IEnumerator DamageFeedback()
    {
        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.unscaledDeltaTime; // funciona mesmo se o jogo congelar na derrota
            if (spriteRenderer != null) spriteRenderer.color = damageColor;
            transform.position = basePosition + (Vector3)(Random.insideUnitCircle * shakeAmount);
            yield return null;
        }
        if (spriteRenderer != null) spriteRenderer.color = baseColor;
        transform.position = basePosition;
        feedback = null;
    }
}
