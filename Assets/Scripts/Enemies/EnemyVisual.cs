using UnityEngine;

/// <summary>
/// Animação puramente visual do inimigo (não afeta a lógica nem a colisão):
/// - flutua/pisa subindo e descendo, com leve achatamento (squash);
/// - balança um pouco (rotação);
/// - a aura pulsa;
/// - a sombra no chão encolhe quando o corpo sobe.
/// Anima o filho "Visual" — o objeto raiz é movido pelo Enemy e não pode ser mexido aqui.
/// </summary>
public class EnemyVisual : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private Transform shadow;
    [SerializeField] private SpriteRenderer aura;

    [Header("Movimento")]
    [SerializeField] private float bobFrequency = 3f;
    [SerializeField] private float bobHeight = 0.08f;
    [Tooltip("Pisada: o movimento usa |sen| (bate no chão) em vez de flutuar suave.")]
    [SerializeField] private bool stomp = false;
    [SerializeField] private float squash = 0.05f;
    [SerializeField] private float swayDegrees = 4f;

    [Header("Aura")]
    [SerializeField] private float auraPulseSpeed = 2.5f;
    [SerializeField] private float auraMinAlpha = 0.25f;
    [SerializeField] private float auraMaxAlpha = 0.55f;

    private float phase;
    private Vector3 visualBasePos;
    private Vector3 visualBaseScale = Vector3.one;
    private Vector3 shadowBaseScale;
    private Color auraBaseColor;

    private void Awake()
    {
        phase = Random.Range(0f, 10f); // cada inimigo num ritmo diferente
        if (visual != null)
        {
            visualBasePos = visual.localPosition;
            visualBaseScale = visual.localScale;
        }
        if (shadow != null) shadowBaseScale = shadow.localScale;
        if (aura != null) auraBaseColor = aura.color;
    }

    private void Update()
    {
        float t = Time.time * bobFrequency + phase;
        float wave = stomp ? Mathf.Abs(Mathf.Sin(t)) : (Mathf.Sin(t) * 0.5f + 0.5f); // 0..1

        if (visual != null)
        {
            visual.localPosition = visualBasePos + Vector3.up * (wave * bobHeight);
            // Achata quando está embaixo, estica quando está em cima.
            float s = (wave - 0.5f) * 2f * squash;
            visual.localScale = Vector3.Scale(visualBaseScale, new Vector3(1f - s, 1f + s, 1f));
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * swayDegrees);
        }

        if (shadow != null)
            shadow.localScale = shadowBaseScale * (1f - wave * 0.2f);

        if (aura != null)
        {
            float k = Mathf.Sin(Time.time * auraPulseSpeed + phase) * 0.5f + 0.5f;
            Color c = auraBaseColor;
            c.a = Mathf.Lerp(auraMinAlpha, auraMaxAlpha, k);
            aura.color = c;
        }
    }
}
