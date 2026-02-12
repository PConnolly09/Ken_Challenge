using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public static int CurrentDown = 1;
    public static bool AutoStartNextLoad = false;
    public static string pendingNotification = "";

    public enum GameState { MainMenu, Playing, Fumble, GameOver, Victory, Paused }
    public GameState currentState;

    private bool debugMode = true;
    private bool hasSubmittedScore = false;

    [Header("UI Panels (Auto-Assigned)")]
    public GameObject mainMenuPanel;
    public GameObject inGameHUD;
    public GameObject fumbleHUD;
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public GameObject highScoreInputGroup;
    public GameObject downNotificationPanel;

    [Header("Pause UI")]
    public GameObject pausePanel;
    public GameObject pauseSettingsPanel;

    [Header("HUD Elements")]
    public TextMeshProUGUI yardsText;
    public TextMeshProUGUI downsText;
    public TextMeshProUGUI attachedText;
    public TextMeshProUGUI fumbleTimerText;
    public TextMeshProUGUI gameOverReasonText;
    public TextMeshProUGUI victoryTimeText;
    public TextMeshProUGUI downNotificationText;
    public TMP_InputField nameInputField;
    public TextMeshProUGUI victoryTitleText;

    [Header("Game Rules")]
    public int maxDowns = 4;
    public Transform playerTransform;
    public float endZoneX = 100f;
    public bool isIntroSequence = false;

    [Header("Fumble Settings")]
    public float maxFumbleTime = 10f;
    private float currentFumbleTimer;
    public Transform currentPackageTransform;

    private float startingX;
    private float startTime;
    private float finalTime;

    private PlayerController _cachedPlayerController;

    private List<string> debugLog = new List<string>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        Physics2D.velocityIterations = 16;
        Physics2D.positionIterations = 16;

        Time.timeScale = 1f;

        if (AutoStartNextLoad) currentState = GameState.Playing;
        else currentState = GameState.MainMenu;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        debugLog.Clear();
        debugLog.Add($"Scene Loaded: {scene.name}");
        StopAllCoroutines();
        StartCoroutine(InitializeLevelRoutine());
    }

    IEnumerator InitializeLevelRoutine()
    {
        yield return null;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p)
            {
                playerTransform = p.transform;
                _cachedPlayerController = p.GetComponent<PlayerController>();
                startingX = playerTransform.position.x;
            }
        }

        if (_cachedPlayerController) _cachedPlayerController.enabled = true;
        isIntroSequence = false;
        hasSubmittedScore = false;

        LocateUIReferences();

        if (AutoStartNextLoad)
        {
            AutoStartNextLoad = false;
            StartGameLogic();
        }
        else
        {
            CurrentDown = 1;
            SetState(GameState.MainMenu);
        }

        if (!string.IsNullOrEmpty(pendingNotification))
        {
            if (downNotificationPanel && downNotificationText)
            {
                downNotificationText.text = pendingNotification;
                downNotificationPanel.SetActive(true);
                downNotificationPanel.transform.SetAsLastSibling();
                StartCoroutine(HideNotificationDelay());
            }
            pendingNotification = "";
        }
    }

    IEnumerator HideNotificationDelay()
    {
        yield return new WaitForSeconds(2.5f);
        if (downNotificationPanel) downNotificationPanel.SetActive(false);
    }

    // --- HELPER FOR BUG REPORTER ---
    public string GetDebugInfo()
    {
        string pPos = playerTransform ? playerTransform.position.ToString() : "null";
        string pVel = _cachedPlayerController ? _cachedPlayerController.GetComponent<Rigidbody2D>().linearVelocity.ToString() : "null";
        string pkgHeld = "unknown";

        // Find package status safely
        if (currentPackageTransform) pkgHeld = "Fumbled (Floor)";
        else if (_cachedPlayerController && _cachedPlayerController.hasPackage) pkgHeld = "Player";
        else pkgHeld = "Unknown/Enemy";

        return $"State: {currentState}\n" +
               $"Down: {CurrentDown}/{maxDowns}\n" +
               $"Time: {Time.time - startTime:F2}s\n" +
               $"Player Pos: {pPos}\n" +
               $"Player Vel: {pVel}\n" +
               $"Package: {pkgHeld}\n" +
               $"Scene: {SceneManager.GetActiveScene().name}";
    }

    private void LocateUIReferences()
    {
        ClearReferences();

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.gameObject.scene.name == null) continue;

            if (inGameHUD == null) inGameHUD = FindChildRecursive(c.transform, "InGameHUD", "HUD", "GameHUD", "HUDPanel");
            if (mainMenuPanel == null) mainMenuPanel = FindChildRecursive(c.transform, "MainMenuPanel", "MainMenu");
            if (gameOverPanel == null) gameOverPanel = FindChildRecursive(c.transform, "GameOverPanel", "GameOver");
            if (fumbleHUD == null) fumbleHUD = FindChildRecursive(c.transform, "FumbleHUD", "FumbleUI", "FumblePanel");
            if (victoryPanel == null) victoryPanel = FindChildRecursive(c.transform, "VictoryPanel", "VictoryScreen");
            if (pausePanel == null) pausePanel = FindChildRecursive(c.transform, "PausePanel", "PauseMenu");
            if (downNotificationPanel == null) downNotificationPanel = FindChildRecursive(c.transform, "DownNotificationPanel", "DownPopup");
        }

        BindComponents();
    }

    private void BindComponents()
    {
        if (inGameHUD != null)
        {
            FixCanvasCamera(inGameHUD);
            if (yardsText == null) yardsText = FindComponentRecursive<TextMeshProUGUI>(inGameHUD.transform, "YardsText");
            if (downsText == null) downsText = FindComponentRecursive<TextMeshProUGUI>(inGameHUD.transform, "DownsText");
            if (attachedText == null) attachedText = FindComponentRecursive<TextMeshProUGUI>(inGameHUD.transform, "WeightText", "AttachedText");
        }

        if (downNotificationPanel != null)
        {
            FixCanvasCamera(downNotificationPanel);
            if (downNotificationText == null) downNotificationText = FindComponentRecursive<TextMeshProUGUI>(downNotificationPanel.transform, "ReasonText", "NotificationText");
        }

        if (gameOverPanel != null)
        {
            FixCanvasCamera(gameOverPanel);
            if (gameOverReasonText == null) gameOverReasonText = FindComponentRecursive<TextMeshProUGUI>(gameOverPanel.transform, "ReasonText");

            BindButton(gameOverPanel, "Restart", FullRestart);
            BindButton(gameOverPanel, "Retry", FullRestart);
            BindButton(gameOverPanel, "Menu", ReturnToMainMenu);
            BindButton(gameOverPanel, "Quit", QuitToDesktop);
        }

        if (fumbleHUD != null)
        {
            FixCanvasCamera(fumbleHUD);
            if (fumbleTimerText == null) fumbleTimerText = FindComponentRecursive<TextMeshProUGUI>(fumbleHUD.transform, "TimerText", "FumbleTimerText");
        }

        if (victoryPanel != null)
        {
            FixCanvasCamera(victoryPanel);
            if (victoryTimeText == null) victoryTimeText = FindComponentRecursive<TextMeshProUGUI>(victoryPanel.transform, "TimeText");
            if (victoryTitleText == null) victoryTitleText = FindComponentRecursive<TextMeshProUGUI>(victoryPanel.transform, "TitleText", "Victory_Text", "VictoryText");
            if (nameInputField == null) nameInputField = FindComponentRecursive<TMP_InputField>(victoryPanel.transform, "NameInput", "Name_Input");

            if (highScoreInputGroup == null && nameInputField != null)
            {
                Transform parent = nameInputField.transform.parent;
                if (parent.name.Contains("Group") || parent.name.Contains("Input"))
                    highScoreInputGroup = parent.gameObject;
                else
                    highScoreInputGroup = nameInputField.gameObject;
            }

            BindButton(victoryPanel, "Menu", ReturnToMainMenu);
            BindButton(victoryPanel, "Restart", FullRestart);
            BindButton(victoryPanel, "PlayAgain", FullRestart);
            BindButton(victoryPanel, "Submit", SubmitScore);
        }

        if (mainMenuPanel != null)
        {
            FixCanvasCamera(mainMenuPanel);
            BindButton(mainMenuPanel, "Start", StartGame);
            BindButton(mainMenuPanel, "Play", StartGame);
            BindButton(mainMenuPanel, "Quit", QuitToDesktop);
        }

        if (pausePanel != null)
        {
            FixCanvasCamera(pausePanel);
            BindButton(pausePanel, "Resume", TogglePause);
            BindButton(pausePanel, "Menu", ReturnToMainMenu);
            BindButton(pausePanel, "Quit", QuitToDesktop);
            // Ensure Try Again does full restart
            BindButton(pausePanel, "Retry", FullRestart);
            BindButton(pausePanel, "Restart", FullRestart);
        }
    }

    private void BindButton(GameObject panel, string partialName, UnityEngine.Events.UnityAction action)
    {
        Button btn = FindComponentRecursive<Button>(panel.transform, partialName);
        if (btn != null)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer &&
               (partialName.Contains("Quit") || action == QuitToDesktop))
            {
                btn.gameObject.SetActive(false);
                return;
            }

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }
    }

    private void ClearReferences()
    {
        mainMenuPanel = null; inGameHUD = null; gameOverPanel = null;
        fumbleHUD = null; victoryPanel = null; pausePanel = null; downNotificationPanel = null;
        if (yardsText != null && yardsText.gameObject == null) yardsText = null;
    }

    private void FixCanvasCamera(GameObject uiObject)
    {
        Canvas c = uiObject.GetComponent<Canvas>();
        if (c == null) c = uiObject.GetComponentInParent<Canvas>();
        if (c != null && c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)
            c.worldCamera = Camera.main;
    }

    private GameObject FindChildRecursive(Transform parent, params string[] names)
    {
        foreach (string n in names)
            if (parent.name.Equals(n, System.StringComparison.OrdinalIgnoreCase)) return parent.gameObject;

        foreach (Transform child in parent)
        {
            GameObject result = FindChildRecursive(child, names);
            if (result != null) return result;
        }
        return null;
    }

    private T FindComponentRecursive<T>(Transform parent, params string[] names) where T : Component
    {
        foreach (string n in names)
        {
            if (parent.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                T comp = parent.GetComponent<T>();
                if (comp != null) return comp;
            }
        }

        foreach (Transform child in parent)
        {
            T result = FindComponentRecursive<T>(child, names);
            if (result != null) return result;
        }
        return null;
    }

    void Update()
    {
        if (GameInput.Instance != null && GameInput.Instance.GetPauseDown())
        {
            TogglePause();
        }

        if (currentState == GameState.Playing)
        {
            if (Time.timeScale == 0f) Time.timeScale = 1f;
            if (isIntroSequence) isIntroSequence = false;
            if (inGameHUD != null && !inGameHUD.activeSelf) inGameHUD.SetActive(true);

            UpdateHUD();
            CheckWinCondition();
        }
        else if (currentState == GameState.Fumble)
        {
            HandleFumbleMode();
        }
    }

    public void StartGameLogic()
    {
        isIntroSequence = false;
        Time.timeScale = 1f;
        startTime = Time.time;

        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (inGameHUD) inGameHUD.SetActive(true);

        MainMenuController mmc = FindFirstObjectByType<MainMenuController>();
        if (mmc) mmc.enabled = false;

        UpdateDownsUI();
        SetState(GameState.Playing);
    }

    // --- STATE MACHINE ---

    public void SetState(GameState newState)
    {
        currentState = newState;
        ResetUI();

        switch (newState)
        {
            case GameState.MainMenu:
                if (mainMenuPanel)
                {
                    mainMenuPanel.SetActive(true);
                    MainMenuController mmc = FindFirstObjectByType<MainMenuController>();
                    if (mmc) mmc.enabled = true;
                }
                Time.timeScale = 0f;
                break;
            case GameState.Playing:
                if (inGameHUD) inGameHUD.SetActive(true);
                Time.timeScale = 1f;
                if (CameraController.Instance) CameraController.Instance.SetCraneView(false);
                break;
            case GameState.Fumble:
                if (fumbleHUD)
                {
                    fumbleHUD.SetActive(true);
                    fumbleHUD.transform.SetAsLastSibling();
                }
                Time.timeScale = 1f;
                break;
            case GameState.GameOver:
                if (gameOverPanel)
                {
                    gameOverPanel.SetActive(true);
                    Time.timeScale = 0f;
                }
                break;
            case GameState.Victory:
                HandleVictory();
                break;
            case GameState.Paused:
                if (pausePanel) pausePanel.SetActive(true);
                Time.timeScale = 0f;
                break;
        }
        UpdateDownsUI();
    }

    private void ResetUI()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (inGameHUD) inGameHUD.SetActive(false);
        if (fumbleHUD) fumbleHUD.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (victoryPanel) victoryPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
        if (pauseSettingsPanel) pauseSettingsPanel.SetActive(false);
        if (downNotificationPanel) downNotificationPanel.SetActive(false);
    }

    // --- GAMEPLAY ACTIONS ---

    public void UseDown(string reason = "TURNOVER ON DOWNS")
    {
        if (currentState == GameState.GameOver || currentState == GameState.MainMenu) return;

        CurrentDown++;
        if (CurrentDown > maxDowns)
        {
            if (gameOverReasonText) gameOverReasonText.text = reason;
            SetState(GameState.GameOver);
        }
        else
        {
            pendingNotification = reason;
            RestartLevel();
        }
    }

    public void RestartLevel()
    {
        AutoStartNextLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void FullRestart()
    {
        CurrentDown = 1;
        RestartLevel();
    }

    public void TogglePause()
    {
        if (currentState == GameState.Playing || currentState == GameState.Fumble) SetState(GameState.Paused);
        else if (currentState == GameState.Paused) SetState(GameState.Playing);
    }

    public void ReturnToMainMenu()
    {
        CurrentDown = 1;
        AutoStartNextLoad = false;
        SetState(GameState.MainMenu);
    }

    public void StartGame() { CurrentDown = 1; StartGameLogic(); }

    public void StartFumbleEvent(Transform package)
    {
        if (currentState != GameState.Playing) return;
        currentPackageTransform = package;
        currentFumbleTimer = maxFumbleTime;
        SetState(GameState.Fumble);
    }

    public void RecoverFumble()
    {
        if (currentState == GameState.Fumble)
        {
            currentPackageTransform = null;
            SetState(GameState.Playing);
        }
    }

    public void PenalizeFumbleTime(float seconds) { currentFumbleTimer -= seconds; }

    private void HandleFumbleMode()
    {
        currentFumbleTimer -= Time.deltaTime;
        if (fumbleTimerText) fumbleTimerText.text = currentFumbleTimer.ToString("F1");
        if (currentFumbleTimer <= 0) UseDown("RECOVERY FAILED");
    }

    private void CheckWinCondition()
    {
        if (playerTransform != null && playerTransform.position.x >= endZoneX)
        {
            bool hasBall = false;
            if (_cachedPlayerController != null) hasBall = _cachedPlayerController.hasPackage;
            else { var pc = playerTransform.GetComponent<PlayerController>(); if (pc) hasBall = pc.hasPackage; }

            if (hasBall) SetState(GameState.Victory);
            else UseDown("Forgot Something?");
        }
    }

    private void HandleVictory()
    {
        Time.timeScale = 0f;
        finalTime = Time.time - startTime;
        if (victoryTimeText) victoryTimeText.text = "TIME: " + finalTime.ToString("F2") + "s";
        if (victoryPanel) victoryPanel.SetActive(true);

        bool isHighScore = false;
        if (LeaderboardManager.Instance != null) isHighScore = LeaderboardManager.Instance.IsHighScore(finalTime);

        if (highScoreInputGroup != null)
        {
            if (hasSubmittedScore)
            {
                highScoreInputGroup.SetActive(false);
            }
            else
            {
                highScoreInputGroup.SetActive(isHighScore);
            }

            if (isHighScore && victoryTitleText) victoryTitleText.text = "NEW RECORD!";
            else if (victoryTitleText) victoryTitleText.text = "TOUCHDOWN!";
        }
        else if (nameInputField != null)
        {
            nameInputField.gameObject.SetActive(isHighScore && !hasSubmittedScore);
        }
    }

    public void SubmitScore()
    {
        if (hasSubmittedScore) return;

        if (nameInputField != null && LeaderboardManager.Instance != null)
        {
            string entryName = nameInputField.text;
            if (string.IsNullOrWhiteSpace(entryName)) entryName = "Unknown";

            LeaderboardManager.Instance.AddScore(entryName, finalTime, CurrentDown);

            hasSubmittedScore = true;

            if (highScoreInputGroup) highScoreInputGroup.SetActive(false);
            else if (nameInputField) nameInputField.gameObject.SetActive(false);

            if (victoryTitleText) victoryTitleText.text = "SAVED!";
        }
    }

    private void UpdateHUD()
    {
        if (playerTransform == null || yardsText == null) return;
        float yards = Mathf.Max(0, playerTransform.position.x - startingX);
        yardsText.text = "YARDS: " + Mathf.FloorToInt(yards).ToString();
    }

    public void UpdateAttachmentCount(int count) { if (attachedText) attachedText.text = "WEIGHT: " + count.ToString(); }

    private void UpdateDownsUI()
    {
        if (downsText)
        {
            string suffix = (CurrentDown == 1) ? "st" : (CurrentDown == 2) ? "nd" : (CurrentDown == 3) ? "rd" : "th";
            downsText.text = $"{CurrentDown}{suffix} & GOAL";
        }
    }

    public void QuitToDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit(); 
#endif
    }

    void OnGUI()
    {
        if (!debugMode) return;
        GUIStyle style = new GUIStyle(); style.fontSize = 20; style.normal.textColor = Color.yellow; style.fontStyle = FontStyle.Bold;
        GUILayout.BeginArea(new Rect(10, 10, 400, 350), "DEBUG INFO", GUI.skin.window);
        GUILayout.Label($"State: {currentState}", style);
        string hudStatus = (inGameHUD != null) ? inGameHUD.activeSelf.ToString() : "NULL REF";
        GUILayout.Label($"HUD Active: {hudStatus}", style);

        GUILayout.Label("LOGS:", style);
        for (int i = Mathf.Max(0, debugLog.Count - 3); i < debugLog.Count; i++)
            GUILayout.Label(debugLog[i], style);

        if (GUILayout.Button("Force Restart")) RestartLevel();
        GUILayout.EndArea();
    }
}