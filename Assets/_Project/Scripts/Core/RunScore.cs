using UnityEngine;

/// <summary>
/// Puntaje de la partida. SOLO LOGICA: sin interfaz ni rangos por ahora; el resultado queda en
/// <see cref="Score"/> y en el desglose de consola.
///
/// Al derrotar al jefe (victoria):
///   kills x pointsPerKill
///   + rapidez hasta el jefe: (bossTimeReference - segundos hasta el jefe) x pointsPerSecondSaved (si es positivo)
///   + poco daño: max(0, noDamageBonus - daño recibido x pointsPerDamageLost)
///   + derrota del jefe: bossKillBonus + (fightTimeReference - duracion de la pelea) x pointsPerFightSecondSaved (si es positivo)
///
/// Al morir (sin bonos de victoria): kills x pointsPerKill + nivel alcanzado x pointsPerLevelOnDeath.
/// Escucha eventos y cuenta; no modifica el juego. Va en el mismo objeto que RunMetrics.
/// </summary>
public class RunScore : MonoBehaviour
{
    [Header("Kills")]
    [SerializeField, Min(0)] private int pointsPerKill = 10;

    [Header("Rapidez hasta el jefe")]
    [Tooltip("Segundos de referencia: llegar antes que esto suma puntos.")]
    [SerializeField, Min(1f)] private float bossTimeReference = 360f;
    [SerializeField, Min(0)] private int pointsPerSecondSaved = 20;

    [Header("Daño recibido")]
    [SerializeField, Min(0)] private int noDamageBonus = 4000;
    [SerializeField, Min(0)] private int pointsPerDamageLost = 10;

    [Header("Jefe")]
    [SerializeField, Min(0)] private int bossKillBonus = 5000;
    [Tooltip("Segundos de referencia para la pelea: matarlo antes que esto suma puntos.")]
    [SerializeField, Min(1f)] private float fightTimeReference = 120f;
    [SerializeField, Min(0)] private int pointsPerFightSecondSaved = 30;

    [Header("Muerte (sin victoria)")]
    [SerializeField, Min(0)] private int pointsPerLevelOnDeath = 100;

    [Header("Depuracion")]
    [SerializeField] private bool debugLog = true;

    public int Score { get; private set; }
    public bool Finished { get; private set; }
    public bool Won { get; private set; }
    /// <summary>Duracion de la partida en el momento de terminar (muerte o victoria), lista para el reporte final.</summary>
    public float FinalTime { get; private set; }

    private PlayerXP playerXP;
    private Health playerHealth;
    private NightTimer nightTimer;

    private float Elapsed => RunClock.Instance != null ? RunClock.Instance.Elapsed : 0f;   // fuente unica
    private float bossSpawnTime = -1f;
    private int kills;
    private float damageTaken;
    private float lastHealth;

    private void OnEnable()
    {
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.BossDefeated += OnBossDefeated;
    }

    private void OnDisable()
    {
        // GameEvents es estatico: siempre desuscribirse.
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.BossDefeated -= OnBossDefeated;
        if (playerHealth != null)
        {
            playerHealth.Changed -= OnHealthChanged;
            playerHealth.Died -= OnPlayerDied;
        }
        if (nightTimer != null) nightTimer.OnBossSpawned -= OnBossSpawned;
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.TryGetComponent(out playerXP);
            player.TryGetComponent(out playerHealth);
        }

        if (playerHealth != null)
        {
            lastHealth = playerHealth.Current;
            playerHealth.Changed += OnHealthChanged;
            playerHealth.Died += OnPlayerDied;
        }

        nightTimer = FindAnyObjectByType<NightTimer>();
        if (nightTimer != null) nightTimer.OnBossSpawned += OnBossSpawned;
    }

    private void OnEnemyKilled(GameObject enemy)
    {
        if (Finished) return;
        kills++;
        Score = kills * pointsPerKill;
        GameEvents.RaiseScoreChanged(Score);
    }

    private void OnHealthChanged(float current, float max)
    {
        if (current < lastHealth) damageTaken += lastHealth - current;   // solo cuenta bajadas
        lastHealth = current;
    }

    private void OnBossSpawned(GameObject boss)
    {
        bossSpawnTime = Elapsed;
    }

    private void OnBossDefeated()
    {
        if (Finished) return;

        float bossTime = bossSpawnTime >= 0f ? bossSpawnTime : Elapsed;
        float fightTime = Elapsed - bossTime;

        int killPoints = kills * pointsPerKill;
        int speedPoints = Mathf.RoundToInt(Mathf.Max(0f, bossTimeReference - bossTime) * pointsPerSecondSaved);
        int damagePoints = Mathf.Max(0, noDamageBonus - Mathf.RoundToInt(damageTaken * pointsPerDamageLost));
        int bossPoints = bossKillBonus + Mathf.RoundToInt(Mathf.Max(0f, fightTimeReference - fightTime) * pointsPerFightSecondSaved);

        Score = killPoints + speedPoints + damagePoints + bossPoints;
        Finish(true);

        Log($"[Puntaje] VICTORIA {Score} pts | tiempo {RunClock.Format(FinalTime)} | kills {killPoints} | rapidez hasta el jefe +{speedPoints} (jefe a los {bossTime:0}s) | poco daño +{damagePoints} (daño {damageTaken:0}) | jefe +{bossPoints} (pelea {fightTime:0}s)");
    }

    private void OnPlayerDied()
    {
        if (Finished) return;

        int level = playerXP != null ? playerXP.Level : 1;
        int killPoints = kills * pointsPerKill;
        int levelPoints = level * pointsPerLevelOnDeath;

        Score = killPoints + levelPoints;
        Finish(false);

        Log($"[Puntaje] DERROTA {Score} pts | tiempo {RunClock.Format(FinalTime)} | kills {killPoints} | nivel {level} +{levelPoints} (sin bonos de victoria)");
    }

    private void Finish(bool won)
    {
        Finished = true;
        Won = won;
        FinalTime = Elapsed;
        GameEvents.RaiseScoreChanged(Score);
    }

    private void Log(string message)
    {
        if (debugLog) Debug.Log(message, this);
    }
}
