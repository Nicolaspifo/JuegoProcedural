using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Solo para escena de prueba: teclas 1-5 cambian el estado del GameManager.
/// 1 Menu, 2 Playing, 3 Paused, 4 GameOver, 5 Victory.
/// </summary>
public class GameStateTester : MonoBehaviour
{
    private void Update()
    {
        var gm = GameManager.Instance;
        var kb = Keyboard.current;
        if (gm == null || kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) gm.SetState(GameState.Menu);
        if (kb.digit2Key.wasPressedThisFrame) gm.SetState(GameState.Playing);
        if (kb.digit3Key.wasPressedThisFrame) gm.SetState(GameState.Paused);
        if (kb.digit4Key.wasPressedThisFrame) gm.SetState(GameState.GameOver);
        if (kb.digit5Key.wasPressedThisFrame) gm.SetState(GameState.Victory);
    }
}
