using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InputDisplay : MonoBehaviour
{
    [Header("Panel Containers")]
    public GameObject playerControlsParent;
    public GameObject craneControlsParent;

    [Header("Settings")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.5f);
    public Color pressedColor = new Color(1f, 0.8f, 0.2f, 1f);

    private Dictionary<string, Image> playerImages = new Dictionary<string, Image>();
    private Dictionary<string, Image> craneImages = new Dictionary<string, Image>();

    void Start()
    {
        // 1. Auto-find Panels based on your hierarchy image
        if (playerControlsParent == null) playerControlsParent = transform.Find("PlayerPanel")?.gameObject;
        if (craneControlsParent == null) craneControlsParent = transform.Find("CranePanel")?.gameObject;

        // 2. Map Keys
        if (playerControlsParent) MapKeys(playerControlsParent.transform, playerImages);
        if (craneControlsParent) MapKeys(craneControlsParent.transform, craneImages);
    }

    void Update()
    {
        // Hide if game isn't running
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameManager.GameState.Playing)
        {
            if (playerControlsParent) playerControlsParent.SetActive(false);
            if (craneControlsParent) craneControlsParent.SetActive(false);
            return;
        }

        // Determine Mode
        bool isCraneMode = CraneController.ActiveCrane != null && CraneController.ActiveCrane.isPlayerControlling;

        if (playerControlsParent) playerControlsParent.SetActive(!isCraneMode);
        if (craneControlsParent) craneControlsParent.SetActive(isCraneMode);

        // Update Visuals
        if (isCraneMode) UpdateCraneVisuals();
        else UpdatePlayerVisuals();
    }

    private void UpdatePlayerVisuals()
    {
        if (GameInput.Instance == null) return;

        Vector2 move = GameInput.Instance.GetMovementInput();

        SetKeyColor(playerImages, "W", move.y > 0.1f);
        SetKeyColor(playerImages, "S", move.y < -0.1f);
        SetKeyColor(playerImages, "A", move.x < -0.1f);
        SetKeyColor(playerImages, "D", move.x > 0.1f);

        SetKeyColor(playerImages, "Space", GameInput.Instance.GetJumpHeld());
        SetKeyColor(playerImages, "E", GameInput.Instance.GetStiffArmDown());
        SetKeyColor(playerImages, "Q", GameInput.Instance.GetSpinDown());
        SetKeyColor(playerImages, "Shift", GameInput.Instance.GetJukeDown()); // Mapped to Key_Shift
        SetKeyColor(playerImages, "F", GameInput.Instance.GetInteractDown());
        SetKeyColor(playerImages, "ESC", GameInput.Instance.GetPauseDown());
    }

    private void UpdateCraneVisuals()
    {
        if (GameInput.Instance == null) return;

        Vector2 move = GameInput.Instance.GetMovementInput();

        SetKeyColor(craneImages, "W", move.y > 0.1f);
        SetKeyColor(craneImages, "S", move.y < -0.1f);
        SetKeyColor(craneImages, "A", move.x < -0.1f);
        SetKeyColor(craneImages, "D", move.x > 0.1f);

        SetKeyColor(craneImages, "Space", GameInput.Instance.GetJumpHeld());
        SetKeyColor(craneImages, "Tab", GameInput.Instance.GetCraneModeSwitch());
        SetKeyColor(craneImages, "F", GameInput.Instance.GetInteractDown()); // Exit Crane
        SetKeyColor(craneImages, "ESC", GameInput.Instance.GetPauseDown());
    }

    private void SetKeyColor(Dictionary<string, Image> map, string keyName, bool isPressed)
    {
        if (map.TryGetValue(keyName, out Image img))
        {
            img.color = Color.Lerp(img.color, isPressed ? pressedColor : normalColor, Time.deltaTime * 20f);

            float targetScale = isPressed ? 1.2f : 1.0f;
            img.transform.localScale = Vector3.Lerp(img.transform.localScale, Vector3.one * targetScale, Time.deltaTime * 20f);
        }
    }

    private void MapKeys(Transform parent, Dictionary<string, Image> map)
    {
        foreach (Transform child in parent)
        {
            Image img = child.GetComponent<Image>();
            if (img == null) continue;

            string name = child.name;

            // Strict mapping based on your screenshot names
            if (name == "Key_W") map["W"] = img;
            else if (name == "Key_A") map["A"] = img;
            else if (name == "Key_S") map["S"] = img;
            else if (name == "Key_D") map["D"] = img;
            else if (name == "Key_Space") map["Space"] = img;
            else if (name == "Key_E") map["E"] = img;
            else if (name == "Key_Q") map["Q"] = img;
            else if (name == "Key_F") map["F"] = img;
            else if (name == "Key_Shift") map["Shift"] = img;
            else if (name == "Key_Tab") map["Tab"] = img;
            else if (name == "Key_ESC") map["ESC"] = img;

            // Set initial state
            if (map.ContainsValue(img)) img.color = normalColor;
        }
    }
}