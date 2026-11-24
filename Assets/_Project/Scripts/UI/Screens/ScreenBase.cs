using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Base de los controladores de pantalla: boton "atras" de Android (Escape) y estado ocupado.</summary>
public abstract class ScreenBase : MonoBehaviour
{
    protected IBackendService B => Backend.Service;
    protected bool Busy { get; set; }

    /// <summary>Que hacer con el boton atras de Android / Escape. Null = nada.</summary>
    protected virtual void OnBack() { }

    protected virtual void Update()
    {
        if (Busy || UIAviso.IsVisible) return;
        Keyboard kb = Keyboard.current;   // en Android el boton "atras" llega como Escape
        if (kb != null && kb.escapeKey.wasPressedThisFrame) OnBack();
    }

    /// <summary>Si no hay sesion, manda al login. Devuelve true si hay sesion.</summary>
    protected bool RequireSession()
    {
        if (B.IsLoggedIn) return true;
        ScreenRouter.Go(ScreenRouter.Login);
        return false;
    }
}
