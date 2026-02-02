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

    public enum GameState { MainMenu, Playing, Fumble, GameOver, Victory, Paused }
    public GameState currentState;

    private bool debugMode = true;

    [Header("UI Panels (Auto-Assigned)")]
    public GameObject mainMenuPanel;
    public GameObject inGameHUD;
    public GameObject fumbleHUD;
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public GameObject highScoreInputGroup;

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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Time.timeScale = 1f;

        if (AutoStartNextLoad) currentState = GameState.Playing;
        else currentState = GameState.MainMenu;
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();
        StartCoroutine(InitializeLevelRoutine());
    }

    IEnumerator InitializeLevelRoutine()
    {
        yield return null;

        // 1. Find Player
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

        // 2. Find UI using exact names from your screenshot
        LocateUIReferences();

        // 3. Apply State
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
    }

    private void LocateUIReferences()
    {
        ClearReferences();

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            // Using exact names from your hierarchy image
            if (inGameHUD == null) inGameHUD = FindChildRecursive(c.transform, "HUDPanel");
            if (mainMenuPanel == null) mainMenuPanel = FindChildRecursive(c.transform, "MainMenuPanel");
            if (gameOverPanel == null) gameOverPanel = FindChildRecursive(c.transform, "GameOverPanel");
            if (fumbleHUD == null) fumbleHUD = FindChildRecursive(c.transform, "FumblePanel");
            if (victoryPanel == null) victoryPanel = FindChildRecursive(c.transform, "VictoryPanel");
            if (pausePanel == null) pausePanel = FindChildRecursive(c.transform, "PausePanel");
        }

        BindComponents();
    }

    private void BindComponents()
    {
        if (inGameHUD != null)
        {
            FixCanvasCamera(inGameHUD);
            yardsText = FindComponentRecursive<TextMeshProUGUI>(inGameHUD.transform, "YardsText");
            downsText = FindComponentRecursive<TextMeshProUGUI>(inGameHUD.transform, "DownsText");
            attachedText = FindComponentRecursive<TextMeshProUGUI>(inGameHUD.transform, "AttachedText");
        }

        if (gameOverPanel != null)
        {
            FixCanvasCamera(gameOverPanel);
            gameOverReasonText = FindComponentRecursive<TextMeshProUGUI>(gameOverPanel.transform, "ReasonText");

            BindButton(gameOverPanel, "RetryButton", FullRestart);
            BindButton(gameOverPanel, "QuitButton", QuitToDesktop);
        }

        if (fumbleHUD != null)
        {
            FixCanvasCamera(fumbleHUD);
            fumbleTimerText = FindComponentRecursive<TextMeshProUGUI>(fumbleHUD.transform, "FumbleTimerText");
        }

        if (victoryPanel != null)
        {
            FixCanvasCamera(victoryPanel);
            victoryTimeText = FindComponentRecursive<TextMeshProUGUI>(victoryPanel.transform, "TimeText");
            // Assuming Victory_Text is the title? Or maybe New_Record object contains the text
            victoryTitleText = FindComponentRecursive<TextMeshProUGUI>(victoryPanel.transform, "Victory_Text");
            nameInputField = FindComponentRecursive<TMP_InputField>(victoryPanel.transform, "Name_Input");
            highScoreInputGroup = nameInputField?.gameObject; // The input field itself or its parent

            BindButton(victoryPanel, "Button_Menu", ReturnToMainMenu);
            BindButton(victoryPanel, "PlayAgainButton", FullRestart);
            BindButton(victoryPanel, "Button_Submit", SubmitScore);
            BindButton(victoryPanel, "QuitButton", QuitToDesktop);
        }

        if (mainMenuPanel != null)
        {
            FixCanvasCamera(mainMenuPanel);
            BindButton(mainMenuPanel, "Button_Play", StartGame);
            BindButton(mainMenuPanel, "Button_Quit", QuitToDesktop);
            // Settings/Credits usually handled by MainMenuController script on the panel
        }

        if (pausePanel != null)
        {
            FixCanvasCamera(pausePanel);
            BindButton(pausePanel, "RestartButton", FullRestart); // Full restart from pause
            BindButton(pausePanel, "RetryButton", FullRestart);   // Just in case
            BindButton(pausePanel, "Button_Settings", OpenPauseSettings);
            BindButton(pausePanel, "QuitButton", QuitToDesktop);
        }
    }

    private void BindButton(GameObject panel, string exactName, UnityEngine.Events.UnityAction action)
    {
        // Search recursively because buttons might be inside containers (RootMenu, etc.)
        Button btn = FindComponentRecursive<Button>(panel.transform, exactName);
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }
        else
        {
            Debug.LogWarning($"[GameManager] Could not find button '{exactName}' in '{panel.name}'");
        }
    }

    private void ClearReferences()
    {
        mainMenuPanel = null; inGameHUD = null; gameOverPanel = null;
        fumbleHUD = null; victoryPanel = null; pausePanel = null;
        if (yardsText != null && yardsText.gameObject == null) yardsText = null;
    }

    private void FixCanvasCamera(GameObject uiObject)
    {
        Canvas c = uiObject.GetComponent<Canvas>();
        if (c == null) c = uiObject.GetComponentInParent<Canvas>();
        if (c != null && c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)
            c.worldCamera = Camera.main;
    }

    private GameObject FindChildRecursive(Transform parent, string name)
    {
        if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return parent.gameObject;
        foreach (Transform child in parent)
        {
            GameObject result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private T FindComponentRecursive<T>(Transform parent, string name) where T : Component
    {
        if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
        {
            T comp = parent.GetComponent<T>();
            if (comp != null) return comp;
        }
        foreach (Transform child in parent)
        {
            T result = FindComponentRecursive<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    void Update()
    {
        if (GameInput.Instance != null && GameInput.Instance.GetPauseDown()) TogglePause();

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
                if (CameraController.Instance) CameraController.Instance.SetFumbleMode(false);
                break;
            case GameState.Fumble:
                if (fumbleHUD)
                {
                    fumbleHUD.SetActive(true);
                    fumbleHUD.transform.SetAsLastSibling();
                }
                Time.timeScale = 1f;
                if (CameraController.Instance) CameraController.Instance.SetFumbleMode(true);
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
    public void StartFumbleEvent(Transform package) { if (currentState != GameState.Playing) return; currentPackageTransform = package; currentFumbleTimer = maxFumbleTime; SetState(GameState.Fumble); }
    public void RecoverFumble() { if (currentState == GameState.Fumble) { currentPackageTransform = null; SetState(GameState.Playing); } }
    public void PenalizeFumbleTime(float seconds) { currentFumbleTimer -= seconds; }

    // --- INTERNAL LOGIC ---

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

        // Explicitly activate the input area if it's a high score
        if (highScoreInputGroup != null)
        {
            highScoreInputGroup.SetActive(isHighScore);
            if (isHighScore && victoryTitleText) victoryTitleText.text = "NEW RECORD!";
            else if (victoryTitleText) victoryTitleText.text = "TOUCHDOWN!";
        }
    }

    public void SubmitScore()
    {
        if (nameInputField != null && LeaderboardManager.Instance != null)
        {
            string entryName = nameInputField.text;
            if (string.IsNullOrWhiteSpace(entryName)) entryName = "Unknown";

            LeaderboardManager.Instance.AddScore(entryName, finalTime, CurrentDown);

            // Visual Feedback
            if (highScoreInputGroup) highScoreInputGroup.SetActive(false);
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

    public void OpenPauseSettings() { if (pausePanel) pausePanel.SetActive(false); if (pauseSettingsPanel) pauseSettingsPanel.SetActive(true); }
    public void ClosePauseSettings() { if (pauseSettingsPanel) pauseSettingsPanel.SetActive(false); if (pausePanel) pausePanel.SetActive(true); }

    void OnGUI()
    {
        if (!debugMode) return;
        GUIStyle style = new GUIStyle(); style.fontSize = 20; style.normal.textColor = Color.yellow; style.fontStyle = FontStyle.Bold;
        GUILayout.BeginArea(new Rect(10, 10, 400, 350), "DEBUG INFO", GUI.skin.window);
        GUILayout.Label($"State: {currentState}", style);
        string hudStatus = (inGameHUD != null) ? inGameHUD.activeSelf.ToString() : "NULL REF";
        GUILayout.Label($"HUD Active: {hudStatus}", style);
        if (GUILayout.Button("Force Restart")) RestartLevel();
        GUILayout.EndArea();
    }
}