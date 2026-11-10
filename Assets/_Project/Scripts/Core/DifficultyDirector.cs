using System;
using UnityEngine;

/// <summary>
/// Curva de dificultad por RONDAS, desacoplada del nivel del jugador. Cada ronda exige matar una cuota de
/// enemigos (<see cref="killsPerRound"/>) antes de pasar a la siguiente, asi que hay que pelear de verdad contra
/// la configuracion de esa ronda; el nivel/XP sigue gobernando las cartas de mejora, pero ya no adelanta la ronda
/// por si solo (antes un jugador agresivo subia de nivel y la ronda saltaba con el, sin vivirla). Por si un
/// jugador se estanca sin matar lo suficiente, el tiempo tambien empuja (una ronda extra cada
/// <see cref="secondsPerRoundFallback"/> segundos). La ronda nunca avanza mas de 1 a la vez.
///
/// Cada frame evalua la curva (interpolacion lineal entre puntos) y escribe en <see cref="DifficultyScale"/> los
/// multiplicadores de vida, dano y velocidad de los enemigos, y expone el ritmo del spawner.
/// Va en un objeto vacio de la escena. Sin director el spawner usa su ritmo fijo de siempre.
/// </summary>
public class DifficultyDirector : MonoBehaviour
{
    [Serializable]
    public struct Point
    {
        [Tooltip("Ronda en la que se alcanzan estos valores.")]
        public float round;
        [Tooltip("Segundos entre tandas de enemigos.")]
        public float spawnInterval;
        [Tooltip("Maximo de enemigos vivos a la vez.")]
        public int maxAlive;
        public float hp;
        public float damage;
        public float speed;

        public Point(float round, float spawnInterval, int maxAlive, float hp, float damage, float speed)
        {
            this.round = round;
            this.spawnInterval = spawnInterval;
            this.maxAlive = maxAlive;
            this.hp = hp;
            this.damage = damage;
            this.speed = speed;
        }
    }

    // El jefe aparece al llegar a la ultima ronda de esta curva (NightTimer.bossRound, hoy 18). Despues del
    // ultimo punto se mantienen los valores. Ver docs/10 - Rondas, boss y puntaje.
    [SerializeField] private Point[] curve =
    {
        new Point(1f,  2.5f, 10, 1.0f, 1.0f, 1.00f),  // calentamiento
        new Point(6f,  1.8f, 14, 1.0f, 1.0f, 1.00f),
        new Point(11f, 1.2f, 20, 1.6f, 1.0f, 1.15f),  // presion
        new Point(16f, 0.7f, 26, 2.4f, 1.2f, 1.35f),  // escalada
        new Point(18f, 0.7f, 26, 2.4f, 1.2f, 1.35f),  // ronda del boss
    };

    [Tooltip("Kills necesarios dentro de una ronda para pasar a la siguiente: hay que pelear, no solo subir de nivel.")]
    [SerializeField, Min(1)] private int killsPerRound = 10;

    [Tooltip("Respaldo por tiempo: si se supera ronda x este valor en segundos, la ronda avanza igual aunque falten kills.")]
    [SerializeField, Min(5f)] private float secondsPerRoundFallback = 40f;

    [Header("Modo infinito (mas alla de la ultima ronda)")]
    [Tooltip("Cuanto sube la vida de los enemigos por cada ronda extra, una vez pasada la ultima ronda de la curva.")]
    [SerializeField, Min(0f)] private float endlessHpGrowthPerRound = 0.12f;
    [Tooltip("Cuanto sube el daño por cada ronda extra.")]
    [SerializeField, Min(0f)] private float endlessDamageGrowthPerRound = 0.04f;
    [Tooltip("Cuanto sube la velocidad por cada ronda extra (cada enemigo igual tiene su propio tope en EnemyScaling).")]
    [SerializeField, Min(0f)] private float endlessSpeedGrowthPerRound = 0.01f;
    [Tooltip("Cuanto baja el intervalo de aparicion por cada ronda extra.")]
    [SerializeField, Min(0f)] private float endlessIntervalDecreasePerRound = 0.015f;
    [SerializeField, Min(0.1f)] private float endlessMinInterval = 0.4f;
    [Tooltip("Cuantos enemigos vivos mas se permiten por cada ronda extra.")]
    [SerializeField, Min(0f)] private float endlessMaxAliveGrowthPerRound = 0.5f;
    [SerializeField, Min(1)] private int endlessMaxAliveCap = 60;

    [Header("Depuracion")]
    [SerializeField] private bool debugLog = false;

    public static DifficultyDirector Instance { get; private set; }

    /// <summary>Segundos jugados (fuente unica: <see cref="RunClock"/>).</summary>
    public float Elapsed => RunClock.Instance != null ? RunClock.Instance.Elapsed : 0f;
    /// <summary>Progreso fraccionario sobre el que se evalua la curva: ronda + avance hacia la cuota de kills.</summary>
    public float Progress { get; private set; } = 1f;
    /// <summary>Ronda actual (numero entero).</summary>
    public int Round { get; private set; } = 1;
    /// <summary>Ronda mas alta de la curva: la del jefe.</summary>
    public int MaxRound { get; private set; } = 1;
    /// <summary>Kills acumulados en la ronda actual.</summary>
    public int RoundKillCount { get; private set; }
    public int KillsPerRound => killsPerRound;
    public float SpawnInterval { get; private set; } = 3f;
    public int MaxAlive { get; private set; } = 20;

