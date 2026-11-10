using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interface durante a partida. Só LÊ o estado do GameManager/WaveSpawner/BuildManager e mostra;
/// os botões chamam os métodos públicos daqui (ligue-os no OnClick() do Button, no Inspector).
/// Todos os campos são opcionais: o que não estiver ligado é simplesmente ignorado.
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private WaveSpawner spawner;

    [Header("Textos")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Slider baseHealthSlider;

    [Header("Botões de torre (mesma ordem do BuildManager)")]
    [SerializeField] private Button[] towerButtons;
    [SerializeField] private TMP_Text[] towerButtonLabels;
    [SerializeField] private Color selectedButtonColor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Image sellButtonImage;

    [Header("Painéis")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMP_Text endTitleText;
    [SerializeField] private TMP_Text endDetailsText;

    [SerializeField] private float messageDuration = 2f;

    private float messageTimer;

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        gm.StatsChanged += RefreshStats;
        gm.StateChanged += OnStateChanged;

        BuildManager build = BuildManager.Instance;
        if (build != null)
        {
            build.SelectionChanged += RefreshTowerButtons;
            build.Message += ShowMessage;
            build.SellModeChanged += OnSellModeChanged;
            SetupTowerButtons(build);
        }

        if (pausePanel != null) pausePanel.SetActive(false);
        if (endPanel != null) endPanel.SetActive(false);
        if (messageText != null) messageText.text = "";

        ShowMessage($"Dificuldade: {gm.Difficulty.displayName} — proteja o farol!");
        RefreshStats();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StatsChanged -= RefreshStats;
            GameManager.Instance.StateChanged -= OnStateChanged;
        }
        if (BuildManager.Instance != null)
        {
            BuildManager.Instance.SelectionChanged -= RefreshTowerButtons;
            BuildManager.Instance.Message -= ShowMessage;
            BuildManager.Instance.SellModeChanged -= OnSellModeChanged;
        }
    }

    private void SetupTowerButtons(BuildManager build)
    {
        if (towerButtons == null) return;
        for (int i = 0; i < towerButtons.Length; i++)
        {
            if (towerButtons[i] == null) continue;
            int index = i; // cópia local para a closure
            towerButtons[i].onClick.AddListener(() => build.SelectTower(index));

            if (towerButtonLabels != null && i < towerButtonLabels.Length && towerButtonLabels[i] != null
                && i < build.TowerPrefabs.Length)
            {
                Tower t = build.TowerPrefabs[i];
                towerButtonLabels[i].text = $"[{i + 1}] {t.DisplayName}\n{t.Cost} luz";
            }
        }
        RefreshTowerButtons(build.SelectedIndex);
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(gm.TimeRemaining);
            timerText.text = $"Tempo {seconds / 60:00}:{seconds % 60:00}";
            timerText.color = gm.TimeRemaining < 30f ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }

        if (waveText != null && spawner != null)
        {
            string wave = $"Onda {spawner.CurrentWave}/{spawner.TotalWaves}";
            if (spawner.NextWaveCountdown > 0f)
                wave += $" — próxima em {Mathf.CeilToInt(spawner.NextWaveCountdown)}s";
            waveText.text = wave;
        }

        // Desabilita botões de torres que o jogador não consegue pagar.
        BuildManager build = BuildManager.Instance;
        if (build != null && towerButtons != null)
        {
            for (int i = 0; i < towerButtons.Length && i < build.TowerPrefabs.Length; i++)
                if (towerButtons[i] != null) towerButtons[i].interactable = gm.Currency >= build.TowerPrefabs[i].Cost;
        }

        if (messageTimer > 0f)
        {
            messageTimer -= Time.unscaledDeltaTime;
            if (messageTimer <= 0f && messageText != null) messageText.text = "";
        }
    }

    private void RefreshStats()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (healthText != null) healthText.text = $"Farol {gm.BaseHealth}/{gm.MaxBaseHealth}";
        if (currencyText != null) currencyText.text = $"Luz {gm.Currency}";
        if (killsText != null) killsText.text = $"Abates {gm.Kills}/{gm.Difficulty.minKillsToWin}";
        if (baseHealthSlider != null)
        {
            baseHealthSlider.maxValue = gm.MaxBaseHealth;
            baseHealthSlider.value = gm.BaseHealth;
        }
    }

    private void RefreshTowerButtons(int selected)
    {
        if (towerButtons == null) return;
        for (int i = 0; i < towerButtons.Length; i++)
            if (towerButtons[i] != null && towerButtons[i].image != null)
                towerButtons[i].image.color = i == selected ? selectedButtonColor : normalButtonColor;
    }

    private void OnSellModeChanged(bool on)
    {
        if (sellButtonImage != null) sellButtonImage.color = on ? selectedButtonColor : normalButtonColor;
        ShowMessage(on ? "Modo vender: clique numa torre" : "Modo construir");
    }

    private void OnStateChanged(GameState state)
    {
        if (pausePanel != null) pausePanel.SetActive(state == GameState.Paused);

        if (state == GameState.Victory || state == GameState.Defeat)
        {
            GameManager gm = GameManager.Instance;
            if (endPanel != null) endPanel.SetActive(true);
            if (endTitleText != null) endTitleText.text = state == GameState.Victory ? "VITÓRIA!" : "DERROTA";
            if (endDetailsText != null)
            {
                endDetailsText.text =
                    $"{gm.EndReason}\n\n" +
                    $"Inimigos eliminados: {gm.Kills}\n" +
                    $"Vida do farol: {gm.BaseHealth}/{gm.MaxBaseHealth}\n" +
                    $"Pontuação: {gm.Score}";
            }
        }
    }

    public void ShowMessage(string text)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageTimer = messageDuration;
    }

    // ------------------------------------------------ Métodos para os botões (OnClick)

    public void OnPauseButton() => GameManager.Instance.TogglePause();
    public void OnRestartButton() => GameManager.Instance.Restart();
    public void OnMenuButton() => GameManager.Instance.GoToMenu();
    public void OnNextWaveButton() { if (spawner != null) spawner.SkipCountdown(); }
    public void OnSellModeButton() { if (BuildManager.Instance != null) BuildManager.Instance.ToggleSellMode(); }
}
