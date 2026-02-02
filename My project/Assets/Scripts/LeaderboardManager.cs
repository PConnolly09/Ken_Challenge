using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;

    [System.Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public float time;
        public int down; // Added Down tracking
    }

    [System.Serializable]
    public class LeaderboardData
    {
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }

    public LeaderboardData data;
    private const string PREF_KEY = "LeaderboardData_V2"; // V2 to reset old data format if needed

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadScores();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddScore(string name, float time, int down)
    {
        // 1. Create Entry
        LeaderboardEntry newEntry = new LeaderboardEntry
        {
            name = string.IsNullOrEmpty(name) ? "Unknown" : name,
            time = time,
            down = down
        };

        // 2. Add and Sort (Lowest Time is Best)
        data.entries.Add(newEntry);
        data.entries = data.entries.OrderBy(x => x.time).ToList();

        // 3. Keep Top 10
        if (data.entries.Count > 10)
        {
            data.entries.RemoveRange(10, data.entries.Count - 10);
        }

        // 4. Save
        SaveScores();
    }

    public bool IsHighScore(float time)
    {
        if (data.entries.Count < 10) return true;
        return time < data.entries[data.entries.Count - 1].time;
    }

    private void SaveScores()
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(PREF_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadScores()
    {
        if (PlayerPrefs.HasKey(PREF_KEY))
        {
            string json = PlayerPrefs.GetString(PREF_KEY);
            data = JsonUtility.FromJson<LeaderboardData>(json);
        }
        else
        {
            data = new LeaderboardData();
        }
    }

    // Debug helper to clear data
    [ContextMenu("Clear Leaderboard")]
    public void ClearLeaderboard()
    {
        PlayerPrefs.DeleteKey(PREF_KEY);
        data = new LeaderboardData();
    }
}