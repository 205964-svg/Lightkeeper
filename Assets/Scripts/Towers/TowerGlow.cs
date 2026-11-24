using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Faz o brilho da torre "viver": a chama da Lanterna tremula e o cristal do Prisma pulsa.
/// Varia a transparência do sprite de brilho e a intensidade da luz 2D juntas.
/// Puramente visual — o alcance de luz do jogo (LightSource) não muda.
/// </summary>
public class TowerGlow : MonoBehaviour
{
    [SerializeField] private SpriteRenderer glow;
    [SerializeField] private Light2D light2D;
    [Tooltip("Velocidade da variação. Alto = tremular de chama; baixo = pulsar suave.")]
    [SerializeField] private float speed = 6f;
    [Tooltip("Quanto o brilho varia (0.15 = ±15%).")]
    [SerializeField] private float amount = 0.15f;

    private float baseAlpha = 1f;
    private float baseIntensity = 1f;
    private float phase;

    private void Awake()
    {
        phase = Random.Range(0f, 100f);
        if (glow != null) baseAlpha = glow.color.a;
        if (light2D != null) baseIntensity = light2D.intensity;
    }

    private void Update()
    {
        // Ruído de Perlin dá uma variação orgânica, sem padrão repetitivo.
        float k = Mathf.PerlinNoise(Time.time * speed, phase);
        float factor = 1f - amount + amount * 2f * k;

        if (glow != null)
        {
            Color c = glow.color;
            c.a = baseAlpha * factor;
            glow.color = c;
        }
        if (light2D != null) light2D.intensity = baseIntensity * factor;
    }
}
