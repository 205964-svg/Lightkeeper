using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class RankingEntry
{
    public string playerName;
    public int score;
    public string difficulty;
    public bool victory;
    public string date;
}

/// <summary>
/// Ranking simples (ponto-extra): guarda as melhores pontuações em PlayerPrefs, como JSON.
/// PlayerPrefs persiste entre execuções do jogo no mesmo computador.
/// </summary>
public static class RankingManager
{
    private const string Key = "Lightkeeper.Ranking";
    private const int MaxEntries = 10;

    // JsonUtility não serializa listas "soltas", por isso o wrapper.
    [Serializable]
    private class RankingData
    {
        public List<RankingEntry> entries = new List<RankingEntry>();
    }

    public static List<RankingEntry> Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return new List<RankingEntry>();

        RankingData data = JsonUtility.FromJson<RankingData>(json);
        return data?.entries ?? new List<RankingEntry>();
    }

    public static void AddEntry(string playerName, int score, string difficulty, bool victory)
    {
        List<RankingEntry> entries = Load();
        entries.Add(new RankingEntry
        {
            playerName = playerName,
            score = score,
            difficulty = difficulty,
            victory = victory,
            date = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        });

        entries.Sort((a, b) => b.score.CompareTo(a.score)); // maior pontuação primeiro
        if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

        PlayerPrefs.SetString(Key, JsonUtility.ToJson(new RankingData { entries = entries }));
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    /// <summary>Texto pronto para exibir num TMP_Text.</summary>
    public static string Format()
    {
        List<RankingEntry> entries = Load();
        if (entries.Count == 0) return "Nenhuma partida registrada ainda.";

        var sb = new StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            RankingEntry e = entries[i];
            string result = e.victory ? "Vitória" : "Derrota";
            sb.AppendLine($"{i + 1}. {e.playerName} — {e.score} pts ({e.difficulty}, {result}) {e.date}");
        }
        return sb.ToString();
    }
}
