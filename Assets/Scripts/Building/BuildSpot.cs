using UnityEngine;

/// <summary>
/// Posição predefinida onde o jogador pode construir uma torre.
/// Requisitos: Collider2D (para o clique ser detectado) e, de preferência, um SpriteRenderer
/// (um "pedestal") que muda de cor quando o mouse passa por cima.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BuildSpot : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.4f, 0.9f);
    [SerializeField] private Color occupiedColor = new Color(1f, 1f, 1f, 0.15f);

    public Tower CurrentTower { get; private set; }
    public bool IsOccupied => CurrentTower != null;

    private bool hovered;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        RefreshColor();
    }

    public Tower Build(Tower towerPrefab, Transform parent)
    {
        if (IsOccupied || towerPrefab == null) return null;
        CurrentTower = Instantiate(towerPrefab, transform.position, Quaternion.identity, parent);
        CurrentTower.Spot = this;
        RefreshColor();
        return CurrentTower;
    }

    /// <summary>Remove a torre e retorna o valor devolvido ao jogador.</summary>
    public int Sell(float refundRatio)
    {
        if (!IsOccupied) return 0;
        int refund = Mathf.RoundToInt(CurrentTower.Cost * refundRatio);
        Destroy(CurrentTower.gameObject);
        CurrentTower = null;
        RefreshColor();
        return refund;
    }

    public void SetHovered(bool value)
    {
        hovered = value;
        if (CurrentTower != null) CurrentTower.ShowRange(value);
        RefreshColor();
    }

    private void RefreshColor()
    {
        if (spriteRenderer == null) return;
        if (IsOccupied) spriteRenderer.color = occupiedColor;
        else spriteRenderer.color = hovered ? hoverColor : normalColor;
    }
}
