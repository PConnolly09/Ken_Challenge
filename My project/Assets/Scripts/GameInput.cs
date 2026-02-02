using UnityEngine;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    public static GameInput Instance;

    public InputAction moveAction;
    public InputAction jumpAction;
    public InputAction interactAction;     // 'F' or Gamepad West
    public InputAction spinAction;         // 'Q' or Gamepad East
    public InputAction stiffArmAction;     // 'E' or Gamepad Shoulder
    public InputAction jukeAction;         // 'Shift' or Gamepad Trigger
    public InputAction pauseAction;        // 'Escape' or Start
    public InputAction craneModeAction;    // 'Tab' or Select

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeActions();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeActions()
    {
        // 1. Movement (Vector2)
        moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/s")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/a")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d")
            .With("Right", "<Keyboard>/rightArrow");

        // 2. Actions
        jumpAction = new InputAction("Jump", binding: "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        interactAction = new InputAction("Interact", binding: "<Keyboard>/f");
        interactAction.AddBinding("<Gamepad>/buttonWest");

        spinAction = new InputAction("Spin", binding: "<Keyboard>/q");
        spinAction.AddBinding("<Gamepad>/buttonEast");

        stiffArmAction = new InputAction("StiffArm", binding: "<Keyboard>/e");
        stiffArmAction.AddBinding("<Gamepad>/rightShoulder");

        jukeAction = new InputAction("Juke", binding: "<Keyboard>/leftShift");
        jukeAction.AddBinding("<Gamepad>/leftTrigger");

        pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");

        craneModeAction = new InputAction("CraneSwitch", binding: "<Keyboard>/tab");
        craneModeAction.AddBinding("<Gamepad>/select");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        interactAction.Enable();
        spinAction.Enable();
        stiffArmAction.Enable();
        jukeAction.Enable();
        pauseAction.Enable();
        craneModeAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        interactAction.Disable();
        spinAction.Disable();
        stiffArmAction.Disable();
        jukeAction.Disable();
        pauseAction.Disable();
        craneModeAction.Disable();
    }

    // --- Helpers ---
    public Vector2 GetMovementInput() => moveAction.ReadValue<Vector2>();
    public bool GetJumpDown() => jumpAction.WasPressedThisFrame();
    public bool GetJumpHeld() => jumpAction.IsPressed();
    public bool GetInteractDown() => interactAction.WasPressedThisFrame();
    public bool GetSpinDown() => spinAction.WasPressedThisFrame();
    public bool GetStiffArmDown() => stiffArmAction.WasPressedThisFrame();
    public bool GetJukeDown() => jukeAction.WasPressedThisFrame();
    public bool GetPauseDown() => pauseAction.WasPressedThisFrame();
    public bool GetCraneModeSwitch() => craneModeAction.WasPressedThisFrame();
}