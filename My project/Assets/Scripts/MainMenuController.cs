using UnityEngine;
using TMPro;
using System.Text;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Sub-Panels")]
    public GameObject rootMenu;
    public GameObject leaderboardPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    [Header("Leaderboard UI")]
    public TextMeshProUGUI leaderboardText;

    void OnEnable()
    {
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.Playing)
        {
            gameObject.SetActive(false);
            return;
        }

        ShowRoot();

        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnScoresLoaded += UpdateLeaderboardText;
            LeaderboardManager.Instance.RefreshScores();
        }
    }

    void OnDisable()
    {
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnScoresLoaded -= UpdateLeaderboardText;
        }
    }

    public void ShowRoot()
    {
        if (rootMenu) rootMenu.SetActive(true);
        if (leaderboardPanel) leaderboardPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(false);
    }

    public void ShowLeaderboard()
    {
        if (rootMenu) rootMenu.SetActive(false);
        if (leaderboardPanel) leaderboardPanel.SetActive(true);

        if (leaderboardText) leaderboardText.text = "Loading Scores...";

        if (LeaderboardManager.Instance) LeaderboardManager.Instance.RefreshScores();
    }

    public void ShowSettings()
    {
        if (rootMenu) rootMenu.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void ShowCredits()
    {
        if (rootMenu) rootMenu.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(true);
    }

    public void SetMasterVolume(float value) { AudioListener.volume = value; }
    public void SetMusicVolume(float value) { if (AudioManager.Instance) AudioManager.Instance.SetMusicVolume(value); }
    public void SetSFXVolume(float value) { if (AudioManager.Instance) AudioManager.Instance.SetSFXVolume(value); }

    // --- LEADERBOARD FORMATTING ---

    private void UpdateLeaderboardText()
    {
        if (LeaderboardManager.Instance == null || leaderboardText == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>TOP 50 RUSHERS (GLOBAL)</b>\n");

        var entries = LeaderboardManager.Instance.data.entries;
        int count = Mathf.Min(entries.Count, 50);

        for (int i = 0; i < count; i++)
        {
            var e = entries[i];

            // FIX: High Precision Formatting (00:00.000)
            int minutes = Mathf.FloorToInt(e.time / 60F);
            int seconds = Mathf.FloorToInt(e.time % 60F);
            int milliseconds = Mathf.FloorToInt((e.time * 1000F) % 1000F);

            string timeStr = string.Format("{0:00}:{1:00}.{2:000}", minutes, seconds, milliseconds);

            string downSuffix = GetOrdinalSuffix(e.down);
            string downStr = $"{e.down}{downSuffix} down";

            string rank = $"#{i + 1}";
            string line = $"{rank}: {e.name} ................. {timeStr} / {downStr}";

            sb.AppendLine(line);
        }

        if (entries.Count == 0) sb.AppendLine("No Records Yet!");

        leaderboardText.text = sb.ToString();
    }

    private string GetOrdinalSuffix(int num)
    {
        if (num <= 0) return "th";
        switch (num % 100)
        {
            case 11:
            case 12:
            case 13:
                return "th";
        }
        switch (num % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }
}