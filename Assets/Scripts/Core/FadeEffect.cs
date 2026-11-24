using UnityEngine;

/// <summary>
/// Efeito visual simples: o sprite cresce e some (fade out), depois o objeto é destruído.
/// Usado em impactos de projéteis, morte de inimigos e construção de torres.
/// </summary>
public class FadeEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private float startScale = 0.3f;
    [SerializeField] private float endScale = 1.2f;

    private float elapsed;
    private Color baseColor = Color.white;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
        transform.localScale = Vector3.one * startScale;
    }

    private void Update()
    {
        // Tempo "real" para o efeito terminar mesmo se o jogo congelar no fim da partida.
        elapsed += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(elapsed / duration);

        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, k);
        if (spriteRenderer != null)
        {
            Color c = baseColor;
            c.a = baseColor.a * (1f - k);
            spriteRenderer.color = c;
        }

        if (k >= 1f) Destroy(gameObject);
    }
}
