using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unico dueño de Time.timeScale. Cada sistema que quiera pausar el juego llama a
/// Push(this) y al terminar Pop(this). El juego solo se reanuda cuando NADIE lo retiene,
/// asi un panel de cartas no reanuda el juego detras de la pantalla de Game Over.
/// </summary>
public static class PauseControl
{
    private static readonly HashSet<object> holders = new();

    public static bool IsPaused => holders.Count > 0;

    public static void Push(object owner)
    {
        if (owner == null) return;
        holders.Add(owner);
        Apply();
    }

    public static void Pop(object owner)
    {
        if (owner == null) return;
        holders.Remove(owner);
        Apply();
    }

    /// <summary>Libera todas las pausas (al cargar o reiniciar una escena).</summary>
    public static void Reset()
    {
        holders.Clear();
        Apply();
    }

    private static void Apply() => Time.timeScale = holders.Count > 0 ? 0f : 1f;

    // Con "Enter Play Mode Options" sin recarga de dominio, los estaticos sobreviven entre partidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => holders.Clear();
}