    /// <summary>Se dispara al pasar a una ronda mayor. Parametro: la nueva ronda.</summary>
    public event Action<int> RoundChanged;

    private float nextLog;
    private int lastAdvanceFrame = -1;
    private bool initialized;

    private void OnEnable()
    {
        Instance = this;
        MaxRound = curve != null && curve.Length > 0 ? Mathf.Max(1, Mathf.RoundToInt(curve[curve.Length - 1].round)) : 1;

        // Solo se reinicia la primera vez: BossEncounter apaga este componente durante la pelea del jefe y
        // VictoryFlow lo reactiva en el modo infinito; ahi debe seguir desde donde iba, no volver a la ronda 1.
        if (!initialized)
        {
            initialized = true;
            Round = 1;
            RoundKillCount = 0;
            Progress = 1f;
        }

        Evaluate(Progress);
        GameEvents.EnemyKilled += OnEnemyKilled;
    }

    private void OnDisable()
    {
        GameEvents.EnemyKilled -= OnEnemyKilled;
        if (Instance == this) Instance = null;
        DifficultyScale.Hp = DifficultyScale.Damage = DifficultyScale.Speed = 1f;
    }

    // En el modo infinito las rondas siguen subiendo mas alla de MaxRound (la del jefe), para que la partida
    // despues de ganar siga poniendose mas dificil en vez de quedarse plana.
    private bool CanAdvance => Round < MaxRound || VictoryFlow.IsEndless;

    // Cada kill cuenta para la cuota de la ronda actual: asi hay que pelear de verdad, no solo subir de nivel.
    private void OnEnemyKilled(GameObject enemy)
    {
        if (!CanAdvance) return;

        RoundKillCount++;
        if (RoundKillCount >= killsPerRound) AdvanceRound();
    }

    private void Update()
    {
        // Respaldo por tiempo: si el jugador se estanca sin cumplir la cuota de kills, la ronda avanza igual.
        if (CanAdvance && Elapsed >= Round * secondsPerRoundFallback) AdvanceRound();

        Progress = Round + (CanAdvance ? Mathf.Clamp01(RoundKillCount / (float)killsPerRound) : 0f);
        Evaluate(Progress);

        if (debugLog && Elapsed >= nextLog)
        {
            nextLog = Elapsed + 15f;
            string roundLabel = VictoryFlow.IsEndless ? $"ronda {Round} (infinito)" : $"ronda {Round}/{MaxRound}";
            Debug.Log($"[Director {Elapsed:0}s] {roundLabel} | kills {RoundKillCount}/{killsPerRound} | intervalo {SpawnInterval:0.00} | maxVivos {MaxAlive} | vida x{DifficultyScale.Hp:0.00} | dano x{DifficultyScale.Damage:0.00} | velocidad x{DifficultyScale.Speed:0.00}", this);
        }
    }

    // Nunca avanza mas de 1 ronda por frame, aunque el evento de kill y el respaldo por tiempo coincidan.
    private void AdvanceRound()
    {
        if (Time.frameCount == lastAdvanceFrame) return;
        lastAdvanceFrame = Time.frameCount;

        Round = VictoryFlow.IsEndless ? Round + 1 : Mathf.Min(Round + 1, MaxRound);
        RoundKillCount = 0;
        RoundChanged?.Invoke(Round);
    }

    private void Evaluate(float progress)
    {
        if (curve == null || curve.Length == 0) return;

        // Antes del primer punto o despues del ultimo: se mantienen los valores de ese extremo.
        Point a = curve[0];
        Point b = curve[0];

        for (int i = 0; i < curve.Length; i++)
        {
            if (curve[i].round <= progress)
            {
                a = curve[i];
                b = i + 1 < curve.Length ? curve[i + 1] : curve[i];
            }
        }

        float span = b.round - a.round;
        float k = span > 0.0001f ? Mathf.Clamp01((progress - a.round) / span) : 0f;

        SpawnInterval = Mathf.Max(0.1f, Mathf.Lerp(a.spawnInterval, b.spawnInterval, k));
        MaxAlive = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(a.maxAlive, b.maxAlive, k)));
        DifficultyScale.Hp = Mathf.Lerp(a.hp, b.hp, k);
        DifficultyScale.Damage = Mathf.Lerp(a.damage, b.damage, k);
        DifficultyScale.Speed = Mathf.Lerp(a.speed, b.speed, k);

        // Modo infinito: mas alla de la ultima ronda de la curva, se sigue endureciendo poco a poco en vez de
        // quedarse plano para siempre. Usa tasas propias (no la pendiente del ultimo tramo, que puede ser 0).
        Point last = curve[curve.Length - 1];
        float extraRounds = progress - last.round;
        if (extraRounds > 0f)
        {
            SpawnInterval = Mathf.Max(endlessMinInterval, last.spawnInterval - extraRounds * endlessIntervalDecreasePerRound);
            MaxAlive = Mathf.Min(endlessMaxAliveCap, Mathf.RoundToInt(last.maxAlive + extraRounds * endlessMaxAliveGrowthPerRound));
            DifficultyScale.Hp = last.hp + extraRounds * endlessHpGrowthPerRound;
            DifficultyScale.Damage = last.damage + extraRounds * endlessDamageGrowthPerRound;
            DifficultyScale.Speed = last.speed + extraRounds * endlessSpeedGrowthPerRound;
        }
    }
}
