using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reparte "casillas" en anillos concentricos alrededor del jugador. Cada enemigo reclama una casilla
/// propia y camina hacia ella, asi nunca hay mas enemigos apuntando al mismo punto de los que caben.
/// Si un anillo se llena, el siguiente enemigo toma el anillo de afuera.
/// Se reinicia al entrar en Play (igual que AttackTokens y PauseControl).
/// </summary>
public static class EnemySlots
{
    private const int MaxIndexPerRing = 100;

    private static readonly Dictionary<long, object> taken = new();
    private static readonly Dictionary<object, long> owned = new();

    /// <summary>
    /// Clave unica del anillo: mismo radio base, separacion y numero de anillo. En el anillo interior tambien
    /// cuenta <paramref name="maxInner"/> (cambia cuantas casillas tiene y por tanto sus angulos), asi dos
    /// enemigos configurados distinto nunca comparten indices que significan angulos diferentes.
    /// </summary>
    public static long RingKey(float baseRadius, float spacing, int tier, int maxInner)
    {
        long key = (long)Mathf.RoundToInt(baseRadius * 10f) * 1000L + Mathf.RoundToInt(spacing * 10f);
        key = key * 100L + (tier == 0 ? Mathf.Clamp(maxInner, 0, 99) : 0);
        return key * 10L + tier;
    }

    /// <summary>Casillas que caben en un anillo de este radio; el anillo interior puede limitarse para dejar huecos.</summary>
    public static int SlotCount(float radius, float spacing, int tier, int maxInner)
    {
        int count = Mathf.FloorToInt(2f * Mathf.PI * radius / Mathf.Max(spacing, 0.1f));
        if (tier == 0 && maxInner > 0) count = Mathf.Min(count, maxInner);
        return Mathf.Clamp(count, 3, MaxIndexPerRing);
    }

    /// <summary>
    /// Reclama la casilla libre mas cercana (en angulo) a <paramref name="preferredAngle"/> (radianes).
    /// Si ya tenia una, solo la cambia cuando encuentra otra libre. Devuelve false si el anillo esta lleno.
    /// </summary>
    public static bool TryClaim(object owner, long ringKey, int count, float preferredAngle, out float angle)
    {
        angle = preferredAngle;
        if (owner == null || count <= 0) return false;

        float step = 2f * Mathf.PI / count;
        int best = -1;
        float bestDiff = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            long key = ringKey * MaxIndexPerRing + i;
            if (taken.TryGetValue(key, out object holder) && holder != owner) continue;

            float diff = Mathf.Abs(Mathf.DeltaAngle(i * step * Mathf.Rad2Deg, preferredAngle * Mathf.Rad2Deg));
            if (diff >= bestDiff) continue;

            bestDiff = diff;
            best = i;
        }

        if (best < 0) return false;

        Release(owner);
        long chosen = ringKey * MaxIndexPerRing + best;
        taken[chosen] = owner;
        owned[owner] = chosen;
        angle = best * step;
        return true;
    }

    public static void Release(object owner)
    {
        if (owner == null || !owned.TryGetValue(owner, out long key)) return;

        taken.Remove(key);
        owned.Remove(owner);
    }

    // Con "Enter Play Mode Options" sin recarga de dominio, los estaticos sobreviven entre partidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        taken.Clear();
        owned.Clear();
    }
}
