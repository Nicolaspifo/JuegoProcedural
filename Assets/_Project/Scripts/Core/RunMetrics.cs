using UnityEngine;

/// <summary>
/// Medidor de partida para balancear: imprime una linea de resumen cada N segundos.
/// Solo escucha eventos y cuenta; no modifica nada del juego.
/// </summary>
public class RunMetrics : MonoBehaviour
{
    [SerializeField] private PlayerXP playerXP;
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerRuntimeStats stats;
    [SerializeField, Min(5f)] private float logInterval = 30f;

    private float Elapsed => RunClock.Instance != null ? RunClock.Instance.Elapsed : 0f;   // fuente unica
    private float nextLog;
    private int kills;
    private float minHealth;
    private float damageTaken;
    private float lastHealth;
    private float firstLevelTime = -1f;
    private MultiHitXpBonus multiHit;
    private NightTimer nightTimer;

    private void OnEnable()
    {
        GameEvents.EnemyKilled += OnEnemyKilled;
        if (playerXP != null) playerXP.OnLevelUp += OnLevelUp;
        if (playerHealth != null) playerHealth.Changed += OnHealthChanged;
    }

    private void OnDisable()
    {
        // Siempre desuscribirse: GameEvents es estatico y sobrevive a este objeto.
        GameEvents.EnemyKilled -= OnEnemyKilled;
        if (playerXP != null) playerXP.OnLevelUp -= OnLevelUp;
        if (playerHealth != null) playerHealth.Changed -= OnHealthChanged;
        if (nightTimer != null) nightTimer.OnBossSpawned -= OnBossSpawned;
    }

    private void Start()
    {
        nextLog = logInterval;
        if (playerHealth != null) lastHealth = minHealth = playerHealth.Current;

        multiHit = FindAnyObjectByType<MultiHitXpBonus>();
        if (multiHit == null)
            Debug.LogWarning("[Run] No hay MultiHitXpBonus en el Player: no se mide ni se da el bonus de XP por impactos multiples.", this);

        nightTimer = FindAnyObjectByType<NightTimer>();
        if (nightTimer != null) nightTimer.OnBossSpawned += OnBossSpawned;
    }

    private void OnBossSpawned(GameObject boss)
    {
        string level = playerXP != null ? playerXP.Level.ToString() : "?";
        Debug.Log($"[Run] ¡Aparecio el jefe a los {Elapsed:0}s ({RunClock.Format(Elapsed)}) con el jugador en nivel {level}! | kills {kills} | dano recibido {damageTaken:0}", this);
    }

    private void Update()
    {
        if (Elapsed < nextLog) return;

        nextLog += logInterval;
        LogSummary();
    }

    private void OnEnemyKilled(GameObject enemy) => kills++;

    private void OnLevelUp(int level)
    {
        if (firstLevelTime < 0f) firstLevelTime = Elapsed;
    }

    private void OnHealthChanged(float current, float max)
    {
        if (current < lastHealth) damageTaken += lastHealth - current; // solo cuenta bajadas
        lastHealth = current;
        minHealth = Mathf.Min(minHealth, current);
    }

    private void LogSummary()
    {
        int alive = FindObjectsByType<EnemyContactDamage>(FindObjectsInactive.Exclude).Length;
        int minutes = (int)(Elapsed / 60f);
        int seconds = (int)(Elapsed % 60f);

        string level = playerXP != null ? playerXP.Level.ToString() : "?";
        string hp = playerHealth != null ? $"{playerHealth.Current:0}/{playerHealth.Max:0} (min {minHealth:0})" : "?";
        string speed = stats != null ? $"x{stats.MoveSpeedMultiplier:0.00}" : "?";
        string first = firstLevelTime >= 0f ? $"{firstLevelTime:0}s" : "-";

        DifficultyDirector director = DifficultyDirector.Instance;
        string round = director != null ? $"ronda {director.Round}/{director.MaxRound} (kills {director.RoundKillCount}/{director.KillsPerRound}) | " : "";
        string difficulty = $"{round}vida x{DifficultyScale.Hp:0.0} dmg x{DifficultyScale.Damage:0.0} vel x{DifficultyScale.Speed:0.00}";

        Debug.Log($"[Run {minutes:00}:{seconds:00}] nivel {level} (1er nivel a {first}) | kills {kills} | vivos {alive} | vida {hp} | daño recibido {damageTaken:0} | velocidad {speed} | enemigos: {difficulty}", this);

        // Bonus de XP por impactos multiples: cuanto se usa y cuanto aporta.
        if (multiHit != null && multiHit.Swings > 0)
        {
            Debug.Log($"[Run]   Machete: {multiHit.Swings} golpes, {100f * multiHit.MultiHitSwings / multiHit.Swings:0}% con 3+ impactos, {(float)multiHit.TotalHits / multiHit.Swings:0.0} impactos por golpe, +{multiHit.BonusXpTotal} XP por bonus", this);
        }

        // Acierto por tipo: ataques que empezaron vs golpes que realmente conectaron.
        foreach (var pair in EnemyContactDamage.WindupCount)
        {
            EnemyContactDamage.HitCount.TryGetValue(pair.Key, out int hits);
            EnemyContactDamage.CancelledCount.TryGetValue(pair.Key, out int cancelled);
            int resolved = Mathf.Max(1, pair.Value - cancelled);   // ataques que llegaron al golpe (sin los cancelados por muerte)
            Debug.Log($"[Run]   {pair.Key}: {hits} golpes acertados de {pair.Value} ataques ({100f * hits / pair.Value:0}%) | cancelados al morir {cancelled} | acierto real {100f * hits / resolved:0}%", this);
        }

        minHealth = playerHealth != null ? playerHealth.Current : 0f; // el minimo se mide por intervalo
    }
}
