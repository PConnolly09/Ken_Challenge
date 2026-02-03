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
        // Don't override the screen if the game is actually running
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.Playing)
        {
            gameObject.SetActive(false);
            return;
        }

        ShowRoot();
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
        UpdateLeaderboardText();
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

    // --- SETTINGS LOGIC ---

    public void SetMasterVolume(float value)
    {
        AudioListener.volume = value;
    }

    public void SetMusicVolume(float value)
    {
        if (AudioManager.Instance)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
    }

    public void SetSFXVolume(float value)
    {
        if (AudioManager.Instance)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
    }

    // --- LEADERBOARD FORMATTING ---

    private void UpdateLeaderboardText()
    {
        if (LeaderboardManager.Instance == null || leaderboardText == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>TOP RUSHERS</b>\n");

        var entries = LeaderboardManager.Instance.data.entries;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];

            // Format Time (e.g., 65.5s -> "01:05")
            int minutes = Mathf.FloorToInt(e.time / 60F);
            int seconds = Mathf.FloorToInt(e.time - minutes * 60);
            string timeStr = string.Format("{0:0}:{1:00}", minutes, seconds);

            // Format Down (1 -> "1st", 2 -> "2nd", etc)
            string downSuffix = GetOrdinalSuffix(e.down);
            string downStr = $"{e.down}{downSuffix} down";

            // Fixed width formatting using dots
            string rank = $"#{i + 1}";
            string line = $"{rank}: {e.name} ................. {timeStr} / {downStr}";

            sb.AppendLine(line);
        }

        if (entries.Count == 0) sb.AppendLine("No Records Yet!");

        leaderboardText.text = sb.ToString();
    }

    private string GetOrdinalSuffix(int num)
    {
        if (num <= 0) return "th"; // 0th down fallback

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