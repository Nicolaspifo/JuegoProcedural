using System;
using UnityEngine;

/// <summary>Desglose del puntaje de un nivel (para mostrarlo en la pantalla de resultado).</summary>
[Serializable]
public struct LevelScore
{
    public int kills;
    public int killPoints;
    public int victoryBonus;
    public int speedBonus;
    public int healthBonus;
    public int subtotal;
    public float multiplier;
    public int total;
}

/// <summary>
/// Sistema de puntaje por nivel:
/// <code>
/// kills x 10
/// + victoria:   500 + 100 x numero de nivel                (solo si gana)
/// + rapidez:    (tiempo de referencia - tiempo) x 5         (solo si gana y la meta no es de tiempo)
/// + vida:       % de vida restante x 500                    (solo si gana)
/// = subtotal x multiplicador del nivel (1.0 en el nivel 1, +0.1 por cada nivel)
/// </code>
/// Perder tambien da puntos (los kills), pero solo cuenta el mejor intento de cada nivel.
/// </summary>
public static class LevelScoring
{
    public const int PointsPerKill = 10;
    public const int VictoryBase = 500;
    public const int VictoryPerLevel = 100;
    public const int PointsPerSecondSaved = 5;
    public const int FullHealthBonus = 500;
    public const float MultiplierPerLevel = 0.1f;

    public static LevelScore Compute(LevelDefinition def, bool won, int kills, float timeSeconds, float healthFraction)
    {
        var s = new LevelScore { kills = kills, killPoints = kills * PointsPerKill };

        if (won)
        {
            s.victoryBonus = VictoryBase + VictoryPerLevel * def.number;
            if (def.goal != LevelGoal.SurviveTime && def.parTimeSeconds > 0f)
                s.speedBonus = Mathf.RoundToInt(Mathf.Max(0f, def.parTimeSeconds - timeSeconds) * PointsPerSecondSaved);
            s.healthBonus = Mathf.RoundToInt(Mathf.Clamp01(healthFraction) * FullHealthBonus);
        }

        s.subtotal = s.killPoints + s.victoryBonus + s.speedBonus + s.healthBonus;
        s.multiplier = 1f + MultiplierPerLevel * (def.number - 1);
        s.total = Mathf.RoundToInt(s.subtotal * s.multiplier);
        return s;
    }
}
