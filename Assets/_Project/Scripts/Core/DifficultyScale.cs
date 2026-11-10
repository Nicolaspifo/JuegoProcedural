using UnityEngine;

/// <summary>
/// Multiplicadores globales de dificultad. Los escribe el DifficultyDirector (Paso 7);
/// los leen los enemigos al nacer. 1 = sin cambios.
/// </summary>
public static class DifficultyScale
{
    public static float Hp = 1f;
    public static float Damage = 1f;
    public static float Speed = 1f;

    // Los estaticos sobreviven entre partidas si el dominio no se recarga: se reinician al entrar en Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Hp = Damage = Speed = 1f;
}
