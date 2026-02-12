using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;

    [Header("Dreamlo Settings")]
    [Tooltip("Check dreamlo.com to get these. PRIVATE key allows writing.")]
    public string privateCode = "";
    [Tooltip("PUBLIC key allows reading only.")]
    public string publicCode = "";

    // HTTPS is mandatory for secure WebGL/Desktop compliance
    private const string webURL = "https://dreamlo.com/lb/";

    [System.Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public float time;
        public int down;
    }

    [System.Serializable]
    public class LeaderboardData
    {
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }

    public LeaderboardData data = new LeaderboardData();
    public event Action OnScoresLoaded;

    private const int MAX_SCORES = 50;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Auto-fetch on game start so data is ready for the menu
        RefreshScores();
    }

    public void AddScore(string playerName, float time, int down)
    {
        StartCoroutine(UploadNewScore(playerName, time, down));
    }

    public void RefreshScores()
    {
        StartCoroutine(DownloadScoresFromCloud());
    }

    public bool IsHighScore(float time)
    {
        if (data.entries.Count < MAX_SCORES) return true;
        if (data.entries.Count == 0) return true;

        // Check if better (lower) than the worst score
        return time < data.entries[data.entries.Count - 1].time;
    }

    // --- UPLOAD ---

    IEnumerator UploadNewScore(string playerName, float time, int down)
    {
        if (string.IsNullOrEmpty(privateCode))
        {
            Debug.LogError("LeaderboardManager: Private Code is missing!");
            yield break;
        }

        // Dreamlo sorts by Descending (Highest is best). 
        // For a speedrun (Lowest is best), we store negative milliseconds.
        // Example: 12.345 seconds -> -12345
        int scoreValue = (int)(time * -1000);

        // Sanitize inputs
        string cleanName = UnityWebRequest.EscapeURL(playerName);
        string downText = down.ToString();

        // URL Structure: dreamlo.com/lb/PRIVATE_CODE/add/NAME/SCORE/SECONDS/TEXT
        // We use the 'seconds' slot (0) as dummy, and 'text' slot for the Down count.
        string url = $"{webURL}{privateCode}/add/{cleanName}/{scoreValue}/0/{downText}";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Score Uploaded Successfully");
                // Immediately refresh so the player sees their name on the board
                RefreshScores();
            }
            else
            {
                Debug.LogError($"Upload Failed: {www.error}");
            }
        }
    }

    // --- DOWNLOAD ---

    IEnumerator DownloadScoresFromCloud()
    {
        if (string.IsNullOrEmpty(publicCode)) yield break;

        // Pipe separates scores. no_cache prevents Unity/Browser caching old data.
        string url = $"{webURL}{publicCode}/pipe/{MAX_SCORES}?no_cache={UnityEngine.Random.Range(0, 100000)}";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                FormatScores(www.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning("Error Downloading Scores: " + www.error);
            }
        }
    }

    void FormatScores(string textStream)
    {
        data.entries.Clear();

        if (string.IsNullOrEmpty(textStream)) return;

        // Dreamlo pipe format: Name|Score|Seconds|ShortText|Date...
        string[] rows = textStream.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string row in rows)
        {
            string[] parts = row.Split('|');
            if (parts.Length < 2) continue;

            try
            {
                LeaderboardEntry entry = new LeaderboardEntry();
                entry.name = parts[0];

                // Parse Score
                if (int.TryParse(parts[1], out int score))
                {
                    // Convert back to positive float seconds
                    entry.time = Mathf.Abs(score) / 1000f;
                }

                // Parse Down (stored in the 'text' field, index 3)
                if (parts.Length > 3)
                {
                    int.TryParse(parts[3], out entry.down);
                }

                data.entries.Add(entry);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Skipped malformed row: {e.Message}");
            }
        }

        // Explicitly sort locally just in case (Lowest time first)
        data.entries = data.entries.OrderBy(x => x.time).ToList();

        OnScoresLoaded?.Invoke();
    }
}