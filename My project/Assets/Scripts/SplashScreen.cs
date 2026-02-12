using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class SplashScreen : MonoBehaviour
{
    [System.Serializable]
    public class SplashData
    {
        [Tooltip("The Canvas Group containing the logo/text")]
        public CanvasGroup splashGroup;

        [Tooltip("Time it takes to fade in")]
        public float fadeInTime = 1.0f;

        [Tooltip("Time it stays fully visible")]
        public float showTime = 2.0f;

        [Tooltip("Time it takes to fade out")]
        public float fadeOutTime = 1.0f;
    }

    [Header("Splash Sequence")]
    [Tooltip("Add your logos here in the order they should appear.")]
    public List<SplashData> splashScreens;

    [Header("Settings")]
    [Tooltip("Name of the scene to load next (usually MainMenu)")]
    public string nextSceneName = "Level_1";

    [Tooltip("Can the player press a button to skip the intro?")]
    public bool isSkippable = true;

    void Start()
    {
        Cursor.visible = false;

        // Ensure all splash screens are hidden at the start
        foreach (var splash in splashScreens)
        {
            if (splash.splashGroup != null)
            {
                splash.splashGroup.alpha = 0f;
                splash.splashGroup.gameObject.SetActive(false);
            }
        }

        // Start the sequence
        StartCoroutine(PlaySplashSequence());
    }

    IEnumerator PlaySplashSequence()
    {
        foreach (var splash in splashScreens)
        {
            if (splash.splashGroup == null) continue;

            // 1. Activate the object
            splash.splashGroup.gameObject.SetActive(true);

            // 2. Fade In
            yield return FadeCanvasGroup(splash.splashGroup, 0f, 1f, splash.fadeInTime);

            // 3. Wait/Show
            yield return new WaitForSeconds(splash.showTime);

            // 4. Fade Out
            yield return FadeCanvasGroup(splash.splashGroup, 1f, 0f, splash.fadeOutTime);

            // 5. Deactivate
            splash.splashGroup.gameObject.SetActive(false);
        }

        // Sequence finished, load game
        FinishSplash();
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float startAlpha, float endAlpha, float duration)
    {
        if (duration <= 0f)
        {
            cg.alpha = endAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        cg.alpha = endAlpha;
    }

    void Update()
    {
        if (isSkippable && Input.anyKeyDown)
        {
            StopAllCoroutines();
            FinishSplash();
        }
    }

    void FinishSplash()
    {
        Cursor.visible = true;
        SceneManager.LoadScene(nextSceneName);
    }
}