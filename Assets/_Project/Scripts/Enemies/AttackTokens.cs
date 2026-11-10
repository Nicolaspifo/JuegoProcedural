using System.Collections.Generic;

/// <summary>
/// Limita cuantos enemigos pueden estar atacando (windup/strike) a la vez.
/// El resto espera y rodea al jugador. Se reinicia al entrar en Play.
/// </summary>
public static class AttackTokens
{
    private static readonly HashSet<object> active = new();

    public static int ActiveCount => active.Count;

    /// <summary>Devuelve true si el dueño obtuvo (o ya tenía) una ficha.</summary>
    public static bool TryAcquire(object owner, int maxConcurrent)
    {
        if (owner == null) return false;
        if (active.Contains(owner)) return true;
        if (active.Count >= maxConcurrent) return false;

        active.Add(owner);
        return true;
    }

    public static void Release(object owner)
    {
        if (owner != null) active.Remove(owner);
    }

    // Con "Enter Play Mode Options" sin recarga de dominio, los estaticos sobreviven entre partidas.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => active.Clear();
}
