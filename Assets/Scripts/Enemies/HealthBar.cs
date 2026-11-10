using UnityEngine;

/// <summary>
/// Barrinha de vida acima do inimigo, feita com dois sprites (fundo + preenchimento).
/// Montagem: filho "HealthBar" no prefab do inimigo, com um sprite de fundo e um filho "Fill".
/// O sprite do Fill deve ter o pivô na ESQUERDA (Sprite Editor > Pivot: Left) para encolher
/// da direita para a esquerda.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private Transform fill;
    [Tooltip("Objeto mostrado/escondido. Se vazio, usa este próprio objeto.")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private bool hideWhenFull = true;

    private Vector3 fullScale;

    private void Awake()
    {
        if (enemy == null) enemy = GetComponentInParent<Enemy>();
        if (visualRoot == null) visualRoot = gameObject;
        if (fill != null) fullScale = fill.localScale;
        if (enemy != null) enemy.HealthChanged += OnHealthChanged;
        SetVisible(!hideWhenFull);
    }

    private void OnDestroy()
    {
        if (enemy != null) enemy.HealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (fill != null) fill.localScale = new Vector3(fullScale.x * ratio, fullScale.y, fullScale.z);
        SetVisible(!hideWhenFull || ratio < 1f);
    }

    private void SetVisible(bool visible)
    {
        // Se o visualRoot for este mesmo objeto, desligamos só os renderers para não parar o script.
        if (visualRoot == gameObject)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }
        else
        {
            visualRoot.SetActive(visible);
        }
    }

    private void LateUpdate()
    {
        // Mantém a barra reta mesmo que o inimigo gire.
        transform.rotation = Quaternion.identity;
    }
}
