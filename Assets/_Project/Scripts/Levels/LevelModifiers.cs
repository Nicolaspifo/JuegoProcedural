using UnityEngine;

/// <summary>
/// Multiplicadores de dificultad del nivel actual. <see cref="DifficultyDirector"/> los aplica ENCIMA de su curva
/// por rondas, asi cada nivel es mas dificil que el anterior sin tocar la curva. 1 = sin cambios (si no hay
/// nivel activo el juego se comporta como siempre).
/// </summary>
public static class LevelModifiers
{
    public static float Hp = 1f;
    public static float Damage = 1f;
    public static float Speed = 1f;
    /// <summary>&gt;1 = aparecen mas seguido (divide el intervalo del spawner).</summary>
    public static float SpawnRate = 1f;
    public static float MaxAlive = 1f;

    public static void Set(LevelDefinition def)
    {
        Hp = def.enemyHp;
        Damage = def.enemyDamage;
        Speed = def.enemySpeed;
        SpawnRate = def.spawnRate;
        MaxAlive = def.maxAlive;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() => Hp = Damage = Speed = SpawnRate = MaxAlive = 1f;
}
