using System;
using UnityEngine;

/// <summary>
/// Bus de eventos estatico para desacoplar sistemas. Solo declaraciones, sin logica.
/// </summary>
public static class GameEvents
{
    public static event Action<float, float> PlayerHealthChanged;   // actual, maximo
    public static event Action PlayerDied;
    public static event Action<float, float> UltiChargeChanged;     // actual, maximo
    public static event Action UltiActivated;
    public static event Action<int> ScoreChanged;
    public static event Action<GameObject> EnemyKilled;
    public static event Action BossDefeated;
    public static event Action<int> CoffeeChanged;

    public static void RaisePlayerHealthChanged(float current, float max) => PlayerHealthChanged?.Invoke(current, max);
    public static void RaisePlayerDied() => PlayerDied?.Invoke();
    public static void RaiseUltiChargeChanged(float current, float max) => UltiChargeChanged?.Invoke(current, max);
    public static void RaiseUltiActivated() => UltiActivated?.Invoke();
    public static void RaiseScoreChanged(int score) => ScoreChanged?.Invoke(score);
    public static void RaiseEnemyKilled(GameObject enemy) => EnemyKilled?.Invoke(enemy);
    public static void RaiseBossDefeated() => BossDefeated?.Invoke();
    public static void RaiseCoffeeChanged(int total) => CoffeeChanged?.Invoke(total);
}
