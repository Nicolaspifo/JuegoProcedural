using UnityEngine;

/// <summary>Oculta el aviso de exito cuando se cumple su tiempo. Lo agrega <see cref="UIAviso"/>.</summary>
public class UIAvisoTicker : MonoBehaviour
{
    private void Update() => UIAviso.Tick();
}
