using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Interação do jogador com o mapa (usa o Input System novo — funciona com mouse e toque):
///   - Clique/toque num BuildSpot vazio: constrói a torre selecionada (se houver luz suficiente).
///   - Clique direito numa torre (ou clique com o "modo vender" ligado): vende a torre.
///   - Teclas 1..9: seleciona o tipo de torre. P ou Esc: pausa.
/// </summary>
public class BuildManager : MonoBehaviour
{
    public static BuildManager Instance { get; private set; }

    [SerializeField] private Tower[] towerPrefabs;
    [SerializeField] private Transform towersParent;
    [Tooltip("Fração do custo devolvida ao vender.")]
    [Range(0f, 1f)] [SerializeField] private float sellRefund = 0.5f;

    [Header("Feedback")]
    [SerializeField] private GameObject buildEffectPrefab;
    [SerializeField] private AudioClip buildSfx;
    [SerializeField] private AudioClip sellSfx;
    [SerializeField] private AudioClip errorSfx;

    public Tower[] TowerPrefabs => towerPrefabs;
    public int SelectedIndex { get; private set; }
    public Tower SelectedTower =>
        towerPrefabs != null && SelectedIndex >= 0 && SelectedIndex < towerPrefabs.Length ? towerPrefabs[SelectedIndex] : null;
    public bool SellMode { get; private set; }

    /// <summary>Índice da torre selecionada mudou.</summary>
    public event Action<int> SelectionChanged;
    /// <summary>Mensagem curta para o jogador (ex.: "Luz insuficiente!").</summary>
    public event Action<string> Message;
    public event Action<bool> SellModeChanged;

    private Camera mainCamera;
    private BuildSpot hoveredSpot;

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        HandleKeyboard();

        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.IsPlaying)
        {
            SetHovered(null);
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        BuildSpot spot = GetSpotAt(pointer.position.ReadValue());
        SetHovered(spot);
        if (spot == null || IsPointerOverUI()) return;

        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        if (rightClick && spot.IsOccupied)
        {
            Sell(spot);
        }
        else if (pointer.press.wasPressedThisFrame)
        {
            if (spot.IsOccupied)
            {
                if (SellMode) Sell(spot);
                else Message?.Invoke($"{spot.CurrentTower.DisplayName} — clique direito para vender");
            }
            else
            {
                TryBuild(spot);
            }
        }
    }

    private void HandleKeyboard()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            GameManager.Instance?.TogglePause();

        if (towerPrefabs == null) return;
        for (int i = 0; i < towerPrefabs.Length && i < 9; i++)
        {
            if (kb[Key.Digit1 + i].wasPressedThisFrame) SelectTower(i);
        }
    }

    public void SelectTower(int index)
    {
        if (towerPrefabs == null || index < 0 || index >= towerPrefabs.Length) return;
        SelectedIndex = index;
        SetSellMode(false);
        SelectionChanged?.Invoke(index);
    }

    public void ToggleSellMode() => SetSellMode(!SellMode);

    private void SetSellMode(bool value)
    {
        if (SellMode == value) return;
        SellMode = value;
        SellModeChanged?.Invoke(value);
    }

    private void TryBuild(BuildSpot spot)
    {
        Tower prefab = SelectedTower;
        if (prefab == null) return;

        if (!GameManager.Instance.TrySpend(prefab.Cost))
        {
            AudioManager.Play(errorSfx);
            Message?.Invoke($"Luz insuficiente! {prefab.DisplayName} custa {prefab.Cost}.");
            return;
        }

        spot.Build(prefab, towersParent);
        AudioManager.Play(buildSfx);
        if (buildEffectPrefab != null) Instantiate(buildEffectPrefab, spot.transform.position, Quaternion.identity);
    }

    private void Sell(BuildSpot spot)
    {
        string towerName = spot.CurrentTower.DisplayName;
        int refund = spot.Sell(sellRefund);
        GameManager.Instance.AddCurrency(refund);
        AudioManager.Play(sellSfx);
        Message?.Invoke($"{towerName} vendida (+{refund})");
    }

    private BuildSpot GetSpotAt(Vector2 screenPosition)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return null;

        Vector2 world = mainCamera.ScreenToWorldPoint(screenPosition);
        foreach (Collider2D col in Physics2D.OverlapPointAll(world))
        {
            BuildSpot spot = col.GetComponentInParent<BuildSpot>();
            if (spot != null) return spot;
        }
        return null;
    }

    private void SetHovered(BuildSpot spot)
    {
        if (spot == hoveredSpot) return;
        if (hoveredSpot != null) hoveredSpot.SetHovered(false);
        hoveredSpot = spot;
        if (hoveredSpot != null) hoveredSpot.SetHovered(true);
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
