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
    public float flashDuration = 0.15f;

    private class VisualKey
    {
        public Image uiImage;
        public SpriteRenderer spriteRenderer;
        public Transform transform;
        public float flashTimer;

        public VisualKey(Image img) { uiImage = img; transform = img.transform; }
        public VisualKey(SpriteRenderer spr)
        {
            spriteRenderer = spr;
            transform = spr.transform;

            // FIX: Force Visibility in Game View for Screen Space Camera Canvas
            spr.sortingOrder = 32000; // Force on top of all other layers
            spr.sortingLayerName = "UI"; // Attempt to use UI layer

            // Force Layer to UI to avoid camera culling
            spr.gameObject.layer = LayerMask.NameToLayer("UI");

            // Pull Z-position closer to camera relative to parent Canvas to prevent Z-fighting
            Vector3 pos = transform.localPosition;
            pos.z = -10f;
            transform.localPosition = pos;
        }

        public void TriggerFlash(float duration)
        {
            flashTimer = duration;
        }

        public void UpdateVisuals(bool isContinuousPress, Color normal, Color pressed)
        {
            bool active = isContinuousPress || flashTimer > 0;
            if (flashTimer > 0) flashTimer -= Time.deltaTime;

            Color targetColor = active ? pressed : normal;
            if (active) targetColor.a = 1f;

            if (uiImage != null) uiImage.color = Color.Lerp(uiImage.color, targetColor, Time.deltaTime * 20f);
            else if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(spriteRenderer.color, targetColor, Time.deltaTime * 20f);
        }
    }

    private Dictionary<string, VisualKey> playerKeys = new Dictionary<string, VisualKey>();
    private Dictionary<string, VisualKey> craneKeys = new Dictionary<string, VisualKey>();

    void Start()
    {
        if (playerControlsParent == null) playerControlsParent = transform.Find("PlayerPanel")?.gameObject;
        if (craneControlsParent == null) craneControlsParent = transform.Find("CranePanel")?.gameObject;

        if (playerControlsParent) MapKeys(playerControlsParent.transform, playerKeys, "Player");
        if (craneControlsParent) MapKeys(craneControlsParent.transform, craneKeys, "Crane");
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameManager.GameState.Playing)
        {
            if (playerControlsParent) playerControlsParent.SetActive(false);
            if (craneControlsParent) craneControlsParent.SetActive(false);
            return;
        }

        bool isCraneMode = CraneController.ActiveCrane != null && CraneController.ActiveCrane.isPlayerControlling;

        if (playerControlsParent) playerControlsParent.SetActive(!isCraneMode);
        if (craneControlsParent) craneControlsParent.SetActive(isCraneMode);

        if (isCraneMode) UpdateCraneVisuals();
        else UpdatePlayerVisuals();
    }

    private void UpdatePlayerVisuals()
    {
        if (GameInput.Instance == null) return;
        Vector2 move = GameInput.Instance.GetMovementInput();

        UpdateKey(playerKeys, "W", move.y > 0.1f);
        UpdateKey(playerKeys, "S", move.y < -0.1f);
        UpdateKey(playerKeys, "A", move.x < -0.1f);
        UpdateKey(playerKeys, "D", move.x > 0.1f);
        UpdateKey(playerKeys, "SPACE", GameInput.Instance.GetJumpHeld());

        if (GameInput.Instance.GetStiffArmDown()) FlashKey(playerKeys, "E");
        if (GameInput.Instance.GetSpinDown()) FlashKey(playerKeys, "Q");
        if (GameInput.Instance.GetJukeDown()) FlashKey(playerKeys, "SHIFT");
        if (GameInput.Instance.GetInteractDown()) FlashKey(playerKeys, "F");
        if (GameInput.Instance.GetPauseDown()) FlashKey(playerKeys, "ESC");

        foreach (var key in playerKeys.Values) key.UpdateVisuals(false, normalColor, pressedColor);
    }

    private void UpdateCraneVisuals()
    {
        if (GameInput.Instance == null) return;
        Vector2 move = GameInput.Instance.GetMovementInput();

        UpdateKey(craneKeys, "W", move.y > 0.1f);
        UpdateKey(craneKeys, "S", move.y < -0.1f);
        UpdateKey(craneKeys, "A", move.x < -0.1f);
        UpdateKey(craneKeys, "D", move.x > 0.1f);
        UpdateKey(craneKeys, "SPACE", GameInput.Instance.GetJumpHeld());

        if (GameInput.Instance.GetCraneModeSwitch()) FlashKey(craneKeys, "TAB");
        if (GameInput.Instance.GetInteractDown()) FlashKey(craneKeys, "F");
        if (GameInput.Instance.GetPauseDown()) FlashKey(craneKeys, "ESC");

        foreach (var key in craneKeys.Values) key.UpdateVisuals(false, normalColor, pressedColor);
    }

    private void UpdateKey(Dictionary<string, VisualKey> map, string keyName, bool isHeld)
    {
        if (map.TryGetValue(keyName, out VisualKey key))
        {
            key.UpdateVisuals(isHeld, normalColor, pressedColor);
        }
    }

    private void FlashKey(Dictionary<string, VisualKey> map, string keyName)
    {
        if (map.TryGetValue(keyName, out VisualKey key))
        {
            key.TriggerFlash(flashDuration);
        }
    }

    private void MapKeys(Transform parent, Dictionary<string, VisualKey> map, string context)
    {
        Transform[] allChildren = parent.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            if (child == parent || child == transform) continue;

            VisualKey vKey = null;
            Image img = child.GetComponent<Image>();

            if (img != null) vKey = new VisualKey(img);
            else
            {
                SpriteRenderer spr = child.GetComponent<SpriteRenderer>();
                if (spr != null) vKey = new VisualKey(spr);
            }

            if (vKey == null) continue;

            string name = child.name.ToUpper();

            if (name.Contains("SHIFT")) Map(map, "SHIFT", vKey);
            else if (name.Contains("SPACE")) Map(map, "SPACE", vKey);
            else if (name.Contains("TAB")) Map(map, "TAB", vKey);
            else if (name.Contains("ESC") || name.Contains("ESCAPE")) Map(map, "ESC", vKey);

            else if (name.Contains("Q")) Map(map, "Q", vKey);
            else if (name.Contains("W")) Map(map, "W", vKey);
            else if (name.Contains("F")) Map(map, "F", vKey);
            else if (name.Contains("A")) Map(map, "A", vKey);
            else if (name.Contains("S")) Map(map, "S", vKey);
            else if (name.Contains("D")) Map(map, "D", vKey);
            else if (name.Contains("E")) Map(map, "E", vKey);
        }
    }

    private void Map(Dictionary<string, VisualKey> map, string key, VisualKey vKey)
    {
        if (!map.ContainsKey(key))
        {
            map[key] = vKey;
            vKey.UpdateVisuals(false, normalColor, pressedColor);
        }
    }
}