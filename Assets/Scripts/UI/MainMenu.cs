using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu principal: escolher dificuldade e iniciar, ver instruções ("Como jogar"),
/// ver ranking e sair. Ligue os botões aos métodos públicos pelo OnClick() no Inspector.
/// Ex.: botão "Fácil" -> StartGame(0); botão "Difícil" -> StartGame(1).
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [Tooltip("Dificuldades disponíveis, na ordem dos botões.")]
    [SerializeField] private DifficultySettings[] difficulties;

    [Header("UI")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject rankingPanel;
    [SerializeField] private TMP_Text rankingText;
    [SerializeField] private TMP_Text difficultyDescriptionText;

    private void Start()
    {
        Time.timeScale = 1f;
        if (playerNameInput != null) playerNameInput.text = GameSession.PlayerName;
        ShowMain();
    }

    public void StartGame(int difficultyIndex)
    {
        if (difficulties == null || difficultyIndex < 0 || difficultyIndex >= difficulties.Length)
        {
            Debug.LogError($"MainMenu: dificuldade {difficultyIndex} não configurada.");
            return;
        }

        if (playerNameInput != null) GameSession.PlayerName = playerNameInput.text;
        GameSession.SelectedDifficulty = difficulties[difficultyIndex];
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>Opcional: chame no PointerEnter de um botão de dificuldade (Event Trigger).</summary>
    public void ShowDifficultyDescription(int difficultyIndex)
    {
        if (difficultyDescriptionText == null || difficulties == null) return;
        if (difficultyIndex < 0 || difficultyIndex >= difficulties.Length) return;

        DifficultySettings d = difficulties[difficultyIndex];
        difficultyDescriptionText.text =
            $"{d.displayName}: {d.description}\n" +
            $"{d.waves.Count} ondas, {d.TotalEnemies} inimigos, {Mathf.RoundToInt(d.timeLimitSeconds)}s";
    }

    public void ShowMain() => ShowPanel(mainPanel);
    public void ShowInfo() => ShowPanel(infoPanel);

    public void ShowRanking()
    {
        if (rankingText != null) rankingText.text = RankingManager.Format();
        ShowPanel(rankingPanel);
    }

    public void ClearRanking()
    {
        RankingManager.Clear();
        if (rankingText != null) rankingText.text = RankingManager.Format();
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowPanel(GameObject panel)
    {
        if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
        if (infoPanel != null) infoPanel.SetActive(panel == infoPanel);
        if (rankingPanel != null) rankingPanel.SetActive(panel == rankingPanel);
    }
}
