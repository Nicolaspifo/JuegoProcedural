using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Imprime en consola las acciones de GameInputActions. Gamepad y teclado producen
/// las mismas acciones. Asignar el asset en el Inspector y poner en la escena de prueba.
/// </summary>
public class InputLogger : MonoBehaviour
{
    [SerializeField] private InputActionAsset actions;
    [SerializeField] private bool logAim = false; // Aim con mouse se dispara cada frame

    private InputActionMap map;

    private void OnEnable()
    {
        if (actions == null) { Debug.LogWarning("[Input] Falta asignar GameInputActions"); return; }
        map = actions.FindActionMap("Gameplay", true);
        foreach (var a in map.actions)
        {
            a.performed += OnAction;
            a.canceled += OnAction;
        }
        map.Enable();
    }

    private void OnDisable()
    {
        if (map == null) return;
        foreach (var a in map.actions)
        {
            a.performed -= OnAction;
            a.canceled -= OnAction;
        }
        map.Disable();
    }

    private void OnAction(InputAction.CallbackContext ctx)
    {
        bool vector = ctx.valueType == typeof(Vector2);
        if (!logAim && ctx.action.name == "Aim") return;

        string value = vector ? ctx.ReadValue<Vector2>().ToString() : (ctx.performed ? "pressed" : "released");
        Debug.Log($"[Input] {ctx.action.name}: {value} ({ctx.control.device.displayName})");
    }
}
