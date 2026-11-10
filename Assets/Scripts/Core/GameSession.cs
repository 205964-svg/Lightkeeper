using UnityEngine;

/// <summary>
/// Dados que precisam "sobreviver" à troca de cena (menu -> jogo).
/// Campos estáticos não são destruídos quando a cena é recarregada.
/// </summary>
public static class GameSession
{
    private const string PlayerNameKey = "Lightkeeper.PlayerName";

    /// <summary>Dificuldade escolhida no menu. Null = usar a padrão da cena.</summary>
    public static DifficultySettings SelectedDifficulty;

    /// <summary>Nome do jogador, salvo em PlayerPrefs para o ranking.</summary>
    public static string PlayerName
    {
        get => PlayerPrefs.GetString(PlayerNameKey, "Jogador");
        set
        {
            string clean = string.IsNullOrWhiteSpace(value) ? "Jogador" : value.Trim();
            PlayerPrefs.SetString(PlayerNameKey, clean);
            PlayerPrefs.Save();
        }
    }
}
