using System;

/// <summary>Tipo de meta (condicion de victoria) de un nivel.</summary>
public enum LevelGoal
{
    /// <summary>Matar <c>goalTarget</c> enemigos.</summary>
    KillCount,
    /// <summary>Sobrevivir <c>goalTarget</c> segundos.</summary>
    SurviveTime,
    /// <summary>Llegar a la ronda <c>goalTarget</c>, que aparezca La Llorona y vencerla.</summary>
    DefeatBoss
}

/// <summary>Bioma del nivel: define en que rango se generan los parametros del terreno.</summary>
public enum BiomeTheme
{
    Rio,        // mucha agua
    Llanura,    // poca agua, campo abierto
    Pantano,    // mucho pantano
    Paramo,     // rocoso
    Selva       // mezcla
}

/// <summary>Parametros del terreno procedural (los usa <see cref="InfiniteTilemapFloor"/>).</summary>
[Serializable]
public class BiomeSettings
{
    public BiomeTheme theme;
    public int baseSeed;
    public float waterLevel = 0.40f;
    public float beachWidth = 0.08f;
    public float swampThreshold = 0.60f;
    public float rockThreshold = 0.64f;
    public float landScale = 0.009f;
}

/// <summary>
/// Definicion de un nivel: meta, condicion de victoria, dificultad y terreno. Los 10 niveles se crean por codigo
/// en <see cref="LevelCatalog"/>.
/// </summary>
[Serializable]
public class LevelDefinition
{
    public int number;
    public string name;
    public string description;

    public LevelGoal goal;
    /// <summary>Kills (KillCount), segundos (SurviveTime) o ronda en la que aparece el jefe (DefeatBoss).</summary>
    public int goalTarget;
    /// <summary>Tiempo de referencia: ganar antes suma bono de rapidez (no aplica a SurviveTime).</summary>
    public float parTimeSeconds;

    [UnityEngine.Header("Dificultad (multiplicadores sobre la curva normal)")]
    public float enemyHp = 1f;
    public float enemyDamage = 1f;
    public float enemySpeed = 1f;
    /// <summary>&gt;1 = los enemigos aparecen mas seguido.</summary>
    public float spawnRate = 1f;
    public float maxAlive = 1f;

    public BiomeSettings biome = new BiomeSettings();

    /// <summary>Indice de dificultad legible (1 = normal) para mostrar en el selector.</summary>
    public float DifficultyIndex => (enemyHp + enemyDamage + enemySpeed + spawnRate) / 4f;

    /// <summary>Texto de la meta para el selector de niveles y el HUD, por ejemplo "Mata 40 enemigos".</summary>
    public string GoalText
    {
        get
        {
            switch (goal)
            {
                case LevelGoal.KillCount: return $"Mata {goalTarget} enemigos";
                case LevelGoal.SurviveTime: return $"Sobrevive {FormatTime(goalTarget)}";
                case LevelGoal.DefeatBoss: return $"Resiste {goalTarget} rondas y vence a La Llorona";
                default: return "";
            }
        }
    }

    public bool IsBossLevel => goal == LevelGoal.DefeatBoss;

    public static string FormatTime(float seconds)
    {
        int total = Math.Max(0, (int)Math.Floor(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
