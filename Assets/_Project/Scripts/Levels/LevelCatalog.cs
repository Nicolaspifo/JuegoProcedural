using System;
using System.Collections.Generic;

/// <summary>
/// Los 10 niveles del juego, generados por codigo.
///
/// La tabla de abajo define la meta y la dificultad de cada nivel (diseno). Los parametros del terreno se
/// GENERAN PROCEDURALMENTE a partir de la semilla del nivel y su bioma: cada bioma tiene un rango y la semilla
/// elige valores dentro de el (mas agua, mas pantano, mas roca, escala del terreno...). Ademas, cada intento usa
/// una semilla distinta (ver <see cref="LevelSession"/>), asi que el mapa cambia en cada partida.
/// </summary>
public static class LevelCatalog
{
    public const int Count = 10;

    private static LevelDefinition[] levels;

    public static IReadOnlyList<LevelDefinition> All
    {
        get
        {
            if (levels == null) levels = Build();
            return levels;
        }
    }

    /// <summary>Nivel 1..10 (null si no existe).</summary>
    public static LevelDefinition Get(int number)
    {
        if (number < 1 || number > Count) return null;
        return All[number - 1];
    }

    private static LevelDefinition[] Build() => new[]
    {
        //   n  nombre                   bioma               meta                    obj   par   vida  dano  vel   spawn vivos
        Make(1, "Orilla del río",        BiomeTheme.Rio,     LevelGoal.KillCount,     20,  120, 0.80f, 0.70f, 0.95f, 0.85f, 0.8f,
             "Los zancudos salen del agua. Calienta el machete."),
        Make(2, "El potrero",            BiomeTheme.Llanura, LevelGoal.SurviveTime,   75,    0, 0.90f, 0.85f, 1.00f, 0.95f, 0.9f,
             "Campo abierto y sin donde esconderse: aguanta."),
        Make(3, "Manglar",               BiomeTheme.Pantano, LevelGoal.KillCount,     40,  180, 1.00f, 0.95f, 1.00f, 1.00f, 1.0f,
             "El pantano está lleno de bichos. Limpia la zona."),
        Make(4, "Cañaduzal",             BiomeTheme.Llanura, LevelGoal.SurviveTime,  120,    0, 1.05f, 1.00f, 1.05f, 1.05f, 1.0f,
             "Los hipopótamos rondan la caña. Sobrevive."),
        Make(5, "Noche de La Llorona",   BiomeTheme.Rio,     LevelGoal.DefeatBoss,     5,  300, 1.00f, 1.00f, 1.00f, 1.00f, 1.0f,
             "Se escucha un llanto en el río... ¡Ella viene!"),
        Make(6, "Ciénaga",               BiomeTheme.Pantano, LevelGoal.KillCount,     70,  240, 1.15f, 1.10f, 1.08f, 1.15f, 1.1f,
             "Más enemigos, más rápidos. Cuidado con el barro."),
        Make(7, "Páramo",                BiomeTheme.Paramo,  LevelGoal.SurviveTime,  180,    0, 1.20f, 1.15f, 1.10f, 1.20f, 1.15f,
             "Frío, roca y enjambres. Resiste en lo alto."),
        Make(8, "Selva profunda",        BiomeTheme.Selva,   LevelGoal.KillCount,    100,  300, 1.30f, 1.20f, 1.12f, 1.30f, 1.2f,
             "La selva no perdona. Abre camino a punta de machete."),
        Make(9, "Tormenta en el llano",  BiomeTheme.Llanura, LevelGoal.SurviveTime,  240,    0, 1.40f, 1.30f, 1.15f, 1.40f, 1.3f,
             "La tormenta trae oleadas sin fin."),
        Make(10, "El llanto final",      BiomeTheme.Pantano, LevelGoal.DefeatBoss,    10,  480, 1.50f, 1.35f, 1.18f, 1.45f, 1.35f,
             "La Llorona regresa más fuerte que nunca. Termina con esto."),
    };

    private static LevelDefinition Make(int n, string name, BiomeTheme theme, LevelGoal goal, int target, float par,
        float hp, float dmg, float speed, float spawn, float alive, string description)
    {
        return new LevelDefinition
        {
            number = n,
            name = name,
            description = description,
            goal = goal,
            goalTarget = target,
            parTimeSeconds = par,
            enemyHp = hp,
            enemyDamage = dmg,
            enemySpeed = speed,
            spawnRate = spawn,
            maxAlive = alive,
            biome = GenerateBiome(theme, BaseSeed(n))
        };
    }

    /// <summary>Semilla fija de cada nivel (el "ADN" del nivel).</summary>
    public static int BaseSeed(int level) => 1000 + level * 7919 % 50000;

    /// <summary>
    /// GENERACION PROCEDURAL del bioma: la semilla elige cada parametro dentro del rango del tema.
    /// Con la misma semilla siempre sale el mismo terreno; con otra, uno distinto del mismo estilo.
    /// </summary>
    public static BiomeSettings GenerateBiome(BiomeTheme theme, int seed)
    {
        var rng = new Random(seed);
        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        var b = new BiomeSettings { theme = theme, baseSeed = seed };
        switch (theme)
        {
            case BiomeTheme.Rio:
                b.waterLevel = Range(0.46f, 0.52f);
                b.swampThreshold = Range(0.62f, 0.70f);
                b.rockThreshold = Range(0.66f, 0.72f);
                break;
            case BiomeTheme.Llanura:
                b.waterLevel = Range(0.26f, 0.33f);
                b.swampThreshold = Range(0.66f, 0.74f);
                b.rockThreshold = Range(0.66f, 0.72f);
                break;
            case BiomeTheme.Pantano:
                b.waterLevel = Range(0.38f, 0.44f);
                b.swampThreshold = Range(0.46f, 0.53f);
                b.rockThreshold = Range(0.70f, 0.76f);
                break;
            case BiomeTheme.Paramo:
                b.waterLevel = Range(0.28f, 0.35f);
                b.swampThreshold = Range(0.70f, 0.76f);
                b.rockThreshold = Range(0.50f, 0.57f);
                break;
            default: // Selva
                b.waterLevel = Range(0.36f, 0.42f);
                b.swampThreshold = Range(0.55f, 0.62f);
                b.rockThreshold = Range(0.60f, 0.66f);
                break;
        }
        b.beachWidth = Range(0.06f, 0.10f);
        b.landScale = Range(0.007f, 0.013f);
        return b;
    }
}
