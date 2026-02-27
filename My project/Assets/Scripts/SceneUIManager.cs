using UnityEngine;
using TMPro;

/// <summary>
/// Place this script in each scene that has UI. 
/// Assign the references in the Inspector. 
/// It explicitly hands UI references to the persistent GameManager, preventing fragile string-based search bugs.
/// Leave Buttons out of here; map those directly via OnClick() in the Inspector!
/// </summary>
public class SceneUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject inGameHUD;
    public GameObject fumbleHUD;
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public GameObject highScoreInputGroup;
    public GameObject downNotificationPanel;
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

    void Start()
    {
        // Register this UI with the GameManager when the scene starts
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterUI(this);
        }
    }
}